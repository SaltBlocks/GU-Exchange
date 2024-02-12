using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace GU_Exchange
{
    /// <summary>
    /// Class used to define a card in Gods unchained.
    /// </summary>
    public class CardData
    {
        #region Class properties
        /// <summary>
        /// ProtoID of the card.
        /// </summary>
        public int ProtoID { get; set; }
        /// <summary>
        /// The name of the card.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The text on the body of the card.
        /// </summary>
        public string Effect { get; set; }
        /// <summary>
        /// The domain this card belongs to.
        /// </summary>
        public string God { get; set; }
        /// <summary>
        /// The rarity of the card (common, rare, epic, legendary)
        /// </summary>
        public string Rarity { get; set; }
        /// <summary>
        /// The tribe this card belongs to.
        /// </summary>
        public string Tribe { get; set; }
        /// <summary>
        /// The cards mana cost.
        /// </summary>
        public int Mana { get; set; }
        /// <summary>
        /// The attack of the card. (0 if it's a spell)
        /// </summary>
        public int Attack { get; set; }
        /// <summary>
        /// The health of the card. (0 if it's a spell)
        /// </summary>
        public int Health { get; set; }
        /// <summary>
        /// The card type.
        /// </summary>
        public string Type { get; set; }
        /// <summary>
        /// The set the card belongs to.
        /// </summary>
        public string Set { get; set; }
        /// <summary>
        /// The Library ID of the card.
        /// </summary>
        public string LibID { get; set; }

        #endregion

        #region Default Constructor

        /// <summary>
        /// Constructs a <see cref="CardData"/> object containing all information about a specific card.
        /// </summary>
        /// <param name="proto"></param>
        /// <param name="name"></param>
        /// <param name="effect"></param>
        /// <param name="god"></param>
        /// <param name="rarity"></param>
        /// <param name="tribe"></param>
        /// <param name="mana"></param>
        /// <param name="attack"></param>
        /// <param name="health"></param>
        /// <param name="type"></param>
        /// <param name="set"></param>
        public CardData(int proto, string name, string effect, string god, string rarity, string tribe, int mana, int attack, int health, string type, string set, string libID)
        {
            this.ProtoID = proto;
            this.Name = name;
            this.Effect = effect;
            this.God = god;
            this.Rarity = rarity;
            this.Tribe = tribe;
            this.Mana = mana;
            this.Attack = attack;
            this.Health = health;
            this.Type = type;
            this.Set = set;
            this.LibID = libID;
        }

        #endregion

        #region Supporting Methods

        /// <summary>
        /// Checks if two <see cref="CardData"/> objects are identical.
        /// </summary>
        /// <param name="other">The <see cref="CardData"/> object to compare to.</param>
        /// <returns>True if the objects are equal.</returns>
        public bool Equals(CardData other)
        {
            if (other == null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (this.Name == other.Name && this.Effect == other.Effect && this.God == other.God &&
                this.Rarity == other.Rarity && this.Tribe == other.Tribe && this.Mana == other.Mana &&
                this.Attack == other.Attack && this.Health == other.Health && this.Type == other.Type &&
                this.Set == other.Set) return true;
            return false;
        }

        #endregion
    }

    /// <summary>
    /// Class used for storing player apolloIDs and last known usernames.
    /// This is used for searching players by name.
    /// </summary>
    [DataContract]
    public class PlayerData
    {
        #region Class properties

        [DataMember]
        public Dictionary<string, string> Players { get; set; }

        [DataMember]
        public DateTime Timestamp { get; set; }

        #endregion

        #region Contructors
        public PlayerData()
        {
            Players = new Dictionary<string, string>();
            Timestamp = DateTime.UnixEpoch;
        }

        public PlayerData(Dictionary<string, string> players, DateTime timeStamp)
        {
            Players = players;
            Timestamp = timeStamp;
        }

        #endregion
    }

    class GameDataManager
    {
        #region Static Fields
        // Loaded players, cards and prices.
        public static PlayerData players = new();
        private static Task? s_setupCardsTask;
        private static Dictionary<int, CardData> s_loadedCardList = new();
        private static readonly Task<Dictionary<int, decimal>> s_cardPriceFetchTask = FetchCardPricesAsync();

        // Wallets linked to players.
        private static Dictionary<int, JObject> _playerWalletData = new();

        // Data used for searching cards.
        private static readonly Task s_setupQueries = SetupQueriesAsync();
        private static readonly HashSet<string> s_setList = new();
        private static readonly HashSet<string> s_godList = new();
        private static readonly HashSet<string> s_rarityList = new();
        private static readonly HashSet<string> s_tribeList = new();
        
        // Cancellation tokens.
        private static CancellationTokenSource? fetchGamesTokenSource = null;
        #endregion

        #region Fetch and query player names
        
        /// <summary>
        /// Fetch a list of all player names for which an apolloID is known.
        /// </summary>
        /// <returns>List of player names.</returns>
        public static List<string> FetchPlayerNames()
        {
            HashSet<string> result = new HashSet<string>();
            foreach (string name in players.Players.Values)
            {
                result.Add(name);
            }
            return result.ToList<string>();
        }

        /// <summary>
        /// Fetch a players current playername.
        /// </summary>
        /// <param name="apolloId">The apolloID of the user for which to fetch the name.</param>
        /// <param name="cancelToken">Canceltoken to stop the request.</param>
        /// <returns>Task returning the players name.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if no user is found with the provided apolloID.</exception>
        public static async Task<string> FetchPlayerNameAsync(int apolloId, CancellationToken cancelToken)
        {
            if (players.Players.ContainsKey(apolloId.ToString()))
            {
                return players.Players[apolloId.ToString()];
            }
            string url = $"https://api.godsunchained.com/v0/properties?user_id={apolloId}";
            string strUserData = await ResourceManager.Client.GetStringAsync(url, cancelToken);
            JObject? jsonUser = (JObject?)JsonConvert.DeserializeObject(strUserData);
            if (jsonUser != null)
            {
                JToken? records = jsonUser.GetValue("records");
                if (records != null && records.Count() > 0)
                {
                    JToken? record = records[0];
                    string? name = record?.Value<string?>("username");
                    if (name != null)
                    {
                        players.Players.Add(apolloId.ToString(), name);
                        return name;
                    }
                }
            }
            throw new KeyNotFoundException("User not found.");
        }

        /// <summary>
        /// Create a list of all known players using a provided playerName.
        /// </summary>
        /// <param name="playerName">The playername to search for.</param>
        /// <returns>A list of players with the provided name.</returns>
        public static List<int> FetchApolloIDs(string playerName)
        {
            List<int> result = new();
            playerName = playerName.ToLower();
            foreach (string apolloID in players.Players.Keys)
            {
                if (playerName.Equals(players.Players[apolloID].ToLower()))
                {
                    result.Add(int.Parse(apolloID));
                }
            }
            return result;
        }

        /// <summary>
        /// Save the list of known players to the disk.
        /// </summary>
        public static void SavePlayers()
        {
            using (FileStream fileStream = new FileStream("players.db", FileMode.Create))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(PlayerData));
                serializer.WriteObject(fileStream, players);
            }
        }

        /// <summary>
        /// Load the list of known players from the disk.
        /// </summary>
        public static void LoadPlayers()
        {
            if (File.Exists("players.db"))
            {
                using (FileStream fileStream = new FileStream("players.db", FileMode.Open))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(PlayerData));
                    PlayerData? loadedPlayerData = (PlayerData?)serializer.ReadObject(fileStream);
                    if (loadedPlayerData != null)
                    {
                        players = loadedPlayerData;
                    }
                }
            }
            else
            {
                Console.WriteLine($"File not found: players.db");
            }
        }

        /// <summary>
        /// Load player list from the disk and fetch players if the stored data was collected over 24 hours ago.
        /// </summary>
        /// <returns>A task that updates the player data.</returns>
        public static async Task SetupPlayerDataAsync()
        {
            LoadPlayers();
            TimeSpan timePassed = DateTime.Now - players.Timestamp;
            if (timePassed.Days == 0)
            {
                return;
            }
            try
            {
                string? data;
                using (var request = new HttpRequestMessage(HttpMethod.Get, "https://gudecks.com/externalFiles/Users.js"))
                {
                    request.Headers.Add("Host", "gudecks.com");
                    request.Headers.Add("Connection", "keep-alive");
                    request.Headers.Add("sec-ch-ua", "\"Microsoft Edge\";v=\"119\", \"Chromium\";v=\"119\", \"Not?A_Brand\";v=\"24\"");
                    request.Headers.Add("sec-ch-ua-mobile", "?0");
                    request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
                    request.Headers.Add("Upgrade-Insecure-Requests", "1");
                    request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36 Edg/119.0.0.0");
                    request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                    request.Headers.Add("Sec-Fetch-Site", "none");
                    request.Headers.Add("Sec-Fetch-Mode", "navigate");
                    request.Headers.Add("Sec-Fetch-User", "?1");
                    request.Headers.Add("Sec-Fetch-Dest", "document");
                    request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                    request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
                    HttpResponseMessage message = await ResourceManager.Client.SendAsync(request);
                    data = await message.Content.ReadAsStringAsync();
                }
                JObject? jsonUsers = (JObject?)JsonConvert.DeserializeObject(data);
                if (jsonUsers == null)
                {
                    return;
                }
                Dictionary<string, string>? userDict = jsonUsers.ToObject<Dictionary<string, string>>();
                if (userDict == null)
                    return;
                players = new PlayerData(userDict, DateTime.Now);
                SavePlayers();
            }
            catch (HttpRequestException)
            {
                Debug.WriteLine("Failed to fetch playernames.");
            }
        }

        #endregion

        #region Fetch and query player connected wallets.
        /// <summary>
        /// Fetch wallets connected to a specific apolloID.
        /// </summary>
        /// <param name="apolloID">The apolloID to get the connected wallets for.</param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        private static async Task<JObject> FetchPlayerConnectedWalletData(int apolloID)
        {
            if (_playerWalletData.ContainsKey(apolloID))
                return _playerWalletData[apolloID];
            byte[] data = await ResourceManager.Client.GetByteArrayAsync($"https://apollo-auth.prod.prod.godsunchained.com/v2/account/{apolloID}");

            JObject? json = (JObject?)JsonConvert.DeserializeObject(System.Text.Encoding.UTF8.GetString(data));
            if (json == null)
                throw new NullReferenceException("Failed to fetch account data.");
            return json;
        }

        /// <summary>
        /// Check if the provided wallet address is linked to the gien apolloID.
        /// </summary>
        /// <param name="apolloID"></param>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        public static async Task<bool> IsWalletLinked(int apolloID, string walletAddress)
        {
            try
            {
                JObject json = await FetchPlayerConnectedWalletData(apolloID);
                JToken? addresses = json.GetValue("addresses");
                if (addresses == null)
                    return false;
                List<string?> addressList = addresses.
                    Select(a => (string?)a["address"]).
                    ToList();
                foreach (var address in addressList)
                {
                    bool? isAddress = address?.ToLower().Equals(walletAddress.ToLower());
                    if (isAddress != null && isAddress == true)
                    {
                        return true;
                    }
                }
            }
            catch (NullReferenceException)
            {
            }
            return false;
        }
        #endregion

        #region Fetch and process games played

        /// <summary>
        /// Fetch GU games played in the past 24 hours and process them for statistics.
        /// </summary>
        public static async void FetchPlayedGamesAsync()
        {
            if (fetchGamesTokenSource != null)
            {
                fetchGamesTokenSource.Cancel();
            }
            fetchGamesTokenSource = new();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            long endTime = now.ToUnixTimeSeconds();
            long startTime = endTime - 60 * 60 * 24;
            string url = $"https://api.godsunchained.com/v0/match?end_time={startTime}-{endTime}&perPage=1000&page=";
            try
            {
                string strGameData = await ResourceManager.Client.GetStringAsync(url + 1, fetchGamesTokenSource.Token);
                JObject? jsonGames = (JObject?)JsonConvert.DeserializeObject(strGameData);
                if (jsonGames == null)
                    return;
                int? gameCount = (int?)jsonGames["total"];
                if (gameCount == null)
                    return;
                processGames(jsonGames);
                Debug.WriteLine($"{gameCount} games played in past 24 hours.");

                try
                {
                    for (int i = 1; i <= gameCount / 1000; i++)
                    {
                        Debug.WriteLine($"{i * 1000} / {gameCount} games fetched.");
                        strGameData = await ResourceManager.Client.GetStringAsync(url + (i + 1), fetchGamesTokenSource.Token);
                        jsonGames = (JObject?)JsonConvert.DeserializeObject(strGameData);
                        if (jsonGames == null)
                            return;
                        processGames(jsonGames);
                    }
                    Debug.WriteLine($"{gameCount} / {gameCount} games fetched.");
                }
                catch (System.Net.Http.HttpRequestException)
                {
                    Console.WriteLine("GU api ratelimit hit.");
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// Process played games for statistics.
        /// </summary>
        /// <param name="jsonGames"></param>
        private static void processGames(JObject jsonGames)
        {
            JToken? games = jsonGames["records"];
            if (games == null)
                return;
            foreach (JToken game in games)
            {
                // Process the game data.
            }
        }

        #endregion

        #region Fetch and process Card data

        /// <summary>
        /// Fetch a snapshot off the current cheapest prices of all cards on the IMX marketplace priced in ETH.
        /// </summary>
        /// <returns>Dictionary containing the protoIDs of cards linked to their market price in ETH.</returns>
        private static async Task<Dictionary<int, decimal>> FetchCardPricesAsync()
        {
            Dictionary<int, decimal> priceList = new();
            Task<string> taskMarket = ResourceManager.Client.GetStringAsync("https://marketplace-api.immutable.com/v1/stacked-assets/0xacb3c6a43d15b907e8433077b6d38ae40936fe2c/search?direction=asc&order_by=buy_quantity_with_fees&page_size=10000&metadata={\"quality\":[\"Meteorite\"]}&token_type=ETH");
            string marketString = await taskMarket;
            JObject? marketData = (JObject?)JsonConvert.DeserializeObject(marketString);
            if (marketData == null)
                return priceList;
            JToken? tokens = marketData.SelectToken("result");
            if (tokens == null)
                return priceList;
            foreach (JToken cardToken in tokens.ToArray())
            {
                string? price = (string?)cardToken.SelectToken("assets_floor_price.quantity_with_fees");
                int? proto = (int?)cardToken.SelectToken("asset_stack_properties.proto");
                if (price != null && proto != null)
                {
                    priceList.Add((int)proto, decimal.Parse(price) / new decimal(Math.Pow(10, 18)));
                }
            }
            return priceList;
        }

        /// <summary>
        /// Get a snapshot off the current cheapest prices of all cards on the IMX marketplace priced in ETH.
        /// Will fetch the data from the marketplace once, after which subsequent calls immediately return this data.
        /// </summary>
        /// <returns></returns>
        public static async Task<Dictionary<int, decimal>> GetCardPricesAsync()
        {
            return await s_cardPriceFetchTask;
        }

        /// <summary>
        /// Get an estimated buy price for a given card in USD.
        /// </summary>
        /// <param name="CardID">The card to estimate the price for.</param>
        /// <returns>A task returning the card price.</returns>
        public static async Task<decimal> GetCardPriceEstimateAsync(int CardID)
        {
            Dictionary<int, decimal> priceList = await GetCardPricesAsync();
            decimal priceETH;
            if (!priceList.TryGetValue(CardID, out priceETH))
            {
                return -1;
            }
            Token EthToken = await Wallet.GetETHToken();
            
            decimal? ConversionRate = EthToken.Value;
            if (ConversionRate == null)
            {
                return -1;
            }
            return priceETH * (decimal) ConversionRate;
        }

        /// <summary>
        /// Fetches the complete list of GU cards from the Gods Unchained API.
        /// </summary>
        private static async Task<Dictionary<int, CardData>?> FetchCardListAsync()
        {
            string CardString;
            try
            {
                Debug.WriteLine("Fetching all GU cards available in game..."); ;
                CardString = await ResourceManager.Client.GetStringAsync("https://api.godsunchained.com/v0/proto?format=flat");
            }
            catch (HttpRequestException)
            {
                return null;
            }

            JObject? jsonData = (JObject?)JsonConvert.DeserializeObject(CardString);
            if (jsonData == null)
                return null;
            Dictionary<string, JObject>? dictObj = jsonData.ToObject<Dictionary<string, JObject>?>();
            if (dictObj == null)
                return null;

            Dictionary<int, CardData> CardList = new();
            foreach (string key in dictObj.Keys)
            {
                int proto = int.Parse(key);
                string? name = (string?)dictObj[key]["name"];
                string? effect = (string?)dictObj[key]["effect"];
                string? god = (string?)dictObj[key]["god"];
                string? rarity = (string?)dictObj[key]["rarity"];
                string? tribe = (string?)dictObj[key]["tribe"];
                int? mana = (int?)dictObj[key]["mana"];
                int? attack = (int?)dictObj[key]["attack"];
                int? health = (int?)dictObj[key]["health"];
                string? type = (string?)dictObj[key]["type"];
                string? set = (string?)dictObj[key]["set"];
                bool? collectable = (bool?)dictObj[key]["collectable"];
                string? lib_id = (string?)dictObj[key]["lib_id"];
                if (name == null || effect == null || god == null || rarity == null ||
                    tribe == null || mana == null || attack == null || health == null ||
                    type == null || set == null || collectable == null || collectable == false || lib_id == null)
                    continue;
                CardData card = new CardData(proto, name, effect, god, rarity,
                    tribe, (int)mana, (int)attack, (int)health, type, set, lib_id);
                CardList[proto] = card;
            }

            using (StreamWriter writer = new StreamWriter("cards.json"))
            {
                writer.Write(CardString);
            }

            return CardList;
        }

        /// <summary>
        /// Retrieve the card list in this inventory from the Disk.
        /// </summary>
        private static async Task<Dictionary<int, CardData>?> GetCachedCardListAsync()
        {
            if (!File.Exists("cards.json"))
                return null;

            string CachedCards;
            using (StreamReader reader = new StreamReader("cards.json"))
                CachedCards = await reader.ReadToEndAsync();

            JObject? jsonData = (JObject?)JsonConvert.DeserializeObject(CachedCards);
            if (jsonData == null)
                return null;
            Dictionary<string, JObject>? dictObj = jsonData.ToObject<Dictionary<string, JObject>?>();
            if (dictObj == null)
                return null;

            Dictionary<int, CardData> CardList = new();
            foreach (string key in dictObj.Keys)
            {
                int proto = int.Parse(key);
                string? name = (string?)dictObj[key]["name"];
                string? effect = (string?)dictObj[key]["effect"];
                string? god = (string?)dictObj[key]["god"];
                string? rarity = (string?)dictObj[key]["rarity"];
                string? tribe = (string?)dictObj[key]["tribe"];
                int? mana = (int?)dictObj[key]["mana"];
                int? attack = (int?)dictObj[key]["attack"];
                int? health = (int?)dictObj[key]["health"];
                string? type = (string?)dictObj[key]["type"];
                string? set = (string?)dictObj[key]["set"];
                bool? collectable = (bool?)dictObj[key]["collectable"];
                string? lib_id = (string?)dictObj[key]["lib_id"];
                if (name == null || effect == null || god == null || rarity == null ||
                    tribe == null || mana == null || attack == null || health == null ||
                    type == null || set == null || collectable == null || collectable == false || lib_id == null)
                    continue;
                CardData card = new CardData(proto, name, effect, god, rarity,
                    tribe, (int)mana, (int)attack, (int)health, type, set, lib_id);
                CardList[proto] = card;
            }
            return CardList;
        }

        /// <summary>
        /// Updates the current cardslist and removes outdated card images.
        /// </summary>
        /// <param name="CachedCardList"></param>
        /// <returns></returns>
        private static async Task SetupCardListAsync(Dictionary<int, CardData>? CachedCardList)
        {
            Dictionary<int, CardData>? CardList = await FetchCardListAsync();
            if (CardList == null || CardList.Count == 0)
                return;
            s_loadedCardList = CardList;
            if (!Directory.Exists("cards") || CachedCardList == null)
                return;
            foreach (int key in CardList.Keys) // Delete images of cards that were modified so the user always sees the most recent one.
            {
                if (File.Exists("cards/" + key + "q4.webp") && (!CachedCardList.ContainsKey(key) || !CachedCardList[key].Equals(CardList[key])))
                {
                    File.Delete("cards/" + key + "q4.webp");
                }
            }
        }

        /// <summary>
        /// Get a list of Gods Unchained Cards that can be traded on the market complete with their card data.
        /// </summary>
        /// <returns>A dictionary containing for each protoID a set of CardData.</returns>
        public static async Task<Dictionary<int, CardData>?> GetCardListAsync()
        {
            if (s_loadedCardList.Count != 0)
                return s_loadedCardList;
            Dictionary<int, CardData>? CachedCardList = await GetCachedCardListAsync();
            if (s_setupCardsTask == null)
                s_setupCardsTask = SetupCardListAsync(CachedCardList);
            if (CachedCardList == null)
            {
                await s_setupCardsTask;
                if (s_loadedCardList.Count != 0)
                    return s_loadedCardList;
            } else
                s_loadedCardList = CachedCardList;
            return CachedCardList;
        }

        #endregion

        #region Card searching

        /// <summary>
        /// Setup the different search types that can be used to filter cards.
        /// </summary>
        /// <returns>A task setting up the search filter items.</returns>
        private static async Task SetupQueriesAsync()
        {
            Dictionary<int, CardData>? data = await GetCardListAsync();
            if (data == null)
                return;
            foreach (CardData card in data.Values)
            {
                char[] set = card.Set.ToCharArray();
                set[0] = char.ToUpper(set[0]);
                s_setList.Add(new string(set));
                char[] god = card.God.ToCharArray();
                god[0] = char.ToUpper(god[0]);
                s_godList.Add(new string(god));
                char[] rarity = card.Rarity.ToCharArray();
                rarity[0] = char.ToUpper(rarity[0]);
                s_rarityList.Add(new string(rarity));
                char[] tribe = card.Tribe.ToCharArray();
                if (tribe.Length > 0)
                    tribe[0] = char.ToUpper(tribe[0]);
                s_tribeList.Add(new string(tribe));
            }
        }

        /// <summary>
        /// Getter for all available card sets.
        /// </summary>
        /// <returns></returns>
        public static async Task<HashSet<string>> getSets()
        {
            await s_setupQueries;
            return s_setList;
        }

        /// <summary>
        /// Getter for all GU gods.
        /// </summary>
        /// <returns></returns>
        public static async Task<HashSet<string>> getGods()
        {
            await s_setupQueries;
            return s_godList;
        }

        /// <summary>
        /// Getter for all card rarities.
        /// </summary>
        /// <returns></returns>
        public static async Task<HashSet<string>> getRarities()
        {
            await s_setupQueries;
            return s_rarityList;
        }

        /// <summary>
        /// Getter for all card tribes.
        /// </summary>
        /// <returns></returns>
        public static async Task<HashSet<string>> getTribes()
        {
            await s_setupQueries;
            return s_tribeList;
        }

        /// <summary>
        /// Find all cards matching the provided search conditions.
        /// </summary>
        /// <param name="searchText">The text that should be present in either the card name or effect text.</param>
        /// <param name="cancelToken">Token used to cancel the search.</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException">Thrown if the search is cancelled.</exception>
        public static async Task<List<CardData>> searchCardsAsync(string searchText, CancellationToken cancelToken, string? set = null, string? god = null, string? rarity = null, string? tribe = null, List<int>? manaCosts = null, string? sort = null)
        {
            Task<List<CardData>> cardsGet = Task.Run(() =>
            {
                List<CardData> result = new();
                List<CardData> textInBody = new();
                HashSet<string> cardsInDeck = CardsInDeck(searchText);
                foreach (CardData card in s_loadedCardList.Values)
                {
                    if (cancelToken.IsCancellationRequested)
                        return result;
                    if (set != null && !card.Set.ToLower().Equals(set.ToLower()))
                        continue;
                    if (god != null && !card.God.ToLower().Equals(god.ToLower()))
                        continue;
                    if (rarity != null && !card.Rarity.ToLower().Equals(rarity.ToLower()))
                        continue;
                    if (tribe != null && !card.Tribe.ToLower().Equals(tribe.ToLower()))
                        continue;
                    if (manaCosts != null && !manaCosts.Contains(card.Mana) && !(card.Mana > 9 && manaCosts.Contains(9)))
                        continue;
                    if (card.Name.ToLower().Contains(searchText.ToLower()) || cardsInDeck.Contains(card.LibID))
                        result.Add(card);
                    else if (card.Effect.ToLower().Contains(searchText.ToLower()))
                        textInBody.Add(card);
                }
                if (sort == null)
                {
                    result = result.OrderBy(x => x.Name.ToLower().IndexOf(searchText.ToLower())).ToList();
                    textInBody = textInBody.OrderBy(x => x.Name.ToLower().IndexOf(searchText.ToLower())).ToList();
                }
                else if (sort.Equals("Rarity (Mythic-Common)"))
                {
                    result = result.OrderBy(card => card, new CardRarityComparer()).Reverse().ToList();
                    textInBody = textInBody.OrderBy(card => card, new CardRarityComparer()).Reverse().ToList();
                }
                else if (sort.Equals("Rarity (Common-Mythic)"))
                {
                    result = result.OrderBy(card => card, new CardRarityComparer()).ToList();
                    textInBody = textInBody.OrderBy(card => card, new CardRarityComparer()).ToList();
                }
                else if (sort.Equals("Price (High-Low)"))
                {
                    result = result.OrderBy(card => card, new CardPriceComparer()).Reverse().ToList();
                    textInBody = textInBody.OrderBy(card => card, new CardPriceComparer()).Reverse().ToList();
                }
                else if (sort.Equals("Price (Low-High)"))
                {
                    result = result.OrderBy(card => card, new CardPriceComparer()).ToList();
                    textInBody = textInBody.OrderBy(card => card, new CardPriceComparer()).ToList();
                }
                else
                {
                    result = result.OrderBy(x => x.Name.ToLower().IndexOf(searchText.ToLower())).ToList();
                    textInBody = textInBody.OrderBy(x => x.Name.ToLower().IndexOf(searchText.ToLower())).ToList();
                }
                result.AddRange(textInBody);
                return result;
            });
            if (cancelToken.IsCancellationRequested)
                throw new OperationCanceledException();
            return await cardsGet;
        }

        #endregion

        #region Deckstring decoding

        /// <summary>
        /// Convert a GU deckstring to a list of LibIDs of the cards in the deck.
        /// </summary>
        /// <param name="deckString">The deckstring to convert</param>
        /// <returns>A HashSet containing all LibIDs contained in the deck.</returns>
        private static HashSet<string> CardsInDeck(string deckString)
        {
            HashSet<string> cardsInDeck = new();
            if (!deckString.StartsWith("GU_"))
            {
                return cardsInDeck;
            }
            string[] parts = deckString.Split("_");
            if (parts.Length == 4)
            {
                string c = parts[3];
                Regex r = new Regex(".{1,3}");
                List<string> groups = r.Matches(c).Select(m => m.Value).ToList();
                foreach (string g in groups)
                {
                    long set = Decode(g.Substring(0, 1));
                    int proto = Decode(g.Substring(1));
                    string formatted = string.Format("L{0}-{1:D3}", set, proto);
                    cardsInDeck.Add(formatted);
                }
            }
            return cardsInDeck;
        }

        private static readonly string base52 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        
        /// <summary>
        /// Decode an encoded card into it's id.
        /// </summary>
        /// <param name="encoded"></param>
        /// <returns></returns>
        private static int Decode(string encoded)
        {
            int id = 0;
            foreach (char c in encoded)
            {
                id = id * base52.Length + base52.IndexOf(c);
            }
            return id;
        }

        #endregion

        #region Comparitor classes for GU cards

        private static CardRarityComparer rarityComp = new CardRarityComparer();
        // Define a custom comparer for the CardData class
        public class CardRarityComparer : IComparer<CardData>
        {
            public int Compare(CardData? x, CardData? y)
            {
                try
                {
                    // Define the order of rarity levels
                    var rarityOrder = new List<string> { "common", "rare", "epic", "legendary", "mythic" };

                    // Get the indices of the rarity levels in the order list
                    int xIndex = rarityOrder.IndexOf(x.Rarity);
                    int yIndex = rarityOrder.IndexOf(y.Rarity);

                    // Compare the indices to determine the sort order
                    return xIndex.CompareTo(yIndex);
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }

        public class CardPriceComparer : IComparer<CardData>
        {
            public int Compare(CardData? x, CardData? y)
            {
                if (s_cardPriceFetchTask == null || !s_cardPriceFetchTask.IsCompletedSuccessfully)
                {
                    return rarityComp.Compare(x, y);
                }

                Dictionary<int, decimal> fetchCardPriceList = s_cardPriceFetchTask.Result;

                if (x == null)
                {
                    return 1;
                }
                else if (y == null)
                {
                    return -1;
                }
                try
                {
                    return fetchCardPriceList[x.ProtoID].CompareTo(fetchCardPriceList[y.ProtoID]);
                }
                catch (Exception)
                {
                    int rareComparison = rarityComp.Compare(x, y);
                    if (rareComparison == 0)
                        return x.Name.CompareTo(y.Name);
                    return rareComparison;
                }
            }
        }

        #endregion
    }
}
