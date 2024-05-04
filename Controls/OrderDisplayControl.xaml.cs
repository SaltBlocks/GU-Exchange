using GU_Exchange.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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

        private CancellationTokenSource imgToken;
        private Order? _order;
        public string CardName { get; private set; }
        public int ProtoID { get; private set; }
        public int Quality { get; private set; }
        public DisplayStatus Status { get; private set; }

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

        public async void SetupImage()
        {
            imgCard.Source = await ResourceManager.GetCardImageAsync(ProtoID, Quality, false, imgToken.Token);
        }

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

        public void SetOrder(Order order)
        {
            _order = order;
            tbSubText.Text = $"{Math.Round(order.PriceTotal(), 10)} {order.Currency}";
            ShowStatus(false);
        }

        public Order? GetOrder()
        {
            return _order;
        }

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

        public void SetStatusMessage(string message)
        {
            tbStatus.Text = message;
        }

        public string GetSubText()
        {
            return tbSubText.Text;
        }

        public void SetSubText(string mesage)
        {
            tbSubText.Text = mesage;
        }

        public TextBlock getStatustextBlock()
        {
            return tbStatus;
        }

        public void ShowStatus(bool showStatus)
        {
            if (showStatus)
                spDisplay.Visibility = Visibility.Visible;
            else
                spDisplay.Visibility = Visibility.Collapsed;
        }
    }
}
