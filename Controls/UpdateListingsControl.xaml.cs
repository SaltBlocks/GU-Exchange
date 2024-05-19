using GU_Exchange.Helpers;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.Web;
using static GU_Exchange.Helpers.IMXlib;

namespace GU_Exchange.Controls
{
    /// <summary>
    /// Interaction logic for UpdateListingsControl.xaml
    /// </summary>
    public partial class UpdateListingsControl : UserControl
    {
        #region Class Properties
        private int _displayIndex;
        private CancellationTokenSource _orderFetchCancelSource;
        private readonly Task _setupWalletTask;
        private readonly List<(string name, int proto, int quality, Order activeOrder)> _cardData;
        private readonly Dictionary<string, Task<string>> _orderBookFetchTasks;
        private readonly Dictionary<string, List<Order>> _cachedOrders;
        private readonly Dictionary<string, bool> _listingResults;
        #endregion
        #region Default Constructor
        /// <summary>
        /// Default constructor for a control allowing the user to change the listing price of all their orders to match the market price.
        /// </summary>
        public UpdateListingsControl()
        {
            InitializeComponent();
            _cardData = new();
            _orderFetchCancelSource = new();
            _orderBookFetchTasks = new();
            _cachedOrders = new();
            _listingResults = new();
            _displayIndex = 0;
            _setupWalletTask = setupWallet();
            SetupPrices();
        }
        #endregion
        #region UserControl Setup.
        /// <summary>
        /// Fetch active orders on connected wallet and show them in the cardpanel.
        /// </summary>
        private async Task setupWallet()
        {
            Wallet? wallet = Wallet.GetConnectedWallet();
            if (wallet == null)
            {
                tbStatus.Text = "No wallet connected";
                return;
            }
            try
            {
                Log.Information($"Fetching orders for wallet ");

                bool hasNext = true;
                string urlBase = $"https://api.x.immutable.com/v3/orders?direction=asc&include_fees=true&order_by=buy_quantity&page_size=200&sell_token_address=0xacb3c6a43d15b907e8433077b6d38ae40936fe2c&status=active&user={wallet.Address}";
                string urlInventory = urlBase;
                while (hasNext)
                {
                    string strInventory = await ResourceManager.Client.GetStringAsync(urlInventory, _orderFetchCancelSource.Token);
                    JObject? jsonOrders = (JObject?)JsonConvert.DeserializeObject(strInventory);
                    if (jsonOrders == null)
                        break;
                    hasNext = ((int?)jsonOrders.SelectToken("remaining")) == 1;
                    string cursor = (string?)jsonOrders.SelectToken("cursor") ?? "";
                    JToken? result = jsonOrders["result"];
                    if (result == null)
                        return;
                    foreach (JToken order in result)
                    {
                        string? cardName = (string?)order.SelectToken("sell.data.properties.name");
                        string? img_url = (string?)order.SelectToken("sell.data.properties.image_url");
                        if (cardName == null || img_url == null)
                            continue;
                        string[] card_data = img_url.Split("id=")[1].Split("&q=");
                        Order or = new Order(order, await getOrderCurrencyName(order));
                        _cardData.Add((cardName, int.Parse(card_data[0]), int.Parse(card_data[1]), or));
                        if (_displayIndex < 50)
                        {
                            OrderDisplayControl control = new(cardName, int.Parse(card_data[0]), int.Parse(card_data[1]));
                            control.SetOrder(or);
                            control.ShowStatus(true);
                            control.SetStatusMessage("Checking price...");
                            cardPanel.Children.Add(control);
                            _displayIndex++;
                        }
                    }
                    urlInventory = $"{urlBase}&cursor={cursor}";
                    tbStatus.Text = $"Fetched {_cardData.Count} active orders.";
                }
            }
            catch (Exception ex)
            {
                if (!_orderFetchCancelSource.IsCancellationRequested)
                {
                    Log.Warning($"Failed to fetching orders for wallet \"{wallet.Address}\". {ex.Message}: {ex.StackTrace}");
                    tbStatus.Text = $"Failed to fetch active orders.";
                }
            }
        }

        /// <summary>
        /// Fetch the current market price for all cards listed by the user and show them if the user listed price is higher than the cheapeast available offer.
        /// </summary>
        private async void SetupPrices()
        {
            btnUpdateAll.IsEnabled = false;
            ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = false;
            try
            {
                await _setupWalletTask;
                if (_orderFetchCancelSource.Token.IsCancellationRequested)
                    throw new OperationCanceledException("Fetching orders was cancelled.");
                CancellationToken ct = _orderFetchCancelSource.Token;
                Dictionary<Task<decimal>, (string name, int proto, int quality, Order activeOrder) > priceFetchTasks = new();
                foreach ((string name, int proto, int quality, Order activeOrder) card in _cardData)
                {
                    Token token = (await Wallet.FetchTokens())[card.activeOrder.Currency];
                    priceFetchTasks.Add(GetListPriceForCard(card.proto, card.quality, token, ct), card);
                }
                int updateCounter = 0;
                while (priceFetchTasks.Count > 0)
                {
                    Task<decimal> completed = await Task.WhenAny(priceFetchTasks.Keys);
                    if (ct.IsCancellationRequested)
                    {
                        throw new OperationCanceledException("Fetching of orders was cancelled.");
                    }
                    (string name, int proto, int quality, Order activeOrder) card = priceFetchTasks[completed];
                    if (card.activeOrder.PriceTotal() > await completed)
                        updateCounter++;
                    Token token = (await Wallet.FetchTokens())[card.activeOrder.Currency];
                    try
                    {
                        HashSet<OrderDisplayControl> displaysToRemove = new();
                        foreach (OrderDisplayControl display in cardPanel.Children)
                        {
                            if (display.ProtoID == card.proto && display.Quality == card.quality && display.GetOrder()?.Currency == card.activeOrder.Currency)
                            {
                                decimal? currentPrice = display.GetOrder()?.PriceTotal();
                                if (currentPrice == null)
                                    continue; // This should never happen.
                                
                                if (!currentPrice.Equals(await completed))
                                {
                                    display.ShowStatus(false);
                                    display.SetSubText($"Adjust price to: {Math.Round(await completed, 10)} {token.Name}");
                                }
                                else
                                {
                                    displaysToRemove.Add(display);
                                }
                            }
                        }
                        foreach (OrderDisplayControl control in displaysToRemove) 
                        { 
                            cardPanel.Children.Remove(control);
                            AddOrderDisplay();
                        }

                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        foreach (OrderDisplayControl display in cardPanel.Children)
                        {
                            if (display.ProtoID == card.proto && display.Quality == card.quality)
                            {
                                display.ShowStatus(true);
                                display.SetStatus(OrderDisplayControl.DisplayStatus.Fail);
                                display.SetStatusMessage("Unknown card value");
                            }
                        }
                    }
                    priceFetchTasks.Remove(completed);
                    tbStatus.Text = $"Fetching card prices: {_cardData.Count - priceFetchTasks.Count} / {_cardData.Count} fetched.";
                }
                if (updateCounter > 0)
                {
                    tbStatus.Text = $"Continue to adjust prices for {updateCounter} listings to match the market price.";
                    btnUpdateAll.IsEnabled = true;
                }
                else if (_cardData.Count == 0)
                {
                    tbStatus.Text = $"No listings found.";
                }
                else
                {
                    tbStatus.Text = $"All your listings are the cheapest on the market.";
                }
            }
            catch (Exception ex)
            {
                if (!_orderFetchCancelSource.Token.IsCancellationRequested)
                {
                    tbStatus.Text = $"Error occurred while fetching orders";
                    Log.Warning($"Error occurred while fetching orders. {ex.Message}: {ex.StackTrace}");
                }
            }
            ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = true;
        }
        #endregion

        #region Event Handler
        /// <summary>
        /// Used to detect when the user scrolls to the bottom of the page.
        /// When this happens, new tiles are loaded and added to the window.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange <= 0)
            {
                return;
            }
            if (e.VerticalOffset + e.ViewportHeight != e.ExtentHeight)
            {
                return;
            }
            int added = 0;
            while (added < 50)
            {
                AddOrderDisplay();
                added++;
            }
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
            if (btnClose.IsEnabled)
            {
                _orderFetchCancelSource.Cancel();
                ResourceManager.RateLimiter.CancelRequests();
                ((MainWindow)Application.Current.MainWindow).CloseOverlay();
            }
        }

        /// <summary>
        /// Update the current prices of all listings posted by the user to match the market price.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BtnUpdateAll_Click(object sender, RoutedEventArgs e)
        {
            if (_cardData.Count == 0)
            {
                tbStatus.Text = $"No cards available to sell.";
                return;
            }

            // Prevent user from closing the window until this method finished during the purchase.
            btnUpdateAll.IsEnabled = false;
            btnClose.IsEnabled = false;
            scrollBar.IsEnabled = false;
            ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = false;

            // Get the connected wallet.
            Wallet? wallet = Wallet.GetConnectedWallet();
            if (wallet == null)
            {
                ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = true;
                scrollBar.IsEnabled = true;
                btnClose.IsEnabled = true;
                return;
            }
            OrderDisplayControl[] panelsArray = new OrderDisplayControl[cardPanel.Children.Count];
            cardPanel.Children.CopyTo(panelsArray, 0);
            HashSet<OrderDisplayControl> panels = panelsArray.ToHashSet();

            List<(NFT card, string tokenID, double price, TextBlock? tbListing)> listings = new();
            foreach ((string name, int proto, int quality, Order activeOrder) card in _cardData)
            {
                List<Order>? orders;
                bool loaded = _cachedOrders.TryGetValue($"{card.proto}q{card.quality}t{card.activeOrder.Currency}", out orders);
                if (!loaded || orders == null || orders.Count == 0 || (orders[0].PriceTotal().Equals(card.activeOrder.PriceTotal()) && orders[0].Seller == wallet.Address))
                    continue;

                Console.WriteLine($"Updating price for {card.name} to {orders[0].PriceTotal() - new decimal(0.00000001)} {card.activeOrder.Currency}");
                decimal priceBuyer = 100 * (orders[0].PriceTotal() - new decimal(0.00000001));
                decimal priceList = priceBuyer / getFeeMultiplier(card.quality);
                NFT nft = new NFT()
                {
                    token_address = "0xacb3c6a43d15b907e8433077b6d38ae40936fe2c",
                    token_id = ulong.Parse(card.activeOrder.TokenID)
                };
                TextBlock? statusTextBlock = null;
                foreach (OrderDisplayControl panel in panels)
                {
                    if (panel.ProtoID == card.proto && panel.Quality == card.quality && panel.getStatustextBlock().Text != "Already listed")
                    {
                        panel.ShowStatus(true);
                        panel.SetStatus(OrderDisplayControl.DisplayStatus.Loading);
                        statusTextBlock = panel.getStatustextBlock();
                        panels.Remove(panel);
                        break;
                    }
                }
                listings.Add((nft, (await Wallet.FetchTokens())[card.activeOrder.Currency].Address, decimal.ToDouble(priceList), statusTextBlock));
            }

            Dictionary<NFT, bool> result = await wallet.RequestCreateOrders(Application.Current.MainWindow, listings.ToArray(), this.tbStatus);
            foreach (KeyValuePair<NFT, bool> orderResult in result)
            {
                _listingResults.Add(orderResult.Key.token_id.ToString(), orderResult.Value);
            }

            panels = panelsArray.ToHashSet();
            Dictionary<string, (string name, int proto, int quality, Order activeOrder)> cardDict = _cardData.ToDictionary(x => x.activeOrder.TokenID, x => x);
            foreach (NFT nft in result.Keys)
            {
                (string name, int proto, int quality, Order activeOrder) cardData = cardDict[nft.token_id.ToString()];
                foreach (OrderDisplayControl panel in panels)
                {
                    if (panel.GetOrder() == cardData.activeOrder)
                    {
                        if (result[nft])
                        {
                            panel.SetStatus(OrderDisplayControl.DisplayStatus.Success);
                            panel.SetStatusMessage("Listing created");
                        }
                        else
                        {
                            panel.SetStatus(OrderDisplayControl.DisplayStatus.Fail);
                            panel.SetStatusMessage("Listing failed");
                        }
                        panels.Remove(panel);
                        break;
                    }
                }
            }
            ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = true;
            scrollBar.IsEnabled = true;
            btnClose.IsEnabled = true;
        }

        /// <summary>
        /// Close the cardcontrol and return to the main menu.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            ResourceManager.RateLimiter.CancelRequests();
            ((MainWindow)Application.Current.MainWindow).CloseOverlay();
        }
        #endregion

        #region Supporting methods
        private async Task<string> getOrderCurrencyName(JToken order)
        {
            string? token_address = (string?)order.SelectToken("buy.data.token_address");
            if (token_address == null)
                return "???";
            if (token_address == "")
            {
                string? token_type = (string?)order.SelectToken("buy.type");
                if (token_type == null)
                    return "???";
                return token_type;
            }
            return await Wallet.FetchTokenSymbol(token_address);
        }

        /// <summary>
        /// Fetch the optimal price for a card. Returns the current lowest offer price if it is posted by the user or otherwise an amount slightly below the cheapest offer.
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
        private async Task<decimal> GetListPriceForCard(int protoID, int quality, Token token, CancellationToken cancelToken)
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
                string urlOrderBook = $"https://api.x.immutable.com/v3/orders?buy_token_address={token_str}&direction=asc&include_fees=true&order_by=buy_quantity&page_size=1&sell_metadata={cardData}&sell_token_address=0xacb3c6a43d15b907e8433077b6d38ae40936fe2c&status=active";
                string? strOrderBook;
                Task<string>? fetchOrderBook;
                lock (_orderBookFetchTasks)
                {
                    bool previouslyRequested = _orderBookFetchTasks.TryGetValue($"{protoID}q{quality}c{token.Name}", out fetchOrderBook);
                    if (!previouslyRequested)
                    {
                        fetchOrderBook = ResourceManager.Client.GetStringAsync(urlOrderBook, cancelToken);
                        _orderBookFetchTasks.Add($"{protoID}q{quality}c{token.Name}", fetchOrderBook);
                    }
                }
                if (fetchOrderBook == null)
                    throw new NullReferenceException("Order Fetching task was unexpectedly null");
                strOrderBook = await fetchOrderBook;

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
                            orders.Add(new Order(order, token.Name));
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
            return (wallet != null && wallet.Address.Equals(orders[0].Seller)) ? orders[0].PriceTotal() : orders[0].PriceTotal() - new decimal(0.00000001);
        }

        /// <summary>
        /// Tries to add a new order display to the window. If all cards are on display it does nothing.
        /// </summary>
        /// <returns>true if a display was added.</returns>
        private bool AddOrderDisplay()
        {
            while (_displayIndex < _cardData.Count)
            {
                OrderDisplayControl control = new(_cardData[_displayIndex].Item1, _cardData[_displayIndex].Item2, _cardData[_displayIndex].Item3);
                control.SetOrder(_cardData[_displayIndex].activeOrder);
                control.ShowStatus(true);
                control.SetStatusMessage("Checking price...");
                if (!SetupOrderDisplay(control))
                {
                    _displayIndex++;
                    continue;
                }
                if (_listingResults.ContainsKey(_cardData[_displayIndex].activeOrder.TokenID))
                {
                    if (_listingResults[_cardData[_displayIndex].activeOrder.TokenID])
                    {
                        control.ShowStatus(true);
                        control.SetStatus(OrderDisplayControl.DisplayStatus.Success);
                        control.SetStatusMessage("Price updated");
                    }
                    else
                    {
                        control.ShowStatus(true);
                        control.SetStatus(OrderDisplayControl.DisplayStatus.Fail);
                        control.SetStatusMessage("Listing failed");
                    }
                }
                cardPanel.Children.Add(control);
                _displayIndex++;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Setup the order display for a specific card to show the correct listing price.
        /// </summary>
        /// <param name="display"></param>
        private bool SetupOrderDisplay(OrderDisplayControl display)
        {
            Wallet? wallet = Wallet.GetConnectedWallet();
            string? currency_name = display.GetOrder()?.Currency;
            if (wallet == null || currency_name == null)
            {
                return false;
            }
            List<Order>? orders;
            bool loaded = _cachedOrders.TryGetValue($"{display.ProtoID}q{display.Quality}t{currency_name}", out orders);
            if (loaded && orders != null)
            {
                if (orders.Count == 0)
                {
                    display.ShowStatus(true);
                    display.SetStatus(OrderDisplayControl.DisplayStatus.Fail);
                    display.SetStatusMessage("Unknown card value");
                    return false;
                }
                Order order = orders[0];
                if (wallet.Address.Equals(order.Seller))
                    return false;
                string priceText = $"Adjust price to: {order.PriceTotal() - new decimal(0.00000001)} {currency_name}";
                display.SetSubText(priceText);
                display.ShowStatus(false);
                return true;
            }
            else
            {
                display.SetStatus(OrderDisplayControl.DisplayStatus.Loading);
                display.ShowStatus(true);
                display.SetSubText("");
                return true;
            }
        }

        /// <summary>
        /// Get the fee multiplier for a card of a specifief quality.
        /// </summary>
        /// <param name="quality">The quality of the card (4 = meteorite, 1 = diamond)</param>
        /// <returns></returns>
        private decimal getFeeMultiplier(int quality)
        {
            switch (quality)
            {
                case 4:
                    return 1.08M;
                case 3:
                    return 1.07M;
                case 2:
                    return 1.06M;
                case 1:
                    return 1.035M;
                default:
                    return 1.08M;
            }
        }
        #endregion
    }
}
