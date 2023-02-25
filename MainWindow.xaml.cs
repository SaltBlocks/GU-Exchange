using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace GU_Exchange
{
    public class CheckBoxItem
    {
        public string? Label { get; set; }
        public bool IsChecked { get; set; }

        public CheckBoxItem(string? label, bool isChecked)
        {
            Label = label;
            IsChecked = isChecked;
        }
    }

    public class CheckBoxItems
    {
        public ObservableCollection<CheckBoxItem> Items { get; set; }

        public CheckBoxItems()
        {
            Items = new ObservableCollection<CheckBoxItem>();
            for (int i = 1; i < 9; i++)
            {
                Items.Add(new CheckBoxItem("Cost " + i, true));
            }
            Items.Add(new CheckBoxItem("Cost 9+", true));
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<CardTile> cardTiles;                               // List for the tiles that display cards on the main window.
        private int tileIndex;                                          // Used to track the number of tiles currently displayed.
        private CancellationTokenSource? imgUpdateTokenSource = null;   // Used to keep track of the card tiles being updated.
        private List<CardData> cardList;                                // Contains all cards matching the current search conditions.
        private CheckBoxItems checkBoxes;

        /// <summary>
        /// Constructor for the main application window.
        /// </summary>
        public MainWindow()
        {
            
            InitializeComponent();
            checkBoxes = new CheckBoxItems();
            cbCost.ItemsSource = checkBoxes.Items;
            cardTiles = new();
            cardList = new();
            tileIndex = 0;
            setupSearch();
            cbSet.SelectedIndex = 0;
            cbGod.SelectedIndex = 0;
            cbRarity.SelectedIndex = 0;
            cbTribe.SelectedIndex = 0;
            cbCost.SelectedIndex = 0;
            setupTiles();
        }

        /// <summary>
        /// Sets up the initial tile list to be displayed in the window after startup.
        /// </summary>
        private async void setupTiles()
        {
            cardList.AddRange((await Inventory.getCards()).Values);
            for (int i = 0; i < 30; i++)
            {
                CardTile tile = new CardTile(1);
                if (i >= cardList.Count)
                {
                    tile.Visibility = Visibility.Collapsed;
                    this.cardPanel.Children.Add(tile);
                    cardTiles.Add(tile);
                    continue;
                }
                tile.cardID = cardList[i].proto;
                this.cardPanel.Children.Add(tile);
                cardTiles.Add(tile);
                tileIndex++;
            }
            await Inventory.connectedInventory.updateInventory();
            if (imgUpdateTokenSource != null)
            {
                imgUpdateTokenSource.Cancel();
            }
            imgUpdateTokenSource = new CancellationTokenSource();
            CancellationToken token = imgUpdateTokenSource.Token;
            foreach (CardTile tile in cardTiles)
            {
                tile.updateTile(token);
            }
            Console.WriteLine(cardTiles.Count);
        }

        private async void setupSearch()
        {
            cbSet.Items.Add("Any set");
            foreach (string set in await Inventory.getSets())
            {
                cbSet.Items.Add(set);
            }
            cbGod.Items.Add("Any God");
            foreach (string god in await Inventory.getGods())
            {
                cbGod.Items.Add(god);
            }
            cbRarity.Items.Add("Any rarity");
            string[] rarities = {"Common", "Rare", "Epic", "Legendary", "Mythic"};
            foreach (string rarity in rarities)
            {
                cbRarity.Items.Add(rarity);
            }
            cbTribe.Items.Add("Any tribe");
            foreach (string tribe in await Inventory.getTribes())
            {
                cbTribe.Items.Add(tribe);
            }
        }

        // Handlers used for menu buttons.

        /// <summary>
        /// Opens the setupwindow and allows the user to switch wallets and GU account.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenWalletManager_Click(object sender, RoutedEventArgs e)
        {
            SetupWindow setup = new SetupWindow();
            setup.Owner = this;
            setup.ShowDialog();
            foreach (CardTile tile in cardTiles)
            {
                tile.updateTileText();
            }
        }

        /// <summary>
        /// Open the window to unlock the connected ETH wallet.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenWalletUnlock_Click(object sender, RoutedEventArgs e)
        {
            if (Wallet.connectedWallet == null)
            {
                return;
            }
            UnlockWindow unlock = new(Wallet.connectedWallet.address);
            unlock.Owner = this;
            unlock.ShowDialog();
            if (unlock.result == MessageBoxResult.Cancel)
            {
                Console.WriteLine("Cancelled");
                return;
            }
            bool unlocked = Wallet.connectedWallet.unlockWallet(unlock.getEnteredPassword());
            if (unlocked)
            {
                MessageBox.Show("Wallet unlocked.", "Wallet", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Export the private key of the linked wallet.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenWalletExporter_Click(object sender, RoutedEventArgs e)
        {
            if (Wallet.connectedWallet == null)
            {
                Console.WriteLine("No wallet connected.");
                return;
            }
            if (!Wallet.connectedWallet.isLocked())
            {
                MessageBox.Show(Wallet.connectedWallet.getPrivateKey());
            }
        }

        /// <summary>
        /// Updates the size of objects when the main window is resized.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.cardPanel.MaxWidth = e.NewSize.Width;
            this.searchBar.Width = e.NewSize.Width - 35;
        }

        /// <summary>
        /// Used to detect when the user scrolls to the bottom of the page.
        /// When this happens, new tiles are loaded and added to the window.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange <= 0)
            {
                return;
            }
            if (e.VerticalOffset + e.ViewportHeight != e.ExtentHeight)
            {
                return;
            }
            if (imgUpdateTokenSource != null)
            {
                imgUpdateTokenSource.Cancel();
            }
            imgUpdateTokenSource = new CancellationTokenSource();
            CancellationToken token = imgUpdateTokenSource.Token;
            for (int i = 0; i < 30; i++)
            {
                CardTile tile;
                if (tileIndex >= cardTiles.Count)
                {
                    tile = new CardTile(-1);
                    cardTiles.Add(tile);
                    this.cardPanel.Children.Add(tile);
                }
                else
                    tile = cardTiles[tileIndex];

                if (tileIndex >= cardList.Count)
                {
                    tile.Visibility = Visibility.Collapsed;
                    continue;
                }
                tile.Visibility = Visibility.Visible;
                tile.cardID = cardList[tileIndex].proto;
                tile.updateTile(token);
                tileIndex++;
            }
        }

        private async Task searchChanged()
        {
            this.scrollBar.ScrollToTop();
            if (imgUpdateTokenSource != null)
                imgUpdateTokenSource.Cancel();
            imgUpdateTokenSource = new CancellationTokenSource();
            CancellationToken token = imgUpdateTokenSource.Token;
            try
            {
                string? set = null;
                string? god = null;
                string? rarity = null;
                string? tribe = null;
                if (cbSet.SelectedValue != null && !cbSet.SelectedValue.Equals("Any set"))
                    set = (string)cbSet.SelectedValue;
                if (cbGod.SelectedValue != null && !cbGod.SelectedValue.Equals("Any God"))
                    god = (string)cbGod.SelectedValue;
                if (cbRarity.SelectedValue != null && !cbRarity.SelectedValue.Equals("Any rarity"))
                    rarity = (string)cbRarity.SelectedValue;
                if (cbTribe.SelectedValue != null && !cbTribe.SelectedValue.Equals("Any tribe"))
                    tribe = (string)cbTribe.SelectedValue;
                List<int> manaCosts = new List<int>();
                for (int i = 0; i < 9; i++)
                    if (checkBoxes.Items[i].IsChecked)
                        manaCosts.Add(i + 1);
                cardList = await Inventory.searchCardsAsync(this.searchBar.Text, token, set, god, rarity, tribe, manaCosts);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            List<CardTile> updatedTiles = new();
            for (int i = 0; i < cardTiles.Count; i++)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                if (i >= 30 || i >= cardList.Count)
                {
                    cardTiles[i].Visibility = Visibility.Collapsed;
                    continue;
                }
                cardTiles[i].Visibility = Visibility.Visible;
                if (cardTiles[i].cardID != cardList[i].proto || cardTiles[i].imgCard.Source == null)
                {
                    cardTiles[i].cardID = cardList[i].proto;
                    updatedTiles.Add(cardTiles[i]);
                }
            }
            foreach (CardTile tile in updatedTiles)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                tile.updateTile(token);
            }
            for (int i = 30; i < cardTiles.Count; i++)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                cardTiles[i].clearTile();
            }
            tileIndex = 30;
        }

        /// <summary>
        /// Updates the displayed card tiles when the search query is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void searchBar_TextChanged(object sender, TextChangedEventArgs e)
        {
            await searchChanged();
        }

        private async void filters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await searchChanged();
        }

        private async void CheckBox_action(object sender, RoutedEventArgs e)
        {
            await searchChanged();
        }

        private void TextBlock_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            StackPanel panel = (StackPanel) sender;
            CheckBox cb = (CheckBox)panel.Children[0];
            cb.IsChecked = !cb.IsChecked;
        }
    }
}
