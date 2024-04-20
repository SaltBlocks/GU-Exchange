using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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
using System.Windows.Shapes;
using GU_Exchange.Helpers;
using Microsoft.VisualBasic;
using Serilog;

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for SetupWindow.xaml
    /// </summary>
    public partial class SetupWindow : Window
    {
        #region Default Constructor
        /// <summary>
        /// Default constructor for a window that allows the user to change the connected GU account and wallet.
        /// </summary>
        public SetupWindow()
        {
            InitializeComponent();
            Setup();
        }
        #endregion
        #region Window setup.
        /// <summary>
        /// Setup the UI to show the currently connected GU account and wallets.
        /// </summary>
        private async void Setup()
        {
            int apolloId = Settings.GetApolloID();
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            Task<string> getUsername = GameDataManager.FetchPlayerNameAsync(apolloId, cancellationTokenSource.Token);
            cbWallets.Items.Add("None");
            Wallet? connectedWallet = Wallet.GetConnectedWallet();
            if (connectedWallet != null)
            {
                string address = connectedWallet.Address;
                string info = $"{address.Substring(0, 6)}....{address.Substring(address.Length - 4, 4)}";
                if (connectedWallet is WebWallet)
                    info += " (Webwallet)";
                cbWallets.Items.Add(info);
                cbWallets.SelectedIndex = 1;

            } else
            {
                cbWallets.SelectedIndex = 0;
            }
            foreach (Wallet wallet in Wallet.wallets.Values)
            {
                if (wallet == connectedWallet)
                    continue;
                string address = wallet.Address;
                string info = $"{address.Substring(0, 6)}....{address.Substring(address.Length - 4, 4)}";
                if (wallet is WebWallet)
                    info += " (Webwallet)";
                cbWallets.Items.Add(info);
            }
            string selectedWallet = (string)cbWallets.SelectedItem;
            if (selectedWallet.Contains("Webwallet") || selectedWallet.Equals("None"))
            {
                btnExport.IsEnabled = false;
            }
            try
            {
                txtLinkedAccount.Text = await getUsername;
            }
            catch (HttpRequestException e)
            {
                Log.Warning($"Failed to fetch connected username. {e.Message}: {e.StackTrace}");
            }
        }
        #endregion
        #region GU event handlers.
        /// <summary>
        /// Open the usercontrol needed for searching for and linking a GU account.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LinkAccount_Click(object sender, RoutedEventArgs e)
        {
            this.childGrid.Visibility = Visibility.Collapsed;
            LinkAccountControl link = new LinkAccountControl(this);
            link.Margin = new Thickness(0, 0, 0, 0);
            this.setupGrid.Children.Add(link);
        }
        #endregion
        #region Wallet event handlers.
        /// <summary>
        /// Allow the user to create a new wallet or link an existing one to GU Exchange.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LinkWallet_Click(object sender, RoutedEventArgs e)
        {
            this.childGrid.Visibility = Visibility.Collapsed;
            SetupWalletControl link = new(this)
            {
                Margin = new Thickness(0, 0, 0, 0)
            };
            this.setupGrid.Children.Add(link);
        }

        /// <summary>
        /// Export the private key of the selected wallet.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExportWallet_Click(object sender, RoutedEventArgs e)
        {
            Wallet? wallet = GetWalletFromPartialAddress((string)cbWallets.SelectedValue);
            if (wallet == null)
                return;
            MessageWindow window = new MessageWindow($"This will copy the selected wallets private key into your clipboard, continue?", "Export wallet", MessageType.CONFIRM);
            window.Owner = this;
            window.ShowDialog();
            if (!window.Result)
            {
                return;
            }

            bool wasLocked = wallet.IsLocked();
            UnlockWalletWindow unlockWindow = new UnlockWalletWindow(wallet, false)
            {
                Owner = this
            };
            unlockWindow.ShowDialog();
            if (unlockWindow.Result != UnlockWalletWindow.UnlockResult.Unlock)
            {
                return;
            }
            Clipboard.SetText(wallet.GetPrivateKey());
            if (wasLocked)
                wallet.LockWallet();
            window = new MessageWindow($"Your private key was copies to your clipboard, use Ctrl + V to paste it.", "Export wallet", MessageType.INFORM);
            window.Owner = this;
            window.ShowDialog();
        }

        /// <summary>
        /// Delete the currently selected wallet from the available wallets list and remove the file from disk.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void DeleteWallet_Click(object sender, RoutedEventArgs e)
        {
            string selectedWallet = (string)cbWallets.SelectedValue;
            if (selectedWallet.Length < 13)
                return;

            foreach (string wallet in Wallet.wallets.Keys)
            {
                if (wallet.StartsWith(selectedWallet.Substring(0, 6)) && wallet.EndsWith(selectedWallet.Substring(10, 4)))
                {
                    MessageWindow window = new MessageWindow($"This will delete the following wallet: '{selectedWallet.Substring(0, 13)}'.\nWithout a backup of the wallets private key, its contents will be lost forever. Continue?", "Delete wallet", MessageType.CONFIRM);
                    window.Owner = this;
                    window.ShowDialog();
                    if (!window.Result)
                    {
                        return;
                    }

                    if (Wallet.GetConnectedWallet() == Wallet.wallets[wallet])
                    {
                        await Wallet.SetConnectedWallet(null);
                        Settings.SetSetting("ConnectedWallet", "None");
                        Settings.SaveSettings();
                        await((MainWindow)Application.Current.MainWindow).SetupWalletInfoAsync();
                    }
                    cbWallets.Items.Remove(cbWallets.SelectedValue);
                    cbWallets.SelectedIndex = 0;
                    Wallet.wallets.Remove(wallet);

                    try
                    {
                        string[] walletFiles = Directory.GetFiles(System.IO.Path.Combine(Settings.GetConfigFolder(), "wallets"), "*.wlt");
                        foreach (string walletFile in walletFiles)
                        {
                            try
                            {
                                Wallet wlt = Wallet.LoadWallet(File.Open(walletFile, FileMode.Open));
                                if (wlt.Address.Equals(wallet))
                                {
                                    File.Delete(walletFile);
                                    Log.Information($"Wallet {wlt.Address} was removed from disk.");
                                    break;
                                }
                            }
                            catch (Exception e1)
                            {
                                Log.Warning($"Failed to load and remove wallet: {e1.StackTrace}");
                                continue;
                            }
                        }
                    }
                    catch (DirectoryNotFoundException)
                    { }
                    return;
                }
            }
        }
        #endregion
        #region Other event handlers.
        /// <summary>
        /// Make the user selected wallet the one that is used when trading.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            string selectedWallet = (string) cbWallets.SelectedValue;
            if (selectedWallet.Length < 13)
            {
                await Wallet.SetConnectedWallet(null);
                Settings.SetSetting("ConnectedWallet", "None");
                Settings.SaveSettings();
                await ((MainWindow)Application.Current.MainWindow).SetupWalletInfoAsync();
                return;
            }

            foreach (string wallet in Wallet.wallets.Keys)
            {
                if (wallet.StartsWith(selectedWallet.Substring(0, 6)) && wallet.EndsWith(selectedWallet.Substring(10, 4)))
                {
                    await Wallet.SetConnectedWallet(Wallet.wallets[wallet]);
                    Settings.SetSetting("ConnectedWallet", Wallet.wallets[wallet].Address);
                    Settings.SaveSettings();
                    ((MainWindow)Application.Current.MainWindow).CloseOverlay();
                    await ((MainWindow)Application.Current.MainWindow).SetupWalletInfoAsync();
                    return;
                }
            }
        }

        /// <summary>
        /// Export the wallets private key by putting it in the users clipboard. (always requests a password even if the wallet is unlocked).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WalletSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                string selectedWallet = (string)cbWallets.SelectedItem;
                if (selectedWallet.Contains("Webwallet") || selectedWallet.Equals("None"))
                {
                    btnExport.IsEnabled = false;
                }
                else
                {
                    btnExport.IsEnabled = true;
                }
            } catch (NullReferenceException)
            {

            }
        }

        /// <summary>
        /// Close the window.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        #endregion
        #region Supporting methods.
        /// <summary>
        /// Find the selected wallet from the list of connected wallets.
        /// </summary>
        /// <param name="partialAddress">The abbreviated address (0.xxxx....xxxx)</param>
        /// <returns></returns>
        private static Wallet? GetWalletFromPartialAddress(string partialAddress)
        {
            if (partialAddress.Length < 13)
                return null;

            foreach (string wallet in Wallet.wallets.Keys)
            {
                if (wallet.StartsWith(partialAddress.Substring(0, 6)) && wallet.EndsWith(partialAddress.Substring(10, 4)))
                {
                    return Wallet.wallets[wallet];
                }
            }

            return null;
        }
        #endregion
    }
}
