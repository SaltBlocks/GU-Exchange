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
        private CancellationTokenSource imgToken;
        private Order? _order;
        public string CardName { get; private set; }
        public int ProtoID { get; private set; }
        public int Quality { get; private set; }

        public OrderDisplayControl(string cardName, int protoID, int quality)
        {
            InitializeComponent();
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
                SetError(true);
            }
            return false;
        }

        public void SetOrder(Order order)
        {
            _order = order;
            tbPrice.Text = $"{Math.Round(order.PriceTotal(), 10)} {order.Currency}";
            spLoading.Visibility = Visibility.Collapsed;
        }

        public Order? GetOrder()
        {
            return _order;
        }

        public void SetLoading(bool loading)
        {
            if (loading)
            {
                spLoading.Visibility = Visibility.Visible;
                spOwned.Visibility = Visibility.Collapsed;
                spFailed.Visibility = Visibility.Collapsed;
            }
            else
            {
                spLoading.Visibility = Visibility.Collapsed;
                spOwned.Visibility = Visibility.Collapsed;
                spFailed.Visibility = Visibility.Collapsed;
            }
        }

        public void SetOwned(bool owned)
        {
            if (owned)
            {
                spLoading.Visibility = Visibility.Collapsed;
                spOwned.Visibility = Visibility.Visible;
                spFailed.Visibility = Visibility.Collapsed;
            }
            else
            {
                spLoading.Visibility = Visibility.Visible;
                spOwned.Visibility = Visibility.Collapsed;
                spFailed.Visibility = Visibility.Collapsed;
            }
        }

        public void SetError(bool error, string? errorMsg = null)
        {
            tbError.Text = errorMsg == null ? "Failed to fetch order" : errorMsg;
            if (error)
            {
                spLoading.Visibility = Visibility.Collapsed;
                spOwned.Visibility = Visibility.Collapsed;
                spFailed.Visibility = Visibility.Visible;
            }
            else
            {
                spLoading.Visibility = Visibility.Visible;
                spOwned.Visibility = Visibility.Collapsed;
                spFailed.Visibility = Visibility.Collapsed;
            }
        }

        public TextBlock getStatustextBlock()
        {
            return tbStatus;
        }
    }
}
