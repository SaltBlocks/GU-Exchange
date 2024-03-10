using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for UpdateListingControl.xaml
    /// </summary>
    public partial class UpdateListingControl : UserControl
    {
        #region Class Properties.
        private readonly CardControl _parent;
        private readonly Task<Order?> _cheapestOrder;
        private readonly Order _order;
        #endregion

        #region Default Constructor.
        /// <summary>
        /// Constructor for a control allowing the user to modify an existing listing on the IMX orderbook.
        /// </summary>
        /// <param name="parent">The <see cref="CardControl"/> this usercontrol is placed on top off.</param>
        /// <param name="order">The <see cref="Order"/> that is to be modified.</param>
        /// <param name="image">The card image to show in the control.</param>
        public UpdateListingControl(CardControl parent, Order order, ImageSource image)
        {
            InitializeComponent();
            this._parent = parent;
            tbCurrencyList.Text = order.Currency;
            tbCurrencyReceive.Text = order.Currency;
            DataContext = new ListCardViewModel(parent.tbCardName.Text, (string)parent.cbQuality.SelectedItem, image);
            _order = order;
            _cheapestOrder = FetchCheapestOrder(_order.Currency);
            Setup();
        }
        #endregion

        #region UserControl Setup.
        /// <summary>
        /// Update the price in the window to reflect the current order.
        /// </summary>
        public void Setup()
        {
            // Fetch cards in the connected wallet.
            Wallet? wallet = Wallet.GetConnectedWallet();
            if (wallet == null || _order.Seller != wallet.Address)
            {
                // No wallet connected, close this window.
                _ = _parent.ReloadOrderbookAsync();
                this.Visibility = Visibility.Collapsed;
                return;
            }
            decimal listPrice = _order.PriceTotal();
            tbListPrice.Text = listPrice.ToString("0.##########");
            try
            {
                decimal receiveAmount = listPrice / new decimal(1.08) * new decimal(0.99);
                tbReceiveAmount.Text = receiveAmount.ToString("0.##########");
            }
            catch (FormatException)
            {
                tbReceiveAmount.Text = "";
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
                return null;
            }
        }
        #endregion

        #region Event Handlers.
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
                decimal receiveAmount = listPrice / new decimal(1.08) * new decimal(0.99);
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
                decimal listPrice = receiveAmount * new decimal(1.08) / new decimal(0.99);
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
                        MessageWindow window = new MessageWindow($"The offer price for this listing ({listPrice.ToString("0.##########")} {tbCurrencyList.Text}) is {100 - percentage * 100:0.00}% cheaper than then next cheapest offer. Are you sure you want to post this listing?", "Confirm listing", MessageType.CONFIRM);
                        window.Owner = Application.Current.MainWindow;
                        window.ShowDialog();
                        if (!window.Result)
                        {
                            spinner.Visibility = Visibility.Collapsed;
                            error.Visibility = Visibility.Visible;
                            btnClose.Visibility = Visibility.Visible;
                            tbStatus.Text = "Price update cancelled.";
                            return;
                        }
                    }
                }
                catch (FormatException) { }
            }
            // Get the connected wallet.
            Wallet? wallet = Wallet.GetConnectedWallet();
            if (wallet == null || wallet.Address != _order.Seller)
            {
                // No wallet connected, cannot continue.
                spinner.Visibility = Visibility.Collapsed;
                error.Visibility = Visibility.Visible;
                btnClose.Visibility = Visibility.Visible;
                tbStatus.Text = "Seller wallet not connected";
                return;
            }

            // Submit the order and allow the wallet to update the status message.
            NFT card = new NFT()
            {
                token_address = "0xacb3c6a43d15b907e8433077b6d38ae40936fe2c",
                token_id = ulong.Parse(_order.TokenID)
            };
            
            (NFT, string, double, TextBlock?) listing;
            try
            {
                Dictionary<string, Token> tokens = await Wallet.FetchTokens();
                Token token = tokens[tbCurrencyList.Text];
                double basePrice = double.Parse(tbReceiveAmount.Text) / 0.99;
                listing = (card, token.Address, basePrice, null);
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
            (NFT, string, double, TextBlock?)[] listingArray = { listing };
            Dictionary<NFT, bool> result = await wallet.RequestCreateOrders(Application.Current.MainWindow, listingArray, this.tbStatus);
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
        /// Cancel the existing order for this card posted by the user.
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

            // Cancel the order and allow the wallet to update the status message.
            (string, TextBlock?)[] orderData = { (_order.OrderID.ToString(), tbStatus) };
            Dictionary<string, bool> result = await wallet.RequestCancelOrders(Application.Current.MainWindow, orderData, tbStatus);
            bool resTotal = result.All(x => x.Value);
            if (!resTotal)
            {
                // Cancellation failed.
                spinner.Visibility = Visibility.Collapsed;
                error.Visibility = Visibility.Visible;
                btnClose.Visibility = Visibility.Visible;
                return;
            }

            // Refresh the  parent CardControl.
            spinner.Visibility = Visibility.Collapsed;
            success.Visibility = Visibility.Visible;
            btnClose.Visibility = Visibility.Visible;
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
                decimal receiveAmount = listPrice / new decimal(1.08) * new decimal(0.99);
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
            Order? cheapestOrder = await _cheapestOrder;
            if (cheapestOrder == null) return null;
            return cheapestOrder.PriceTotal();
        }
        #endregion
    }
}
