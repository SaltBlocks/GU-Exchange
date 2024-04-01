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

        public void SetOrder(Order order)
        {
            tbPrice.Text = $"{Math.Round(order.PriceTotal(), 10)} {order.Currency}";
            spLoading.Visibility = Visibility.Collapsed;
        }
    }
}
