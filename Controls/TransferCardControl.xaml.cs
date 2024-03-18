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
using static GU_Exchange.Helpers.IMXlib;
using Serilog;

namespace GU_Exchange.Controls
{
    /// <summary>
    /// Interaction logic for TransferControl.xaml
    /// </summary>
    public partial class TransferCardControl : UserControl
    {
        #region Class Properties
        private readonly CardControl _parent;
        private readonly int _cardID;
        private readonly ImageSource _image;
        private readonly HashSet<string> _cardsOwned;
        #endregion

        #region Default Constructor
        /// <summary>
        /// Constructor for a <see cref="UserControl"/> for transferring cards between wallets.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="cardID"></param>
        /// <param name="image"></param>
        public TransferCardControl(CardControl parent, int cardID, ImageSource image)
        {
            InitializeComponent();
            _parent = parent;
            _cardID = cardID;
            _image = image;
            _cardsOwned = new();
            Setup();
        }
        #endregion

        #region Setup
        /// <summary>
        /// Fetch the number of cards of this type in the connected wallet and setup the window using this data.
        /// </summary>
        private async void Setup()
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
                if (ex is HttpRequestException)
                    Log.Information($"Failed to fetching wallet inventory for {_parent.CardID} of quality {(string)_parent.cbQuality.SelectedItem}. {ex.Message}: {ex.StackTrace}");
                return;
            }

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
        #endregion

        #region Event Handlers.
        /// <summary>
        /// Close the usercontrol if the user clicks outside of it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Get the position of the mouse click relative to the transferGrid
            Point clickPoint = e.GetPosition(transferGrid);

            // Check if the click occurred on the transferGrid
            if (clickPoint.X >= 0 && clickPoint.X < transferGrid.ActualWidth &&
                clickPoint.Y >= 0 && clickPoint.Y < transferGrid.ActualHeight)
            {
                return;
            }
            // Click occurred outside transferGrid, you can call your function here
            this.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Close the <see cref="UserControl"/> by collapsing it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Allow the user to lookup a wallet address belonging to a specific user.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnLookup_Click(object sender, RoutedEventArgs e)
        {
            PlayerLookupWindow window = new PlayerLookupWindow();
            window.Owner = Application.Current.MainWindow;
            window.ShowDialog();
            if (window.Result == PlayerLookupWindow.LookupResult.Select)
                tbAddress.Text = window.GetSelectedAddress();
        }

        /// <summary>
        /// Check if the address entered is valid and enable/disable the transfer button based on this.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TbAddress_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (Wallet.IsValidEthereumAddress(tbAddress.Text))
                {
                    btnTransfer.IsEnabled = true;
                }
                else
                {
                    btnTransfer.IsEnabled = false;
                }
                int.Parse((string)cbNumber.SelectedItem);
            }
            catch (NullReferenceException)
            {
            }
            catch(FormatException)
            {
                btnTransfer.IsEnabled = false;
            }
        }

        /// <summary>
        /// Execute the transfer of cards.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BtnTransfer_Click(object sender, RoutedEventArgs e)
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
            List<NFT> cardsToTransfer = new();

            int transferAmount = int.Parse((string)cbNumber.SelectedItem);
            IEnumerable<string> tokensToList = _cardsOwned.Take(transferAmount);
            foreach (string tokenIDStr in tokensToList)
            {
                try
                {
                    NFT card = new NFT()
                    {
                        token_address = "0xacb3c6a43d15b907e8433077b6d38ae40936fe2c",
                        token_id = ulong.Parse(tokenIDStr)
                    };
                    cardsToTransfer.Add(card);
                }
                catch (FormatException)
                {
                    // Transfer failed? Shouldn't happen.
                    spinner.Visibility = Visibility.Collapsed;
                    error.Visibility = Visibility.Visible;
                    btnClose.Visibility = Visibility.Visible;
                    tbStatus.Text = "Attempted to transfer invalid token.";
                    return;
                }
            }
            bool result = await wallet.RequestTransferCards(Application.Current.MainWindow, cardsToTransfer.ToArray(), this.tbAddress.Text, this.tbStatus);
            spinner.Visibility = Visibility.Collapsed;
            if (!result)
            {
                // Transfers failed.
                error.Visibility = Visibility.Visible;
                btnClose.Visibility = Visibility.Visible;
                return;
            }
            else
            {
                int invChange = 0;
                if (await GameDataManager.IsWalletLinked(Settings.GetApolloID(), wallet.Address))
                    invChange = -transferAmount;
                else if (await GameDataManager.IsWalletLinked(Settings.GetApolloID(), this.tbAddress.Text))
                    invChange = transferAmount;
                Inventory? inv = (Inventory?)App.Current.Properties["Inventory"];
                if (inv != null && invChange != 0)
                {
                    int quality = 0;
                    switch ((string)_parent.cbQuality.SelectedItem)
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
                        inv.SetNumberOwned(_parent.CardID, quality, Math.Max(inv.GetNumberOwned(_parent.CardID, quality) + invChange, 0));
                    await ((MainWindow)Application.Current.MainWindow).RefreshTilesAsync();
                }
            }

            // Buying the order succeeded, now update the inventory and local wallet to reflect the successfull purchase.
            success.Visibility = Visibility.Visible;

            // Refresh the wallet in the parent CardControl.
            _ = _parent.SetupInventoryAsync();
            btnClose.Visibility = Visibility.Visible;
        }
        #endregion
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
