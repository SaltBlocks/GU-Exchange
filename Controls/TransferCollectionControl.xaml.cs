using GU_Exchange.Helpers;
using GU_Exchange.Views;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
using System.Web;
using System.Threading;
using static GU_Exchange.Helpers.IMXlib;
using System.Runtime.CompilerServices;

namespace GU_Exchange.Controls
{
    /// <summary>
    /// Interaction logic for TransferCollectionControl.xaml
    /// </summary>
    public partial class TransferCollectionControl : UserControl
    {
        private enum WindowStatus
        {
            Waiting, Transferring, Transferred, Failed
        }

        private WindowStatus status;
        private int displayIndex;
        private readonly List<(string, int, int)> cardData;
        private readonly HashSet<string> _tokenIds;
        public TransferCollectionControl()
        {
            InitializeComponent();
            status = WindowStatus.Waiting;
            displayIndex = 0;
            cardData = new();
            _tokenIds = new();
            setupInventory();
        }

        private async void setupInventory()
        {
            Wallet? wallet = Wallet.GetConnectedWallet();
            if (wallet == null)
            {
                tbStatus.Text = "No wallet connected";
                return;
            }
            try
            {
                Log.Information($"Fetching orders for wallet ");
                bool hasNext = true;
                string urlBase = $"https://api.x.immutable.com/v1/assets?page_size=200&user={wallet.Address}&collection=0xacb3c6a43d15b907e8433077b6d38ae40936fe2c";
                string urlInventory = urlBase;
                while (hasNext)
                {
                    string strInventory = await ResourceManager.Client.GetStringAsync(urlInventory);
                    JObject? jsonOrders = (JObject?)JsonConvert.DeserializeObject(strInventory);
                    if (jsonOrders == null)
                        break;
                    hasNext = ((int?)jsonOrders.SelectToken("remaining")) == 1;
                    string cursor = (string?)jsonOrders.SelectToken("cursor") ?? "";
                    JToken? result = jsonOrders["result"];
                    if (result == null)
                        return;
                    foreach (JToken order in result)
                    {
                        string? cardName = (string?)order.SelectToken("metadata.name");
                        string? img_url = (string?)order.SelectToken("metadata.image");
                        string? token_id = (string?)order.SelectToken("token_id");
                        if (cardName == null || img_url == null || token_id == null)
                            continue;
                        string[] card_data = img_url.Split("id=")[1].Split("&q=");
                        cardData.Add((cardName, int.Parse(card_data[0]), int.Parse(card_data[1])));
                        if (displayIndex < 50)
                        {
                            OrderDisplayControl control = new(cardName, int.Parse(card_data[0]), int.Parse(card_data[1]));
                            SetupOrderDisplay(control);
                            cardPanel.Children.Add(control);
                            displayIndex++;
                        }
                        _tokenIds.Add(token_id);
                    }
                    urlInventory = $"{urlBase}&cursor={cursor}";
                }
                tbStatus.Text = $"Your wallet currently contains {_tokenIds.Count} GU cards.";
            }
            catch (Exception ex)
            {
                Log.Warning($"Failed to fetching contents of wallet \"{wallet.Address}\". {ex.Message}: {ex.StackTrace}");
                tbStatus.Text = $"Failed to fetch wallet contents.";
                return;
            }
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
            int added = 0;
            while (added < 50 && displayIndex < cardData.Count)
            {
                OrderDisplayControl control = new(cardData[displayIndex].Item1, cardData[displayIndex].Item2, cardData[displayIndex].Item3);
                SetupOrderDisplay(control);
                cardPanel.Children.Add(control);
                displayIndex++;
                added++;
            }
        }

        /// <summary>
        /// Execute the transfer of cards.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BtnTransfer_Click(object sender, RoutedEventArgs e)
        {
            // Get the connected wallet.
            Wallet? wallet = Wallet.GetConnectedWallet();
            if (wallet == null)
            {
                // No wallet connected, cannot continue.
                tbStatus.Text = "No wallet connected";
                return;
            }

            if (_tokenIds.Count == 0)
            {
                tbStatus.Text = "No cards to transfer";
                return;
            }

            // Warn the user if a large number of cards will be transferred.
            if (_tokenIds.Count() > 20)
            {
                tbStatus.Text = "Waiting for user confirmation...";
                MessageWindow window = new MessageWindow($"You are about the transfer your entire collection of {_tokenIds.Count()} GU cards to '{tbAddress.Text}'\nAre you sure you want to continue?", "Confirm Transfer", MessageType.CONFIRM);
                window.Owner = (MainWindow)Application.Current.MainWindow;
                window.ShowDialog();
                if (!window.Result)
                {
                    status = WindowStatus.Failed;
                    tbStatus.Text = "Transfer cancelled...";
                    foreach (OrderDisplayControl display in cardPanel.Children)
                    {
                        display.SetStatusMessage("Transfer failed");
                        display.SetStatus(OrderDisplayControl.DisplayStatus.Fail);
                        display.ShowStatus(true);
                    }
                    return;
                }
            }

            // Prevent user from closing the window until this method finished during the purchase.
            btnTransferAll.IsEnabled = false;
            btnClose.IsEnabled = false;
            ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = false;

            status = WindowStatus.Transferring;
            foreach (OrderDisplayControl display in cardPanel.Children)
            {
                display.SetStatusMessage("Transferring...");
                display.ShowStatus(true);
            }

            // Submit the transfer and allow the wallet to update the status message.
            List<NFT> cardsToTransfer = new();
            foreach (string token in _tokenIds)
            {
                cardsToTransfer.Add(new NFT()
                {
                    token_address = "0xacb3c6a43d15b907e8433077b6d38ae40936fe2c",
                    token_id = ulong.Parse(token)
                });
            }
            bool result = await wallet.RequestTransferCards(Application.Current.MainWindow, cardsToTransfer.ToArray(), this.tbAddress.Text, this.tbStatus);
            if (!result)
            {
                // Transfers failed.
                tbStatus.Text = "All transfers failed";
                status = WindowStatus.Failed;
                foreach (OrderDisplayControl display in cardPanel.Children)
                {
                    display.SetStatusMessage("Transfer failed");
                    display.SetStatus(OrderDisplayControl.DisplayStatus.Fail);
                    display.ShowStatus(true);
                }
            }
            else
            {
                tbStatus.Text = "All transfers succeeded";
                status = WindowStatus.Transferred;
                foreach (OrderDisplayControl display in cardPanel.Children)
                {
                    display.SetStatus(OrderDisplayControl.DisplayStatus.Success);
                    display.SetStatusMessage("Transfer complete");
                    display.ShowStatus(true);
                }
                int invChange = 0;
                if (await GameDataManager.IsWalletLinked(Settings.GetApolloID(), wallet.Address))
                    invChange -= 1;
                if (await GameDataManager.IsWalletLinked(Settings.GetApolloID(), this.tbAddress.Text))
                    invChange += 1;
                Inventory? inv = (Inventory?)App.Current.Properties["Inventory"];
                if (inv != null && invChange != 0)
                {
                    foreach ((string, int, int) card in cardData)
                    {
                        inv.SetNumberOwned(card.Item2, card.Item3, Math.Max(inv.GetNumberOwned(card.Item2, card.Item3) + invChange, 0));
                    }
                    await ((MainWindow)Application.Current.MainWindow).RefreshTilesAsync();
                }
            }
            btnTransferAll.IsEnabled = false;
            btnClose.IsEnabled = true;
            ((MainWindow)Application.Current.MainWindow).menuBar.IsEnabled = true;
        }

    private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            ((MainWindow)Application.Current.MainWindow).CloseOverlay();
        }

        /// <summary>
        /// Allow the user to lookup an address belonging to a specific player.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnLookup_Click(object sender, RoutedEventArgs e)
        {
            PlayerLookupWindow window = new PlayerLookupWindow();
            window.Owner = Application.Current.MainWindow;
            window.ShowDialog();
            if (window.Result == PlayerLookupWindow.LookupResult.Select)
                tbAddress.Text = window.GetSelectedAddress();
        }

        private void tbAddress_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Wallet.IsValidEthereumAddress(tbAddress.Text) && status == WindowStatus.Waiting)
            {
                btnTransferAll.IsEnabled = true;
            }
            else
            {
                btnTransferAll.IsEnabled = false;
            }
        }

        private void SetupOrderDisplay(OrderDisplayControl control)
        {
            if (status == WindowStatus.Waiting)
            {
                control.ShowStatus(false);
            } else if (status == WindowStatus.Transferring)
            {
                control.ShowStatus(true);
                control.SetStatusMessage("Transferring...");
            } else if (status == WindowStatus.Transferred)
            {
                control.ShowStatus(true);
                control.SetStatus(OrderDisplayControl.DisplayStatus.Success);
                control.SetStatusMessage("Transfer complete");
            } else if (status == WindowStatus.Failed)
            {
                control.ShowStatus(true);
                control.SetStatus(OrderDisplayControl.DisplayStatus.Fail);
                control.SetStatusMessage("Transfer failed");
            }
        }
    }
}
