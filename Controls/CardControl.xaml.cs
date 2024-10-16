﻿using GU_Exchange.Controls;
using GU_Exchange.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for CardControl.xaml
    /// </summary>
    public partial class CardControl : UserControl
    {
        #region Static Fields
        private static CancellationTokenSource s_imgTokenSource = new();
        private static CancellationTokenSource s_ordersTokenSource = new();
        #endregion

        #region Class properties
        public readonly int CardID;
        private readonly Task<BitmapSource?> _imgMeteorite;
        private readonly Task<BitmapSource?> _imgShadow;
        private readonly Task<BitmapSource?> _imgGold;
        private readonly Task<BitmapSource?> _imgDiamond;
        private readonly List<Order> _offersList;
        public bool CanClose;
        #endregion

        #region Default Constructor
        /// <summary>
        /// Default contructor for a cardcontrol.
        /// Will automatically load and cache images corresponding to the given cardID.
        /// </summary>
        /// <param name="CardID">The proto ID of the card to display.</param>
        public CardControl(int CardID)
        {
            InitializeComponent();
            btnBuy.AddContextItem("Create offer", OfferButton_Click);
            this.CardID = CardID;
            s_imgTokenSource.Cancel();
            s_imgTokenSource = new();
            s_ordersTokenSource.Cancel();
            s_ordersTokenSource = new();
            _imgMeteorite = ResourceManager.GetCardImageAsync(CardID, 4, false, s_imgTokenSource.Token);
            _imgShadow = ResourceManager.GetCardImageAsync(CardID, 3, false, s_imgTokenSource.Token);
            _imgGold = ResourceManager.GetCardImageAsync(CardID, 2, false, s_imgTokenSource.Token);
            _imgDiamond = ResourceManager.GetCardImageAsync(CardID, 1, false, s_imgTokenSource.Token);
            _offersList = new List<Order>();
            CanClose = true;
            cbToken.Items.Add("ETH");
            cbToken.Items.Add("GODS");
            cbToken.Items.Add("IMX");
            cbToken.SelectedIndex = 0;
            _ = SetupAsync();
        }
        #endregion

        #region Data loading and setup
        /// <summary>
        /// Setup method for the image, inventory and orders.
        /// </summary>
        /// <returns>Task setting up the window.</returns>
        private async Task SetupAsync()
        {
            Task imgSetup = SetupCardAsync();
            Task setupInventory = SetupInventoryAsync();
            await imgSetup;
            await setupInventory;
        }

        /// <summary>
        /// Setup labels and selection boxes on the window.
        /// </summary>
        /// <returns>Task setting up selection boxes.</returns>
        private async Task SetupCardAsync()
        {
            Dictionary<int, CardData>? dict = await GameDataManager.GetCardListAsync();
            if (dict != null && dict.ContainsKey(CardID))
            {
                CardData data = dict[CardID];
                this.tbCardName.Text = data.Name;
                this.tbSet.Text = GameDataManager.GetSetDisplayName(Capitalize(data.Set));
                this.tbGod.Text = Capitalize(data.God);
                this.tbRarity.Text = Capitalize(data.Rarity);
                this.tbPlayrate.Text = $"{(GameDataManager.GetPlayRate(CardID) * 100).ToString("0.###")}% of decks";
                (double, double, double)? winrate = GameDataManager.GetWinRateWithCI(CardID);
                this.tbWinrate.Text = winrate == null ? "Not played" : $"{((double)(winrate.Value.Item1 * 100)).ToString("0.##")}% ({((double)(winrate.Value.Item2 * 100)).ToString("0.##")}% - {((double)(winrate.Value.Item3 * 100)).ToString("0.##")}%)";
                this.cbQuality.Items.Add("Meteorite");
                this.cbQuality.Items.Add("Shadow");
                this.cbQuality.Items.Add("Gold");
                this.cbQuality.Items.Add("Diamond");
                this.cbQuality.SelectedIndex = 0;
            }
            this.imgCard.Source = await _imgMeteorite;
        }

        /// <summary>
        /// Fetches the inventory of the connected wallet and displays the number of cards owned of each quality.
        /// </summary>
        /// <returns>Task setting up the cards owned display</returns>
        public async Task SetupInventoryAsync()
        {
            // Show the No wallet connected message if no wallet it connected.
            Wallet? connectedWallet = Wallet.GetConnectedWallet();
            if (connectedWallet == null)
            {
                gridNoWallet.Visibility = Visibility.Visible;
                gridMeteorite.Visibility = Visibility.Collapsed;
                gridShadow.Visibility = Visibility.Collapsed;
                gridGold.Visibility = Visibility.Collapsed;
                gridDiamond.Visibility = Visibility.Collapsed;
                btnBuy.IsEnabled = false;
                btnOrders.IsEnabled = false;
                btnTransfer.IsEnabled = false;
                tbConnectWallet.Text = "No Wallet Connected";
                return;
            }
            if (!(await connectedWallet.IsLinkedAsync() ?? false))
            {
                gridNoWallet.Visibility = Visibility.Visible;
                gridMeteorite.Visibility = Visibility.Collapsed;
                gridShadow.Visibility = Visibility.Collapsed;
                gridGold.Visibility = Visibility.Collapsed;
                gridDiamond.Visibility = Visibility.Collapsed;
                btnBuy.IsEnabled = false;
                btnOrders.IsEnabled = false;
                btnTransfer.IsEnabled = false;
                tbConnectWallet.Text = "Link wallet to IMX";
                return;
            }
            gridNoWallet.Visibility = Visibility.Collapsed;
            gridMeteorite.Visibility = Visibility.Visible;
            gridShadow.Visibility = Visibility.Visible;
            gridGold.Visibility = Visibility.Visible;
            gridDiamond.Visibility = Visibility.Visible;
            btnBuy.IsEnabled = true;
            btnOrders.IsEnabled = true;
            btnTransfer.IsEnabled = true;

            string cardData = HttpUtility.UrlEncode("{\"proto\":[\"" + CardID + "\"]}");
            string urlInventory = $"https://api.x.immutable.com/v1/assets?page_size=10&user={connectedWallet.Address}&metadata={cardData}&sell_orders=true";
            Log.Information($"Fetching cards with protoID {CardID} in wallet {connectedWallet.Address}");
            string cardString;
            try
            {
                cardString = await ResourceManager.Client.GetStringAsync(urlInventory);
            }
            catch (HttpRequestException e)
            {
                Log.Information($"An exception occurred while Fetching wallet contents: {e.Message}: {e.StackTrace}");
                return;
            }
            // Extract cards owned from the return data.
            JObject? jsonData = (JObject?)JsonConvert.DeserializeObject(cardString);
            if (jsonData == null)
                return;
            JToken? result = jsonData["result"];
            if (result == null)
                return;
            int meteorite = 0;
            int shadow = 0;
            int gold = 0;
            int diamond = 0;
            foreach (JToken card in result)
            {
                JToken? metaData = card["metadata"];
                if (metaData == null)
                    continue;
                string? quality = (string?)metaData["quality"];
                if (quality == null)
                    continue;
                if (quality.Equals("Meteorite"))
                    meteorite++;
                else if (quality.Equals("Shadow"))
                    shadow++;
                else if (quality.Equals("Gold"))
                    gold++;
                else if (quality.Equals("Diamond"))
                    diamond++;
            }
            this.tbMeteorite.Text = meteorite.ToString();
            this.tbShadow.Text = shadow.ToString();
            this.tbGold.Text = gold.ToString();
            this.tbDiamond.Text = diamond.ToString();
        }

        /// <summary>
        /// Fetch the IMX global orderbook and get orders for the card this window displays of the specified quality (Meteorite, Shadow, Gold or Diamond).
        /// </summary>
        /// <param name="quality"> The quality of the card</param>
        /// <param name="token">a <see cref="CancellationToken"/> that can be used to cancel fetching orders.</param>
        /// <returns>Task setting up the local orderbook.</returns>
        private async Task SetupOrderbookAsync(string quality, CancellationToken token)
        {
            if (quality == null)
                return;
            this.orderPanel.Children.Clear(); // Remove the existing orders from the list of orders.
            _offersList.Clear(); // Remove existing offers from the list.
            this.spinner.Visibility = Visibility.Visible; // Show the loading spinner.

            // Fetch orders in the IMX global orderbook for the specified card of the specified quality listed in the selected token.
            string cardData = HttpUtility.UrlEncode("{\"proto\":[\"" + CardID + "\"],\"quality\":[\"" + quality + "\"]}");
            string? currency_name = (string?)this.cbToken.SelectedItem;
            if (currency_name == null)
                return;
            Token currency = (await Wallet.FetchTokens())[currency_name];
            string token_str = currency.Address;
            if (token_str.Equals("ETH"))
                token_str = "&buy_token_type=ETH";
            Wallet? wallet = Wallet.GetConnectedWallet();
            string urlOrderBook = $"https://api.x.immutable.com/v3/orders?buy_token_address={token_str}&direction=asc&include_fees=true&order_by=buy_quantity&page_size=50&sell_metadata={cardData}&sell_token_address=0xacb3c6a43d15b907e8433077b6d38ae40936fe2c&status=active";
            string strOrderBook;
            string strInventory = "{\"result\":[],\"cursor\":\"\",\"remaining\":0}";
            try
            {
                Log.Information($"Fetching orders for {CardID} of quality {quality}");
                Task<string> taskGetOrders = ResourceManager.Client.GetStringAsync(urlOrderBook, token);
                if (wallet != null)
                {
                    string urlInventory = $"https://api.x.immutable.com/v3/orders?buy_token_address={token_str}&direction=asc&include_fees=true&order_by=buy_quantity&page_size=50&sell_metadata={cardData}&sell_token_address=0xacb3c6a43d15b907e8433077b6d38ae40936fe2c&status=active&user={wallet.Address}";
                    strInventory = await ResourceManager.Client.GetStringAsync(urlInventory);
                }
                strOrderBook = await taskGetOrders;
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is HttpRequestException)
            {
                if (ex is HttpRequestException)
                    Log.Information($"Failed to fetching orders for {CardID} of quality {quality}. {ex.Message}: {ex.StackTrace}");
                return;
            }

            // Extract orders from the data returned by the server.
            JObject? jsonOrders = JsonConvert.DeserializeObject<JObject?>(strOrderBook);
            JToken? result = jsonOrders?["result"];

            if (result == null)
                return;

            List<Order> orders = result
                .Select(order => new Order(order, currency.Name))
                .ToList();

            // Extract orders from the users inventory.
            jsonOrders = JsonConvert.DeserializeObject<JObject?>(strInventory);
            result = jsonOrders?["result"];
            if (result == null)
                return;

            orders.AddRange(result
                .Select(order => new Order(order, currency.Name))
                .Where(x => !orders.Select(y => y.OrderID).Contains(x.OrderID))
                .ToList());

            // Sort the orders by cost from lowest to highest before displaying them in the window.
            foreach (Order order in orders.OrderBy(x => x.PriceTotal()))
            {
                OrderBarControl bar = new OrderBarControl(order);
                if (wallet != null && order.Seller.Equals(wallet.Address))
                    bar.SetBackgroundColor("#3F00FF00");
                orderPanel.Children.Add(bar);
                bar.Width = 450f / 800f * this.controlGrid.ActualWidth;
            }

            // Fetch offers that have been made for the displayed card.
            string urlOffers = $"https://api.x.immutable.com/v3/orders?status=active&buy_metadata={cardData}&order_by=sell_quantity&direction=desc&page_size=200";
            string? cursor = null;
            do
            {
                string url = cursor != null ? $"{urlOffers}&cursor={cursor}" : urlOffers;
                jsonOrders = JsonConvert.DeserializeObject<JObject>(await ResourceManager.Client.GetStringAsync(url));
                result = jsonOrders?["result"];
                if (result == null)
                    break;
                _offersList.AddRange(result.Select(order => new Order(order, currency.Name)));

                cursor = jsonOrders?["cursor"]?.ToString();
            } while (jsonOrders?["remaining"]?.ToString() == "1");

            // Color orders that are posted by the user of have offers from the user.
            if (wallet != null)
            {
                foreach (OrderBarControl orderBar in orderPanel.Children)
                {
                    if (_offersList.Where(x => x.Seller.Equals(wallet.Address)).Select(x => x.TokenID).Contains(orderBar.Order.TokenID))
                        orderBar.SetBackgroundColor("#7FAA1EAF");
                }
            }

            spinner.Visibility = Visibility.Collapsed; // Hide the loading spinner.
        }

        /// <summary>
        /// Fetch the orders from the IMX orderbook and display the updated orders in the window.
        /// </summary>
        /// <returns></returns>
        public async Task ReloadOrderbookAsync()
        {
            s_ordersTokenSource.Cancel();
            s_ordersTokenSource = new CancellationTokenSource();
            await SetupOrderbookAsync((string)this.cbQuality.SelectedItem, s_ordersTokenSource.Token);
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// Updates the displayed orders when the user changes the selected currency.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CbToken_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await ReloadOrderbookAsync();
        }

        /// <summary>
        /// Called when the quality is changed in the quality combo box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CbQuality_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string? quality = this.cbQuality.SelectedItem.ToString();
            if (quality == null)
                return;
            await ChangeQualityAsync(quality);
        }

        /// <summary>
        /// Called when the used clicks the Meteorite display.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TbMeteorite_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.cbQuality.SelectedIndex = 0;
        }

        /// <summary>
        /// Called when the used clicks the Shadow display.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TbShadow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.cbQuality.SelectedIndex = 1;
        }

        /// <summary>
        /// Called when the used cickes the Gold display.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TbGold_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.cbQuality.SelectedIndex = 2;
        }

        /// <summary>
        /// Called when the used cickes the Diamond display.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TbDiamond_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.cbQuality.SelectedIndex = 3;
        }

        /// <summary>
        /// Close the cardcontrol and return to the main menu.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (CanClose)
                ((MainWindow)Application.Current.MainWindow).CloseOverlay();
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
            // Dont allow the user to close the window if this is disabled.
            if (!CanClose)
                return;

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
        /// Update the size of the order selection bars when the window is resized.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OrderPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            for (int i = 0; i < orderPanel.Children.Count; i++)
            {
                var child = (OrderBarControl)orderPanel.Children[i];
                child.Width = orderPanel.ActualWidth;
            }
        }

        /// <summary>
        /// Highlight the rectangle to indicate to the user that it is clickable.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RectNoWallet_MouseEnter(object sender, MouseEventArgs e)
        {
            // Change the colors to highlight the rectangle
            rectNoWallet.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x7F, 0xAD, 0xD8, 0xE6)); // Change to the color you want
            rectNoWallet.Stroke = new SolidColorBrush(Colors.DarkBlue); // Change to the color you want
        }

        /// <summary>
        /// Remove the highlighting when the mouse leaves the rectangle.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RectNoWallet_MouseLeave(object sender, MouseEventArgs e)
        {
            // Restore the original colors when the mouse leaves
            rectNoWallet.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x7F, 0xA5, 0xA4, 0xA4)); // Original fill color
            rectNoWallet.Stroke = new SolidColorBrush(Colors.Black); // Original stroke color
        }

        /// <summary>
        /// Open wallet setup if the user clicks the rectangle.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void RectNoWallet_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Wallet? connectedWallet = Wallet.GetConnectedWallet();
            if (connectedWallet == null)
            {
                SetupWalletWindow setup = new();
                setup.Owner = (MainWindow)Application.Current.MainWindow;
                setup.ShowDialog();
                return;
            }
            if (await connectedWallet.RequestLinkAsync((MainWindow)Application.Current.MainWindow))
            {
                MessageWindow window = new MessageWindow($"Wallet linked to IMX successfully.", "Link wallet", MessageType.INFORM);
                window.Owner = (MainWindow)Application.Current.MainWindow;
                window.ShowDialog();
                await SetupInventoryAsync();
            } else
            {
                MessageWindow window = new MessageWindow($"Wallet not linked to IMX.", "Link wallet", MessageType.INFORM);
                window.Owner = (MainWindow)Application.Current.MainWindow;
                window.ShowDialog();
            }
        }

        /// <summary>
        /// Prompt the user to buy the cheapest card on sale.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnBuy_Click(object sender, RoutedEventArgs e)
        {
            Order? cheapestOrder = GetCheapestBuyableOrder();
            if (cheapestOrder == null)
                return;
            OpenOrder(cheapestOrder);
        }

        /// <summary>
        /// Prompt the user to buy the cheapest card on sale.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnList_Click(object sender, RoutedEventArgs e)
        {
            ListControl _sellControl = new ListControl(this, imgCard.Source);
            _sellControl.Margin = new Thickness(0, 0, 0, 0);
            Grid.SetColumnSpan(_sellControl, 2);
            controlGrid.Children.Add(_sellControl);
        }

        /// <summary>
        /// Prompt the user to transfer the card on display.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnTransfer_Click(object sender, RoutedEventArgs e)
        {
            TransferCardControl _transferControl = new TransferCardControl(this, this.CardID, imgCard.Source);
            _transferControl.Margin = new Thickness(0, 0, 0, 0);
            Grid.SetColumnSpan(_transferControl, 2);
            controlGrid.Children.Add(_transferControl);
        }
        #endregion

        #region Supporting methods
        /// <summary>
        /// Return the cheapest <see cref="Order"/> that is not listed by the currencly being displayed or null in case no orders are available.
        /// </summary>
        /// <returns></returns>
        public Order? GetCheapestBuyableOrder()
        {
            if (orderPanel.Children.Count == 0)
            {
                return null;
            }
            Wallet? wlt = Wallet.GetConnectedWallet();
            if (wlt == null)
                return((OrderBarControl)orderPanel.Children[0]).Order;
            Order? cheapestBuyable = null;
            foreach (OrderBarControl orderControl in orderPanel.Children)
            {
                if (!orderControl.Order.Seller.Equals(wlt.Address))
                {
                    cheapestBuyable = orderControl.Order;
                    break;
                }
            }
            return cheapestBuyable;
        }

        /// <summary>
        /// Open the specified <see cref="Order"/> in the main window.
        /// If it was posted by the user, allow them them to edit it, otherwise show the purchase screen.
        /// </summary>
        /// <param name="order"></param>
        public void OpenOrder(Order order)
        {
            if (spinner.Visibility == Visibility.Visible)
                return;
            Wallet? wlt = Wallet.GetConnectedWallet();
            if (wlt == null)
                return;
            if (wlt.Address == order.Seller)
            {
                UpdateListingControl _updateControl = new UpdateListingControl(this, order, imgCard.Source);
                _updateControl.Margin = new Thickness(0, 0, 0, 0);
                Grid.SetColumnSpan(_updateControl, 2);
                controlGrid.Children.Add(_updateControl);
            }
            else
            {
                BuyControl _buyControl = new BuyControl(this, order, imgCard.Source, _offersList.Where(x => x.TokenID.Equals(order.TokenID)).ToList());
                _buyControl.Margin = new Thickness(0, 0, 0, 0);
                Grid.SetColumnSpan(_buyControl, 2);
                controlGrid.Children.Add(_buyControl);
            }
        }

        public void OpenOffer(Order order)
        {
            OfferControl _offerControl = new OfferControl(this, order, imgCard.Source);
            _offerControl.Margin = new Thickness(0, 0, 0, 0);
            Grid.SetColumnSpan(_offerControl, 2);
            controlGrid.Children.Add(_offerControl);
        }

        /// <summary>
        /// Update the window after the selected card quality is changed.
        /// </summary>
        /// <param name="quality">The new selected quality</param>
        /// <returns></returns>
        private async Task ChangeQualityAsync(string quality)
        {
            switch (quality)
            {
                case "Meteorite":
                    this.imgCard.Source = await _imgMeteorite;
                    break;
                case "Shadow":
                    this.imgCard.Source = await _imgShadow;
                    break;
                case "Gold":
                    this.imgCard.Source = await _imgGold;
                    break;
                case "Diamond":
                    this.imgCard.Source = await _imgDiamond;
                    break;
            }
            s_ordersTokenSource.Cancel();
            s_ordersTokenSource = new CancellationTokenSource();
            await SetupOrderbookAsync(quality, s_ordersTokenSource.Token);
        }

        /// <summary>
        /// Capitalize the first letter of a string.
        /// </summary>
        /// <param name="str">The string to capitalize</param>
        /// <returns>The string with the first letter capitalized.</returns>
        private static string Capitalize(string str)
        {
            char[] chars = str.ToCharArray();
            chars[0] = char.ToUpper(chars[0]);
            return new string(chars);
        }

        #endregion

        private async void btnChart_Click(object sender, RoutedEventArgs e)
        {
            string? currency_name = (string?)this.cbToken.SelectedItem;
            if (currency_name == null)
                return;
            Token currency = (await Wallet.FetchTokens())[currency_name];
            PriceChartControl _sellControl = new PriceChartControl(CardID, (string)this.cbQuality.SelectedItem, 30, currency);
            _sellControl.Margin = new Thickness(0, 0, 0, 0);
            Grid.SetColumnSpan(_sellControl, 2);
            controlGrid.Children.Add(_sellControl);
        }

        private void OfferButton_Click(object sender, RoutedEventArgs e)
        {
            Order? cheapestOrder = GetCheapestBuyableOrder();
            if (cheapestOrder == null)
                return;
            OpenOffer(cheapestOrder);
        }
    }

    /// <summary>
    /// <see cref="IValueConverter"/> used to automatically resize the cards owned display.
    /// </summary>
    public class HalfValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double)
            {
                return (double)value / 2;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
