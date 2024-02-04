using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for CardTile.xaml
    /// </summary>
    public partial class CardTile : UserControl
    {
        #region Class properties
        
        /// <summary>
        /// ID of the card being displayed on this tile.
        /// </summary>
        public int CardID { get; set; }

        #endregion

        #region Default constructor

        /// <summary>
        /// Constructor for a CardTile that opens the trading window for the card with the given CardID when clicked.
        /// </summary>
        /// <param name="CardID">proto id for the card to display.</param>
        public CardTile(int CardID)
        {
            InitializeComponent();
            this.CardID = CardID;
        }

        #endregion

        #region Data loading and setup.

        /// <summary>
        /// Creates a task setting up the text and image displayed on the tile
        /// </summary>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> used for cancelling this task.</param>
        public async Task SetupTileAsync(CancellationToken cancellationToken)
        {
            this.imgCard.Source = null;
            Task UpdateImage = SetupImageAsync(cancellationToken);
            await UpdateTileTextAsync();
            await UpdateImage;
        }

        /// <summary>
        /// Creates a task updating the text on this tile to match the cards in the connected <see cref="Inventory"/>.
        /// </summary>
        public async Task UpdateTileTextAsync()
        {
            Inventory? inv = (Inventory?)App.Current.Properties["Inventory"];
            Dictionary<int, CardData>? dict = await GameDataManager.GetCardListAsync();
            if (dict == null)
            {
                return;
            }
            if (dict.ContainsKey(CardID))
            {
                this.tbName.Text = dict[CardID].Name;
            }
            decimal priceUSD = await GameDataManager.GetCardPriceEstimateAsync(CardID);
            string priceStr = priceUSD == -1 ? "--.--" : priceUSD.ToString("0.00");
            this.lblPrice.Content = $"${priceStr}";
            if (inv == null || inv.GetApolloID() == -1)
            {
                this.tbLocked.Text = "?";
                this.tbMeteorite.Text = "?";
                this.tbShadow.Text = "?";
                this.tbGold.Text = "?";
                this.tbDiamond.Text = "?";
                return;
            }
            int ownedLocked = inv.GetNumberOwned(CardID, 5);
            int ownedMeteorite = inv.GetNumberOwned(CardID, 4);
            int ownedShadow = inv.GetNumberOwned(CardID, 3);
            int ownedGold = inv.GetNumberOwned(CardID, 2);
            int ownedDiamond = inv.GetNumberOwned(CardID, 1);
            this.tbLocked.Text = ownedLocked > 9 ? "9+" : ownedLocked.ToString();
            this.tbMeteorite.Text = ownedMeteorite > 9 ? "9+" : ownedMeteorite.ToString();
            this.tbShadow.Text = ownedShadow > 9 ? "9+" : ownedShadow.ToString();
            this.tbGold.Text = ownedGold > 9 ? "9+" : ownedGold.ToString();
            this.tbDiamond.Text = ownedDiamond > 9 ? "9+" : ownedDiamond.ToString();
        }

        /// <summary>
        /// Creates a task updating the image displayed on this tile to match the tiles CardID.
        /// </summary>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> used for cancelling this task.</param>
        public async Task SetupImageAsync(CancellationToken cancellationToken)
        {
            this.imgCard.Source = await ResourceManager.GetCardImageAsync(this.CardID, 4, true, cancellationToken);
        }

        /// <summary>
        /// Removes the image currently on the tile.
        /// </summary>
        public void clearTile()
        {
            this.imgCard.Source = null;
        }

        #endregion

        #region Event Handlers.

        /// <summary>
        /// Shows the highlighting rectangle when the mouse enters the tile.
        /// </summary>
        private void UserControl_MouseEnter(object sender, MouseEventArgs e)
        {
            this.rectHighlight.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Hides the highlighting rectangle when the mouse leaves the tile.
        /// </summary>
        private void UserControl_MouseLeave(object sender, MouseEventArgs e)
        {
            this.rectHighlight.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// Opens the card trading window if the card is clicked.
        /// </summary>
        private void UserControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ((MainWindow)Application.Current.MainWindow).OpenCardControl(CardID);
        }

        #endregion
    }
}