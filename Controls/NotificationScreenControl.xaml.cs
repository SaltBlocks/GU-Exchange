using GU_Exchange.Helpers;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
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

namespace GU_Exchange.Controls
{
    /// <summary>
    /// Interaction logic for NotificationScreenControl.xaml
    /// </summary>
    public partial class NotificationScreenControl : UserControl
    {
        public NotificationScreenControl()
        {
            InitializeComponent();
            SetupScreen();
        }

        private async void SetupScreen()
        {
            Wallet? wlt = Wallet.GetConnectedWallet();
            string Address = "0x1e7ea803fdd86ec32af49b4babbfae98f846c7b7";
            if (wlt == null)
                return;
            string urlListed = $"https://api.x.immutable.com/v3/orders?direction=asc&include_fees=true&order_by=buy_quantity&page_size=200&sell_token_address=0xacb3c6a43d15b907e8433077b6d38ae40936fe2c&status=active&user={Address}";
            string offersUrl = "https://api.x.immutable.com/v3/orders?status=active&order_by=sell_quantity&direction=desc&page_size=1&buy_token_address=0xacb3c6a43d15b907e8433077b6d38ae40936fe2c&buy_token_id=";
            string? cursor = null;
            Dictionary<Order, Task<string>> offers = new();
            JObject? jsonListed;
            do
            {
                string url = cursor != null ? $"{urlListed}&cursor={cursor}" : urlListed;
                Console.WriteLine($"Requesting orders {url}...");
                jsonListed = JsonConvert.DeserializeObject<JObject>(await ResourceManager.Client.GetStringAsync(url));
                Console.WriteLine($"Processing orders...");
                JToken? result = jsonListed?["result"];
                if (result == null)
                    break;
                IEnumerable<Order> listingsPartial = await Task.WhenAll(result.Select(async order => new Order(order, await Order.getOrderCurrencyName(order))));
                Console.WriteLine($"Loaded {listingsPartial.Count()} orders.");
                offers = offers.Concat(listingsPartial.ToDictionary(order => order, async x => await ResourceManager.Client.GetStringAsync(offersUrl + x.TokenID))).ToDictionary(x => x.Key, x => x.Value);
                Console.WriteLine($"Requesting offers for {listingsPartial.Count()} orders.");
                cursor = jsonListed?["cursor"]?.ToString();
            } while (jsonListed?["remaining"]?.ToString() == "1");
            Console.WriteLine("1");
            while (offers.Count > 0)
            {
                Task<string> taskFinished = await Task.WhenAny(offers.Values);
                Order order = offers.First(x => x.Value == taskFinished).Key;
                JObject? jsonOffered = JsonConvert.DeserializeObject<JObject>(await taskFinished);
                JToken? result = jsonOffered?["result"];
                if (result != null)
                {
                    Task<Order>? offerTask = result.Select(async order => new Order(order, await Order.getOrderCurrencyName(order))).FirstOrDefault();
                    if (offerTask != null)
                    {
                        Order offer = await offerTask;
                        //await wlt.RequestBuyOrder(Application.Current.MainWindow, offer, this.tbTitle);
                        Console.WriteLine($"{offer.Seller} offered {offer.PriceTotal()} to buy {offer.Name}");
                    }
                }
                offers.Remove(order);
            }
            Console.WriteLine("2");
        }

        /// <summary>
        /// Adjust the size of the CardControl when the window size is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double maxWidth = 1400;
            double maxHeight = 800;
            double width = Math.Min(ActualWidth, maxWidth);
            double height = width / 1.75;
            if (height > ActualHeight)
            {
                height = Math.Min(ActualHeight, maxHeight);
                width = height * 1.75;
            }
            controlGrid.Height = height - 10;
            controlGrid.Width = width - 10;
        }

        /// <summary>
        /// Close the window when the user clicks on the greyed out background.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Get the position of the mouse click relative to the controlGrid
            Point clickPoint = e.GetPosition(controlGrid);

            // Check if the click occurred on the controlGrid
            if (clickPoint.X >= 0 && clickPoint.X < controlGrid.ActualWidth &&
                clickPoint.Y >= 0 && clickPoint.Y < controlGrid.ActualHeight)
            {
                return;
            }
            // Click occurred outside controlGrid, close the overlay.
            if (btnClose.IsEnabled)
            {
                ((MainWindow)Application.Current.MainWindow).CloseOverlay();
            }
        }

        /// <summary>
        /// Close the cardcontrol and return to the main menu.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            ((MainWindow)Application.Current.MainWindow).CloseOverlay();
        }
    }
}
