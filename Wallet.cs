using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Soap;

namespace GU_Exchange
{
    /// <summary>
    /// Used to securely store Ethereum private keys.
    /// Provided a decent password is used, recovering the key from the wallet stored on disk without the password should be impossible.
    /// AES256 bit encryption is used with PBKDF2 for key generation to secure wallets on the disk.
    /// </summary>
    [Serializable()]
    internal class Wallet
    {
        /// <summary>
        /// Contains all wallets currently available for use (both locked and unlocked).
        /// </summary>
        public static Dictionary<string, Wallet> wallets = loadWallets();

        /// <summary>
        /// The currently active wallet.
        /// </summary>
        public static Wallet? connectedWallet = loadConnectedWallet();

        /// <summary>
        /// Loads the wallets currently available on the disk.
        /// </summary>
        /// <returns>A <see cref="System.Collections.Generic.Dictionary{String, Wallet}"/> linking addresses with the associated <see cref="Wallet"/> object. /></returns>
        private static Dictionary<string, Wallet> loadWallets()
        {
            Dictionary<string, Wallet> wallets = new();
            string[] walletFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.wlt");
            foreach (string file in walletFiles)
            {
                try
                {
                    Wallet wlt = Wallet.loadWallet(File.Open(file, FileMode.Open));
                    wallets.Add(wlt.address, wlt);
                }
                catch (Exception)
                {
                    continue;
                }
            }
            return wallets;
        }

        /// <summary>
        /// Returns the Wallet indicated as the default wallet in the application <see cref="Settings"/>.
        /// Returns null if the default wallet wasn't found on disk.
        /// </summary>
        /// <returns></returns>
        private static Wallet? loadConnectedWallet()
        {
            string walletAddress = Settings.globalSettings.walletAddress;
            if (walletAddress.Length == 0)
                return null;
            try
            {
                return wallets[walletAddress];
            }
            catch (Exception)
            {
                return null;
            }
        }

        // DLL functions used to interact with IMX.

        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr eth_generate_key([Out] char[] result_buffer, int buffer_size);

        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr eth_get_address([In] char[] eth_priv_str, [Out] char[] result_buffer, int buffer_size);

        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr imx_register_address([In] char[] eth_priv_str, [Out] char[] result_buffer, int buffer_size);

        /// <summary>
        /// Load a wallet from a <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to load the wallet from.</param>
        /// <returns></returns>
        public static Wallet loadWallet(Stream stream)
        {
            SoapFormatter formatter = new SoapFormatter();
            Wallet wlt = (Wallet)formatter.Deserialize(stream);
            stream.Close();
            return wlt;
        }

        /// <summary>
        /// Save the provided wallet to a <see cref="Stream"/>.
        /// </summary>
        /// <param name="wlt"></param>
        /// <param name="stream"></param>
        public static void saveWallet(Wallet wlt, Stream stream)
        {
            SoapFormatter formatter = new SoapFormatter();
            formatter.Serialize(stream, wlt);
            stream.Close();
        }

        public string address { get; private set; }
        private byte[] salt;
        private string lockedKey;
        [NonSerialized()] private byte[]? key;

        /// <summary>
        /// Create a wallet object that protects the given private key with the provided password.
        /// The <see cref="Wallet"/> returned is unlocked.
        /// </summary>
        /// <param name="privKey"></param>
        /// <param name="password"></param>
        public Wallet(string privKey, string password = "password")
        {
            this.salt = new byte[32];
            new Random().NextBytes(salt);
            this.key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 1000000, HashAlgorithmName.SHA512, 32);
            lockedKey = encryptString(privKey);
            char[] address = new char[43];
            eth_get_address(privKey.ToCharArray(), address, address.Length);
            this.address = new string(address).Trim('\0');
        }

        /// <summary>
        /// Check if the wallet is unlocked.
        /// </summary>
        /// <returns></returns>
        public bool isLocked()
        {
            return key == null;
        }

        /// <summary>
        /// Delete the reference to the wallet key, locking the wallet.
        /// </summary>
        public void lockWallet()
        {
            key = null;
        }

        /// <summary>
        /// Verify whether or not the given password is correct for this wallet without changing the locked status of the wallet..
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public bool checkPassword(string password)
        {
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 1000000, HashAlgorithmName.SHA512, 32);
            try
            {
                decryptString(lockedKey, key);
            } catch (CryptographicException)
            {
                return false; // Password was incorrect.
            }
            return true; // Password was correct.
        }

        /// <summary>
        /// Try to unlock the wallet using the given password.
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public bool unlockWallet(string password="password")
        {
            this.key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 1000000, HashAlgorithmName.SHA512, 32);
            try
            {
                decryptString(lockedKey);
            } catch (CryptographicException)
            {
                this.key = null; // Incorrect password provided.
                return false;
            }
            return true;
        }

        /// <summary>
        /// Return the private key for this wallet. Will fail if the wallet is locked.
        /// </summary>
        /// <returns>The private key</returns>
        public string getPrivateKey()
        {
            return decryptString(lockedKey);
        }

        /// <summary>
        /// Encrypt the provided string with the AES256 bit key used to secure this wallet.
        /// </summary>
        /// <param name="plainText">The text to encrypt.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">Thrown if the key for this wallet is not available (the wallet is locked).</exception>
        private string encryptString(string plainText)
        {
            if (this.key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            using (Aes algo = Aes.Create())
            {
                algo.Key = this.key;
                algo.GenerateIV();
                ICryptoTransform encryptor = algo.CreateEncryptor();

                byte[] encryptedData;

                //Encryption will be done in a memory stream through a CryptoStream object
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter sw = new StreamWriter(cs))
                        {
                            sw.Write(plainText);
                        }
                        encryptedData = ms.ToArray();
                    }
                }

                byte[] res = algo.IV.Concat(encryptedData).ToArray();
                return Convert.ToBase64String(res);
            }
        }

        /// <summary>
        /// Decrypt the provided encrypted string to plain text.
        /// </summary>
        /// <param name="enc">The data to attempt to decrypt.</param>
        /// <param name="key">The key to use for the decryption.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">Thrown if the provided key is null.</exception>
        private string decryptString(string enc, byte[]? key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            using (Aes aesAlgorithm = Aes.Create())
            {
                byte[] data = Convert.FromBase64String(enc);
                byte[] iv = data.Take(16).ToArray();
                byte[] dataEnc = data.Skip(16).ToArray();
                aesAlgorithm.Key = key;
                aesAlgorithm.IV = iv;

                ICryptoTransform decryptor = aesAlgorithm.CreateDecryptor();
                using (MemoryStream ms = new MemoryStream(dataEnc))
                {
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader sr = new StreamReader(cs))
                        {
                            string plainText = sr.ReadToEnd();
                            return plainText;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Decrypt the provided encrypted string to plain text using this wallets key.
        /// </summary>
        /// <param name="enc">The data to attempt to decrypt.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">Thrown if the key for this wallet is not available (the wallet is locked).</exception>
        private string decryptString(string enc)
        {
            return decryptString(enc, this.key);
        }
    }
}
