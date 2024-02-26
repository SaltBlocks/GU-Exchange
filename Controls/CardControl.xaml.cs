using GU_Exchange.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            this.CardID = CardID;
            s_imgTokenSource.Cancel();
            s_imgTokenSource = new();
            s_ordersTokenSource.Cancel();
            s_ordersTokenSource = new();
            _imgMeteorite = ResourceManager.GetCardImageAsync(CardID, 4, false, s_imgTokenSource.Token);
            _imgShadow = ResourceManager.GetCardImageAsync(CardID, 3, false, s_imgTokenSource.Token);
            _imgGold = ResourceManager.GetCardImageAsync(CardID, 2, false, s_imgTokenSource.Token);
            _imgDiamond = ResourceManager.GetCardImageAsync(CardID, 1, false, s_imgTokenSource.Token);
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
                this.tbSet.Text = Capitalize(data.Set);
                this.tbGod.Text = Capitalize(data.God);
                this.tbRarity.Text = Capitalize(data.Rarity);
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
            if (!await connectedWallet.IsLinkedAsync())
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
            string cardString = await ResourceManager.Client.GetStringAsync(urlInventory);
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
                Debug.WriteLine($"Fetching orders for {CardID} of quality {quality}");
                Task<string> taskGetOrders = ResourceManager.Client.GetStringAsync(urlOrderBook, token);
                if (wallet != null)
                {
                    //TODO setup fetching of cards in users wallet.
                    string urlInventory = $"https://api.x.immutable.com/v3/orders?buy_token_address={token_str}&direction=asc&include_fees=true&order_by=buy_quantity&page_size=50&sell_metadata={cardData}&sell_token_address=0xacb3c6a43d15b907e8433077b6d38ae40936fe2c&status=active&user={wallet.Address}";
                    strInventory = await ResourceManager.Client.GetStringAsync(urlInventory);
                }
                strOrderBook = await taskGetOrders;
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is HttpRequestException)
            {
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
                        orders.Add(or);
                    }
                    catch (NullReferenceException)
                    {
                    }
                }
            }

            // Extract orders from the users inventory.
            jsonOrders = (JObject?)JsonConvert.DeserializeObject(strInventory);
            if (jsonOrders == null)
                return;
            result = jsonOrders["result"];
            if (result == null)
                return;

            foreach (JToken order in result)
            {
                try
                {
                    Order or = new Order(order, currency.Name);
                    bool exists = false;
                    foreach (Order listOrder in orders)
                    {
                        if (listOrder.OrderID.Equals(or.OrderID))
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists)
                        orders.Add(or);
                }
                catch (NullReferenceException)
                {
                }
            }

            // Sort the orders by cost from lowest to highest before displaying them in the window.
            foreach (Order order in orders.OrderBy(x => x.PriceTotal()))
            {
                OrderBar bar = new OrderBar(order);
                // TODO once user wallets are implemented, change the color of orders posted by the user to make them easy to identify.
                if (wallet != null && order.Seller.Equals(wallet.Address))
                {
                    bar.setBackgroundColor("#3F00FF00");
                }
                this.orderPanel.Children.Add(bar);
                bar.Width = 450f / 800f * this.controlGrid.ActualWidth;
            }
            this.spinner.Visibility = Visibility.Collapsed; // Hide the loading spinner.
        }

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
        private async void cbToken_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await ReloadOrderbookAsync();
        }

        /// <summary>
        /// Called when the quality is changed in the quality combo box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void cbQuality_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
        private void tbMeteorite_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.cbQuality.SelectedIndex = 0;
        }

        /// <summary>
        /// Called when the used clicks the Shadow display.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbShadow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.cbQuality.SelectedIndex = 1;
        }

        /// <summary>
        /// Called when the used cickes the Gold display.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbGold_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.cbQuality.SelectedIndex = 2;
        }

        /// <summary>
        /// Called when the used cickes the Diamond display.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbDiamond_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.cbQuality.SelectedIndex = 3;
        }

        /// <summary>
        /// Close the cardcontrol and return to the main menu.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            ((MainWindow)Application.Current.MainWindow).CloseCardControl();
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
            ((MainWindow)Application.Current.MainWindow).CloseCardControl();
        }

        /// <summary>
        /// Update the size of the order selection bars when the window is resized.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void orderPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            for (int i = 0; i < orderPanel.Children.Count; i++)
            {
                var child = (OrderBar)orderPanel.Children[i];
                child.Width = orderPanel.ActualWidth;
            }
        }

        /// <summary>
        /// Highlight the rectangle to indicate to the user that it is clickable.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void rectNoWallet_MouseEnter(object sender, MouseEventArgs e)
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
        private void rectNoWallet_MouseLeave(object sender, MouseEventArgs e)
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
        private async void rectNoWallet_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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
            }
        }

        /// <summary>
        /// Prompt the user to buy the cheapest card on sale.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnBuy_Click(object sender, RoutedEventArgs e)
        {
            Order? cheapestOrder = GetCheapestOrder();
            if (cheapestOrder == null)
                return;
            OpenOrder(cheapestOrder);
        }

        /// <summary>
        /// Prompt the user to buy the cheapest card on sale.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnList_Click(object sender, RoutedEventArgs e)
        {
            ListControl _sellControl = new ListControl(this, imgCard.Source);
            _sellControl.Margin = new Thickness(0, 0, 0, 0);
            Grid.SetColumnSpan(_sellControl, 2);
            controlGrid.Children.Add(_sellControl);
        }

        #endregion

        #region Supporting methods

        public Order? GetCheapestOrder()
        {
            if (orderPanel.Children.Count == 0)
            {
                return null;
            }
            return ((OrderBar)orderPanel.Children[0]).Order;
        }

        public void OpenOrder(Order order)
        {
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
                BuyControl _buyControl = new BuyControl(this, order, imgCard.Source);
                _buyControl.Margin = new Thickness(0, 0, 0, 0);
                Grid.SetColumnSpan(_buyControl, 2);
                controlGrid.Children.Add(_buyControl);
            }
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
        private string Capitalize(string str)
        {
            char[] chars = str.ToCharArray();
            chars[0] = char.ToUpper(chars[0]);
            return new string(chars);
        }

        #endregion
    }

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
