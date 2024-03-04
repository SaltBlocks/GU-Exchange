using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Soap;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static GU_Exchange.Helpers.IMXlib;

namespace GU_Exchange.Helpers
{
    /// <summary>
    /// Used to store price and address data of a token that can be traded on IMX.
    /// </summary>
    public class Token
    {
        #region Class Properties
        public string Name { get; private set; }
        public string Address { get; private set; }
        public decimal? Value { get; set; }
        #endregion

        #region Default Constructor.
        /// <summary>
        /// Class for storing information regarding currencies used for trading on IMX.
        /// </summary>
        /// <param name="name">The name of the token</param>
        /// <param name="address">The address used for trading this token on IMX.</param>
        /// <param name="value">The current dollar value of this currency.</param>
        public Token(string name, string address, decimal? value)
        {
            Name = name;
            Address = address;
            Value = value;
        }
        #endregion
    }

    /// <summary>
    /// Used to store order data on the IMX public orderbook.
    /// </summary>
    public class Order
    {
        #region Class Properties.
        public string Name;
        public string Quality;
        public string Currency;
        public string Seller;
        public ulong OrderID;
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
            string? image_url = (string?)json_order.SelectToken("sell.data.properties.image_url");
            switch (image_url?.Substring(image_url.Length - 1))
            {
                case "4":
                    Quality = "Meteorite";
                    break;
                case "3":
                    Quality = "Shadow";
                    break;
                case "2":
                    Quality = "Gold";
                    break;
                case "1":
                    Quality = "Diamond";
                    break;
                default:
                    Quality = "None";
                    break;
            }
            string? seller = (string?)json_order["user"];
            ulong? orderID = (ulong?)json_order.SelectToken("order_id");
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
                    FeeList[address] = FeeList[address] + decimal.Parse(fee_amount) / multDecimals;
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
            Name = name;
            Currency = currency;
            Seller = seller;
            OrderID = (ulong)orderID;
            TokenAddress = tokenAddress;
            TokenID = tokenID;
            PriceBase = quantity_base;
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
            }
            catch (HttpRequestException)
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
                        Wallet wlt = LoadWallet(File.Open(wallet, FileMode.Open));
                        wallets.Add(wlt.Address, wlt);
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

        #region Static supporting functions.
        /// <summary>
        /// Check if this <see cref="Wallet"/> is linked to IMX.
        /// </summary>
        /// <returns></returns>
        public static async Task<bool?> IsAddressLinkedAsync(string address)
        {
            string linkData;
            try
            {
                HttpResponseMessage response = await ResourceManager.Client.GetAsync($"https://api.x.immutable.com/v1/users/{address}");

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
                return null;
            }
            if (linkData.Contains("Account not found"))
            {
                return false;
            }
            return true;
        }

        public static bool IsValidEthereumAddress(string address)
        {
            // Remove the "0x" prefix if present
            if (address.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                address = address.Substring(2);

            // Check if the address has exactly 40 characters
            if (address.Length != 40)
                return false;

            // Check if the address is a valid hexadecimal string
            if (!Regex.IsMatch(address, "^[0-9a-fA-F]+$"))
                return false;
            return true;
        }
        #endregion

        #region Get/Set connected wallet.

        /// <summary>
        /// Load the default <see cref="Wallet"/> as is currently defined in the user settings.
        /// </summary>
        /// <returns>The default wallet</returns>
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

        /// <summary>
        /// Get the currently connected <see cref="Wallet"/>.
        /// </summary>
        /// <returns></returns>
        public static Wallet? GetConnectedWallet()
        {
            return s_connectedWallet;
        }

        /// <summary>
        /// Set the currently connected wallet and start/stof the local <see cref="SignatureRequestServer"/> depending on whether or not this is a <see cref="WebWallet"/>. 
        /// </summary>
        /// <param name="wallet"></param>
        /// <returns></returns>
        public static async Task SetConnectedWallet(Wallet? wallet)
        {
            if (wallet != null)
            {
                if (wallet is WebWallet)
                {
                    SignatureRequestServer.StartServer();
                    SignatureRequestServer.RequestedAddress = wallet.Address;
                }
                else
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
            }
            else
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
            Task<byte[]> getKey = Task.Run(() =>
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
            Task<byte[]> getKey = Task.Run(() =>
            {
                return Rfc2898DeriveBytes.Pbkdf2(password, _salt, 210000, HashAlgorithmName.SHA512, 32);
            });
            _key = await getKey;
            try
            {
                DecryptStringAES256(_lockedKey, _key);
            }
            catch (CryptographicException)
            {
                _key = null; // Incorrect password provided.
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
        /// Check if this <see cref="Wallet"/> is linked to IMX.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> IsLinkedAsync()
        {
            if (_isLinked != null) return (bool)_isLinked;
            bool? result = await IsAddressLinkedAsync(Address);
            if (result == null)
            {
                return false;
            }
            if ((bool)result)
                _isLinked = true;
            return (bool)result;
        }

        /// <summary>
        /// Request that the user link this wallet to IMX.
        /// </summary>
        /// <param name="parent">The window used to center the request window on.</param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException">Thrown if IMXlib does not return any data. This should never happen.</exception>
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
                IntPtr resultBuffer = Marshal.AllocHGlobal(500);
                string? result = IntPtrToUtf8String(imx_register_address(GetPrivateKey(), resultBuffer, 500));
                Marshal.FreeHGlobal(resultBuffer);
                if (result == null)
                    throw new NullReferenceException("IMXLib returned a null reference while registering an address.");
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

        #region Modify Wallet contents.
        /// <summary>
        /// Reduce the total amount of a specific token owned by this <see cref="Wallet"/> on the client side.
        /// </summary>
        /// <param name="token_name">The name of the token to reduce the amount of</param>
        /// <param name="amount">The amount to reduce it by.</param>
        public void DeductTokenAmount(string tokenName, decimal amount)
        {
            if (!_tokenAmountOwned.ContainsKey(tokenName))
            {
                return; // Cannot deduct below 0;
            }
            decimal amountOwned = _tokenAmountOwned[tokenName] - amount;
            if (amountOwned < 0)
                amountOwned = 0;
            _tokenAmountOwned[tokenName] = amountOwned;
        }
        #endregion

        #region IMX trading.
        /// <summary>
        /// Request the user to buy a specific <see cref="Order"/>.
        /// This will purchase immediately if the private key for the <see cref="Wallet"/> is unlocked and available.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="order"></param>
        /// <param name="tbStatus"></param>
        /// <returns></returns>
        virtual public async Task<bool> RequestBuyOrder(Window parent, Order order, TextBlock tbStatus)
        {
            // Unlock user wallet.
            tbStatus.Text = "Waiting for wallet to be unlocked...";
            bool reLock = false;
            if (IsLocked())
            {
                UnlockWalletWindow unlockWindow = new UnlockWalletWindow(this);
                unlockWindow.Owner = parent;
                unlockWindow.ShowDialog();
                if (unlockWindow.Result == UnlockWalletWindow.UnlockResult.Cancel)
                {
                    tbStatus.Text = "Purchase cancelled";
                    return false;
                }
                if (unlockWindow.Result == UnlockWalletWindow.UnlockResult.Relock)
                    reLock = true;
            }

            // Prompt IMXlib to purchase the order.
            tbStatus.Text = "Submitting order to IMX...";
            Task<string?> purchaseOrder = Task.Run(() =>
            {
                int bufferSize = 1024;
                IntPtr resultBuffer = Marshal.AllocHGlobal(bufferSize);
                string? result = IntPtrToString(imx_buy_order(order.OrderID.ToString(), (double)order.PriceTotal(), new Fee[0], 0, GetPrivateKey(), resultBuffer, bufferSize));
                Marshal.FreeHGlobal(resultBuffer);
                return result;
            });
            string? result = await purchaseOrder;

            // Relock the wallet if requested by the user.
            if (reLock)
                LockWallet();

            // Handle the server response.
            if (result == null)
            {
                tbStatus.Text = "An unknown error occurred";
                return false;
            }

            if (!result.Contains("trade_id"))
            {
                JObject? jsonResult = (JObject?)JsonConvert.DeserializeObject(result);
                string? message = (string?)jsonResult?.SelectToken("message");
                if (message == null)
                {
                    tbStatus.Text = "An unknown error occurred";
                    return false;
                }
                tbStatus.Text = message;
                return false;
            }
            tbStatus.Text = "Purchase complete";
            return true;
        }

        /// <summary>
        /// Request the user to List one or multiple cards.
        /// This will submit the listings immediately if the private key for the <see cref="Wallet"/> is unlocked and available.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="order"></param>
        /// <param name="tbStatus"></param>
        /// <returns></returns>
        virtual public async Task<Dictionary<NFT, bool>> RequestCreateOrders(Window parent, (NFT card, string tokenID, double price, TextBlock? tbStatusListing)[] listings, TextBlock tbStatus)
        {
            // Unlock user wallet.
            foreach ((NFT card, string tokenID, double price, TextBlock? tbStatusListing) listing in listings)
            {
                if (listing.tbStatusListing != null) listing.tbStatusListing.Text = "Waiting for wallet to be unlocked...";
            }
            tbStatus.Text = "Waiting for wallet to be unlocked...";
            bool reLock = false;
            if (IsLocked())
            {
                UnlockWalletWindow unlockWindow = new UnlockWalletWindow(this);
                unlockWindow.Owner = parent;
                unlockWindow.ShowDialog();
                if (unlockWindow.Result == UnlockWalletWindow.UnlockResult.Cancel)
                {
                    foreach ((NFT card, string tokenID, double price, TextBlock? tbStatusListing) listing in listings)
                    {
                        if (listing.tbStatusListing != null)
                        {
                            listing.tbStatusListing.Text = "Listing cancelled.";
                        }
                        tbStatus.Text = "Listing(s) cancelled.";
                    }
                    return listings.ToDictionary(x => x.card, x => false); ;
                }
                reLock = unlockWindow.Result == UnlockWalletWindow.UnlockResult.Relock;
            }

            // Prompt IMXlib to submit the listings.
            tbStatus.Text = "Submitting listing(s) to IMX...";
            List<Task<(NFT card, bool result)>> listTasks = new();
            foreach ((NFT card, string tokenID, double price, TextBlock? tbStatusListing) listing in listings)
            {
                // Create a task for each listing so they can run asynchronously.
                Task<(NFT card, bool result)> createListing = Task.Run(() =>
                {
                    // Submit the listing.
                    int bufferSize = 1024;
                    IntPtr resultBuffer = Marshal.AllocHGlobal(bufferSize);
                    string? result = IntPtrToString(imx_sell_nft(listing.card.token_address, listing.card.token_id.ToString(), listing.tokenID, listing.price, new Fee[0], 0, GetPrivateKey(), resultBuffer, bufferSize));
                    Marshal.FreeHGlobal(resultBuffer);

                    // Handle the server response
                    Console.WriteLine(result ?? "No result");
                    if (result == null)
                    {
                        if (listing.tbStatusListing != null) listing.tbStatusListing.Text = "An unknown error occurred";
                        return (listing.card, false);
                    }
                    if (!result.Contains("order_id"))
                    {
                        JObject? jsonResult = (JObject?)JsonConvert.DeserializeObject(result);
                        string? message = (string?)jsonResult?.SelectToken("message");
                        if (message == null)
                        {
                            if (listing.tbStatusListing != null) listing.tbStatusListing.Text = "An unknown error occurred";
                            return (listing.card, false);
                        }
                        if (listing.tbStatusListing != null) listing.tbStatusListing.Text = message;
                        return (listing.card, false);
                    }
                    return (listing.card, true);
                });
                // Add the task to a list of tasks so we can track when they finish.
                listTasks.Add(createListing);
            }
            // Wait for list tasks to finish.
            (NFT card, bool res)[] results = await Task.WhenAll(listTasks);

            // Relock the wallet if requested by the user.
            if (reLock)
                LockWallet();

            // Inform the user about the combined result of all listings.
            if (results.All(x => x.res))
            {
                tbStatus.Text = $"Listing{(listings.Count() > 1 ? "s" : "")} submitted to IMX";
            }
            else if (results.All(x => !x.res))
            {
                tbStatus.Text = $"Listing{(listings.Count() > 1 ? "s" : "")} submission failed";
            }
            else
            {
                tbStatus.Text = "Not all listings were submitted.";
            }

            return results.ToDictionary(x => x.card, x => x.res);
        }

        /// <summary>
        /// Request the user to cancel one or multiple active orders.
        /// This will cancel the orders immediately if the private key for the <see cref="Wallet"/> is unlocked and available.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="order"></param>
        /// <param name="tbStatus"></param>
        /// <returns></returns>
        virtual public async Task<Dictionary<string, bool>> RequestCancelOrders(Window parent, (string orderID, TextBlock? tbStatusCancellation)[] orders, TextBlock tbStatus)
        {
            // Unlock user wallet.
            foreach ((string orderID, TextBlock? tbStatusCancellation) order in orders)
            {
                if (order.tbStatusCancellation != null) order.tbStatusCancellation.Text = "Waiting for wallet to be unlocked...";
            }
            tbStatus.Text = "Waiting for wallet to be unlocked...";
            bool reLock = false;
            if (IsLocked())
            {
                UnlockWalletWindow unlockWindow = new UnlockWalletWindow(this);
                unlockWindow.Owner = parent;
                unlockWindow.ShowDialog();
                if (unlockWindow.Result == UnlockWalletWindow.UnlockResult.Cancel)
                {
                    foreach ((string orderID, TextBlock? tbStatusCancellation) order in orders)
                    {
                        if (order.tbStatusCancellation != null)
                        {
                            order.tbStatusCancellation.Text = "User cancelled action.";
                        }
                        tbStatus.Text = "Listing(s) cancelled.";
                    }
                    return orders.ToDictionary(x => x.orderID, x => false);
                }
                reLock = unlockWindow.Result == UnlockWalletWindow.UnlockResult.Relock;
            }

            // Prompt IMXlib to cancel orders.
            tbStatus.Text = $"Cancelling listing{(orders.Count() > 1 ? "s" : "")} on IMX...";
            List<Task<(string orderID, bool result)>> cancelTasks = new();
            foreach ((string orderID, TextBlock? tbStatusCancellation) order in orders)
            {
                // Create a task for each cancellation so they can run asynchronously.
                Task<(string orderID, bool result)> cancelListing = Task.Run(() =>
                {
                    // Cancel the order.
                    int bufferSize = 1024;
                    IntPtr resultBuffer = Marshal.AllocHGlobal(bufferSize);
                    string? result = IntPtrToString(imx_cancel_order(order.orderID, GetPrivateKey(), resultBuffer, bufferSize));
                    Marshal.FreeHGlobal(resultBuffer);

                    // Handle the server response.
                    if (result == null)
                    {
                        if (order.tbStatusCancellation != null) order.tbStatusCancellation.Text = "An unknown error occurred";
                        return (order.orderID, false);
                    }
                    if (!result.Contains("order_id"))
                    {
                        JObject? jsonResult = (JObject?)JsonConvert.DeserializeObject(result);
                        string? message = (string?)jsonResult?.SelectToken("message");
                        if (message == null)
                        {
                            if (order.tbStatusCancellation != null) order.tbStatusCancellation.Text = "An unknown error occurred";
                            return (order.orderID, false);
                        }
                        if (order.tbStatusCancellation != null) order.tbStatusCancellation.Text = message;
                        return (order.orderID, false);
                    }
                    return (order.orderID, true);
                });
                cancelTasks.Add(cancelListing);
            }
            // Wait for all cancellation tasks to finish.
            (string orderID, bool res)[] results = await Task.WhenAll(cancelTasks);
            if (reLock)
                LockWallet();

            // Inform the user about the combined result of all cancellations.
            if (results.All(x => x.res))
            {
                tbStatus.Text = $"Listing{(orders.Count() > 1 ? "s" : "")} cancelled on IMX";
            }
            else if (results.All(x => !x.res))
            {
                tbStatus.Text = $"Listing{(orders.Count() > 1 ? "s" : "")} cancellation failed";
            }
            else
            {
                tbStatus.Text = "Not all listings were cancelled.";
            }

            return results.ToDictionary(x => x.orderID, x => x.res);
        }

        /// <summary>
        /// Request the user to transfer one or multiple cards.
        /// This will transfer the cards immediately if the private key for the <see cref="Wallet"/> is unlocked and available.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="order"></param>
        /// <param name="tbStatus"></param>
        /// <returns></returns>
        virtual public async Task<Dictionary<NFT, bool>> RequestTransferCards(Window parent, NFT[] cards, string receiverAddress, TextBlock tbStatus)
        {
            // Check if the receiverAddress is valid and registered on IMX.
            bool? receiverLinked = await IsAddressLinkedAsync(receiverAddress);
            if (receiverLinked == null)
            {
                tbStatus.Text = "Failed to confirm the receiver wallet is linked to IMX.";
                return cards.ToDictionary(x => x, x => false);
            }
            if (!(bool)receiverLinked)
            {
                tbStatus.Text = "Receiver wallet is not linked to IMX.";
                return cards.ToDictionary(x => x, x => false);
            }

            // Unlock user wallet.
            tbStatus.Text = "Waiting for wallet to be unlocked...";
            bool reLock = false;
            if (IsLocked())
            {
                UnlockWalletWindow unlockWindow = new UnlockWalletWindow(this);
                unlockWindow.Owner = parent;
                unlockWindow.ShowDialog();
                if (unlockWindow.Result == UnlockWalletWindow.UnlockResult.Cancel)
                {
                    return cards.ToDictionary(x => x, x => false);
                }
                reLock = unlockWindow.Result == UnlockWalletWindow.UnlockResult.Relock;
            }

            // Prompt IMXlib to submit the listings.
            tbStatus.Text = $"Submitting transfer{(cards.Count() > 1 ? "s" : "")} to IMX...";

            // Create a task for each listing so they can run asynchronously.
            Task<string?> transferCards = Task.Run(() =>
            {
                int bufferSize = 1024;
                IntPtr resultBuffer = Marshal.AllocHGlobal(bufferSize);
                string? result = IntPtrToString(imx_transfer_nfts(cards, cards.Count(), receiverAddress, GetPrivateKey(), resultBuffer, bufferSize));
                Marshal.FreeHGlobal(resultBuffer);
                return result;
            });
            string? result = await transferCards;

            // Relock the wallet if requested by the user.
            if (reLock)
                LockWallet();

            // Handle the server response
            Console.WriteLine(result ?? "No result");
            if (result == null)
            {
                tbStatus.Text = "An unknown error occurred";
                return cards.ToDictionary(x => x, x => false);
            }
            if (!result.Contains("transfer_ids"))
            {
                JObject? jsonResult = (JObject?)JsonConvert.DeserializeObject(result);
                string? message = (string?)jsonResult?.SelectToken("message");
                if (message == null)
                {
                    tbStatus.Text = "An unknown error occurred";
                    return cards.ToDictionary(x => x, x => false);
                }
                tbStatus.Text = message;
                return cards.ToDictionary(x => x, x => false);
            }
            tbStatus.Text = $"Transfer{(cards.Count() > 1 ? "s" : "")} submitted to IMX";
            return cards.ToDictionary(x => x, x => true);
        }
        #endregion

        #region Supporting functions.
        /// <summary>
        /// Calculate the address belonging to this wallet.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        virtual protected string CalculateAddress()
        {
            IntPtr addressBuffer = Marshal.AllocHGlobal(43);
            string? address = IntPtrToString(eth_get_address(GetPrivateKey(), addressBuffer, 43));
            Marshal.FreeHGlobal(addressBuffer);
            if (address == null)
                throw new NullReferenceException("IMXLib returned a null reference while generating an address.");
            return address;
        }
        #endregion
    }

    /// <summary>
    /// Used to interact with a wallet in the browser like MetaMask.
    /// </summary>
    [Serializable()]
    internal class WebWallet : Wallet
    {
        #region Default Constructor.
        /// <summary>
        /// Generate a new webwallet, instead of a private key, the signature to the IMX seed message is provided.
        /// </summary>
        /// <param name="privateKey"></param>
        /// <param name="address"></param>
        /// <param name="password"></param>
        public WebWallet(string imxSeedSignature, string address, string password = "password") : base(imxSeedSignature, password)
        {
            Address = address;
        }
        #endregion

        #region IMX link status.
        /// <summary>
        /// Request that the user link this wallet to IMX.
        /// </summary>
        /// <param name="parent"></param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        public override async Task<bool> RequestLinkAsync(Window parent)
        {
            MessageWindow window = new MessageWindow("Before your wallet can be used to trade on IMX, it must be linked to the platform.\nWould you like to link it now?", "Link wallet", MessageType.CONFIRM);
            window.Owner = parent;
            window.ShowDialog();
            if (!window.Result)
            {
                return false;
            }
            Task<SignatureData> fetchSignature = SignatureRequestServer.RequestSignatureAsync(IMX_LINK_MESSAGE);
            UseWebWalletWindow useWalletWindow = new(fetchSignature);
            useWalletWindow.Owner = parent;
            useWalletWindow.ShowDialog();
            try
            {
                SignatureData linkSignature = await fetchSignature;
                if (IsLocked())
                    await UnlockWallet();
                Task<bool> linkWallet = Task.Run(() =>
                {
                    IntPtr resultBuffer = Marshal.AllocHGlobal(500);
                    string? result = IntPtrToUtf8String(imx_register_address_presigned(Address, linkSignature.Signature, GetPrivateKey(), resultBuffer, 500));
                    Marshal.FreeHGlobal(resultBuffer);
                    if (result == null)
                        throw new NullReferenceException("IMXLib returned a null reference while registering an address.");
                    if (!result.Contains("tx_hash"))
                        return false;
                    return true;
                });
                return await linkWallet;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
        #endregion

        #region IMX trading.
        /// <summary>
        /// Request the user to buy a specific <see cref="Order"/>.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="order"></param>
        /// <param name="tbStatus"></param>
        /// <returns></returns>
        public override async Task<bool> RequestBuyOrder(Window parent, Order order, TextBlock tbStatus)
        {
            // Unlock wallet (using default password).
            tbStatus.Text = "Waiting for wallet...";
            if (IsLocked())
                await UnlockWallet();

            // Fetch data for signature request.
            tbStatus.Text = "Requesting order to purchase...";
            Task<string?> requestBuy = Task.Run(() =>
            {
                int bufferSize = 1024;
                IntPtr resultBuffer = Marshal.AllocHGlobal(bufferSize);
                string? result = IntPtrToUtf8String(imx_request_buy_order(order.OrderID.ToString(), Address, new Fee[0], 0, resultBuffer, bufferSize));
                Marshal.FreeHGlobal(resultBuffer);
                return result;
            });

            // Handle server response.
            string? signableRequest = await requestBuy;
            if (signableRequest == null)
            {
                tbStatus.Text = "An unknown error occurred";
                return false;
            }
            JObject? jsonBuyRequest = (JObject?)JsonConvert.DeserializeObject(signableRequest);
            string? nonce = (string?)jsonBuyRequest?.SelectToken("nonce");
            string? signableMessage = (string?)jsonBuyRequest?.SelectToken("signable_message");
            if (nonce == null || signableMessage == null)
            {
                string? message = (string?)jsonBuyRequest?.SelectToken("message");
                if (message == null)
                {
                    tbStatus.Text = "An unknown error occurred";
                    return false;
                }
                tbStatus.Text = message;
                return false;
            }

            // Request the users signature.
            tbStatus.Text = "Waiting for user signature...";
            Task<SignatureData> fetchSignature = SignatureRequestServer.RequestSignatureAsync(signableMessage);
            UseWebWalletWindow useWalletWindow = new(fetchSignature);
            useWalletWindow.Owner = parent;
            useWalletWindow.ShowDialog();
            SignatureData buySignature;
            try
            {
                buySignature = await fetchSignature;
            }
            catch (OperationCanceledException)
            {
                tbStatus.Text = "Purchase cancelled";
                return false;
            }

            // Prompt IMXlib to buy the order.
            tbStatus.Text = "Submitting order to IMX...";
            Task<string?> purchaseOrder = Task.Run(() =>
            {
                int bufferSize = 1024;
                IntPtr resultBuffer = Marshal.AllocHGlobal(bufferSize);
                string? result = IntPtrToUtf8String(imx_finish_buy_order(nonce, (double)order.PriceTotal(), GetPrivateKey(), buySignature.Signature, resultBuffer, bufferSize));
                Marshal.FreeHGlobal(resultBuffer);
                return result;
            });
            string? result = await purchaseOrder;

            // Handle the server response.
            if (result == null)
            {
                tbStatus.Text = "An unknown error occurred";
                return false;
            }

            if (!result.Contains("trade_id"))
            {
                JObject? jsonResult = (JObject?)JsonConvert.DeserializeObject(result);
                string? message = (string?)jsonResult?.SelectToken("message");
                if (message == null)
                {
                    tbStatus.Text = "An unknown error occurred";
                    return false;
                }
                tbStatus.Text = message;
                return false;
            }
            tbStatus.Text = "Purchase complete";
            return true;
        }

        /// <summary>
        /// Request the user to List one or multiple .
        /// This will submit the listings immediately if the private key for the <see cref="Wallet"/> is unlocked and available.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="order"></param>
        /// <param name="tbStatus"></param>
        /// <returns></returns>
        public override async Task<Dictionary<NFT, bool>> RequestCreateOrders(Window parent, (NFT card, string tokenID, double price, TextBlock? tbStatusListing)[] listings, TextBlock tbStatus)
        {
            // Unlock wallet (using default password).
            foreach ((NFT card, string tokenID, double price, TextBlock? tbStatusListing) listing in listings)
            {
                if (listing.tbStatusListing != null) listing.tbStatusListing.Text = "Waiting for wallet...";
            }
            tbStatus.Text = "Waiting for wallet...";
            if (IsLocked())
                await UnlockWallet();

            // Fetch data for signature requests.
            tbStatus.Text = "Preparing orders to create...";
            List<(Task<string?>, NFT card, TextBlock?)> prepareTasks = new();
            foreach ((NFT card, string tokenID, double price, TextBlock? tbStatusListing) listing in listings)
            {
                Task<string?> createListing = Task.Run(() =>
                {
                    int bufferSize = 1024;
                    IntPtr resultBuffer = Marshal.AllocHGlobal(bufferSize);
                    string? result = IntPtrToUtf8String(imx_request_sell_nft(listing.card.token_address, listing.card.token_id.ToString(), listing.tokenID, listing.price, new Fee[0], 0, Address, resultBuffer, bufferSize));
                    Marshal.FreeHGlobal(resultBuffer);
                    return result;
                });
                prepareTasks.Add((createListing, listing.card, listing.tbStatusListing));
            }
            await Task.WhenAll(prepareTasks.Select(x => x.Item1));

            // Request user signatures.
            tbStatus.Text = $"Waiting for user signature{(listings.Count() > 1 ? "s" : "")}...";
            List<Task<SignatureData>> sigTasks = new();
            List<(Task<bool>, NFT card, TextBlock? tbStatusListing)> listTasks = new();
            foreach ((Task<string?> prep, NFT card, TextBlock? tbStatusListing) prepareTask in prepareTasks)
            {
                // Check if the data is valid.
                string? data = await prepareTask.prep;
                if (data == null)
                {
                    if (prepareTask.tbStatusListing != null) prepareTask.tbStatusListing.Text = "Order creation failed (No data)";
                    continue;
                }
                JObject? jsonData = (JObject?)JsonConvert.DeserializeObject(data);
                if (jsonData == null)
                {
                    if (prepareTask.tbStatusListing != null) prepareTask.tbStatusListing.Text = "Order creation failed (Invalid JSON)";
                    continue;
                }

                string? nonce = (string?)jsonData?.SelectToken("nonce");
                string? signableMessage = (string?)jsonData?.SelectToken("signable_message");
                if (nonce == null || signableMessage == null)
                {
                    string? message = (string?)jsonData?.SelectToken("message");
                    if (message == null)
                    {
                        if (prepareTask.tbStatusListing != null) prepareTask.tbStatusListing.Text = "Order creation failed (Invalid JSON)";
                        continue;
                    }
                    if (prepareTask.tbStatusListing != null) prepareTask.tbStatusListing.Text = message;
                    continue;
                }

                // Request signature.
                Task<SignatureData> getSignature = SignatureRequestServer.RequestSignatureAsync(signableMessage);

                // Submit listing to IMX.
                Task<bool> createListing = Task.Run(async () =>
                {
                    SignatureData signature;
                    try
                    {
                        signature = await getSignature;
                    }
                    catch (OperationCanceledException)
                    {
                        if (prepareTask.tbStatusListing != null) prepareTask.tbStatusListing.Text = "Order creation failed (Cancelled by user)";
                        return false;
                    }

                    int bufferSize = 1024;
                    IntPtr resultBuffer = Marshal.AllocHGlobal(bufferSize);
                    string? result = IntPtrToUtf8String(imx_finish_sell_or_offer_nft(nonce, GetPrivateKey(), signature.Signature, resultBuffer, bufferSize));
                    Marshal.FreeHGlobal(resultBuffer);
                    Console.WriteLine(result ?? "No result");
                    if (result == null)
                    {
                        if (prepareTask.tbStatusListing != null) prepareTask.tbStatusListing.Text = "An unknown error occurred";
                        return false;
                    }
                    if (!result.Contains("order_id"))
                    {
                        JObject? jsonResult = (JObject?)JsonConvert.DeserializeObject(result);
                        string? message = (string?)jsonResult?.SelectToken("message");
                        if (message == null)
                        {
                            if (prepareTask.tbStatusListing != null) prepareTask.tbStatusListing.Text = "An unknown error occurred";
                            return false;
                        }
                        if (prepareTask.tbStatusListing != null) prepareTask.tbStatusListing.Text = message;
                        return false;
                    }
                    return true;
                });

                sigTasks.Add(getSignature);
                listTasks.Add((createListing, prepareTask.card, prepareTask.tbStatusListing));
            }

            // Show signature request window.
            UseWebWalletWindow useWalletWindow = new(sigTasks);
            useWalletWindow.Owner = parent;
            useWalletWindow.ShowDialog();

            // Wait for listings to finish.
            Dictionary<NFT, bool> results = (await Task.WhenAll(listTasks.Select(async x =>
            {
                try
                {
                    return (x.card, await x.Item1);
                }
                catch (Exception)
                {
                    return (x.card, false);
                }
            }))).ToDictionary(x => x.card, x => x.Item2);

            // Inform the user of the listing result.
            if (results.All(x => x.Value))
            {
                tbStatus.Text = $"Listing{(listings.Count() > 1 ? "s" : "")} submitted to IMX";
            }
            else if (results.All(x => !x.Value))
            {
                tbStatus.Text = $"Listing submission{(listings.Count() > 1 ? "s" : "")} failed";
            }
            else
            {
                tbStatus.Text = "Not all listings were submitted.";
            }
            return results;
        }

        /// <summary>
        /// Request the user to cancel one or multiple active orders.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="order"></param>
        /// <param name="tbStatus"></param>
        /// <returns></returns>
        public override async Task<Dictionary<string, bool>> RequestCancelOrders(Window parent, (string orderID, TextBlock? tbStatusCancellation)[] orders, TextBlock tbStatus)
        {
            // Unlock user wallet.
            foreach ((string orderID, TextBlock? tbStatusCancellation) order in orders)
            {
                if (order.tbStatusCancellation != null) order.tbStatusCancellation.Text = "Waiting for wallet to be unlocked...";
            }
            tbStatus.Text = "Waiting for wallet...";
            if (IsLocked())
                await UnlockWallet();

            // Fetch data for signature requests.
            tbStatus.Text = "Preparing orders to cancel...";
            List<(Task<string?>, string orderID, TextBlock?)> prepareTasks = new();
            foreach ((string orderID, TextBlock? tbStatusCancellation) order in orders)
            {
                Task<string?> cancelListing = Task.Run(() =>
                {
                    int bufferSize = 1024;
                    IntPtr resultBuffer = Marshal.AllocHGlobal(bufferSize);
                    string? result = IntPtrToUtf8String(imx_request_cancel_order(order.orderID, resultBuffer, bufferSize));
                    Marshal.FreeHGlobal(resultBuffer);
                    return result;
                });
                prepareTasks.Add((cancelListing, order.orderID, order.tbStatusCancellation));
            }
            await Task.WhenAll(prepareTasks.Select(x => x.Item1));

            // Request user signatures.
            tbStatus.Text = $"Waiting for user signature{(orders.Count() > 1 ? "s" : "")}...";
            List<Task<SignatureData>> sigTasks = new();
            List<(Task<bool>, string orderID, TextBlock? tbStatusListing)> cancelTasks = new();
            foreach ((Task<string?> prep, string orderID, TextBlock? tbStatusListing) prepareTask in prepareTasks)
            {
                // Check if the data is valid.
                string? signableMessage = await prepareTask.prep;
                if (signableMessage == null)
                {
                    if (prepareTask.tbStatusListing != null) prepareTask.tbStatusListing.Text = "Order cancellation failed (No data)";
                    continue;
                }

                // Request signature.
                Task<SignatureData> getSignature = SignatureRequestServer.RequestSignatureAsync(signableMessage);

                // Submit cancellation to IMX.
                Task<bool> cancelListing = Task.Run(async () =>
                {
                    SignatureData signature;
                    try
                    {
                        signature = await getSignature;
                    }
                    catch (OperationCanceledException)
                    {
                        if (prepareTask.tbStatusListing != null) prepareTask.tbStatusListing.Text = "Order cancellation failed (Cancelled by user)";
                        return false;
                    }

                    int bufferSize = 1024;
                    IntPtr resultBuffer = Marshal.AllocHGlobal(bufferSize);
                    string? result = IntPtrToUtf8String(imx_finish_cancel_order(prepareTask.orderID, Address, GetPrivateKey(), signature.Signature, resultBuffer, bufferSize));
                    Marshal.FreeHGlobal(resultBuffer);
                    Console.WriteLine(result ?? "No result");
                    if (result == null)
                    {
                        if (prepareTask.tbStatusListing != null) prepareTask.tbStatusListing.Text = "An unknown error occurred";
                        return false;
                    }
                    if (!result.Contains("order_id"))
                    {
                        JObject? jsonResult = (JObject?)JsonConvert.DeserializeObject(result);
                        string? message = (string?)jsonResult?.SelectToken("message");
                        if (message == null)
                        {
                            if (prepareTask.tbStatusListing != null) prepareTask.tbStatusListing.Text = "An unknown error occurred";
                            return false;
                        }
                        if (prepareTask.tbStatusListing != null) prepareTask.tbStatusListing.Text = message;
                        return false;
                    }
                    return true;
                });

                sigTasks.Add(getSignature);
                cancelTasks.Add((cancelListing, prepareTask.orderID, prepareTask.tbStatusListing));
            }

            // Show signature request window.
            UseWebWalletWindow useWalletWindow = new(sigTasks);
            useWalletWindow.Owner = parent;
            useWalletWindow.ShowDialog();


            // Wait for all cancellation tasks to finish.
            Dictionary<string, bool> results = (await Task.WhenAll(cancelTasks.Select(async x =>
            {
                try
                {
                    return (x.orderID, await x.Item1);
                }
                catch (Exception)
                {
                    return (x.orderID, false);
                }
            }))).ToDictionary(x => x.orderID, x => x.Item2);
            //Dictionary<string, bool> results = (await Task.WhenAll(cancelTasks.Select(async x => (x.orderID, await x.Item1)))).ToDictionary(x => x.orderID, x => x.Item2);

            // Inform the user about the combined result of all cancellations.
            if (results.All(x => x.Value))
            {
                tbStatus.Text = $"Listing{(orders.Count() > 1 ? "s" : "")} cancelled on IMX";
            }
            else if (results.All(x => !x.Value))
            {
                tbStatus.Text = $"Listing cancellation{(orders.Count() > 1 ? "s" : "")} failed";
            }
            else
            {
                tbStatus.Text = "Not all listings were cancelled.";
            }
            return results;
        }
        #endregion

        #region Supporting functions.
        /// <summary>
        /// No address can be calculated since no private key is available.
        /// </summary>
        /// <returns></returns>
        protected override string CalculateAddress()
        {
            // Do nothing, we can't calculate the address for this wallet.
            return "";
        }
        #endregion
    }
}
