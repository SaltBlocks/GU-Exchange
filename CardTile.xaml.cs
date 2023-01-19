using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for CardTile.xaml
    /// </summary>
    public partial class CardTile : UserControl
    {
        /// <summary>
        /// ID of the card being displayed on this tile.
        /// </summary>
        public int cardID { get; set; }

        /// <summary>
        /// Creates a new <see cref="CardTile"/> displaying the card with the provided ID. 
        /// </summary>
        /// <param name="cardID"></param>
        public CardTile(int cardID)
        {
            InitializeComponent();
            this.cardID = cardID;
        }

        /// <summary>
        /// Updates the text on this tile to match the cards in the connected <see cref="Inventory"/>.
        /// </summary>
        public void updateTileText()
        {
            Dictionary<int, CardData> dict = Inventory.getCards();
            if (dict.ContainsKey(cardID))
            {
                this.lblName.Content = dict[cardID].name;
            }
            int num = Inventory.connectedInventory.getNumberOwned(cardID, 5);
            string text = num > 9 ? "9+" : num.ToString();
            this.tbLocked.Text = text;
            num = Inventory.connectedInventory.getNumberOwned(cardID, 4);
            text = num > 9 ? "9+" : num.ToString();
            this.tbMeteorite.Text = text;
            num = Inventory.connectedInventory.getNumberOwned(cardID, 3);
            text = num > 9 ? "9+" : num.ToString();
            this.tbShadow.Text = text;
            num = Inventory.connectedInventory.getNumberOwned(cardID, 2);
            text = num > 9 ? "9+" : num.ToString();
            this.tbGold.Text = text;
            num = Inventory.connectedInventory.getNumberOwned(cardID, 1);
            text = num > 9 ? "9+" : num.ToString();
            this.tbDiamond.Text = text;
        }

        /// <summary>
        /// Updates the image on this tile to the image of the card matching the CardID.
        /// </summary>
        /// <param name="cancelToken">Token used to cancel the image update.</param>
        /// <returns></returns>
        public async Task updateTileImage(CancellationToken cancelToken)
        {
            this.imgCard.Source = null;
            try
            {
                BitmapSource? img = await ResourceFactory.GetCardImageAsync(cardID, cancelToken);
                if (cancelToken.IsCancellationRequested)
                {
                    return;
                }
                this.imgCard.Source = img;
            }
            catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
            {
                return;
            }
        }

        /// <summary>
        /// Updates the text and the image on this tile to match cards in the connected <see cref="Inventory"/>.
        /// </summary>
        /// <param name="cancelToken">Token used to cancel the image update.</param>
        public async void updateTile(CancellationToken cancelToken)
        {
            updateTileText();
            await updateTileImage(cancelToken);
        }

        /// <summary>
        /// Removes the image currently on the tile.
        /// </summary>
        public void clearTile()
        {
            this.imgCard.Source = null;
        }

        /// <summary>
        /// Shows the highlighting rectangle when the user mouse overs the tile.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_MouseEnter(object sender, MouseEventArgs e)
        {
            this.rectHighlight.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Hides the highlighting rectangle when the users mouse leaves the tile.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_MouseLeave(object sender, MouseEventArgs e)
        {
            this.rectHighlight.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// Used to handle clicks on the tile.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Console.WriteLine("Clicked: " + cardID);
        }
    }
}
