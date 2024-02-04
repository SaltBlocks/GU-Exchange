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
        private Order _order;
        #endregion

        #region Default Constructor
        /// <summary>
        /// Create a BuyControl that can be used to purchase the provided Order.
        /// </summary>
        /// <param name="order">The order that should be purchased.</param>
        /// <param name="image">An image to display to the user.</param>
        public BuyControl(Order order, ImageSource image)
        {
            InitializeComponent();
            _order = order;
            DataContext = new PurchaseConfirmationViewModel(order, image);
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// Handle the user clicking the buy button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BuyButton_Click(object sender, RoutedEventArgs e)
        {
            // Handle buy button click logic
            MessageBox.Show("Buy button clicked!");
        }

        /// <summary>
        /// Handle the user clicking cancel by closing the usercontrol.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
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
            this.Visibility = Visibility.Collapsed;
        }
        #endregion
    }

    /// <summary>
    /// Class used to display details about the order to the user.
    /// </summary>
    public class PurchaseConfirmationViewModel : INotifyPropertyChanged
    {
        private string cardName;
        private string cardQuality;
        private string cardPrice;
        private ImageSource cardImageSource;

        public PurchaseConfirmationViewModel(Order order, ImageSource image)
        {
            cardName = order.Name;
            cardQuality = order.Quality;
            cardPrice = $"{order.PriceTotal()} {order.Currency}";
            cardImageSource = image;
        }

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
    }
}
