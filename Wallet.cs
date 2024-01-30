using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Collections;
using System.Net.Http;
using System.Security.Cryptography;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Diagnostics;
using System.Runtime.Serialization.Formatters.Soap;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Policy;
using System.Windows;

namespace GU_Exchange
{
    public class Token
    {
        #region Class Properties
        public string Name { get; private set; }
        public string Address { get; private set; }
        public decimal? Value { get; set; }
        #endregion

        /// <summary>
        /// Class for storing information regarding currencies used for trading on IMX.
        /// </summary>
        /// <param name="name">The name of the token</param>
        /// <param name="address">The address used for trading this token on IMX.</param>
        /// <param name="value">The current dollar value of this currency.</param>
        public Token(string name, string address, decimal? value)
        {
            this.Name = name;
            this.Address = address;
            this.Value = value;
        }
    }

    public class Order
    {
        #region Class Properties.
        public string Name;
        public string Currency;
        public string Seller;
        public UInt64 OrderID;
        public string TokenAddress;
        public string TokenID;
        public decimal PriceBase;
        public Dictionary<string, decimal> FeeList;
        #endregion

        #region Default Constructor.

        /// <summary>
        /// Constructs an order object from json data.
        /// </summary>
        /// <param name="json_order">THe json object containing the order.</param>
        /// <param name="currency">The name of the currency the order is filed in.</param>
        /// <exception cref="NullReferenceException"></exception>
        public Order(JToken json_order, string currency)
        {
            // Collect data pertaining to the order from the json object.
            string? name = (string?)json_order.SelectToken("sell.data.properties.name");
            string? seller = (string?)json_order["user"];
            UInt64? orderID = (UInt64?)json_order.SelectToken("order_id");
            string? tokenAddress = (string?)json_order.SelectToken("sell.data.token_address");
            string? tokenID = (string?)json_order.SelectToken("sell.data.token_id");
            string? quantity = (string?)json_order.SelectToken("buy.data.quantity");
            uint? decimals = (uint?)json_order.SelectToken("buy.data.decimals");
            JToken? fees = json_order["fees"];

            // Ensure that the order is valid.
            if (name == null || seller == null || orderID == null ||
                tokenAddress == null || tokenID == null ||
                quantity == null || fees == null || decimals == null)
            {
                throw new NullReferenceException("JSON object does not contain valid order.");
            }

            // Calculate the fee amount on the order.
            FeeList = new Dictionary<string, decimal>();
            decimal multDecimals = Pow(new decimal(10), (uint)decimals);
            decimal quantity_base = decimal.Parse(quantity);
            foreach (JToken fee in fees)
            {
                string? address = (string?)fee["address"];
                string? fee_amount = (string?)fee["amount"];
                if (fee_amount == null || address == null)
                    throw new NullReferenceException("Not Implemented");
                if (FeeList.ContainsKey(address))
                {
                    FeeList[address] = FeeList[address] + (decimal.Parse(fee_amount) / multDecimals);
                }
                else
                {
                    FeeList.Add(address, decimal.Parse(fee_amount) / multDecimals);
                }
            }

            // Take into account the 1% maker marketplace fee to ensure we are using correct price values later on.
            FeeList.Add("MARKET_TAKER", quantity_base * new decimal(0.01) / multDecimals);
            quantity_base /= multDecimals;

            // Store the gathered order data.
            this.Name = name;
            this.Currency = currency;
            this.Seller = seller;
            this.OrderID = (UInt64)orderID;
            this.TokenAddress = tokenAddress;
            this.TokenID = tokenID;
            this.PriceBase = quantity_base;
        }

        #endregion

        #region Price Calculation.

        /// <summary>
        /// Get the total fee price on this order excluding user and taker marketplace fees.
        /// </summary>
        /// <returns>Total amount of fees on this order.</returns>
        public decimal PriceFees()
        {
            decimal fee_amount = 0;
            foreach (string fee_address in FeeList.Keys)
            {
                fee_amount += FeeList[fee_address];
            }
            return fee_amount;
        }

        /// <summary>
        /// Get the total price for this order excluding user and taker marketplace fees.
        /// </summary>
        /// <returns>The total order price.</returns>
        public decimal PriceTotal()
        {
            return PriceBase + PriceFees();
        }

        #endregion

        #region Helper Functions.
        /// <summary>
        /// Power function for decimal values.
        /// </summary>
        /// <param name="x">The base number</param>
        /// <param name="y">The power by which to raise the base number.</param>
        /// <returns>Returns x^y.</returns>
        public static decimal Pow(decimal x, uint y)
        {
            decimal A = 1m;
            BitArray e = new BitArray(BitConverter.GetBytes(y));
            int t = e.Count;

            for (int i = t - 1; i >= 0; --i)
            {
                A *= A;
                if (e[i] == true)
                {
                    A *= x;
                }
            }
            return A;
        }
        #endregion
    }

    /// <summary>
    /// Used to securely store Ethereum private keys.
    /// Provided a decent password is used, recovering the key from the wallet stored on disk without the password should be impossible.
    /// AES256 bit encryption is used with PBKDF2 for key generation to secure wallets on the disk.
    /// </summary>
    [Serializable()]
    public class Wallet
    {
        #region Static Fields.
        public static Dictionary<string, Wallet> wallets = LoadWallets();
        private static Wallet? s_connectedWallet = SetupConnectedWallet();
        private static readonly Dictionary<string, Token> s_currencyList = new()
        {
            { "ETH", new Token("ETH", "ETH", null) },
            { "GODS", new Token("GODS", "0xccc8cb5229b0ac8069c51fd58367fd1e622afd97", null) },
            { "IMX", new Token("IMX", "0xf57e7e7c23978c3caec3c3548e3d615c346e79ff", null) }
        };
        private static Task<Dictionary<string, Token>>? s_fetchTokenTask;
        #endregion

        #region Setup token prices.

        /// <summary>
        /// Fetch the current prices for ETH, GODS and IMX from coingecko and store them locally for later use.
        /// </summary>
        /// <returns></returns>
        private static async Task<Dictionary<string, Token>> FetchTokensWebAsync()
        {
            try
            {
                string token_data = await ResourceManager.Client.GetStringAsync("https://api.coingecko.com/api/v3/simple/price?ids=ethereum,gods-unchained,immutable-x&vs_currencies=usd");
                JObject? jsonData = (JObject?)JsonConvert.DeserializeObject(token_data);
                if (jsonData == null)
                {
                    return s_currencyList;
                }
                decimal? price_eth = (decimal?)jsonData.SelectToken("ethereum.usd");
                decimal? price_gods = (decimal?)jsonData.SelectToken("gods-unchained.usd");
                decimal? price_imx = (decimal?)jsonData.SelectToken("immutable-x.usd");

                // Store prices for ETH, GODS and IMX.
                s_currencyList["ETH"].Value = price_eth;
                s_currencyList["GODS"].Value = price_gods;
                s_currencyList["IMX"].Value = price_imx;
            } catch (HttpRequestException)
            {
                s_currencyList["ETH"].Value = 0;
                s_currencyList["GODS"].Value = 0;
                s_currencyList["IMX"].Value = 0;
            }
            return s_currencyList;
        }

        /// <summary>
        /// Fetch and store prices for currencies used to trade on IMX.
        /// </summary>
        /// <returns></returns>
        public static async Task<Dictionary<string, Token>> FetchTokens()
        {
            // This should only be run once, if the value of ETH has already been stored, we can return here.
            if (s_currencyList["ETH"].Value != null)
                return s_currencyList;

            if (s_fetchTokenTask != null)
            {
                return await s_fetchTokenTask;
            }

            s_fetchTokenTask = FetchTokensWebAsync();
            return await s_fetchTokenTask;
        }

        #endregion

        #region Get token prices.

        /// <summary>
        /// Get the current price of ETH in USD.
        /// </summary>
        /// <returns></returns>
        public static async Task<Token> GetETHToken()
        {
            Dictionary<string, Token> tokens = await FetchTokens();
            return tokens["ETH"];
        }

        /// <summary>
        /// Get the current price of GODS in USD.
        /// </summary>
        /// <returns></returns>
        public static async Task<Token> GetGODSToken()
        {
            Dictionary<string, Token> tokens = await FetchTokens();
            return tokens["GODS"];
        }

        /// <summary>
        /// Get the current price of IMX in USD.
        /// </summary>
        /// <returns></returns>
        public static async Task<Token> GetIMXToken()
        {
            Dictionary<string, Token> tokens = await FetchTokens();
            return tokens["IMX"];
        }

        #endregion

        #region Encryption.

        /// <summary>
        /// Encrypt the provided string with the AES256 bit key used to secure this wallet.
        /// </summary>
        /// <param name="plainText">The text to encrypt.</param>
        /// <param name="plainText">The key to use for the encryption.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">Thrown if the key for this wallet is not available (the wallet is locked).</exception>
        private static string EncryptStringAES256(string plainText, byte[]? key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            using (Aes algo = Aes.Create())
            {
                algo.Key = key;
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
        private static string DecryptStringAES256(string enc, byte[]? key)
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
        #endregion

        #region Loading and Saving.
        /// <summary>
        /// Load a wallet from a <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to load the wallet from.</param>
        /// <returns></returns>
        public static Wallet LoadWallet(Stream stream)
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
        public static void SaveWallet(Wallet wlt, Stream stream)
        {
            SoapFormatter formatter = new SoapFormatter();
            formatter.Serialize(stream, wlt);
            stream.Close();
        }

        /// <summary>
        /// Load available wallets from the disk.
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, Wallet> LoadWallets()
        {
            Dictionary<string, Wallet> wallets = new();
            try
            {
                string[] walletFiles = Directory.GetFiles(Directory.GetCurrentDirectory() + "\\wallets", "*.wlt");
                foreach (string wallet in walletFiles)
                {
                    try
                    {
                        Wallet wlt = Wallet.LoadWallet(File.Open(wallet, FileMode.Open));
                        wallets.Add(wlt.Address, wlt);
                        Console.WriteLine($"{wlt.Address}, Locked: {wlt.IsLocked()}, Webwallet: {wlt is WebWallet}");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.StackTrace);
                        continue;
                    }
                }
            }
            catch (DirectoryNotFoundException)
            { }
            return wallets;
        }
        #endregion

        #region Get/Set connected wallet.
        private static Wallet? SetupConnectedWallet()
        {
            string address = Settings.GetSetting("ConnectedWallet");
            foreach (Wallet wlt in wallets.Values)
            {
                if (wlt.Address.Equals(address))
                {
                    if (wlt is WebWallet)
                    {
                        SignatureRequestServer.StartServer();
                        SignatureRequestServer.RequestedAddress = wlt.Address;
                    }
                    return wlt;
                }
            }
            return null;
        }

        public static Wallet? GetConnectedWallet()
        {
            return s_connectedWallet;
        }

        public static async Task SetConnectedWallet(Wallet? wallet)
        {
            if (wallet != null)
            {
                if (wallet is WebWallet)
                {
                    SignatureRequestServer.StartServer();
                    SignatureRequestServer.RequestedAddress = wallet.Address;
                } else
                {
                    SignatureRequestServer.RequestedAddress = "*";
                    SignatureRequestServer.StopServer();
                }
                bool isLinked = await wallet.IsLinkedAsync();
                if (!isLinked)
                {
                    if (await wallet.RequestLinkAsync((MainWindow)Application.Current.MainWindow))
                    {
                        MessageWindow window = new MessageWindow($"Wallet linked to IMX successfully.", "Link wallet", MessageType.INFORM);
                        window.Owner = (MainWindow)Application.Current.MainWindow;
                        window.ShowDialog();
                    }
                }
            } else
            {
                SignatureRequestServer.RequestedAddress = "*";
                SignatureRequestServer.StopServer();
            }
            s_connectedWallet = wallet;
        }
        #endregion

        #region Class Parameters.
        public string Address { get; protected set; }
        protected byte[] _salt;
        protected string _lockedKey;
        [NonSerialized()] private bool? _isLinked;
        [NonSerialized()] protected byte[]? _key;
        [NonSerialized()] protected Dictionary<string, decimal> _tokenAmountOwned;
        #endregion

        #region Default Constructor
        /// <summary>
        /// Create a wallet object that protects the given private key with the provided password.
        /// The <see cref="Wallet"/> returned is unlocked.
        /// </summary>
        /// <param name="privKey">The wallets private key</param>
        /// <param name="password">The password that will be used to encrypt the key.</param>
        public Wallet(string privateKey, string password = "password")
        {
            // Create a 32 byte random salt to protect against password cracking using pre-generated tables.
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                _salt = new byte[32];
                rng.GetBytes(_salt);
            }
            // Derive the encryption key using Pbkdf2 to render brute force password cracking attempts ineffective.
            _key = Rfc2898DeriveBytes.Pbkdf2(password, _salt, 210000, HashAlgorithmName.SHA512, 32);
            _lockedKey = EncryptStringAES256(privateKey, _key);
            // Get and store the public eth address associated with the provided private key.
            Address = CalculateAddress();
            _tokenAmountOwned = new Dictionary<string, decimal>();
        }
        #endregion

        #region Serialization (saving and loading from disk).
        /// <summary>
        /// Ensure that _tokenAmountOwned is initialized to an empty dictionary when wallets are loaded.
        /// </summary>
        /// <param name="context"></param>
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            _tokenAmountOwned = new Dictionary<string, decimal>();
        }
        #endregion

        #region Key access and handling.
        /// <summary>
        /// Check if the wallet is unlocked.
        /// </summary>
        /// <returns></returns>
        public bool IsLocked()
        {
            return _key == null;
        }

        /// <summary>
        /// Delete the reference to the wallet key, locking the wallet.
        /// </summary>
        public void LockWallet()
        {
            _key = null;
        }

        /// <summary>
        /// Verify whether or not the given password is correct for this wallet without changing the locked status of the wallet..
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public async Task<bool> CheckPassword(string password)
        {
            Task<Byte[]> getKey = Task.Run(() =>
            {
                return Rfc2898DeriveBytes.Pbkdf2(password, _salt, 210000, HashAlgorithmName.SHA512, 32);
            });
            byte[] key = await getKey;
            try
            {
                DecryptStringAES256(_lockedKey, key);
            }
            catch (CryptographicException)
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
        public async Task<bool> UnlockWallet(string password = "password")
        {
            Task<Byte[]> getKey = Task.Run(() =>
            {
                return Rfc2898DeriveBytes.Pbkdf2(password, _salt, 210000, HashAlgorithmName.SHA512, 32);
            });
            this._key = await getKey;
            try
            {
                DecryptStringAES256(_lockedKey, _key);
            }
            catch (CryptographicException)
            {
                this._key = null; // Incorrect password provided.
                return false;
            }
            return true;
        }

        /// <summary>
        /// Return the private key for this wallet. Will fail if the wallet is locked.
        /// </summary>
        /// <returns>The private key</returns>
        public string GetPrivateKey()
        {
            return DecryptStringAES256(_lockedKey, _key);
        }
        #endregion

        #region IMX link status.
        /// <summary>
        /// Check if this wallet is linked to IMX.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> IsLinkedAsync()
        {
            if (_isLinked != null) return (bool)_isLinked;
            string linkData;
            try {
                HttpResponseMessage response = await ResourceManager.Client.GetAsync($"https://api.x.immutable.com/v1/users/{(Address)}");

                // Check if the request was successful.
                if (response.IsSuccessStatusCode)
                {
                    linkData = await response.Content.ReadAsStringAsync(); // Process successful response
                }
                else
                {
                    linkData = await response.Content.ReadAsStringAsync(); // Get the response body for more details
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Failed to check wallet link status with exception: {ex.Message}");
                return false;
            }
            if (linkData.Contains("Account not found"))
            {
                _isLinked = false;
                return false;
            }
            _isLinked = true;
            return true;
        }

        virtual public async Task<bool> RequestLinkAsync(Window parent)
        {
            MessageWindow window = new MessageWindow("Before your wallet can be used to trade on IMX, it must be linked to the platform.\nWould you like to link it now?", "Link wallet", MessageType.CONFIRM);
            window.Owner = parent;
            window.ShowDialog();
            if (!window.Result)
            {
                return false;
            }

            bool reLock = false;
            if (IsLocked())
            {
                UnlockWalletWindow unlockWindow = new UnlockWalletWindow(this);
                unlockWindow.Owner = parent;
                unlockWindow.ShowDialog();
                if (unlockWindow.Result == UnlockWalletWindow.UnlockResult.Cancel)
                {
                    return false;
                }
                if (unlockWindow.Result == UnlockWalletWindow.UnlockResult.Relock)
                    reLock = true;
            }

            Task<bool> linkWallet = Task.Run(() =>
            {
                char[] data = new char[500];
                IMXlib.imx_register_address((GetPrivateKey() + "\0").ToCharArray(), data, data.Length);
                string result = new string(data).Trim('\0');
                Console.WriteLine(result);
                if (reLock)
                    LockWallet();
                if (!result.Contains("tx_hash"))
                {
                    return false;
                }
                _isLinked = true;
                return true;
            });
            return await linkWallet;
        }
        #endregion

        #region Get Wallet contents.
        /// <summary>
        /// Fetch or lookup the total amount of a specific token owned by this wallet.
        /// </summary>
        /// <param name="token_name">The name of the token to lookup or fetch</param>
        /// <param name="force_update">If set to true, always fetch the latest account balance from the IMX api. Otherwise, use cached values if available.</param>
        /// <returns>The account balance.</returns>
        public async Task<decimal> GetTokenAmountAsync(string tokenName, bool forceUpdate = false)
        {
            if (forceUpdate || !_tokenAmountOwned.ContainsKey(tokenName))
            {
                string data = await ResourceManager.Client.GetStringAsync($"https://api.x.immutable.com/v2/balances/{Address}");
                JObject? jsonData = (JObject?)JsonConvert.DeserializeObject(data);
                if (jsonData != null)
                {
                    JToken? results = jsonData["result"];
                    if (results != null)
                    {
                        foreach (JToken wltToken in results.ToArray<JToken>())
                        {
                            string? symbol = (string?)wltToken["symbol"];
                            if (symbol != null)
                            {
                                string? balance = (string?)wltToken["balance"];
                                if (balance != null)
                                {
                                    if (_tokenAmountOwned.ContainsKey(symbol))
                                        _tokenAmountOwned[symbol] = decimal.Parse(balance) / new decimal(Math.Pow(10, 18));
                                    else
                                        _tokenAmountOwned.Add(symbol, decimal.Parse(balance) / new decimal(Math.Pow(10, 18)));
                                }
                            }
                        }
                    }
                }
            }
            if (_tokenAmountOwned.ContainsKey(tokenName))
            {
                return _tokenAmountOwned[tokenName];
            }
            return new decimal(00);
        }
        #endregion

        #region Supporting functions.
        virtual protected string CalculateAddress()
        {
            char[] address = new char[43];
            IMXlib.eth_get_address((GetPrivateKey() + "\0").ToCharArray(), address, address.Length);
            return new string(address).Trim('\0');
        }
        #endregion
    }

    [Serializable()]
    internal class WebWallet : Wallet
    {
        public WebWallet(string privateKey, string address, string password = "password") : base(privateKey, password)
        {
            this.Address = address;
        }
         protected override string CalculateAddress()
        {
            // Do nothing, we can't calculate the address for this wallet.
            return "";
        }

        public override async Task<bool> RequestLinkAsync(Window parent)
        {
            MessageWindow window = new MessageWindow("Before your wallet can be used to trade on IMX, it must be linked to the platform.\nWould you like to link it now?", "Link wallet", MessageType.CONFIRM);
            window.Owner = parent;
            window.ShowDialog();
            if (!window.Result)
            {
                return false;
            }
            Task<SignatureData> fetchSignature = SignatureRequestServer.RequestSignatureAsync(IMXlib.IMX_LINK_MESSAGE);
            UseWebWalletWindow useWalletWindow = new(fetchSignature);
            useWalletWindow.Owner = parent;
            useWalletWindow.ShowDialog();
            try
            {
                SignatureData linkSignature = await fetchSignature;
                if (this.IsLocked())
                    await this.UnlockWallet();
                Task<bool> linkWallet = Task.Run(() =>
                {
                    char[] data = new char[500];
                    IMXlib.imx_register_address_presigned((Address + "\0").ToCharArray(), (linkSignature.Signature + "\0").ToCharArray(), (this.GetPrivateKey() + "\0").ToCharArray(), data, data.Length);
                    string result = new string(data).Trim('\0');
                    Console.WriteLine(result);
                    if (!result.Contains("tx_hash"))
                        return false;
                    return true;
                });
                return await linkWallet;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Cancelled");
                return false;
            }
        }
    }
}
