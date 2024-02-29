using GU_Exchange.Helpers;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
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
using GU_Exchange.Views;

namespace GU_Exchange.Controls
{
    /// <summary>
    /// Interaction logic for TransferControl.xaml
    /// </summary>
    public partial class TransferControl : UserControl
    {
        private CardControl _parent;
        private int _cardID;
        private ImageSource _image;
        private HashSet<string> _cardsOwned;

        public TransferControl(CardControl parent, int cardID, ImageSource image)
        {
            InitializeComponent();
            _parent = parent;
            _cardID = cardID;
            _image = image;
            _cardsOwned = new();
            setup();
        }

        private async void setup()
        {
            // Fetch cards in the connected wallet.
            Wallet? wallet = Wallet.GetConnectedWallet();
            CardData? cardToTransfer = (await GameDataManager.GetCardListAsync())?[_cardID] ?? null;
            if (cardToTransfer == null || wallet == null)
            {
                this.Visibility = Visibility.Collapsed;
                return;
            }
            DataContext = new CardInformationViewModel(cardToTransfer.Name, (string)_parent.cbQuality.SelectedItem, _image);

            string cardData = HttpUtility.UrlEncode("{\"proto\":[\"" + _parent.CardID + "\"],\"quality\":[\"" + (string)_parent.cbQuality.SelectedItem + "\"]}");
            string urlInventory = $"https://api.x.immutable.com/v1/assets?user={wallet.Address}&metadata={cardData}&sell_orders=true";
            string cardString;
            try
            {
                cardString = await ResourceManager.Client.GetStringAsync(urlInventory);
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is HttpRequestException)
            {
                return;
            }

            Console.WriteLine(cardString);

            JObject? jsonData = JsonConvert.DeserializeObject<JObject?>(cardString);
            if (jsonData?["result"] is not JToken result)
                return;
            foreach (JToken card in result)
            {
                string? tokenID = card["token_id"]?.Value<string>();
                if (tokenID == null) continue;
                _cardsOwned.Add(tokenID);
            }
            cbNumber.Items.Clear();
            for (int i = 0; i < _cardsOwned.Count(); i++)
            {
                cbNumber.Items.Add((i + 1).ToString());
            }
            if (_cardsOwned.Count() == 0) cbNumber.Items.Add("None available");
            tbNumber.Text = $"/ {_cardsOwned.Count()}";
            cbNumber.SelectedIndex = 0;
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            PlayerLookupWindow window = new PlayerLookupWindow();
            window.Owner = Application.Current.MainWindow;
            window.ShowDialog();
        }
    }

    /// <summary>
    /// Class used to display details about the order to the user.
    /// </summary>
    public class CardInformationViewModel : INotifyPropertyChanged
    {
        #region Class Properties
        private string _cardName;
        private string _cardQuality;
        private ImageSource _cardImageSource;
        #endregion

        #region Default Constructor
        public CardInformationViewModel(string cardName, string cardQuality, ImageSource image)
        {
            _cardName = cardName;
            _cardQuality = cardQuality;
            _cardImageSource = image;
        }
        #endregion

        #region Getters and Setters
        public string CardName
        {
            get { return _cardName; }
            set
            {
                _cardName = value;
                OnPropertyChanged(nameof(CardName));
            }
        }

        public string CardQuality
        {
            get { return _cardQuality; }
            set
            {
                _cardQuality = value;
                OnPropertyChanged(nameof(CardQuality));
            }
        }

        public ImageSource CardImageSource
        {
            get { return _cardImageSource; }
            set
            {
                _cardImageSource = value;
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
