using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ImageProcessor.Processors;
using System.Diagnostics;
using System.Net.Http;
using static GU_Exchange.IMXlib;
using System.Runtime.InteropServices;

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for ListControl.xaml
    /// </summary>
    public partial class ListControl : UserControl
    {
        private CardControl _parent;
        Dictionary<string, Task<Order?>> cheapestOrders;

        public ListControl(CardControl parent, ImageSource image)
        {
            InitializeComponent();
            _parent = parent;
            List<string> items = new List<string> { "ETH", "GODS", "IMX" };
            cbCurrency.ItemsSource = items;
            cbCurrency.SelectedIndex = 0;
            DataContext = new ListCardViewModel(parent.tbCardName.Text, (string)parent.cbQuality.SelectedItem, image);
            cheapestOrders = new();
            FetchCheapestOrders();
            setup();
        }

        public async void setup()
        {
            // Fetch cards in the connected wallet.
            Wallet? wallet = Wallet.GetConnectedWallet();
            if (wallet == null)
            {
                // No wallet connected, close this window.
                _ = _parent.ReloadOrderbookAsync();
                this.Visibility = Visibility.Collapsed;
                return;
            }
            string cardData = HttpUtility.UrlEncode("{\"proto\":[\"" + _parent.CardID + "\"]}");
            string urlInventory = $"https://api.x.immutable.com/v1/assets?page_size=10&user={wallet.Address}&metadata={cardData}&sell_orders=true";
            string cardString;
            try 
            {
                cardString = await ResourceManager.Client.GetStringAsync(urlInventory);
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is HttpRequestException)
            { 
                return; 
            }    
            JObject? jsonData = JsonConvert.DeserializeObject<JObject?>(cardString);
            if (jsonData?["result"] is not JToken result)
                return;

            int num_owned = 0;
            int num_listed = 0;
            foreach (JToken card in result)
            {
                string? quality = card["metadata"]?["quality"]?.Value<string>();
                if (quality == ((ListCardViewModel)DataContext).CardQuality)
                {
                    num_owned++;
                    if (card["orders"] != null)
                        num_listed++;
                }
            }
            this.tbNumber.Text = $"/ {num_owned} ({num_listed} listed)";
            cbNumber.Items.Clear();
            for (int i = 0; i < num_owned - num_listed; i++)
            {
                cbNumber.Items.Add((i + 1).ToString());
            }
            if (num_owned - num_listed > 0)
            {
                cbNumber.SelectedIndex = 0;
                btnList.IsEnabled = true;
            }
            string? tokenName = cbCurrency.SelectedItem.ToString();
            if (tokenName == null) return;
            Order? cheapestOrder = await cheapestOrders[tokenName];
            if (cheapestOrder == null) return;
            decimal listPrice = cheapestOrder.PriceTotal() - new decimal(0.00000001);
            tbListprice.Text = listPrice.ToString("0.##########");
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
        /// Fetch orders for all currencies for the card that will be listed.
        /// </summary>
        private async void FetchCheapestOrders()
        {
            Dictionary<string, Token> currency = await Wallet.FetchTokens();
            foreach (Token token in currency.Values)
            {
                cheapestOrders[token.Name] = FetchCheapestOrder(token.Address);
            }
        }

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
                JToken? order = jsonOrders["result"]?[0]?.Value<JToken?>();
                if (order == null)
                    return null;
                return new Order(order, tokenAddress);
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is HttpRequestException || ex is NullReferenceException)
            {
                return null;
            }
        }

        private async void cbCurrency_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            tbCurrency.Text = cbCurrency.SelectedItem.ToString();
            await AutoAdjustPrice();
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

        private void tbListprice_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!tbListprice.IsKeyboardFocusWithin) // Prevent a loop by not updating anything when this method is called due to another event modifying the value.
                return;
            try
            {
                decimal listPrice = Decimal.Parse(tbListprice.Text);
                decimal receiveAmount = listPrice / new decimal(1.08) * new decimal(0.99);
                tbReceiveAmount.Text = receiveAmount.ToString("0.##########");
            } catch (FormatException)
            {
                tbReceiveAmount.Text = "";
            }
        }

        private void tbReceiveAmount_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!tbReceiveAmount.IsKeyboardFocusWithin) // Prevent a loop by not updating anything when this method is called due to another event modifying the value.
                return;
            try
            {
                decimal receiveAmount = Decimal.Parse(tbReceiveAmount.Text);
                decimal listPrice = receiveAmount * new decimal(1.08) / new decimal(0.99);
                tbListprice.Text = listPrice.ToString("0.##########");
            }
            catch (FormatException)
            {
                tbListprice.Text = "";
            }
        }

        private async Task AutoAdjustPrice()
        {
            string? tokenName = cbCurrency.SelectedItem.ToString();
            if (tokenName == null) return;
            if (cheapestOrders == null) return;
            if (!cheapestOrders.ContainsKey(tokenName)) return;
            Order? cheapestOrder = await cheapestOrders[tokenName];
            if (cheapestOrder == null) return;
            decimal listPrice = cheapestOrder.PriceTotal() - new decimal(0.00000001);
            tbListprice.Text = listPrice.ToString("0.##########");
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

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            await AutoAdjustPrice();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            _ = _parent.ReloadOrderbookAsync();
            this.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Class used to display details about the order to the user.
    /// </summary>
    public class ListCardViewModel : INotifyPropertyChanged
    {
        #region Class Properties
        private string cardName;
        private string cardQuality;
        private ImageSource cardImageSource;
        #endregion

        #region Default Constructor
        public ListCardViewModel(string cardName, string cardQuality, ImageSource image)
        {
            this.cardName = cardName;
            this.cardQuality = cardQuality;
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
