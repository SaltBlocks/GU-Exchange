using GU_Exchange.Helpers;
using ImageProcessor.Processors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GU_Exchange.Controls
{
    /// <summary>
    /// Interaction logic for BuyDeckControl.xaml
    /// </summary>
    public partial class BuyDeckControl : UserControl
    {
        private Dictionary<string, List<Order>> _cachedOrders;
        public BuyDeckControl()
        {
            InitializeComponent();
            _cachedOrders = new Dictionary<string, List<Order>>();
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cbMinQuality.Items.Add("Plain");
            cbMinQuality.Items.Add("Meteorite");
            cbMinQuality.Items.Add("Shadow");
            cbMinQuality.Items.Add("Gold");
            cbMinQuality.Items.Add("Diamond");
            cbMinQuality.SelectedIndex = 0;
            cbCurrency.Items.Add("ETH");
            cbCurrency.Items.Add("GODS");
            cbCurrency.Items.Add("IMX");
            cbCurrency.SelectedIndex = 0;
            setup(cancellationTokenSource.Token);
        }

        private async void setup(CancellationToken cancelToken)
        {
            int CardID = 1;
            int quality = 1;
            string qualityStr = "Meteorite";
            switch (quality)
            {
                case 4:
                    qualityStr = "Meteorite";
                    break;
                case 3:
                    qualityStr = "Shadow";
                    break;
                case 2:
                    qualityStr = "Gold";
                    break;
                case 1:
                    qualityStr = "Diamond";
                    break;
            }
            Wallet? wallet = Wallet.GetConnectedWallet();
            if (wallet == null)
            {
                return;
            }

            OrderDisplayControl control = new("adsf", CardID, quality);
            cardPanel.Children.Add(control);
            string cardData = HttpUtility.UrlEncode("{\"proto\":[\"" + CardID + "\"],\"quality\":[\"" + qualityStr + "\"]}");
            string? currency_name = "ETH";// (string?)this.cbToken.SelectedItem;
            if (currency_name == null)
                return;
            Token currency = (await Wallet.FetchTokens())[currency_name];
            string token_str = currency.Address;
            if (token_str.Equals("ETH"))
                token_str = "&buy_token_type=ETH";
            string urlOrderBook = $"https://api.x.immutable.com/v3/orders?buy_token_address={token_str}&direction=asc&include_fees=true&order_by=buy_quantity&page_size=50&sell_metadata={cardData}&sell_token_address=0xacb3c6a43d15b907e8433077b6d38ae40936fe2c&status=active";
            Console.WriteLine(urlOrderBook);
            string strOrderBook;
            try
            {
                Log.Information($"Fetching orders for {CardID} of quality {quality}");
                strOrderBook = await ResourceManager.Client.GetStringAsync(urlOrderBook, cancelToken);
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is HttpRequestException)
            {
                if (ex is HttpRequestException)
                    Log.Information($"Failed to fetching orders for {CardID} of quality {quality}. {ex.Message}: {ex.StackTrace}");
                return;
            }
            List<Order> orders = new();

            // Extract orders from the data returned by the server.
            JObject? jsonOrders = (JObject?)JsonConvert.DeserializeObject(strOrderBook);
            if (jsonOrders == null)
                return;
            JToken? result = jsonOrders["result"];
            if (result != null)
            {
                foreach (JToken order in result)
                {
                    try
                    {
                        Order or = new Order(order, currency.Name);
                        if (or.Seller.ToLower().Equals(wallet.Address))
                        {
                            continue;
                        }
                        orders.Add(or);
                    }
                    catch (NullReferenceException)
                    {
                    }
                }
            }

            if (orders.Count > 0)
            {
                control.SetOrder(orders[0]);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="protoID"></param>
        /// <param name="quality"></param>
        /// <param name="token"></param>
        /// <param name="orderIndex"></param>
        /// <param name="cancelToken"></param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        /// <exception cref="OperationCanceledException"/>
        /// <exception cref="HttpRequestException"/>
        private async Task<Order> GetOrderForCard(int protoID, int quality, Token token, int orderIndex, CancellationToken cancelToken)
        {
            Wallet? wallet = Wallet.GetConnectedWallet();
            if (wallet == null)
                throw new NullReferenceException("No Wallet connected.");
            List<Order>? orders;
            bool loaded = _cachedOrders.TryGetValue($"{protoID}q{quality}t{token.Name}", out orders);
            if (!loaded)
            {
                orders = new();
                string qualityStr = "Meteorite";
                switch (quality)
                {
                    case 4:
                        qualityStr = "Meteorite";
                        break;
                    case 3:
                        qualityStr = "Shadow";
                        break;
                    case 2:
                        qualityStr = "Gold";
                        break;
                    case 1:
                        qualityStr = "Diamond";
                        break;
                }
                string cardData = HttpUtility.UrlEncode("{\"proto\":[\"" + protoID + "\"],\"quality\":[\"" + qualityStr + "\"]}");
                string token_str = token.Address;
                if (token_str.Equals("ETH"))
                    token_str = "&buy_token_type=ETH";
                string urlOrderBook = $"https://api.x.immutable.com/v3/orders?buy_token_address={token_str}&direction=asc&include_fees=true&order_by=buy_quantity&page_size=50&sell_metadata={cardData}&sell_token_address=0xacb3c6a43d15b907e8433077b6d38ae40936fe2c&status=active";
                Log.Information($"Fetching orders for {protoID} of quality {qualityStr}");
                string strOrderBook = await ResourceManager.Client.GetStringAsync(urlOrderBook, cancelToken);

                // Extract orders from the data returned by the server.
                JObject? jsonOrders = (JObject?)JsonConvert.DeserializeObject(strOrderBook);
                if (jsonOrders == null)
                    throw new ArgumentNullException("Invalid order data.");
                JToken? result = jsonOrders["result"];
                if (result != null)
                {
                    foreach (JToken order in result)
                    {
                        try
                        {
                            Order or = new Order(order, token.Name);
                            if (or.Seller.ToLower().Equals(wallet.Address))
                            {
                                continue;
                            }
                            orders.Add(or);
                        }
                        catch (NullReferenceException)
                        {
                        }
                    }
                    _cachedOrders[$"{protoID}q{quality}t{token.Name}"] = orders;
                }
            }
            if (orders == null)
                throw new ArgumentNullException("No orders loaded");
            return orders[orderIndex];
        }

        /// <summary>
        /// Adjust the size of the CardControl when the window size is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double maxWidth = 1400;
            double maxHeight = 700;

            double width = Math.Min(this.ActualWidth, maxWidth);
            double height = width / 2;

            if (height > this.ActualHeight)
            {
                height = Math.Min(this.ActualHeight, maxHeight);
                width = height * 2;
            }

            this.controlGrid.Height = height - 10;
            this.controlGrid.Width = width - 10;
        }

        /// <summary>
        /// Close the window when the user clicks on the greyed out background.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Get the position of the mouse click relative to the controlGrid
            Point clickPoint = e.GetPosition(controlGrid);

            // Check if the click occurred on the controlGrid
            if (clickPoint.X >= 0 && clickPoint.X < controlGrid.ActualWidth &&
                clickPoint.Y >= 0 && clickPoint.Y < controlGrid.ActualHeight)
            {
                return;
            }
            // Click occurred outside controlGrid, you can call your function here
            ((MainWindow)Application.Current.MainWindow).CloseOverlay();
        }

        /// <summary>
        /// Close the cardcontrol and return to the main menu.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            ((MainWindow)Application.Current.MainWindow).CloseOverlay();
        }

        private async void SearchDeck_Click(object sender, RoutedEventArgs e)
        {
            cardPanel.Children.Clear();
            string deckString = tbDeck.Text;
            try
            {
                List<int> deck = GameDataManager.GetDeckList(deckString);
                Dictionary<int, CardData>? cardList = await GameDataManager.GetCardListAsync();
                if (cardList == null)
                    return;
                string? currency_name = (string?)this.cbCurrency.SelectedItem;
                if (currency_name == null)
                    throw new NullReferenceException("No token selected for purchase.");
                Token token = (await Wallet.FetchTokens())[currency_name];
                CancellationTokenSource cts = new();
                foreach (int i in deck)
                {
                    string cardName = cardList[i].Name;
                    OrderDisplayControl control = new(cardName, i, 3);
                    cardPanel.Children.Add(control);
                    control.SetOrder(await GetOrderForCard(i, 4, token, 0, cts.Token));
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Invalid Deckstring");
            }
        }

        private async void updateOrderDisplay(OrderDisplayControl display)
        {

        }
    }
}
