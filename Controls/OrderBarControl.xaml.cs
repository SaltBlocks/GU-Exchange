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
using GU_Exchange.Helpers;

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for OrderBar.xaml
    /// </summary>
    public partial class OrderBarControl : UserControl
    {
        #region Class Properties
        public Order Order;
        #endregion

        #region Default Constructor.
        /// <summary>
        /// Constructor for an orderbar displaying the price and wallet associated with a specific listing.
        /// </summary>
        /// <param name="order">The <see cref="Helpers.Order"/> to display</param>
        public OrderBarControl(Order order)
        {
            InitializeComponent();
            this.Order = order;
            _ = SetupAsync(order);
        }
        #endregion

        #region Setup methods.
        /// <summary>
        /// Sets the text of the orderbar to match the provided order.
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        private async Task SetupAsync(Order order)
        {
            decimal? token_price = (await Wallet.FetchTokens())[order.Currency].Value;
            txtPrice.Text = $"{Math.Round(order.PriceTotal(), 10)} {order.Currency}";
            if (token_price != null)
                txtDollarPrice.Text = $"(${Math.Round(order.PriceTotal() * new decimal((double)token_price), 2)})";
            this.txtSeller.Text = GetAddressString();
        }
        #endregion

        #region Getters and Setters.
        /// <summary>
        /// Returns a formatted string showing the first and last characters of the listers wallet address.
        /// </summary>
        /// <returns>The formatted string</returns>
        private string GetAddressString()
        {
            return $"Listed by {Order.Seller.Substring(0, 6)}....{Order.Seller.Substring(Order.Seller.Length - 4)}";
        }

        /// <summary>
        /// Used to set the background color in case the Order displayed was created by the user.
        /// </summary>
        /// <param name="ColorRGB">The color to set the background to</param>
        public void setBackgroundColor(String ColorRGB)
        {
            Color color = (Color)ColorConverter.ConvertFromString(ColorRGB);
            SolidColorBrush newBrush = new SolidColorBrush(color);
            this.Background = newBrush;
        }
        #endregion

        #region Event Handlers.
        /// <summary>
        /// Highlight the order when the mouse moves over it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_MouseEnter(object sender, MouseEventArgs e)
        {
            SolidColorBrush brushFill = new SolidColorBrush();
            brushFill.Color = Color.FromArgb(127, 165, 165, 164);
            this.rectHighlight.Fill = brushFill;
            this.rectHighlight.Stroke = Brushes.Black;
        }

        /// <summary>
        /// Removes the highlighting on the order when the mouse leaves the orderbar.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_MouseLeave(object sender, MouseEventArgs e)
        {
            SolidColorBrush brushFill = new SolidColorBrush();
            brushFill.Color = Color.FromArgb(0, 165, 165, 164);
            SolidColorBrush brushStroke = new SolidColorBrush();
            brushStroke.Color = Color.FromArgb(0, 0, 0, 0);
            this.rectHighlight.Fill = brushFill;
            this.rectHighlight.Stroke = brushStroke;
        }

        /// <summary>
        /// TODO let the user buy (or modify) an order by clicking it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ((MainWindow)Application.Current.MainWindow).OpenOrder(Order);
        }

        /// <summary>
        /// Resize the components of the OrderBar when it is resized.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            updateSize();
        }
        #endregion

        #region Supporting Methods.
        /// <summary>
        /// Update the size or the highligting bar.
        /// </summary>
        public void updateSize()
        {
            rectHighlight.Width = this.ActualWidth;
        }
        #endregion
    }
}
