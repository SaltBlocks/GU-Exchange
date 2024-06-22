using GU_Exchange.Helpers;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;

namespace GU_Exchange.Controls
{
    /// <summary>
    /// Interaction logic for CancelListingsControl.xaml
    /// </summary>
    public partial class CancelListingsControl : UserControl
    {
        #region Class Properties
        private int _orderIndex;
        private readonly List<(string cardName, string[] cardData, Order order)> _orders;
        private Dictionary<string, bool> cancellationResults;
        #endregion
        #region Default Constructor
        /// <summary>
        /// Constructor for a User Control to let the user cancel all active sell orders for GU cards.
        /// </summary>
        public CancelListingsControl()
        {
            InitializeComponent();
            _orderIndex = 0;
            _orders = new();
            cancellationResults = new();
            setupOrders();
        }
        #endregion
        #region UserControl Setup.
        /// <summary>
        /// Fetch active orders on connected wallet and show them in the cardpanel.
        /// </summary>
        private async void setupOrders()
        {
            Wallet? wallet = Wallet.GetConnectedWallet();
            if (wallet == null)
            {
                tbStatus.Text = "No wallet connected";
                return;
            }
            try
            {
                Log.Information($"Fetching orders for wallet {wallet.Address}");
                bool hasNext = true;
                string urlBase = $"https://api.x.immutable.com/v3/orders?direction=asc&include_fees=true&order_by=buy_quantity&page_size=200&sell_token_address=0xacb3c6a43d15b907e8433077b6d38ae40936fe2c&status=active&user={wallet.Address}";
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
                        string? cardName = (string?)order.SelectToken("sell.data.properties.name");
                        string? img_url = (string?) order.SelectToken("sell.data.properties.image_url");
                        if (cardName == null || img_url == null)
                            continue;
                        string[] card_data = img_url.Split("id=")[1].Split("&q=");
                        Order or = new Order(order, await getOrderCurrencyName(order));
                        _orders.Add((cardName, card_data, or));
                        if (_orderIndex < 50)
                        {
                            OrderDisplayControl control = new(cardName, int.Parse(card_data[0]), int.Parse(card_data[1]));
                            control.ShowStatus(false);
                            control.SetOrder(or);
                            cardPanel.Children.Add(control);
                            _orderIndex++;
                        }
                    }
                    urlInventory = $"{urlBase}&cursor={cursor}";
                }
                tbStatus.Text = $"You currently have {_orders.Count} active orders.";
                btnCancelAll.IsEnabled = true;
            }
            catch (Exception ex)
            {
                Log.Warning($"Failed to fetching orders for wallet \"{wallet.Address}\". {ex.Message}: {ex.StackTrace}");
                tbStatus.Text = $"Failed to fetch active orders.";
                return;
            }
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
            if (btnClose.IsEnabled)
            {
                ((MainWindow)Application.Current.MainWindow).CloseOverlay();
            }
        }

        /// <summary>
        /// Close the cardcontrol and return to the main menu.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            ((MainWindow)Application.Current.MainWindow).CloseOverlay();
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
            while (added < 50 && _orderIndex < _orders.Count)
            {
                OrderDisplayControl control = new(_orders[_orderIndex].cardName, int.Parse(_orders[_orderIndex].cardData[0]), int.Parse(_orders[_orderIndex].cardData[1]));
                control.SetOrder(_orders[_orderIndex].order);

                if (!cancellationResults.ContainsKey(_orders[_orderIndex].order.OrderID.ToString()))
                    control.ShowStatus(false);
                else if (cancellationResults[_orders[_orderIndex].order.OrderID.ToString()])
                {
                    control.ShowStatus(true);
                    control.SetStatusMessage("Listing cancelled");
                    control.SetStatus(OrderDisplayControl.DisplayStatus.Success);
                }
                else
                {
                    control.ShowStatus(true);
                    control.SetStatusMessage("Failed");
                    control.SetStatus(OrderDisplayControl.DisplayStatus.Fail);
                }

                cardPanel.Children.Add(control);
                _orderIndex++;
                added++;
            }
        }

        /// <summary>
        /// Cancel all currently active listings created by the connected wallet.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BtnCancelAll_Click(object sender, RoutedEventArgs e)
        {
            if (cardPanel.Children.Count == 0)
            {
                tbStatus.Text = $"No active orders to cancel.";
                return;
            }

            // Prevent user from closing the window until this method finished.
            btnCancelAll.IsEnabled = false;
            btnClose.IsEnabled = false;
            scrollBar.IsEnabled = false;
            ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = false;

            // Get the connected wallet.
            Wallet? wallet = Wallet.GetConnectedWallet();
            if (wallet == null)
            {
                ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = true;
                btnClose.IsEnabled = true;
                scrollBar.IsEnabled = true;
                return;
            }

            // Collect data needed to purchase the cards.
            List<(string, TextBlock? statusText)> orderData = new();
            Dictionary<string, OrderDisplayControl> controlDict = new();
            foreach (OrderDisplayControl orderDisplay in cardPanel.Children)
            {
                Order? order = orderDisplay.GetOrder();
                if (order != null)
                {
                    orderDisplay.ShowStatus(true);
                    orderDisplay.SetStatus(OrderDisplayControl.DisplayStatus.Loading);
                    controlDict.Add(order.OrderID.ToString(), orderDisplay);
                    orderData.Add((order.OrderID.ToString(), orderDisplay.getStatustextBlock()));
                }
            }
            HashSet<string> displayedOrders = orderData.Select(x => x.Item1).ToHashSet();
            foreach ((string cardName, string[] cardData, Order order) order in _orders)
            {
                if (!displayedOrders.Contains(order.order.OrderID.ToString()))
                {
                    orderData.Add((order.order.OrderID.ToString(), null));
                }
            }

            // Cancel the orders and allow the wallet to update the status message.
            cancellationResults = await wallet.RequestCancelOrders(Application.Current.MainWindow, orderData.ToArray(), tbStatus);

            foreach (string order in cancellationResults.Keys)
            {
                if (!controlDict.ContainsKey(order))
                    continue;
                if (cancellationResults[order])
                {
                    controlDict[order].SetStatus(OrderDisplayControl.DisplayStatus.Success);
                }
                else
                {
                    controlDict[order].SetStatus(OrderDisplayControl.DisplayStatus.Fail);
                }
            }
            ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = true;
            scrollBar.IsEnabled = true;
            btnClose.IsEnabled = true;
        }
        #endregion
        #region Supporting methods
        /// <summary>
        /// Get the token symbol associated with a provided order in json format.
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        private async Task<string> getOrderCurrencyName(JToken order)
        {
            string? token_address = (string?)order.SelectToken("buy.data.token_address");
            if (token_address == null )
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
        #endregion
    }
}
