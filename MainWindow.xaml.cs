using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GU_Exchange
{
    #region Logic for a combobox containing checkboxes.

    /// <summary>
    /// Class for displaying items with a checkbox in a combobox.
    /// </summary>
    public class CheckBoxItem : INotifyPropertyChanged
    {
        public string? Label { get; set; }
        private bool isChecked;
        public bool IsChecked
        {
            get { return isChecked; }
            set
            {
                if (isChecked != value)
                {
                    isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                }
            }
        }

        public CheckBoxItem(string? label, bool isChecked)
        {
            Label = label;
            IsChecked = isChecked;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Class for containing the <see cref="CheckBoxItem"/> objects for each mana cost.
    /// </summary>
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

    #endregion

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Class Properties
        private List<CardData> _cardList;                               // Contains all cards matching the current search conditions.
        private List<CardTile> _cardTiles;                              // List for the tiles that display cards on the main window.
        private int _tileIndex;                                         // Used to track the number of tiles currently displayed.
        private CheckBoxItems _checkBoxes;                              // Checkboxes used for filtering searches by mana cost.
        private CancellationTokenSource? _imgUpdateTokenSource = null;  // Used to keep track of the card tiles being updated.
        private CardControl? _cardControl;                              // Overlay used for trading a selected card.
        private bool _setupComplete;                                    // Indicates that search setup has finished.
        #endregion

        #region Default Constructor.
        /// <summary>
        /// Initialise the window and setup the search functions.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            Console.WriteLine(Directory.GetCurrentDirectory());
            _cardList = new List<CardData>();
            _cardTiles = new List<CardTile>();
            _checkBoxes = new CheckBoxItems();
            _tileIndex = 0;
            _setupComplete = false;
            SetupSearchAsync();
            SetupAsync();
            _ = SetupWalletInfoAsync();
        }
        #endregion

        #region Window setup.

        /// <summary>
        /// Load settings, Setup player inventory, create card tiles, ask user to link account if none is linked. 
        /// </summary>
        public async void SetupAsync()
        {
            Task setupPlayers = GameDataManager.SetupPlayerDataAsync();                                 // Start fetching player names and apollo IDs locally or from web.
            Settings.LoadSettings();                                                                    // Load GU Exchange settings.
            Inventory inv = new Inventory(Settings.GetApolloID());                                      // Setup player inventory.
            Task inventoryUpdate = inv.UpdateInventoryAsync();
            App.Current.Properties["Inventory"] = inv;

            Dictionary<int, CardData>? cardData = await GameDataManager.GetCardListAsync();             // Initialize card list.
            if (cardData == null)
            {
                return;
            }
            _cardList.AddRange(cardData.Values);
            for (int i = 0; i < 30; i++)
            {
                CardTile tile = new CardTile(1);
                if (i >= _cardList.Count)
                {
                    tile.Visibility = Visibility.Collapsed;
                    this.cardPanel.Children.Add(tile);
                    _cardTiles.Add(tile);
                    continue;
                }
                tile.CardID = _cardList[i].ProtoID;
                this.cardPanel.Children.Add(tile);
                _cardTiles.Add(tile);
                _tileIndex++;
            }
            if (_imgUpdateTokenSource != null)
                _imgUpdateTokenSource.Cancel();
            _imgUpdateTokenSource = new CancellationTokenSource();
            CancellationToken token = _imgUpdateTokenSource.Token;
            await inventoryUpdate;
            try
            {
                foreach (CardTile tile in _cardTiles)
                {
                    _ = tile.SetupTileAsync(token);
                }
            }
            catch (OperationCanceledException) { }
            if (Settings.GetApolloID() == -1 && !Settings.GetSetting("dont_ask_link").Equals("True"))   // Request the user to link their account.
            {
                await setupPlayers;
                while (!IsLoaded || !IsVisible) // This window cannot be used as a parent before it is loaded. Wait untill this is the case.
                {
                    await Task.Delay(100);
                }
                LinkAccountWindow setup = new();
                setup.Owner = this;
                setup.ShowDialog();
            }
            Console.WriteLine(Wallet.GetConnectedWallet() == null);
        }

        /// <summary>
        /// Setup the filterboxes the user can use to filter search results on various criteria.
        /// </summary>
        private async void SetupSearchAsync()
        {
            cbSet.Items.Add("Any set");
            foreach (string set in await GameDataManager.getSets())
            {
                cbSet.Items.Add(set);
            }
            cbGod.Items.Add("Any God");
            foreach (string god in await GameDataManager.getGods())
            {
                cbGod.Items.Add(god);
            }
            cbRarity.Items.Add("Any rarity");
            string[] rarities = { "Common", "Rare", "Epic", "Legendary", "Mythic" };
            foreach (string rarity in rarities)
            {
                cbRarity.Items.Add(rarity);
            }
            cbTribe.Items.Add("Any tribe");
            foreach (string tribe in await GameDataManager.getTribes())
            {
                if (tribe.Length == 0)
                    cbTribe.Items.Add("None");
                else
                    cbTribe.Items.Add(tribe);
            }
            string[] sortTypes = { "Search Query", "Rarity (Mythic-Common)", "Rarity (Common-Mythic)", "Price (High-Low)", "Price (Low-High)" };
            foreach (string sortType in sortTypes)
            {
                cbSort.Items.Add(sortType);
            }
            cbCost.ItemsSource = _checkBoxes.Items;
            cbSet.SelectedIndex = 0;
            cbGod.SelectedIndex = 0;
            cbRarity.SelectedIndex = 0;
            cbTribe.SelectedIndex = 0;
            cbCost.SelectedIndex = 0;
            cbSort.SelectedIndex = 0;
            _setupComplete = true;
        }

        public async Task SetupInventoryAsync()
        {
            Inventory? inv = (Inventory?) App.Current.Properties["Inventory"];
            if (inv == null)
                return;
            await inv.SetApolloIDAsync(Settings.GetApolloID());
            foreach (CardTile tile in _cardTiles)
            {
                _ = tile.UpdateTileTextAsync(); // Update the text of all tiles to reflect the newly linked inventory.
            }
        }

        public async Task SetupWalletInfoAsync()
        {
            Wallet? connectedWallet = Wallet.GetConnectedWallet();
            if (connectedWallet == null)
            {
                Console.WriteLine("Null");
            }
            else
            {
                Console.WriteLine(connectedWallet.Address);
            }
            if (connectedWallet == null)
            {
                rectNoWallet.Visibility = Visibility.Visible;
                lblNoWallet.Visibility = Visibility.Visible;
            } else
            {
                rectNoWallet.Visibility= Visibility.Collapsed;
                lblNoWallet.Visibility = Visibility.Collapsed;
                try
                {
                    Console.WriteLine($"Fetching wallet content for wallet {connectedWallet.Address}");
                    txtEth.Text = $"{Math.Round(await connectedWallet.GetTokenAmountAsync("ETH"), 6)} ETH";
                    txtGods.Text = $"{Math.Round(await connectedWallet.GetTokenAmountAsync("GODS"), 2)} GODS";
                    txtImx.Text = $"{Math.Round(await connectedWallet.GetTokenAmountAsync("IMX"), 2)} IMX";
                } catch (HttpRequestException e)
                {
                    Console.WriteLine($"Failed to fetch wallet contents: {e.StackTrace}");
                }
            }
        }

        #endregion

        #region Search Function.

        /// <summary>
        /// Updates the displayed cardtiles when the user changes the search criteria.
        /// </summary>
        /// <returns></returns>
        private async Task SearchChangedAsync()
        {
            if (!_setupComplete)                                    // Setup needs to be completed before the search can function.
                return;
            this.scrollBar.ScrollToTop();                           // Scroll back to the top of the page when the search criteria change.
            if (_imgUpdateTokenSource != null)
                _imgUpdateTokenSource.Cancel();                     // Cancel any updates to the search window that haven't yet completed.
            _imgUpdateTokenSource = new CancellationTokenSource();
            CancellationToken token = _imgUpdateTokenSource.Token;
            try                                                     // Search the card inventory for cards meeting the selected criteria.
            {
                string? set = null;
                string? god = null;
                string? rarity = null;
                string? tribe = null;
                string? sort = null;
                if (cbSet.SelectedValue != null && !cbSet.SelectedValue.Equals("Any set"))
                    set = (string)cbSet.SelectedValue;
                if (cbGod.SelectedValue != null && !cbGod.SelectedValue.Equals("Any God"))
                    god = (string)cbGod.SelectedValue;
                if (cbRarity.SelectedValue != null && !cbRarity.SelectedValue.Equals("Any rarity"))
                    rarity = (string)cbRarity.SelectedValue;
                if (cbTribe.SelectedValue != null && !cbTribe.SelectedValue.Equals("Any tribe"))
                {
                    tribe = (string)cbTribe.SelectedValue;
                    if (tribe.Equals("None"))
                        tribe = "";
                }
                List<int> manaCosts = new List<int>();
                for (int i = 0; i < 9; i++)
                    if (_checkBoxes.Items[i].IsChecked)
                        manaCosts.Add(i + 1);
                if (cbSort.SelectedValue != null)
                    sort = (string)cbSort.SelectedValue;
                _cardList = await GameDataManager.searchCardsAsync(this.searchBar.Text, token, set, god, rarity, tribe, manaCosts, sort);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            List<CardTile> updatedTiles = new();                    // Create a list tiles that need to be updated.
            for (int i = 0; i < _cardTiles.Count; i++)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                if (i >= 30 || i >= _cardList.Count)
                {
                    _cardTiles[i].Visibility = Visibility.Collapsed;
                    continue;
                }
                _cardTiles[i].Visibility = Visibility.Visible;
                if (_cardTiles[i].CardID != _cardList[i].ProtoID || _cardTiles[i].imgCard.Source == null)
                {
                    _cardTiles[i].CardID = _cardList[i].ProtoID;
                    updatedTiles.Add(_cardTiles[i]);
                }
            }
            foreach (CardTile tile in updatedTiles)                 // Update all changed tiles.
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                try
                {
                    _ = tile.SetupTileAsync(token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
            for (int i = 30; i < _cardTiles.Count; i++)             // Clear images that are no longer needed from unloaded tiles.
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                _cardTiles[i].clearTile();
            }
            _tileIndex = 30;
        }

        #endregion

        #region Event Handlers.

        /// <summary>
        /// Updates the size and placement of objects when the main window is resized.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.cardPanel.MaxWidth = e.NewSize.Width;
            this.searchBar.Width = e.NewSize.Width - 35;
            this.rectNoWallet.Width = e.NewSize.Width - 95;
            this.lblNoWallet.Width = e.NewSize.Width - 95;
            ComboBox[] searchBoxes = { cbSet, cbGod, cbRarity, cbTribe, cbCost, cbSort };
            Label[] searchLabels = { lblSet, lblGod, lblRarity, lblTribe, lblCost, lblSort };
            for (int i = 0; i < searchBoxes.Length; i++)
            {
                searchBoxes[i].Width = searchBar.Width / 6;
                searchBoxes[i].Margin = new Thickness(i * this.searchBar.Width / 6 + 10, 80, 0, 0);
                searchLabels[i].Width = searchBar.Width / 6;
                searchLabels[i].Margin = new Thickness(i * this.searchBar.Width / 6 + 10, 55, 0, 0);
            }
            btnReset.Margin = new Thickness(this.searchBar.Width - 70, 125, 0, 0);
            this.imgEth.Margin = new Thickness(1 * this.searchBar.Width / 4 - 70, 25, 0, 0);
            this.txtEth.Margin = new Thickness(1 * this.searchBar.Width / 4 - 35, 25, 0, 0);
            this.imgGods.Margin = new Thickness(2 * this.searchBar.Width / 4 - 70, 25, 0, 0);
            this.txtGods.Margin = new Thickness(2 * this.searchBar.Width / 4 - 35, 25, 0, 0);
            this.imgImx.Margin = new Thickness(3 * this.searchBar.Width / 4 - 70, 25, 0, 0);
            this.txtImx.Margin = new Thickness(3 * this.searchBar.Width / 4 - 35, 25, 0, 0);
            this.rectNoWallet.Width = e.NewSize.Width - 35;
            this.lblNoWallet.Width = e.NewSize.Width - 35;
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
            if (_imgUpdateTokenSource == null)
                _imgUpdateTokenSource = new CancellationTokenSource();
            CancellationToken token = _imgUpdateTokenSource.Token;
            for (int i = 0; i < 30; i++)
            {
                CardTile tile;
                if (_tileIndex >= _cardTiles.Count)
                {
                    tile = new CardTile(-1);
                    _cardTiles.Add(tile);
                    this.cardPanel.Children.Add(tile);
                }
                else
                    tile = _cardTiles[_tileIndex];

                if (_tileIndex >= _cardList.Count)
                {
                    tile.Visibility = Visibility.Collapsed;
                    continue;
                }
                tile.Visibility = Visibility.Visible;
                tile.CardID = _cardList[_tileIndex].ProtoID;
                _ = tile.SetupTileAsync(token);
                _tileIndex++;
            }
        }

        /// <summary>
        /// Used to update the card window when the user types in the search field.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            await SearchChangedAsync();
        }

        /// <summary>
        /// Used to update the card window when the user modifies the filters.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Filter_SelectionChangedAsync(object sender, SelectionChangedEventArgs e)
        {
            await SearchChangedAsync();
        }

        /// <summary>
        /// Checks or Unchecks the checkbox for the manacost when the item is selected.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CostFilter_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            StackPanel panel = (StackPanel)sender;
            CheckBox cb = (CheckBox)panel.Children[0];
            cb.IsChecked = !cb.IsChecked;
        }

        /// <summary>
        /// Updates the displayed card tiles when the mana cost selection is modified.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CheckBox_actionAsync(object sender, RoutedEventArgs e)
        {
            await SearchChangedAsync();
        }

        /// <summary>
        /// Reset all filters if the reset button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            ComboBox[] searchBoxes = { cbSet, cbGod, cbRarity, cbTribe, cbSort };
            foreach (ComboBox comboBox in searchBoxes)
            {
                comboBox.SelectedIndex = 0;
            }
            for (int i = 0; i <  cbCost.Items.Count; i++)
            {
                ((CheckBoxItem)cbCost.Items[i]).IsChecked = true;
            }
            searchBar.Text = "";
        }

        /// <summary>
        /// Highlight the no wallet rectangle if the user hovers over it to indicate that it can be clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void rectNoWallet_MouseEnter(object sender, MouseEventArgs e)
        {
            // Change the colors to highlight the rectangle
            rectNoWallet.Fill = new SolidColorBrush(Colors.LightBlue); // Change to the color you want
            rectNoWallet.Stroke = new SolidColorBrush(Colors.DarkBlue); // Change to the color you want
        }

        /// <summary>
        /// Restore the color of the rectangle when the mouse leaves it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void rectNoWallet_MouseLeave(object sender, MouseEventArgs e)
        {
            // Restore the original colors when the mouse leaves
            rectNoWallet.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0x8A, 0xB3, 0xF7)); // Original fill color
            rectNoWallet.Stroke = new SolidColorBrush(Colors.Black); // Original stroke color
        }

        /// <summary>
        /// Handle the no wallet rectangle being clicked and showing the setup window for adding a wallet.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void noWallet_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SetupWalletWindow setup = new();
            setup.Owner = this;
            setup.ShowDialog();
        }

        #endregion

        #region Open/Close trading overlay.

        /// <summary>
        /// Open the trading overlay for the card with the provided CardID.
        /// </summary>
        /// <param name="CardID">CardID for the card to trade.</param>
        public void OpenCardControl(int CardID)
        {
            _cardControl = new CardControl(CardID);
            _cardControl.Margin = new Thickness(0, 60, 0, 0);
            this.MainGrid.Children.Add(_cardControl);
        }

        /// <summary>
        /// Close the currently opened trading overlay.
        /// </summary>
        public void CloseCardControl()
        {
            if (_cardControl != null)
                this.MainGrid.Children.Remove(_cardControl);
            _cardControl = null;
        }

        #endregion


        // Temp trash to delete later.
        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetupWindow setup = new();
            setup.Owner = this;
            setup.ShowDialog();
        }

        private void miLock_Click(object sender, RoutedEventArgs e)
        {
            Wallet? wlt = Wallet.GetConnectedWallet();
            if (wlt == null)
                return;
            UnlockWalletWindow unlock = new(wlt);
            unlock.Owner = this;
            unlock.ShowDialog();
        }
    }
}
