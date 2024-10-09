using ImageProcessor.Processors;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using static GU_Exchange.Helpers.IMXlib;
using System.Threading;
using System.IO;

namespace GU_Exchange.Helpers
{
    [Serializable()]
    public class LimitListing
    {
        private NFT card;
        private string tokenID;
        private double minimumPrice;
        private string saleAddress;
        private string parentAddress;

        public LimitListing(NFT card, string tokenID, double minimumPrice, string saleAddress, string parentAddress)
        {
            this.card = card;
            this.tokenID = tokenID;
            this.minimumPrice = minimumPrice;
            this.saleAddress = saleAddress;
            this.parentAddress = parentAddress;
        }
    }

    internal class MarketAutomation
    {

        public static async Task<Order> FetchCheapestOrder(int CardID, string quality, string tokenID, CancellationToken token)
        {
            // Fetch orders in the IMX global orderbook for the specified card of the specified quality listed in the selected token.
            string cardData = HttpUtility.UrlEncode("{\"proto\":[\"" + CardID + "\"],\"quality\":[\"" + quality + "\"]}");
            if (tokenID.Equals("ETH"))
                tokenID = "&buy_token_type=ETH";
            string urlOrderBook = $"https://api.x.immutable.com/v3/orders?buy_token_address={tokenID}&direction=asc&include_fees=true&order_by=buy_quantity&page_size=50&sell_metadata={cardData}&sell_token_address=0xacb3c6a43d15b907e8433077b6d38ae40936fe2c&status=active";
            Log.Information($"Fetching orders for {CardID} of quality {quality}");
            string strOrderBook = await ResourceManager.Client.GetStringAsync(urlOrderBook, token);

            List<Order> orders = new();

            // Extract orders from the data returned by the server.
            JObject? jsonOrders = (JObject?)JsonConvert.DeserializeObject(strOrderBook);
            if (jsonOrders == null)
                throw new InvalidDataException($"Invalid or no orders received from IMX for proto {CardID} and quality {quality}.");
            JToken? result = jsonOrders["result"];
            if (result != null)
            {
                foreach (JToken order in result)
                {
                    try
                    {
                        Order or = new Order(order, "CURRENCY");
                        orders.Add(or);
                    }
                    catch (NullReferenceException)
                    {
                    }
                }
            } else
            {
                throw new InvalidDataException($"Invalid or no orders received from IMX for proto {CardID} and quality {quality}.");
            }
            return orders.OrderBy(x => x.PriceTotal()).First();
        }
    }
}
