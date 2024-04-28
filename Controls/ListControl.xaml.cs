using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Net.Http;
using static GU_Exchange.Helpers.IMXlib;
using GU_Exchange.Helpers;
using ImageProcessor.Processors;
using Serilog;

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for ListControl.xaml
    /// </summary>
    public partial class ListControl : UserControl
    {
        #region Class Properties.
        private readonly CardControl _parent;
        private readonly Dictionary<string, Task<Order?>> _cheapestOrders;
        private readonly HashSet<string> _allTokens;
        private readonly HashSet<string> _listableTokens;
        private readonly HashSet<string> _activeOrders;
        #endregion

        #region Default Constructor.
        /// <summary>
        /// Constructor for a control allowing the user to list a specific GU card on the IMX orderbook.
        /// </summary>
        /// <param name="parent">The <see cref="CardControl"/> this usercontrol is placed on top off.</param>
        /// <param name="image">The card image to show in the listcontrol.</param>
        public ListControl(CardControl parent, ImageSource image)
        {
            InitializeComponent();
            this._parent = parent;
            List<string> items = new List<string> { "ETH", "GODS", "IMX" };
            cbCurrency.ItemsSource = items;
            cbCurrency.SelectedIndex = 0;
            DataContext = new ListCardViewModel(parent.tbCardName.Text, (string)parent.cbQuality.SelectedItem, image);
            _cheapestOrders = new();
            _allTokens = new();
            _listableTokens = new();
            _activeOrders = new();
            FetchCheapestOrders();
            setup();
        }
        #endregion

        #region UserControl Setup.
        /// <summary>
        /// Fetch user wallet data to show in the window and update the windows content.
        /// </summary>
        public async void setup()
        {
            _activeOrders.Clear();
            _allTokens.Clear();
            _listableTokens.Clear();

            // Fetch cards in the connected wallet.
            Wallet? wallet = Wallet.GetConnectedWallet();
            if (wallet == null)
            {
                // No wallet connected, close this window.
                _ = _parent.ReloadOrderbookAsync();
                this.Visibility = Visibility.Collapsed;
                return;
            }
            string cardData = HttpUtility.UrlEncode("{\"proto\":[\"" + _parent.CardID + "\"],\"quality\":[\"" + (string)_parent.cbQuality.SelectedItem + "\"]}");
            string urlListed = $"https://api.x.immutable.com/v3/orders?sell_metadata={cardData}&sell_token_address=0xacb3c6a43d15b907e8433077b6d38ae40936fe2c&status=active&user={wallet.Address}";
            string urlInventory = $"https://api.x.immutable.com/v1/assets?user={wallet.Address}&metadata={cardData}&sell_orders=true";
            string cardString;
            string listingString;
            try 
            {
                Task<string> fetchInventory = ResourceManager.Client.GetStringAsync(urlInventory);
                listingString = await ResourceManager.Client.GetStringAsync(urlListed);
                cardString = await fetchInventory;
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is HttpRequestException)
            {
                if (ex is HttpRequestException)
                    Log.Information($"Failed to fetching wallet inventory for {_parent.CardID} of quality {(string)_parent.cbQuality.SelectedItem}. {ex.Message}: {ex.StackTrace}");
                return;
            }

            // Deserialize the JSON into a JObject
            JObject jsonObj = JObject.Parse(listingString);

            JArray? resultArray = jsonObj["result"] as JArray;
            HashSet<string?> listedTokens = new();
            if (resultArray != null)
            {
                foreach (JToken token in resultArray)
                {
                    string? orderId = token["order_id"]?.ToObject<string?>();
                    string? tokenId = (string?)token.SelectToken("sell.data.token_id");
                    if (orderId != null)
                    {
                        _activeOrders.Add(orderId);
                    }
                    if (tokenId != null)
                    {
                        listedTokens.Add(tokenId);
                    }
                }
            }

            JObject? jsonData = JsonConvert.DeserializeObject<JObject?>(cardString);
            if (jsonData?["result"] is not JToken result)
                return;
            foreach (JToken card in result)
            {
                string? tokenID = card["token_id"]?.Value<string>();
                if (tokenID == null) continue;
                Console.WriteLine(imx_get_token_trade_fee("0xacb3c6a43d15b907e8433077b6d38ae40936fe2c", tokenID));
                _allTokens.Add(tokenID);
                if (!listedTokens.Contains(tokenID)) _listableTokens.Add(tokenID);
            }
            int num_owned = _allTokens.Count();
            int num_listed = listedTokens.Count();

            if (num_listed > 0) btnCancel.Visibility = Visibility.Visible;
            else   btnCancel.Visibility = Visibility.Collapsed;
            tbNumber.Text = $"/ {num_owned} ({num_listed} listed)";
            cbNumber.Items.Clear();
            for (int i = 0; i < num_owned - num_listed; i++)
            {
                cbNumber.Items.Add((i + 1).ToString());
            }
            if (num_owned - num_listed == 0) cbNumber.Items.Add("None available");
            cbNumber.SelectedIndex = 0;
            if (num_owned - num_listed > 0)
            {
                btnList.IsEnabled = true;
            }
            string? tokenName = cbCurrency.SelectedItem.ToString();
            if (tokenName == null) return;
            Order? cheapestOrder = await _cheapestOrders[tokenName];
            if (cheapestOrder == null) return;
            decimal listPrice = cheapestOrder.PriceTotal() - new decimal(0.00000001);
            tbListPrice.Text = listPrice.ToString("0.##########");
            try
            {
                decimal receiveAmount = listPrice / getFeeMultiplier() * new decimal(0.99);
                tbReceiveAmount.Text = receiveAmount.ToString("0.##########");
            }
            catch (FormatException)
            {
                tbReceiveAmount.Text = "";
            }
        }

        /// <summary>
        /// Fetch orders for all currencies for the card that will be listed.
        /// </summary>
        private async void FetchCheapestOrders()
        {
            Dictionary<string, Token> currency = await Wallet.FetchTokens();
            foreach (Token token in currency.Values)
            {
                _cheapestOrders[token.Name] = FetchCheapestOrder(token.Address);
            }
        }

        /// <summary>
        /// Fetch cheapest order for a specific currency for the card that will be listed.
        /// </summary>
        /// <param name="tokenAddress"></param>
        /// <returns></returns>
        private async Task<Order?> FetchCheapestOrder(string tokenAddress)
        {
            string token_str = tokenAddress;
            if (token_str.Equals("ETH"))
                token_str = "&buy_token_type=ETH";
            string cardData = HttpUtility.UrlEncode("{\"proto\":[\"" + _parent.CardID + "\"],\"quality\":[\"" + ((ListCardViewModel)DataContext).CardQuality + "\"]}");
            string urlOrderBook = $"https://api.x.immutable.com/v3/orders?buy_token_address={token_str}&direction=asc&include_fees=true&order_by=buy_quantity&page_size=1&sell_metadata={cardData}&sell_token_address=0xacb3c6a43d15b907e8433077b6d38ae40936fe2c&status=active";
            try
            {
                string strOrderBook = await ResourceManager.Client.GetStringAsync(urlOrderBook);
                List<Order> orders = new();
                // Extract orders from the data returned by the server.
                JObject? jsonOrders = (JObject?)JsonConvert.DeserializeObject(strOrderBook);
                if (jsonOrders == null)
                    return null;
                JToken? order = (jsonOrders["result"]?.Any() ?? false) ? jsonOrders["result"]?[0]?.Value<JToken?>() : null;
                if (order == null)
                    return null;
                return new Order(order, tokenAddress);
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is HttpRequestException || ex is NullReferenceException)
            {
                if (ex is HttpRequestException)
                    Log.Information($"Failed to fetch cheapest order for {_parent.CardID} of quality {(string)_parent.cbQuality.SelectedItem}. {ex.Message}: {ex.StackTrace}");
                return null;
            }
        }
        #endregion

        #region Event Handlers.
        /// <summary>
        /// Update the currency text and suggested price when the user changes the selected sale currency.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void cbCurrency_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            tbCurrency.Text = cbCurrency.SelectedItem.ToString();
            await AutoAdjustPrice();
        }

        /// <summary>
        /// Close the usercontrol if the user clicks outside of it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Get the position of the mouse click relative to the buyGrid
            Point clickPoint = e.GetPosition(buyGrid);

            // Check if the click occurred on the buyGrid
            if (clickPoint.X >= 0 && clickPoint.X < buyGrid.ActualWidth &&
                clickPoint.Y >= 0 && clickPoint.Y < buyGrid.ActualHeight)
            {
                return;
            }
            // Click occurred outside buyGrid, you can call your function here
            _ = _parent.ReloadOrderbookAsync();
            this.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Update the receive amount when the user modifies the list price.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TbListPrice_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!tbListPrice.IsKeyboardFocusWithin) // Prevent a loop by not updating anything when this method is called due to another event modifying the value.
                return;
            try
            {
                decimal listPrice = Decimal.Parse(tbListPrice.Text);
                decimal receiveAmount = listPrice / getFeeMultiplier() * new decimal(0.99);
                tbReceiveAmount.Text = receiveAmount.ToString("0.##########");
            } catch (FormatException)
            {
                tbReceiveAmount.Text = "";
            }
        }

        /// <summary>
        /// Update the list price when the user modifies the receive amount.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TbReceiveAmount_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!tbReceiveAmount.IsKeyboardFocusWithin) // Prevent a loop by not updating anything when this method is called due to another event modifying the value.
                return;
            try
            {
                decimal receiveAmount = Decimal.Parse(tbReceiveAmount.Text);
                decimal listPrice = receiveAmount * getFeeMultiplier() / new decimal(0.99);
                tbListPrice.Text = listPrice.ToString("0.##########");
            }
            catch (FormatException)
            {
                tbListPrice.Text = "";
            }
        }
        
        /// <summary>
        /// Adjust the price to the suggested amount.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BtnLowestPrice_Click(object sender, RoutedEventArgs e)
        {
            await AutoAdjustPrice();
        }

        /// <summary>
        /// Close the ListControl.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _ = _parent.ReloadOrderbookAsync();
            this.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// List the card using the data provided by the user.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BtnList_Click(object sender, RoutedEventArgs e)
        {
            userChoicePanel.Visibility = Visibility.Collapsed;
            loadingPanel.Visibility = Visibility.Visible;

            decimal? bestPrice = await GetCheapestPrice();
            if (bestPrice != null)
            {
                try
                {
                    decimal listPrice = decimal.Parse(tbListPrice.Text);
                    decimal percentage = listPrice / (decimal)bestPrice;
                    if (percentage < new decimal(0.75))
                    {
                        MessageWindow window = new MessageWindow($"The offer price for this listing ({listPrice.ToString("0.##########")} {(string)cbCurrency.SelectedItem}) is {100 - percentage * 100:0.00}% cheaper than then next cheapest offer. Are you sure you want to post this listing?", "Confirm listing", MessageType.CONFIRM);
                        window.Owner = Application.Current.MainWindow;
                        window.ShowDialog();
                        if (!window.Result)
                        {
                            spinner.Visibility = Visibility.Collapsed;
                            error.Visibility = Visibility.Visible;
                            btnClose.Visibility = Visibility.Visible;
                            tbStatus.Text = "Listing(s) cancelled.";
                            return;
                        }
                    }
                }
                catch (FormatException) { }
            }
            // Get the connected wallet.
            Wallet? wallet = Wallet.GetConnectedWallet();
            if (wallet == null)
            {
                // No wallet connected, cannot continue.
                spinner.Visibility = Visibility.Collapsed;
                error.Visibility = Visibility.Visible;
                btnClose.Visibility = Visibility.Visible;
                tbStatus.Text = "No wallet connected";
                return;
            }

            // Submit the order and allow the wallet to update the status message.
            List<(NFT card, string tokenID, double price, TextBlock? tbListing)> listings = new();
            IEnumerable<string> tokensToList = _listableTokens.Take(int.Parse((string)cbNumber.SelectedItem));
            foreach (string tokenIDStr in tokensToList)
            {
                try
                {
                    NFT card = new NFT()
                    {
                        token_address = "0xacb3c6a43d15b907e8433077b6d38ae40936fe2c",
                        token_id = ulong.Parse(tokenIDStr)
                    };
                    Dictionary<string, Token> tokens = await Wallet.FetchTokens();
                    Token token = tokens[(string)cbCurrency.SelectedItem];
                    double basePrice = double.Parse(tbReceiveAmount.Text) / 0.99;
                    listings.Add((card, token.Address, basePrice, null));
                }
                catch (FormatException) 
                {
                    // Purchase failed.
                    spinner.Visibility = Visibility.Collapsed;
                    error.Visibility = Visibility.Visible;
                    btnClose.Visibility = Visibility.Visible;
                    tbStatus.Text = "Sell price improperly formatted.";
                    return;
                }
            }
            Dictionary<NFT, bool> result = await wallet.RequestCreateOrders(Application.Current.MainWindow, listings.ToArray(), this.tbStatus);
            bool resTotal = result.All(x => x.Value);
            spinner.Visibility = Visibility.Collapsed;
            if (!resTotal)
            {
                // Purchase failed.
                error.Visibility = Visibility.Visible;
                btnClose.Visibility = Visibility.Visible;
                return;
            }

            // Buying the order succeeded, now update the inventory and local wallet to reflect the successfull purchase.
            success.Visibility = Visibility.Visible;

            // Refresh the wallet in the parent CardControl.
            _ = _parent.SetupInventoryAsync();
            btnClose.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Cancel all existing orders for this card posted by the user.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            userChoicePanel.Visibility = Visibility.Collapsed;
            loadingPanel.Visibility = Visibility.Visible;

            // Get the connected wallet.
            Wallet? wallet = Wallet.GetConnectedWallet();
            if (wallet == null)
            {
                // No wallet connected, cannot continue.
                spinner.Visibility = Visibility.Collapsed;
                error.Visibility = Visibility.Visible;
                btnClose.Visibility = Visibility.Visible;
                tbStatus.Text = "No wallet connected";
                return;
            }

            // Cancel the order(s) and allow the wallet to update the status message.
            List<(string orderID, TextBlock? tbListing)> listings = _activeOrders.Select(orderID => (orderID, (TextBlock?)null)).ToList();
            Dictionary<string, bool> result = await wallet.RequestCancelOrders(Application.Current.MainWindow, listings.ToArray(), tbStatus);
            bool resTotal = result.All(x => x.Value);
            if (!resTotal)
            {
                // Cancellation failed.
                spinner.Visibility = Visibility.Collapsed;
                error.Visibility = Visibility.Visible;
                btnClose.Visibility = Visibility.Visible;
                return;
            }

            // Cancelling the order(s) succeeded.
            _activeOrders.Clear();
            cbNumber.Items.Clear();
            _listableTokens.Clear();
            _listableTokens.UnionWith(_allTokens);
            for (int i = 0; i < _listableTokens.Count(); i++)
            {
                cbNumber.Items.Add((i + 1).ToString());
            }
            if (_listableTokens.Count() == 0) cbNumber.Items.Add("None available");
            cbNumber.SelectedIndex = 0;
            if (_listableTokens.Count() > 0)
            {
                btnList.IsEnabled = true;
            }
            tbNumber.Text = $"/ {_listableTokens.Count()} (0 listed)";
            loadingPanel.Visibility = Visibility.Collapsed;
            userChoicePanel.Visibility = Visibility.Visible;
        }
        #endregion

        #region Supporting Methods.
        /// <summary>
        /// Adjust the price in the window to a suggested price based on existing listings.
        /// </summary>
        /// <returns></returns>
        private async Task AutoAdjustPrice()
        {
            decimal? cheapestPrice = await GetCheapestPrice();
            if (cheapestPrice == null) return;
            decimal listPrice = (decimal)cheapestPrice - new decimal(0.00000001);
            tbListPrice.Text = listPrice.ToString("0.##########");
            try
            {
                decimal receiveAmount = listPrice / getFeeMultiplier() * new decimal(0.99);
                tbReceiveAmount.Text = receiveAmount.ToString("0.##########");
            }
            catch (FormatException)
            {
                tbReceiveAmount.Text = "";
            }
        }

        /// <summary>
        /// Get the sale price for the cheapest listed order of the card with the selected currency.
        /// </summary>
        /// <returns></returns>
        private async Task<decimal?> GetCheapestPrice()
        {
            string? tokenName = cbCurrency.SelectedItem.ToString();
            if (tokenName == null) return null;
            if (_cheapestOrders == null) return null;
            if (!_cheapestOrders.ContainsKey(tokenName)) return null;
            Order? cheapestOrder = await _cheapestOrders[tokenName];
            if (cheapestOrder == null) return null;
            return cheapestOrder.PriceTotal();
        }

        private decimal getFeeMultiplier()
        {
            switch ((string)_parent.cbQuality.SelectedItem)
            {
                case "Meteorite":
                    return 1.08M;
                case "Shadow":
                    return 1.07M;
                case "Gold":
                    return 1.06M;
                case "Diamond":
                    return 1.035M;
                default:
                    return 1.08M;
            }
        }
        #endregion
    }

    /// <summary>
    /// Class used to display details about the order to the user.
    /// </summary>
    public class ListCardViewModel : INotifyPropertyChanged
    {
        #region Class Properties
        private string cardName;
        private string cardQuality;
        private ImageSource cardImageSource;
        #endregion

        #region Default Constructor
        public ListCardViewModel(string cardName, string cardQuality, ImageSource image)
        {
            this.cardName = cardName;
            this.cardQuality = cardQuality;
            cardImageSource = image;
        }
        #endregion

        #region Getters and Setters
        public string CardName
        {
            get { return cardName; }
            set
            {
                cardName = value;
                OnPropertyChanged(nameof(CardName));
            }
        }

        public string CardQuality
        {
            get { return cardQuality; }
            set
            {
                cardQuality = value;
                OnPropertyChanged(nameof(CardQuality));
            }
        }

        public ImageSource CardImageSource
        {
            get { return cardImageSource; }
            set
            {
                cardImageSource = value;
                OnPropertyChanged(nameof(CardImageSource));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
