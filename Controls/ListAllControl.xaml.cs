using GU_Exchange.Helpers;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading;
using System.Web;
using static GU_Exchange.Helpers.IMXlib;

namespace GU_Exchange.Controls
{
    /// <summary>
    /// Interaction logic for ListAllControl.xaml
    /// </summary>
    public partial class ListAllControl : UserControl
    {
        #region Class Properties
        private int _displayIndex;
        private CancellationTokenSource _orderFetchCancelSource;
        private readonly Dictionary<string, List<Order>> _cachedOrders;
        private readonly Task _setupWalletTask;
        private readonly List<(string, int, int, ulong, bool)> _cardData;
        private readonly Dictionary<string, Task<string>> _orderBookFetchTasks;
        private readonly Dictionary<ulong, bool> _listingResults;
        #endregion
        #region Default constructor.
        /// <summary>
        /// Constructor for a user control allowing the user to create listings for all owned GU cards at the current cheapest price.
        /// </summary>
        public ListAllControl()
        {
            InitializeComponent();
            cbCurrency.Items.Add("ETH");
            cbCurrency.Items.Add("GODS");
            cbCurrency.Items.Add("IMX");
            cbCurrency.SelectedIndex = 0;
            _displayIndex = 0;
            _cachedOrders = new();
            _cardData = new();
            _orderFetchCancelSource = new();
            _listingResults = new();
            _orderBookFetchTasks = new();
            _setupWalletTask = SetupWallet();
            SetupPrices();
        }
        #endregion
        #region Setup methods
        /// <summary>
        /// Fetch all cards in the users wallet.
        /// </summary>
        /// <returns></returns>
        private async Task SetupWallet()
        {
            Wallet? wallet = Wallet.GetConnectedWallet();
            if (wallet == null)
            {
                tbStatus.Text = "No wallet connected";
                return;
            }
            try
            {
                Log.Information($"Fetching cards in wallet {wallet.Address}.");
                bool hasNext = true;
                string urlBase = $"https://api.x.immutable.com/v1/assets?page_size=200&user={wallet.Address}&collection=0xacb3c6a43d15b907e8433077b6d38ae40936fe2c&sell_orders=true";
                string urlInventory = urlBase;
                while (hasNext)
                {
                    string strInventory = await ResourceManager.Client.GetStringAsync(urlInventory);
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
                        string? cardName = (string?)order.SelectToken("metadata.name");
                        string? img_url = (string?)order.SelectToken("metadata.image");
                        string? token_id = (string?)order.SelectToken("token_id");
                        bool hasOrders = order.SelectToken("orders") != null;
                        if (cardName == null || img_url == null || token_id == null)
                            continue;
                        string[] card_data = img_url.Split("id=")[1].Split("&q=");
                        _cardData.Add((cardName, int.Parse(card_data[0]), int.Parse(card_data[1]), ulong.Parse(token_id), hasOrders));
                        if (_displayIndex < 50)
                        {
                            OrderDisplayControl control = new(cardName, int.Parse(card_data[0]), int.Parse(card_data[1]));
                            if (hasOrders)
                            {
                                control.SetStatus(OrderDisplayControl.DisplayStatus.Success);
                                control.SetStatusMessage("Already listed");
                            }
                            cardPanel.Children.Add(control);
                            _displayIndex++;
                        }
                    }
                    urlInventory = $"{urlBase}&cursor={cursor}";
                }
                cbCurrency.IsEnabled = true;
            }
            catch (Exception ex)
            {
                Log.Warning($"Failed to fetching contents of wallet \"{wallet.Address}\". {ex.Message}: {ex.StackTrace}");
                tbStatus.Text = $"Failed to fetch wallet contents.";
                return;
            }
        }

        /// <summary>
        /// Fetch current prices for all cards owned by the user and display them to the user.
        /// </summary>
        private async void SetupPrices()
        {
            btnListAll.IsEnabled = false;
            ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = false;

            CancellationTokenSource cts = new();
            
            try
            {
                await _setupWalletTask;
                string? currency_name = (string?)cbCurrency.SelectedItem;
                if (currency_name == null)
                {
                    tbStatus.Text = "Failed to find buy token.";
                    return;
                }

                // Get orders for cards needed to complete the deck.
                Token token = (await Wallet.FetchTokens())[currency_name];
                if (_orderFetchCancelSource.IsCancellationRequested)
                    throw new OperationCanceledException("Setting up prices was cancelled");
                _orderFetchCancelSource.Cancel();
                _orderFetchCancelSource = cts;
                lock (_orderBookFetchTasks)
                    _orderBookFetchTasks.Clear();
                CancellationToken ct = _orderFetchCancelSource.Token;
                Dictionary<Task<decimal>, (string name, int proto, int quality, ulong token_id, bool hasOrders)> priceFetchTasks = new();
                foreach ((string name, int proto, int quality, ulong token_id, bool hasOrders) card in _cardData)
                {
                    if (!card.hasOrders)
                        priceFetchTasks.Add(GetListPriceForCard(card.proto, card.quality, token, ct), card);
                }

                while (priceFetchTasks.Count > 0)
                {
                    Task<decimal> completed = await Task.WhenAny(priceFetchTasks.Keys);
                    if (ct.IsCancellationRequested)
                    {
                        throw new OperationCanceledException("Fetching of orders was cancelled.");
                    }
                    (string name, int proto, int quality, ulong token_id, bool hasOrders) card = priceFetchTasks[completed];
                    try
                    {
                        foreach (OrderDisplayControl display in cardPanel.Children)
                        {
                            if (display.ProtoID == card.proto && display.Quality == card.quality && !display.getStatustextBlock().Text.Equals("Already listed"))
                            {
                                display.SetSubText($"List for: {Math.Round(await completed, 10)} {token.Name}");
                                display.ShowStatus(false);
                            }
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

                int cardCount = _cardData.Count;
                decimal priceTotal = 0M;
                foreach ((string name, int proto, int quality, ulong token_id, bool hasOrders) card in _cardData)
                {
                    List<Order>? orders;
                    bool loaded = _cachedOrders.TryGetValue($"{card.proto}q{card.quality}t{currency_name}", out orders);
                    if (loaded && orders != null)
                    {
                        if (orders.Count == 0)
                        {
                            cardCount--;
                            continue;
                        }
                        Order order = orders[0];
                        priceTotal += order.PriceTotal() - new decimal(0.00000001);
                    }
                    else
                    {
                        cardCount--;
                    }
                }
                // Show the deck price to the user.
                decimal? ConversionRate = token.Value;
                string statusFinal = $"Continue to list {cardCount} cards for {Math.Round(priceTotal, 10)} {currency_name}";
                if (ConversionRate != null)
                {
                    statusFinal += $" (${ (priceTotal * (decimal)ConversionRate).ToString("0.00")})";
                }
                if (cardCount > 0)
                {
                    tbStatus.Text = statusFinal;
                    btnListAll.IsEnabled = true;
                }
                else
                {
                    tbStatus.Text = "No cards to list";
                }
                
            }
            catch (Exception ex)
            {
                if (!cts.Token.IsCancellationRequested)
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
            while (added < 50 && _displayIndex < _cardData.Count)
            {
                OrderDisplayControl control = new(_cardData[_displayIndex].Item1, _cardData[_displayIndex].Item2, _cardData[_displayIndex].Item3);
                if (_listingResults.ContainsKey(_cardData[_displayIndex].Item4))
                {
                    if (_listingResults.ContainsKey(_cardData[_displayIndex].Item4))
                    {
                        SetupOrderDisplay(control);
                        control.ShowStatus(true);
                        control.SetStatus(OrderDisplayControl.DisplayStatus.Success);
                        control.SetStatusMessage("Listing created");
                    }
                    else
                    {
                        SetupOrderDisplay(control);
                        control.ShowStatus(true);
                        control.SetStatus(OrderDisplayControl.DisplayStatus.Fail);
                        control.SetStatusMessage("Listing failed");
                    }
                }
                else
                {
                    if (_cardData[_displayIndex].Item5)
                    {
                        control.SetStatus(OrderDisplayControl.DisplayStatus.Success);
                        control.SetStatusMessage("Already listed");
                    }
                    else
                        SetupOrderDisplay(control);
                }
                cardPanel.Children.Add(control);
                _displayIndex++;
                added++;
            }
        }

        /// <summary>
        /// Close the cardcontrol and return to the main menu.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _orderFetchCancelSource.Cancel();
            ResourceManager.RateLimiter.CancelRequests();
            ((MainWindow)Application.Current.MainWindow).CloseOverlay();
        }

        /// <summary>
        /// Cancel existing search task and restart if the user changes the currency.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_setupWalletTask != null && _setupWalletTask.IsCompleted)
            {
                ResourceManager.RateLimiter.CancelRequests();
                foreach (OrderDisplayControl control in cardPanel.Children)
                {
                    SetupOrderDisplay(control);
                }
                SetupPrices();
            }
        }

        /// <summary>
        /// Create listings for all cards in the users wallet that do not allready have listings.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void btnListAll_Click(object sender, RoutedEventArgs e)
        {
            if (_cardData.Count == 0)
            {
                tbStatus.Text = $"No cards available to sell.";
                return;
            }

            // Prevent user from closing the window until this method finished during the purchase.
            btnListAll.IsEnabled = false;
            btnClose.IsEnabled = false;
            cbCurrency.IsEnabled = false;
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
            string? currency_name = (string?)cbCurrency.SelectedItem;
            if (currency_name == null)
            {
                ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = true;
                scrollBar.IsEnabled = true;
                btnClose.IsEnabled = true;
                tbStatus.Text = "Failed to find buy token.";
                return;
            }
            Token token = (await Wallet.FetchTokens())[currency_name];
            OrderDisplayControl[] panelsArray = new OrderDisplayControl[cardPanel.Children.Count];
            cardPanel.Children.CopyTo(panelsArray, 0);
            HashSet<OrderDisplayControl> panels = panelsArray.ToHashSet();

            List<(NFT card, string tokenID, double price, TextBlock? tbListing)> listings = new();
            foreach ((string name, int proto, int quality, ulong token_id, bool hasOrders) card in _cardData)
            {
                if (card.hasOrders)
                    continue;
                List<Order>? orders;
                bool loaded = _cachedOrders.TryGetValue($"{card.proto}q{card.quality}t{currency_name}", out orders);
                if (!loaded || orders == null || orders.Count == 0)
                    continue;
                decimal priceBuyer = 100M;//wallet.Address.Equals(orders[0].Seller) ? orders[0].PriceTotal() : orders[0].PriceTotal() - new decimal(0.00000001);
                decimal priceList = priceBuyer / getFeeMultiplier(card.quality);
                NFT nft = new NFT()
                {
                    token_address = "0xacb3c6a43d15b907e8433077b6d38ae40936fe2c",
                    token_id = card.token_id
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
                listings.Add((nft, token.Address, decimal.ToDouble(priceList), statusTextBlock));
            }

            Dictionary<NFT, bool> result = await wallet.RequestCreateOrders(Application.Current.MainWindow, listings.ToArray(), this.tbStatus);
            foreach (KeyValuePair<NFT, bool> orderResult in result)
            {
                _listingResults.Add(orderResult.Key.token_id, orderResult.Value);
            }

            panels = panelsArray.ToHashSet();
            Dictionary<ulong, (string, int, int, ulong, bool)> cardDict = _cardData.ToDictionary(x => x.Item4, x => x);
            foreach (NFT nft in result.Keys)
            {
                (string name, int proto, int quality, ulong token_id, bool hasOrders) cardData = cardDict[nft.token_id];
                foreach (OrderDisplayControl panel in panels)
                {
                    if (panel.ProtoID == cardData.proto && panel.Quality == cardData.quality && panel.getStatustextBlock().Text != "Already listed")
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
        #endregion
        #region Supporting methods
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

        /// <summary>
        /// Setup the order display for a specific card to show the correct listing price.
        /// </summary>
        /// <param name="display"></param>
        private void SetupOrderDisplay(OrderDisplayControl display)
        {
            string? currency_name = (string?)cbCurrency.SelectedItem;
            if (currency_name == null)
            {
                return;
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
                    return;
                }
                Order order = orders[0];
                display.SetSubText($"List for: {Math.Round(order.PriceTotal() - new decimal(0.00000001), 10)} {currency_name}");
                display.ShowStatus(false);
            } else
            {
                display.SetStatus(OrderDisplayControl.DisplayStatus.Loading);
                display.ShowStatus(true);
                display.SetSubText("");
            }
        }
        #endregion
    }
}
