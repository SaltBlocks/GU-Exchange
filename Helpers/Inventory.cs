using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace GU_Exchange.Helpers
{
    internal class Inventory
    {
        #region Class Properties

        private int _apolloID;
        private Dictionary<int, Dictionary<int, int>> _inventory;

        #endregion

        #region Default Constructor

        /// <summary>
        /// Constructs an inventory for the given apolloID.
        /// </summary>
        /// <param name="apolloID"></param>
        public Inventory(int apolloID)
        {
            _apolloID = apolloID;
            _inventory = new();
        }

        #endregion

        #region Update Inventory

        /// <summary>
        /// Loads the users inventory from the api and updates the local inventory to contain the same cards.
        /// </summary>
        /// <returns></returns>
        public async Task UpdateInventoryAsync()
        {
            if (_apolloID == -1)
            {
                Debug.WriteLine("No inventory linked, skipped loading inventory.");
                return;
            }
            Debug.WriteLine($"Fetching inventory for user {_apolloID}");
            Dictionary<int, Dictionary<int, int>> UpdatedInventory = new();
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get,
                        $"https://marketplace-legacy.prod.prod.godsunchained.com/v2/asset?type=card&user_id={_apolloID}");
            try
            {
                HttpResponseMessage inventoryData = await ResourceManager.Client.SendAsync(requestMessage);
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
                    UpdatedInventory.Add(int.Parse(key), inv);
                }
                _inventory = UpdatedInventory;
            }
            catch (HttpRequestException)
            {
                Debug.WriteLine("Failed to load inventory.");
            }
        }

        #endregion

        #region Getters and Setters

        /// <summary>
        /// Get the number of a specific card in this wallet.
        /// </summary>
        /// <param name="proto">The proto ID of the card.</param>
        /// <param name="quality">The quality of the card.</param>
        /// <returns></returns>
        public int GetNumberOwned(int proto, int quality)
        {
            if (!_inventory.ContainsKey(proto))
                return 0;
            if (!_inventory[proto].ContainsKey(quality))
                return 0;
            return _inventory[proto][quality];
        }

        /// <summary>
        /// Set the number of a specific card in this wallet.
        /// </summary>
        /// <param name="proto">The proto ID of the card.</param>
        /// <param name="quality">The quality of the card.</param>
        /// <param name="amount">The number of cards to put in the wallet.</param>
        public void SetNumberOwned(int proto, int quality, int amount)
        {
            if (!_inventory.ContainsKey(proto))
                _inventory[proto] = new();
            _inventory[proto][quality] = amount;
        }


        /// <summary>
        /// Get the ApolloID for the account linked to this wallet.
        /// </summary>
        /// <returns></returns>
        public int GetApolloID()
        {
            return _apolloID;
        }

        /// <summary>
        /// Set the apolloID for this wallet and update its contents.
        /// </summary>
        /// <param name="apolloID"></param>
        /// <returns></returns>
        public async Task SetApolloIDAsync(int apolloID)
        {
            _apolloID = apolloID;
            await UpdateInventoryAsync();
        }

        #endregion
    }
}
