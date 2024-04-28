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
using ImageProcessor.Processors;

namespace GU_Exchange.Controls
{
    /// <summary>
    /// Interaction logic for ListAllControl.xaml
    /// </summary>
    public partial class ListAllControl : UserControl
    {
        private int displayIndex;
        private readonly Object _setupLock;
        private CancellationTokenSource orderFetchCancelSource;
        private readonly Dictionary<string, List<Order>> _cachedOrders;
        private readonly Task setupWalletTask;
        private readonly List<(string, int, int, ulong)> _cardData;
        private readonly Dictionary<string, Task<string>> orderBookFetchTasks;

        public ListAllControl()
        {
            InitializeComponent();
            cbCurrency.Items.Add("ETH");
            cbCurrency.Items.Add("GODS");
            cbCurrency.Items.Add("IMX");
            cbCurrency.SelectedIndex = 0;
            displayIndex = 0;
            _setupLock = new object();
            _cachedOrders = new();
            _cardData = new();
            orderFetchCancelSource = new();
            orderBookFetchTasks = new();
            setupWalletTask = setupWallet();
            setupPrices();
        }

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
                Log.Information($"Fetching cards in wallet {wallet.Address}.");
                bool hasNext = true;
                string urlBase = $"https://api.x.immutable.com/v1/assets?page_size=200&user={wallet.Address}&collection=0xacb3c6a43d15b907e8433077b6d38ae40936fe2c";
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
                        if (cardName == null || img_url == null || token_id == null)
                            continue;
                        string[] card_data = img_url.Split("id=")[1].Split("&q=");
                        _cardData.Add((cardName, int.Parse(card_data[0]), int.Parse(card_data[1]), ulong.Parse(token_id)));
                        if (displayIndex < 50)
                        {
                            OrderDisplayControl control = new(cardName, int.Parse(card_data[0]), int.Parse(card_data[1]));
                            cardPanel.Children.Add(control);
                            displayIndex++;
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

        private async void setupPrices()
        {
            btnListAll.IsEnabled = false;
            ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = false;

            CancellationTokenSource cts = new();
            
            try
            {
                await setupWalletTask;
                string? currency_name = (string?)cbCurrency.SelectedItem;
                if (currency_name == null)
                {
                    tbStatus.Text = "Failed to find buy token.";
                    return;
                }

                // Get orders for cards needed to complete the deck.
                Token token = (await Wallet.FetchTokens())[currency_name];
                if (orderFetchCancelSource.IsCancellationRequested)
                    throw new OperationCanceledException("Setting up prices was cancelled");
                orderFetchCancelSource.Cancel();
                orderFetchCancelSource = cts;
                lock (orderBookFetchTasks)
                    orderBookFetchTasks.Clear();
                CancellationToken ct = orderFetchCancelSource.Token;
                Dictionary<Task<decimal>, (string name, int proto, int quality, ulong token_id)> priceFetchTasks = new();
                foreach ((string name, int proto, int quality, ulong token_id) card in _cardData)
                {
                    priceFetchTasks.Add(GetListPriceForCard(card.proto, card.quality, token, ct), card);
                }

                Console.WriteLine(priceFetchTasks.Count);
                while (priceFetchTasks.Count > 0)
                {
                    Task<decimal> completed = await Task.WhenAny(priceFetchTasks.Keys);
                    if (ct.IsCancellationRequested)
                    {
                        throw new OperationCanceledException("Fetching of orders was cancelled.");
                    }
                    (string name, int proto, int quality, ulong token_id) card = priceFetchTasks[completed];
                    try
                    {
                        foreach (OrderDisplayControl display in cardPanel.Children)
                        {
                            if (display.ProtoID == card.proto && display.Quality == card.quality)
                            {
                                display.ShowStatus(false);
                            }
                        }
                        Console.WriteLine($"{card.name} costs {await completed} {token.Name}");
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
                        Console.WriteLine($"{card.name} is of unknown value");
                    }
                    priceFetchTasks.Remove(completed);
                    tbStatus.Text = $"Fetching card prices: {_cardData.Count - priceFetchTasks.Count} / {_cardData.Count} fetched.";
                }
                btnListAll.IsEnabled = true;
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
            Console.WriteLine("---DONE---");
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
            if (btnCancel.IsEnabled)
            {
                orderFetchCancelSource.Cancel();
                ResourceManager.RateLimiter.CancelRequests();
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
            orderFetchCancelSource.Cancel();
            ResourceManager.RateLimiter.CancelRequests();
            ((MainWindow)Application.Current.MainWindow).CloseOverlay();
        }

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
                //Log.Information($"Fetching orders for {protoID} of quality {qualityStr}");
                string? strOrderBook = null;
                Task<string>? fetchOrderBook;
                lock (orderBookFetchTasks)
                {
                    bool previouslyRequested = orderBookFetchTasks.TryGetValue($"{protoID}q{quality}c{token.Name}", out fetchOrderBook);
                    if (!previouslyRequested)
                    {
                        fetchOrderBook = ResourceManager.Client.GetStringAsync(urlOrderBook, cancelToken);
                        orderBookFetchTasks.Add($"{protoID}q{quality}c{token.Name}", fetchOrderBook);
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
            return orders[0].PriceTotal() - new decimal(0.00000001);
        }

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

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (setupWalletTask != null && setupWalletTask.IsCompleted)
            {
                ResourceManager.RateLimiter.CancelRequests();
                setupPrices();
            }
        }
    }
}
