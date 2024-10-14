using GU_Exchange.Helpers;
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
using static GU_Exchange.Helpers.IMXlib;

namespace GU_Exchange.Controls
{
    /// <summary>
    /// Interaction logic for OfferControl.xaml
    /// </summary>
    public partial class OfferControl : UserControl
    {
        #region Class Properties
        private readonly CardControl _parent;
        private readonly Order _order;
        #endregion

        public OfferControl(CardControl parent, Order order, ImageSource image)
        {
            InitializeComponent();
            _parent = parent;
            _order = order;
            tbCurrency.Text = _order.Currency;
            DataContext = new PurchaseConfirmationViewModel(order, image);
        }

        #region Event Handlers.
        /// <summary>
        /// Handle the user clicking cancel by closing the usercontrol.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _ = _parent.ReloadOrderbookAsync();
            this.Visibility = Visibility.Collapsed;
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
        #endregion

        private async void btnOffer_Click(object sender, RoutedEventArgs e)
        {
            userChoicePanel.Visibility = Visibility.Collapsed;
            loadingPanel.Visibility = Visibility.Visible;
            ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = false;
            _parent.CanClose = false;

            decimal buyPrice = _order.PriceTotal();
            try
            {
                decimal offerPrice = decimal.Parse(tbOfferAmount.Text);
                decimal percentage = offerPrice / (decimal)buyPrice;
                if (percentage >= 1)
                {
                    MessageWindow window = new MessageWindow($"The provided offer price ({offerPrice.ToString("0.##########")} {_order.Currency}) exceeds the current listing prive of the item. ({buyPrice.ToString("0.##########")} {tbCurrency.Text}).\nPlease adjust the offer price to between 10% and 100% of the list price.", "Offer price too high", MessageType.INFORM);
                    window.Owner = Application.Current.MainWindow;
                    window.ShowDialog();
                    spinner.Visibility = Visibility.Collapsed;
                    error.Visibility = Visibility.Visible;
                    btnClose.Visibility = Visibility.Visible;
                    tbStatus.Text = "Offer(s) cancelled.";
                    ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = true;
                    _parent.CanClose = true;
                    return;
                }
            }
            catch (FormatException) 
            {
                // Purchase failed.
                spinner.Visibility = Visibility.Collapsed;
                error.Visibility = Visibility.Visible;
                btnClose.Visibility = Visibility.Visible;
                tbStatus.Text = "Offer price improperly formatted.";
                ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = true;
                _parent.CanClose = true;
                return;
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
                ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = true;
                _parent.CanClose = true;
                return;
            }

            // Submit the order and allow the wallet to update the status message.
            List<(NFT card, string tokenID, double price, TextBlock? tbListing)> listings = new();
            try
            {
                NFT card = new NFT()
                {
                    token_address = "0xacb3c6a43d15b907e8433077b6d38ae40936fe2c",
                    token_id = ulong.Parse(_order.TokenID)
                };
                Dictionary<string, Token> tokens = await Wallet.FetchTokens();
                Token token = tokens[_order.Currency];
                listings.Add((card, token.Address, double.Parse(tbOfferAmount.Text), null));
            }
            catch (FormatException)
            {
                // Purchase failed.
                spinner.Visibility = Visibility.Collapsed;
                error.Visibility = Visibility.Visible;
                btnClose.Visibility = Visibility.Visible;
                tbStatus.Text = "Offer price improperly formatted.";
                ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = true;
                _parent.CanClose = true;
                return;
            }
            Dictionary<NFT, bool> result = await wallet.RequestCreateOffers(Application.Current.MainWindow, listings.ToArray(), this.tbStatus);
            bool resTotal = result.All(x => x.Value);
            spinner.Visibility = Visibility.Collapsed;
            if (!resTotal)
            {
                // Purchase failed.
                error.Visibility = Visibility.Visible;
                btnClose.Visibility = Visibility.Visible;
                ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = true;
                _parent.CanClose = true;
                return;
            }

            // Buying the order succeeded, now update the inventory and local wallet to reflect the successfull purchase.
            success.Visibility = Visibility.Visible;

            // Refresh the wallet in the parent CardControl.
            _ = _parent.SetupInventoryAsync();
            btnClose.Visibility = Visibility.Visible;
            ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = true;
            _parent.CanClose = true;
        }
    }
}
