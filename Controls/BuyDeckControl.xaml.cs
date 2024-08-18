using GU_Exchange.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GU_Exchange.Controls
{
    /// <summary>
    /// Interaction logic for BuyDeckControl.xaml
    /// </summary>
    public partial class BuyDeckControl : UserControl
    {
        #region Class Properties.
        private readonly Dictionary<string, List<Order>> _cachedOrders;
        #endregion
        #region Default Constructor
        /// <summary>
        /// Constructor for a user control allowing the user to buy cards they are missing for a specific deck.
        /// </summary>
        public BuyDeckControl()
        {
            InitializeComponent();
            _cachedOrders = new Dictionary<string, List<Order>>();
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
        }
        #endregion
        #region Event Handlers
        /// <summary>
        /// Adjust the size of the CardControl when the window size is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double maxWidth = 1400;
            double maxHeight = 800;
            double width = Math.Min(ActualWidth, maxWidth);
            double height = width / 1.75;
            if (height > ActualHeight)
            {
                height = Math.Min(ActualHeight, maxHeight);
                width = height * 1.75;
            }
            controlGrid.Height = height - 10;
            controlGrid.Width = width - 10;
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
            // Click occurred outside controlGrid, close the overlay.
            if (btnCancel.IsEnabled)
            {
                ((MainWindow)Application.Current.MainWindow).CloseOverlay();
            }
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

        /// <summary>
        /// Search for orders that the user can buy to complete the deck with the provided deck code.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SearchDeck_Click(object sender, RoutedEventArgs e)
        {
            btnBuy.IsEnabled = false; // Disable the buy button until we know all needed cards are available for purchase.
            tbStatus.Text = "Loading orders...";
            cardPanel.Children.Clear();
            string deckString = tbDeck.Text;
            try
            {
                // Get cards from provided deckstring.
                Inventory? inv = (Inventory?)App.Current.Properties["Inventory"];
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
                
                // Get orders for cards needed to complete the deck.
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
                            control.SetStatus(OrderDisplayControl.DisplayStatus.Success);
                            control.SetStatusMessage("Owned");
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

                // Verify that all cards are available for purchase.
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

                    // Show the deck price to the user.
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
            catch (Exception)
            {
                tbStatus.Text = "Invalid deckstring entered";
                Log.Information($"Invalid deckstring entered: {tbDeck?.Text ?? "None"}");
            }
        }

        /// <summary>
        /// Continue with purchasing the deck.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void btnBuy_Click(object sender, RoutedEventArgs e)
        {
            // Prevent user from closing the window until this method finished during the purchase.
            btnBuy.IsEnabled = false;
            btnCancel.IsEnabled = false;
            ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = false;

            // Check if the user has a wallet connected.
            Wallet? wallet = Wallet.GetConnectedWallet();
            if (wallet == null)
            {
                ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = true;
                btnCancel.IsEnabled = true;
                return;
            }

            // Check if the wallet has sufficient balance for the purchase.
            decimal priceTotal = 0;
            string? currency_name = (string?)this.cbCurrency.SelectedItem;
            if (currency_name == null)
            {
                tbStatus.Text = "Failed to find buy token";
                ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = true;
                btnCancel.IsEnabled = true;
                return;
            }
            foreach (OrderDisplayControl control in cardPanel.Children)
            {
                Order? order = control.GetOrder();
                if (order != null)
                {
                    priceTotal += order.PriceTotal();
                }
            }
            decimal walletBalance = await wallet.GetTokenAmountAsync(currency_name, false);
            if (walletBalance < priceTotal)
            {
                ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = true;
                btnCancel.IsEnabled = true;
                tbStatus.Text = "Insufficient wallet balance";
                return;
            }
            _cachedOrders.Clear(); // Clear cached orders, these should be recollected when buying more cards afterwards.
            
            // Collect data needed to purchase the cards.
            List<(Order order, TextBlock? statusText)> orderData = new();
            Dictionary<Order, OrderDisplayControl> controlDict = new();
            foreach (OrderDisplayControl orderDisplay in cardPanel.Children)
            {
                Order? order = orderDisplay.GetOrder();
                if (order != null)
                {
                    orderDisplay.ShowStatus(true);
                    orderDisplay.SetStatus(OrderDisplayControl.DisplayStatus.Loading);
                    orderDisplay.SetStatusMessage("Loading");
                    controlDict.Add(order, orderDisplay);
                    orderData.Add((order, orderDisplay.getStatustextBlock()));
                }
            }
            // Submit orders to IMX.
            Dictionary<Order, bool> resultData = await wallet.RequestBuyOrders(Application.Current.MainWindow, orderData.ToArray(), tbStatus);
            
            // Update UI elements and the internal inventory.
            bool accountConnected = await GameDataManager.IsWalletLinked(Settings.GetApolloID(), wallet.Address);
            Inventory? inv = (Inventory?)App.Current.Properties["Inventory"];
            foreach (Order order in resultData.Keys)
            {
                if (resultData[order])
                {
                    if (accountConnected && inv != null)
                    {
                        int quality = 4;
                        switch (order.Quality)
                        {
                            case "Meteorite":
                                quality = 4;
                                break;
                            case "Shadow":
                                quality = 3;
                                break;
                            case "Gold":
                                quality = 2;
                                break;
                            case "Diamond":
                                quality = 1;
                                break;
                        }
                        int proto = controlDict[order].ProtoID;
                        inv.SetNumberOwned(proto, quality, Math.Max(inv.GetNumberOwned(proto, quality) + 1, 0));
                    }

                    controlDict[order].SetStatus(OrderDisplayControl.DisplayStatus.Success);
                    controlDict[order].SetStatusMessage("Owned");
                }
                else
                {
                    controlDict[order].SetStatus(OrderDisplayControl.DisplayStatus.Fail);
                    controlDict[order].SetStatusMessage("Purchase failed");
                }
            }
            
            // Re-enable UI elements.
            await ((MainWindow)Application.Current.MainWindow).RefreshTilesAsync();
            ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = true;
            btnCancel.IsEnabled = true;
        }
        #endregion
        #region Supporting Methods
        /// <summary>
        /// Fetch an order for a card. Will return the cheapest card offset by the orderIndex.
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
                            if (wallet != null && or.Seller.ToLower().Equals(wallet.Address))
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
        #endregion
    }
}
