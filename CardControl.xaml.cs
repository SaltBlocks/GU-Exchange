using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
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
        private readonly int _cardID;
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
            this._cardID = CardID;
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
            if (dict != null && dict.ContainsKey(_cardID))
            {
                CardData data = dict[_cardID];
                this.lblCardName.Content = data.Name;
                this.lblSet.Text = Capitalize(data.Set);
                this.lblGod.Text = Capitalize(data.God);
                this.lblRarity.Text = Capitalize(data.Rarity);
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
        /// TODO implement loading of wallet contents.
        /// </summary>
        /// <returns>Task setting up the cards owned display</returns>
        private async Task SetupInventoryAsync()
        {
            // Show the No wallet connected message if no wallet it connected.
            if (Wallet.GetConnectedWallet() == null)
            {
                this.rectNoWallet.Visibility = Visibility.Visible;
                this.tbNoWallet.Visibility = Visibility.Visible;
                this.btnBuy.Visibility = Visibility.Collapsed;
                this.btnOrders.Visibility = Visibility.Collapsed;
                this.btnTransfer.Visibility = Visibility.Collapsed;
                return;
            }
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
            string cardData = HttpUtility.UrlEncode("{\"proto\":[\"" + _cardID + "\"],\"quality\":[\"" + quality + "\"]}");
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
                Debug.WriteLine($"Fetching orders for {_cardID} of quality {quality}");
                Task<string> taskGetOrders = ResourceManager.Client.GetStringAsync(urlOrderBook, token);
                if (wallet != null)
                {
                    //TODO setup fetching of cards in users wallet.
                    //string urlInventory = $"https://api.x.immutable.com/v3/orders?buy_token_address={token_str}&direction=asc&include_fees=true&order_by=buy_quantity&page_size=50&sell_metadata={cardData}&sell_token_address=0xacb3c6a43d15b907e8433077b6d38ae40936fe2c&status=active&user={wallet.address}";
                    //strInventory = await ResourceManager.client.GetStringAsync(urlInventory);
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
                //if (Wallet.ConnectedWallet != null && order.seller.Equals(Wallet.ConnectedWallet.address))
                //{
                //    bar.setBackgroundColor("#3F00FF00");
                //}
                this.orderPanel.Children.Add(bar);
                bar.Width = 450f / 800f * this.controlGrid.ActualWidth;
            }
            this.spinner.Visibility = Visibility.Collapsed; // Hide the loading spinner.
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
            s_ordersTokenSource.Cancel();
            s_ordersTokenSource = new CancellationTokenSource();
            await SetupOrderbookAsync((string)this.cbQuality.SelectedItem, s_ordersTokenSource.Token);
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
        private void tbMeteorite_MouseUp(object sender, MouseButtonEventArgs e)
        {
            this.cbQuality.SelectedIndex = 0;
        }

        /// <summary>
        /// Called when the used clicks the Shadow display.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbShadow_MouseUp(object sender, MouseButtonEventArgs e)
        {
            this.cbQuality.SelectedIndex = 1;
        }

        /// <summary>
        /// Called when the used cickes the Gold display.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbGold_MouseUp(object sender, MouseButtonEventArgs e)
        {
            this.cbQuality.SelectedIndex = 2;
        }

        /// <summary>
        /// Called when the used cickes the Diamond display.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbDiamond_MouseUp(object sender, MouseButtonEventArgs e)
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
        /// Adjust the size of elements on the CardControl when the window size is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double width = Math.Min(this.ActualWidth, 1400);
            double height = width / 800 * 450;
            if (height > this.ActualHeight)
            {
                height = Math.Min(this.ActualHeight, 940);
                width = height / 450 * 800;
            }

            this.controlGrid.Height = height;
            this.controlGrid.Width = width;

            this.lblCardName.Width = 491f / 800f * width;
            this.rectInfo.Margin = new Thickness(31f / 800f * width, 70f / 450f * height, 0, 0);
            this.rectInfo.Width = 469f / 800f * width;
            this.rectInfo.Height = 98f / 450f * height;

            this.tbSet.Margin = new Thickness(48f / 800f * width, 80f / 450f * height, 0, 0);
            this.tbGod.Margin = new Thickness(48f / 800f * width, 100f / 450f * height, 0, 0);
            this.tbQuality.Margin = new Thickness(48f / 800f * width, 120f / 450f * height, 0, 0);
            this.tbRarity.Margin = new Thickness(48f / 800f * width, 140f / 450f * height, 0, 0);

            this.lblSet.Margin = new Thickness(110f / 800f * width, 80f / 450f * height, 0, 0);
            this.lblGod.Margin = new Thickness(110f / 800f * width, 100f / 450f * height, 0, 0);
            this.cbQuality.Margin = new Thickness(110f / 800f * width, 120f / 450f * height, 0, 0);
            this.lblRarity.Margin = new Thickness(110f / 800f * width, 140f / 450f * height, 0, 0);
            this.lblOffers.Margin = new Thickness(31f / 800f * width, 180f / 450f * height, 0, 0);
            this.cbToken.Margin = new Thickness(70f / 800f * width, 180f / 450f * height, 0, 0);

            
            this.svOrders.Margin = new Thickness(31f / 800f * width, 202f / 450f * height, 0, 0);
            this.svOrders.Width = 469f / 800f * width;
            this.svOrders.Height = 190f / 450f * height;
            this.orderPanel.Width = 470f / 800f * width;
            for (int i = 0; i < orderPanel.Children.Count; i++)
            {
                var child = (OrderBar)orderPanel.Children[i];
                child.Width = 450f / 800f * width;
            }
            this.spinner.Margin = new Thickness(31f / 800f * width + this.svOrders.Width / 2 - 25, 202f / 450f * height + this.svOrders.Height / 2 - 25, 0, 0);

            this.imgCard.Margin = new Thickness(536f / 800f * width, 20, 0, 0);
            this.imgCard.Width = 235f / 800f * width;
            this.imgCard.Height = 300f / 450f * height;

            this.tbMeteorite.Margin = new Thickness(553f / 800f * width, 320f / 450f * height, 0, 0);
            this.tbShadow.Margin = new Thickness(611f / 800f * width, 320f / 450f * height, 0, 0);
            this.tbGold.Margin = new Thickness(670f / 800f * width, 320f / 450f * height, 0, 0);
            this.tbDiamond.Margin = new Thickness(728f / 800f * width, 320f / 450f * height, 0, 0);
            this.elMeteorite.Margin = new Thickness(553f / 800f * width, 320f / 450f * height, 0, 0);
            this.elShadow.Margin = new Thickness(611f / 800f * width, 320f / 450f * height, 0, 0);
            this.elGold.Margin = new Thickness(670f / 800f * width, 320f / 450f * height, 0, 0);
            this.elDiamond.Margin = new Thickness(728f / 800f * width, 320f / 450f * height, 0, 0);

            double btnWidth = 59d / 800d * width;
            double centerPos = 611f / 800f * width + 13;
            double NoWalletTextPosX = (611f / 800f * width + 26 + 670f / 800f * width) / 2 - this.tbNoWallet.Width / 2;
            this.btnBuy.Margin = new Thickness(centerPos - btnWidth - 20, 358f / 450f * height, 0, 0);
            this.btnOrders.Margin = new Thickness(centerPos, 358f / 450f * height, 0, 0);
            this.btnTransfer.Margin = new Thickness(centerPos + btnWidth + 20, 358f / 450f * height, 0, 0);
            this.tbNoWallet.Margin = new Thickness(NoWalletTextPosX, 355f / 450f * height, 0, 0);
            this.rectNoWallet.Margin = new Thickness(centerPos - btnWidth - 20, 355f / 450f * height, 0, 0);
            this.rectNoWallet.Width = btnWidth * 3 + 40;

            this.btnBuy.Width = btnWidth;
            this.btnOrders.Width = btnWidth;
            this.btnTransfer.Width = btnWidth;
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
        private void rectNoWallet_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SetupWalletWindow setup = new();
            setup.Owner = (MainWindow)Application.Current.MainWindow;
            setup.ShowDialog();
        }

        #endregion

        #region Supporting methods

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
}
