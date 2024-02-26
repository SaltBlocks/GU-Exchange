using GU_Exchange.Helpers;
using ImageProcessor.Processors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for BuyControl.xaml
    /// </summary>
    public partial class BuyControl : UserControl
    {
        #region Class Properties
        private CardControl _parent;
        private Order _order;
        #endregion

        #region Default Constructor
        /// <summary>
        /// Create a BuyControl that can be used to purchase the provided Order.
        /// </summary>
        /// <param name="order">The order that should be purchased.</param>
        /// <param name="image">An image to display to the user.</param>
        public BuyControl(CardControl parent, Order order, ImageSource image)
        {
            InitializeComponent();
            _parent = parent;
            _order = order;
            DataContext = new PurchaseConfirmationViewModel(order, image);
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// Handle the user clicking the buy button.
        /// Purchase the order and update relevant UI elements.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BuyButton_Click(object sender, RoutedEventArgs e)
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

            // Submit the order and allow the wallet to update the status message.
            bool result = await wallet.RequestBuyOrder(Application.Current.MainWindow, _order, tbStatus);
            spinner.Visibility = Visibility.Collapsed;
            if (!result)
            {
                // Purchase failed.
                error.Visibility = Visibility.Visible;
                btnClose.Visibility = Visibility.Visible;
                return;
            }
            
            // Buying the order succeeded, now update the inventory and local wallet to reflect the successfull purchase.
            success.Visibility = Visibility.Visible;

            // Update the card inventory by adding the purchased card, if the wallet used for the purchase is linked to the connected GU account.
            if (await GameDataManager.IsWalletLinked(Settings.GetApolloID(), wallet.Address))
            {
                Inventory? inv = (Inventory?)App.Current.Properties["Inventory"];
                if (inv != null)
                {
                    int quality = 0;
                    switch (_order.Quality)
                    {
                        case "Meteorite":
                            quality = 4;
                            break;
                        case "Shadow":
                            quality = 3;
                            break;
                        case "Gold":
                            quality = 2;
                            break;
                        case "Diamond":
                            quality = 1;
                            break;
                    }
                    if (quality != 0)
                        inv.SetNumberOwned(_parent.CardID, quality, inv.GetNumberOwned(_parent.CardID, quality) + 1);
                }
            }
            
            
            // Deduct the spent amount of currency from the local wallet content.
            wallet.DeductTokenAmount(_order.Currency, _order.PriceTotal());

            // Update UI.
            await ((MainWindow)Application.Current.MainWindow).RefreshTilesAsync();
            await ((MainWindow)Application.Current.MainWindow).RefreshWalletInfoAsync();
            
            // Refresh the wallet in the parent CardControl.
            _ = _parent.SetupInventoryAsync();
            btnClose.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Handle the user clicking cancel by closing the usercontrol.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
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
    }

    /// <summary>
    /// Class used to display details about the order to the user.
    /// </summary>
    public class PurchaseConfirmationViewModel : INotifyPropertyChanged
    {
        #region Class Properties
        private string cardName;
        private string cardQuality;
        private string cardPrice;
        private ImageSource cardImageSource;
        #endregion

        #region Default Constructor
        public PurchaseConfirmationViewModel(Order order, ImageSource image)
        {
            cardName = order.Name;
            cardQuality = order.Quality;
            cardPrice = $"{order.PriceTotal()} {order.Currency}";
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

        public string CardPrice
        {
            get { return cardPrice; }
            set
            {
                cardPrice = value;
                OnPropertyChanged(nameof(CardPrice));
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
