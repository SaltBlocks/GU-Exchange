using GU_Exchange.Helpers;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace GU_Exchange.Controls
{
    /// <summary>
    /// Interaction logic for OrderDisplayControl.xaml
    /// </summary>
    public partial class OrderDisplayControl : UserControl
    {
        public enum DisplayStatus
        {
            Loading, Success, Fail
        }

        #region Class properties.
        private CancellationTokenSource imgToken;
        private Order? _order;
        public string CardName { get; private set; }
        public int ProtoID { get; private set; }
        public int Quality { get; private set; }
        public DisplayStatus Status { get; private set; }
        #endregion
        #region Default constructor.
        /// <summary>
        /// Constructor for a display showing a card image with a status.
        /// </summary>
        /// <param name="cardName"></param>
        /// <param name="protoID"></param>
        /// <param name="quality"></param>
        public OrderDisplayControl(string cardName, int protoID, int quality)
        {
            InitializeComponent();
            Status = DisplayStatus.Loading;
            CardName = cardName;
            ProtoID = protoID;
            Quality = quality;
            tbName.Text = CardName;
            imgToken = new();
            SetupImage();
        }
        #endregion
        #region Supporting methods.
        /// <summary>
        /// Setup the image to display.
        /// </summary>
        public async void SetupImage()
        {
            imgCard.Source = await ResourceManager.GetCardImageAsync(ProtoID, Quality, false, imgToken.Token);
        }

        /// <summary>
        /// Set the order associated with the display using a task.
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public async Task<bool> SetOrder(Task<Order> task)
        {
            try
            {
                SetOrder(await task);
                return true;
            }
            catch (Exception)
            {
                SetStatus(DisplayStatus.Fail);
                SetStatusMessage("Fetching order failed");
            }
            return false;
        }

        /// <summary>
        /// Set the order associated with the display.
        /// </summary>
        /// <param name="order"></param>
        public void SetOrder(Order order)
        {
            _order = order;
            tbSubText.Text = $"{Math.Round(order.PriceTotal(), 10)} {order.Currency}";
            ShowStatus(false);
        }

        /// <summary>
        /// Get the order associated with this display.
        /// </summary>
        /// <returns></returns>
        public Order? GetOrder()
        {
            return _order;
        }

        /// <summary>
        /// Set the status of the display.
        /// </summary>
        /// <param name="status"></param>
        public void SetStatus(DisplayStatus status)
        {
            Status = status;
            switch (status) {
                case DisplayStatus.Success:
                    spinner.Visibility = Visibility.Collapsed;
                    success.Visibility = Visibility.Visible;
                    error.Visibility = Visibility.Collapsed;
                    break;
                case DisplayStatus.Fail:
                    spinner.Visibility = Visibility.Collapsed;
                    success.Visibility = Visibility.Collapsed;
                    error.Visibility = Visibility.Visible;
                    break;
                default:
                case DisplayStatus.Loading:
                    spinner.Visibility = Visibility.Visible;
                    success.Visibility = Visibility.Collapsed;
                    error.Visibility = Visibility.Collapsed;
                    break;

            }
        }

        /// <summary>
        /// Set the status message of this display.
        /// </summary>
        /// <param name="message"></param>
        public void SetStatusMessage(string message)
        {
            tbStatus.Text = message;
        }

        /// <summary>
        /// Get the subtext below the card image.
        /// </summary>
        /// <returns></returns>
        public string GetSubText()
        {
            return tbSubText.Text;
        }

        /// <summary>
        /// Set the subtext below the card image.
        /// </summary>
        /// <param name="mesage"></param>
        public void SetSubText(string mesage)
        {
            tbSubText.Text = mesage;
        }

        /// <summary>
        /// Get the textblock for this display.
        /// </summary>
        /// <returns></returns>
        public TextBlock getStatustextBlock()
        {
            return tbStatus;
        }

        /// <summary>
        /// Set whether or not the status bar on this display should be shown.
        /// </summary>
        /// <param name="showStatus"></param>
        public void ShowStatus(bool showStatus)
        {
            if (showStatus)
                spDisplay.Visibility = Visibility.Visible;
            else
                spDisplay.Visibility = Visibility.Collapsed;
        }
        #endregion
    }
}
