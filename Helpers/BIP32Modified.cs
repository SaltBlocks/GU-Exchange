using System.Text;
using System.Security.Cryptography;
using System.Numerics;
using System.Linq;
using System;

namespace GU_Exchange.Helpers
{
    /// <summary>
    /// Used to deterministically derive child keys from a parent wallet.
    /// These keys are used by GU-Exchange to offer trading functionality that would normally require private key access in a secure manner,
    /// while also allowing keys to always be recovered as long as the user maintains access to parent wallet.
    /// </summary>
    internal class BIP32Modified
    {
        // Order for the eliptic curve used by ETH.
        private static readonly BigInteger SECP256K1_ORDER = BigInteger.Parse("115792089237316195423570985008687907852837564279074904382605163141518161494337");

        /// <summary>
        /// Utility to convert hex string to byte array
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        private static byte[] HexToBytes(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        /// <summary>
        /// Utility to convert bytes to hex string
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        private static string BytesToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        /// <summary>
        /// Perform HMAC-SHA512 and return the digest
        /// </summary>
        /// <param name="key"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private static byte[] HMAC_SHA512(byte[] key, byte[] data)
        {
            using (var hmac = new HMACSHA512(key))
            {
                return hmac.ComputeHash(data);
            }
        }

        /// <summary>
        /// Convert byte array to BigInteger
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        private static BigInteger BytesToBigInteger(byte[] bytes)
        {
            byte[] temp = new byte[bytes.Length + 1];
            Array.Copy(bytes, 0, temp, 1, bytes.Length); // Prevents treating data as negative number
            Array.Reverse(temp); // Convert to big-endian
            return new BigInteger(temp);
        }

        /// <summary>
        /// Convert BigInteger to byte array
        /// </summary>
        /// <param name="value"></param>
        /// <param name="byteSize"></param>
        /// <returns></returns>
        private static byte[] BigIntegerToBytes(BigInteger value, int byteSize)
        {
            byte[] bytes = value.ToByteArray();
            Array.Reverse(bytes); // Convert to big-endian
            if (bytes.Length > byteSize)
            {
                byte[] temp = new byte[byteSize];
                Array.Copy(bytes, bytes.Length - byteSize, temp, 0, byteSize);
                return temp;
            }
            else if (bytes.Length < byteSize)
            {
                byte[] temp = new byte[byteSize];
                Array.Copy(bytes, 0, temp, byteSize - bytes.Length, bytes.Length);
                return temp;
            }
            return bytes;
        }

        /// <summary>
        /// Derive a hardened private key
        /// </summary>
        /// <param name="parentPrivateKey"></param>
        /// <param name="chainCode"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private static byte[] DeriveHardenedKey(byte[] parentPrivateKey, byte[] chainCode, ulong index)
        {
            // Hardened derivation uses the private key (0x00 + parentPrivateKey) and index (0x80000000 + index)
            byte[] data = new byte[1 + parentPrivateKey.Length + 4];
            data[0] = 0x00; // Prepend 0x00.
            Array.Copy(parentPrivateKey, 0, data, 1, parentPrivateKey.Length);

            // Append the index in big-endian format (hardened by adding 0x80000000)
            index += 0x80000000;
            data[data.Length - 4] = (byte)((index >> 24) & 0xFF);
            data[data.Length - 3] = (byte)((index >> 16) & 0xFF);
            data[data.Length - 2] = (byte)((index >> 8) & 0xFF);
            data[data.Length - 1] = (byte)(index & 0xFF);

            // Perform HMAC-SHA512 using the chain code
            byte[] hmacResult = HMAC_SHA512(chainCode, data);

            // Split the HMAC result into the new private key and chain code
            byte[] newKeyPart = hmacResult.Take(32).ToArray();
            //byte[] newChainCode = hmacResult.Skip(32).Take(32).ToArray(); // We're not going to use the chaincode.

            BigInteger newPrivateKey = (BytesToBigInteger(newKeyPart) + BytesToBigInteger(parentPrivateKey)) % SECP256K1_ORDER;
            return BigIntegerToBytes(newPrivateKey, 32);
        }

        /// <summary>
        /// Derive a hardened private key from a seed.
        /// </summary>
        /// <param name="seedStr"></param>
        /// <param name="keyIndex"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string DeriveHardenedKey(string seedStr, ulong keyIndex, string key = "GU-Exchange")
        {
            // Step 1: Convert the signature to a byte array
            byte[] seedBytes = HexToBytes(seedStr.StartsWith("0x") ? seedStr.Substring(2) : seedStr);
            // Step 2: Hash the signature to get the master seed (using SHA256)
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] seedHash = sha256.ComputeHash(seedBytes);

                // Generate the master private key.
                byte[] keyBytes = Encoding.UTF8.GetBytes(key);

                byte[] hmacResult = HMAC_SHA512(keyBytes, seedHash);

                // The first 32 bytes are the master private key
                byte[] masterPrivateKey = hmacResult.Take(32).ToArray();
                byte[] privateKey;
                if (keyIndex == 0)
                {
                    privateKey = masterPrivateKey;
                }
                else
                {
                    // Generate a random chain code (32 bytes)
                    byte[] chainCode = hmacResult.Skip(32).Take(32).ToArray();
                    keyIndex -= 1;

                    // Step 5: Derive the hardened private key at the given index
                    privateKey = DeriveHardenedKey(masterPrivateKey, chainCode, keyIndex);
                }

                return "0x" + BytesToHex(privateKey);
            }
        }
    }
}
