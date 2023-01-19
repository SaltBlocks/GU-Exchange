using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GU_Exchange
{
    public class CardData
    {
        /// <summary>
        /// ProtoID of the card.
        /// </summary>
        public int proto { get; set; }
        /// <summary>
        /// The name of the card.
        /// </summary>
        public string name { get; set; }
        /// <summary>
        /// The text on the body of the card.
        /// </summary>
        public string effect { get; set; }
        /// <summary>
        /// The domain this card belongs to.
        /// </summary>
        public string god { get; set; }
        /// <summary>
        /// The rarity of the card (common, rare, epic, legendary)
        /// </summary>
        public string rarity { get; set; }
        /// <summary>
        /// The tribe this card belongs to.
        /// </summary>
        public string tribe { get; set; }
        /// <summary>
        /// The cards mana cost.
        /// </summary>
        public int mana { get; set; }
        /// <summary>
        /// The attack of the card. (0 if it's a spell)
        /// </summary>
        public int attack { get; set; }
        /// <summary>
        /// The health of the card. (0 if it's a spell)
        /// </summary>
        public int health { get; set; }
        /// <summary>
        /// The card type.
        /// </summary>
        public string type { get; set; }
        /// <summary>
        /// The set the card belongs to.
        /// </summary>
        public string set { get; set; }
        
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
        public CardData(int proto, string name, string effect, string god, string rarity, string tribe, int mana, int attack, int health, string type, string set)
        {
            this.proto = proto;
            this.name = name;
            this.effect = effect;
            this.god = god;
            this.rarity = rarity;
            this.tribe = tribe;
            this.mana = mana;
            this.attack = attack;
            this.health = health;
            this.type = type;
            this.set = set;
        }

        /// <summary>
        /// Checks if two <see cref="CardData"/> objects are identical.
        /// </summary>
        /// <param name="other">The <see cref="CardData"/> object to compare to.</param>
        /// <returns>True if the objects are equal.</returns>
        public bool Equals(CardData other)
        {
            if (other == null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (this.name == other.name && this.effect == other.effect && this.god == other.god &&
                this.rarity == other.rarity && this.tribe == other.tribe && this.mana == other.mana &&
                this.attack == other.attack && this.health == other.health && this.type == other.type &&
                this.set == other.set) return true;
            return false;
        }
    }

    /// <summary>
    /// Class to represent a users inventory.
    /// </summary>
    internal class Inventory
    {
        private static bool listUpdated = false;
        private static Dictionary<int, CardData> CardList = new();
        public static Inventory connectedInventory = new(Settings.globalSettings.apolloID);

        /// <summary>
        /// Find all cards matching the provided search conditions.
        /// </summary>
        /// <param name="searchText">The text that should be present in either the card name or effect text.</param>
        /// <param name="cancelToken">Token used to cancel the search.</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException">Thrown if the search is cancelled.</exception>
        public static async Task<List<CardData>> searchCardsAsync(string searchText, CancellationToken cancelToken)
        {
            Task<List<CardData>> cardsGet = Task.Run(() =>
            {
                List<CardData> result = new();
                List<CardData> textInBody = new();
                foreach (CardData card in CardList.Values)
                {
                    if (cancelToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException();
                    }
                    if (card.name.ToLower().Contains(searchText.ToLower()))
                    {
                        result.Add(card);
                    }
                    else if (card.effect.ToLower().Contains(searchText.ToLower()))
                    {
                        textInBody.Add(card);
                    }
                }
                result = result.OrderBy(x => x.name.ToLower().IndexOf(searchText.ToLower())).ToList();
                result.AddRange(textInBody);
                return result;
            });
            return await cardsGet;
        }

        /// <summary>
        /// Updates the card list in this inventory to contain all the most current cards available in Gods Unchained.
        /// </summary>
        private static async void updateCardList()
        {
            using (HttpClient client = new HttpClient())
            {
                if (!Directory.Exists("cards"))
                {
                    Directory.CreateDirectory("cards");
                }
                Console.WriteLine("Get cards:");
                String CardString;
                while (true)
                {
                    try
                    {
                        CardString = await client.GetStringAsync("https://api.godsunchained.com/v0/proto?format=flat");
                        break;
                    }
                    catch (HttpRequestException exc)
                    {
                        await Task.Delay(5000);
                    }
                }
                using (StreamWriter writer = new StreamWriter("cards.json"))
                {
                    writer.Write(CardString);
                    Console.WriteLine("Saved cards");
                }
                JObject? jsonData = (JObject?)JsonConvert.DeserializeObject(CardString);
                if (jsonData == null)
                    return;
                Dictionary<string, JObject>? dictObj = jsonData.ToObject<Dictionary<string, JObject>?>();
                if (dictObj == null)
                    return;
                Dictionary<int, CardData> updatedList = new();
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
                    if (name == null || effect == null || god == null || rarity == null || 
                        tribe == null || mana == null || attack == null || health == null || 
                        type == null || set == null || collectable == null || collectable == false)
                        continue;
                    CardData card = new CardData(proto, name, effect, god, rarity,
                        tribe, (int)mana, (int)attack, (int)health, type, set);
                    updatedList[proto] = card;
                }
                foreach (int key in updatedList.Keys) // Delete images of cards that were modified so the user always sees the most recent one.
                {
                    if (File.Exists("cards/" + key + "q5.webp") && (!CardList.ContainsKey(key) || !CardList[key].Equals(updatedList[key])))
                    {
                        File.Delete("cards/" + key + "q5.webp");
                    }
                }
                CardList = updatedList;
            }
        }

        /// <summary>
        /// Returns the current list of cards that can be offered on the market.
        /// </summary>
        /// <returns></returns>
        public static Dictionary<int, CardData> getCards()
        {
            if (listUpdated)
            {
                return CardList;
            }
            string oldCards = "{}";
            if (File.Exists("cards.json"))
            {
                using (StreamReader reader = new StreamReader("cards.json"))
                {
                    oldCards = reader.ReadToEnd();
                }
            }
            JObject? jsonData = (JObject?)JsonConvert.DeserializeObject(oldCards);
            if (jsonData == null)
            {
                updateCardList();
                return CardList;
            }
            Dictionary<string, JObject>? dictObj = jsonData.ToObject<Dictionary<string, JObject>?>();
            if (dictObj == null)
            {
                updateCardList();
                return CardList;
            }
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
                if (name == null || effect == null || god == null || rarity == null || 
                    tribe == null || mana == null || attack == null || health == null || 
                    type == null || set == null || collectable == null || collectable == false)
                    continue;
                CardData card = new CardData(proto, name, effect, god, rarity,
                    tribe, (int)mana, (int)attack, (int)health, type, set);
                CardList[proto] = card;
            }
            Console.WriteLine("Updating cards...");
            updateCardList();
            listUpdated = true;
            return CardList;
        }

        private int apolloID;
        private Dictionary<int, Dictionary<int, int>> inventory;

        /// <summary>
        /// Constructs an inventory for the given apolloID.
        /// </summary>
        /// <param name="apolloID"></param>
        public Inventory(int apolloID)
        {
            this.apolloID = apolloID;
            inventory = new();
        }

        /// <summary>
        /// Get the ApolloID for the account linked to this wallet.
        /// </summary>
        /// <returns></returns>
        public int getApolloID()
        {
            return apolloID;
        }

        /// <summary>
        /// Set the apolloID for this wallet and update its contents.
        /// </summary>
        /// <param name="apolloID"></param>
        /// <returns></returns>
        public async Task setApolloID(int apolloID)
        {
            this.apolloID = apolloID;
            await updateInventory();
        }

        /// <summary>
        /// Get the number of a specific cards in this wallet.
        /// </summary>
        /// <param name="proto">The proto ID of the card</param>
        /// <param name="quality">The quality of the card.</param>
        /// <returns></returns>
        public int getNumberOwned(int proto, int quality)
        {
            if (!inventory.ContainsKey(proto))
                return 0;
            if (!inventory[proto].ContainsKey(quality))
                return 0;
            return inventory[proto][quality];
        }

        /// <summary>
        /// Updates the cards in this inventory using data from the Gods Unchained website.
        /// </summary>
        /// <returns></returns>
        public async Task updateInventory()
        {
            Dictionary<int, Dictionary<int, int>> invUpdates = new();
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage inventoryData;
                while (true)
                {
                    try
                    {
                        HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get,
                            "https://marketplace-legacy.prod.prod.godsunchained.com/v2/asset?type=card&user_id=" + apolloID);
                        inventoryData = await client.SendAsync(requestMessage);
                        break;
                    }
                    catch (HttpRequestException exc)
                    {
                        await Task.Delay(5000);
                    }
                }
                byte[] buf = await inventoryData.Content.ReadAsByteArrayAsync();
                string strInventory = Encoding.UTF8.GetString(buf);
                JObject? jsonData = (JObject?)JsonConvert.DeserializeObject(strInventory);
                if (jsonData == null)
                    return;
                Dictionary<string, JObject>? dictObj = jsonData["protos"]?.ToObject<Dictionary<string, JObject>?>();
                if (dictObj == null)
                    return;
                foreach (string key in dictObj.Keys)
                {
                    JToken? token = dictObj?[key].GetValue("assets");
                    if (token == null)
                        continue;
                    
                    int count = token.Count();
                    Dictionary<int, int> inv = new();
                    for (int i = 0; i < count; i++)
                    {
                        string? num = (string?)dictObj?[key]?["assets"]?[i]?["properties"]?["quality"];
                        if (num == null)
                            continue;
                        int quality = int.Parse(num);
                        int amount = inv.ContainsKey(quality) ? inv[quality] + 1 : 1;
                        inv[quality] = amount;
                    }
                    invUpdates.Add(int.Parse(key), inv);
                }
                Console.WriteLine("Finished");
            }
            inventory = invUpdates;
        }
    }
}
