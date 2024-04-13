using GU_Exchange.Helpers;
using ImageProcessor.Processors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
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
            double maxHeight = 800;

            double width = Math.Min(this.ActualWidth, maxWidth);
            double height = width / 1.75;

            if (height > this.ActualHeight)
            {
                height = Math.Min(this.ActualHeight, maxHeight);
                width = height * 1.75;
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
            btnBuy.IsEnabled = false;
            tbStatus.Text = "Loading orders...";
            cardPanel.Children.Clear();
            string deckString = tbDeck.Text;
            try
            {
                Inventory? inv = (Inventory?) App.Current.Properties["Inventory"];
                if (inv == null)
                {
                    tbStatus.Text = "No inventory connected to check decklist against.";
                    return;
                }
                Dictionary<int, CardData>? cardList = await GameDataManager.GetCardListAsync();
                if (cardList == null)
                {
                    tbStatus.Text = "Error loading cardlist.";
                    return;
                }
                List<int> deck = GameDataManager.GetDeckList(deckString);
                Dictionary<int, int> deckCounts = deck.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
                string? currency_name = (string?)this.cbCurrency.SelectedItem;
                if (currency_name == null)
                {
                    tbStatus.Text = "Failed to find buy token.";
                    return;
                }
                Token token = (await Wallet.FetchTokens())[currency_name];
                CancellationTokenSource cts = new();
                int minQuality = 5 - cbMinQuality.SelectedIndex;
                int buyQuality = minQuality < 5 ? minQuality : 4;
                List<OrderDisplayControl> ownedCards = new();
                List<Task<bool>> loadOrderTasks = new();
                foreach (int proto in deckCounts.Keys)
                {
                    string cardName = cardList[proto].Name;
                    int amount = deckCounts[proto];
                    for (int quality = 1; quality <= minQuality; quality++)
                    {
                        int numOwned = inv.GetNumberOwned(proto, quality);
                        for (int i = 0; i < numOwned; i++)
                        {
                            if (amount == 0)
                                break;
                            OrderDisplayControl control = new(cardName, proto, quality);
                            control.SetOwned(true);
                            ownedCards.Add(control);
                            amount--;
                        }
                    }

                    
                    for (int i = 0; i < amount; i++)
                    {
                        OrderDisplayControl control = new(cardName, proto, buyQuality);
                        cardPanel.Children.Add(control);
                        Task<bool> loadOrderTask = control.SetOrder(GetOrderForCard(proto, buyQuality, token, i, cts.Token));
                        loadOrderTasks.Add(loadOrderTask);
                    }
                }
                foreach (OrderDisplayControl cardOwned in ownedCards)
                {
                    cardPanel.Children.Add(cardOwned);
                }
                bool[] results = await Task.WhenAll(loadOrderTasks.ToArray());
                bool success = results.All(x => x);
                
                if (success)
                {
                    decimal priceTotal = 0;
                    foreach (OrderDisplayControl control in cardPanel.Children)
                    {
                        Order? order = control.GetOrder();
                        if (order != null)
                        {
                            priceTotal += order.PriceTotal();
                        }
                    }
                    decimal? ConversionRate = token.Value;
                    if (ConversionRate == null)
                    {
                        tbStatus.Text = $"Total: {priceTotal} {currency_name}";
                    }
                    else
                    {
                        tbStatus.Text = $"Total: {priceTotal} {currency_name} (${(priceTotal * (decimal)ConversionRate).ToString("0.00")})";
                    }
                    btnBuy.IsEnabled = true;
                }
                else
                {
                    tbStatus.Text = "Failed to find offers for all required cards.";
                }
                
            }
            catch (Exception e1)
            {
                Console.WriteLine("Invalid Deckstring");
                Console.WriteLine(e1.Message);
                Console.WriteLine(e1.StackTrace);
            }
        }

        private async void btnBuy_Click(object sender, RoutedEventArgs e)
        {
            List<(Order order, TextBlock? statusText)> orderData = new();
            foreach (OrderDisplayControl orderDisplay in cardPanel.Children)
            {
                Order? order = orderDisplay.GetOrder();
                if (order != null)
                {
                    orderDisplay.SetLoading(true);
                    orderData.Add((order, orderDisplay.getStatustextBlock()));
                }
            }
            foreach ((Order order, TextBlock? statusText) order in orderData)
            {
                Console.WriteLine($"{order.order.Name}");
            }
            Wallet? wallet = Wallet.GetConnectedWallet();
            if (wallet == null)
            {
                return;
            }
            await wallet.RequestBuyOrders(Application.Current.MainWindow, orderData.ToArray(), tbStatus);
        }
    }
}
