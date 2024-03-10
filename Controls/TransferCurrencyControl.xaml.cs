using GU_Exchange.Helpers;
using GU_Exchange.Views;
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
using static GU_Exchange.Helpers.IMXlib;

namespace GU_Exchange.Controls
{
    /// <summary>
    /// Interaction logic for TransferCurrencyControl.xaml
    /// </summary>
    public partial class TransferCurrencyControl : UserControl
    {
        #region Default Constructor
        public TransferCurrencyControl()
        {
            InitializeComponent();

            List<string> items = new List<string> { "ETH", "GODS", "IMX" };
            cbCurrency.ItemsSource = items;
            cbCurrency.SelectedIndex = 0;
        }
        #endregion

        #region Event Handlers
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
            // Click occurred outside controlGrid, you can call your function here
            ((MainWindow)Application.Current.MainWindow).CloseOverlay();
        }

        /// <summary>
        /// Signal to the main window to close this <see cref="UserControl"/> when the close button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            ((MainWindow)Application.Current.MainWindow).CloseOverlay();
        }

        /// <summary>
        /// Update the transfer amount to the maximum amount possible.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BtnMax_Click(object sender, RoutedEventArgs e)
        {
            Wallet? wallet = Wallet.GetConnectedWallet();
            if (wallet == null)
            {
                return;
            }
            decimal amount = await wallet.GetTokenAmountAsync((string)cbCurrency.SelectedItem);
            tbAmount.Text = amount.ToString();
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
            Console.WriteLine(window.GetSelectedAddress());
        }

        /// <summary>
        /// Verify if the data entered is valid if the amount is modified.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void TbAmount_TextChanged(object sender, TextChangedEventArgs e)
        {
            await ValidateContent();
        }

        /// <summary>
        /// Verify if the data entered is valid if the address is modified.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void TbAddress_TextChanged(object sender, TextChangedEventArgs e)
        {
            await ValidateContent();
        }

        /// <summary>
        /// Execute the currency transfer.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BtnTransfer_Click(object sender, RoutedEventArgs e)
        {
            userChoicePanel.Visibility = Visibility.Collapsed;
            loadingPanel.Visibility = Visibility.Visible;

            // Calculate the value of the transfer, if it's above a certain amount, warn the user.
            Dictionary<string, Token> tokens = await Wallet.FetchTokens();
            Token token = tokens[(string)cbCurrency.SelectedItem];

            decimal? ConversionRate = token.Value;
            if (ConversionRate != null)
            {
                decimal transferValue = decimal.Parse(tbAmount.Text) * (decimal)ConversionRate;
                decimal transferWarningLimit = Settings.GetTransferWarningLimit();
                if (transferValue > transferWarningLimit)
                {
                    tbStatus.Text = "Waiting for user confirmation...";
                    MessageWindow window = new MessageWindow($"You are about to transfer {transferValue.ToString("#.##")} USD worth of {(string)cbCurrency.SelectedItem} to '{tbAddress.Text}'\nAre you sure you want to continue?", "Confirm Transfer", MessageType.CONFIRM);
                    window.Owner = (MainWindow)Application.Current.MainWindow;
                    window.ShowDialog();
                    if (!window.Result)
                    {
                        spinner.Visibility = Visibility.Collapsed;
                        error.Visibility = Visibility.Visible;
                        btnClose.Visibility = Visibility.Visible;
                        tbStatus.Text = "Transfer cancelled";
                        return;
                    }
                }
            }
            else
            {
                tbStatus.Text = "Waiting for user confirmation...";
                MessageWindow window = new MessageWindow($"You are about to transfer {tbAmount.Text} {(string)cbCurrency.SelectedItem} to '{tbAddress.Text}'\nAre you sure you want to continue?", "Confirm Transfer", MessageType.CONFIRM);
                window.Owner = (MainWindow)Application.Current.MainWindow;
                window.ShowDialog();
                if (!window.Result)
                {
                    spinner.Visibility = Visibility.Collapsed;
                    error.Visibility = Visibility.Visible;
                    btnClose.Visibility = Visibility.Visible;
                    tbStatus.Text = "Transfer cancelled";
                    return;
                }
            }

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
            double transferAmount;
            try
            {
                transferAmount = double.Parse((string)tbAmount.Text);
            }
            catch (FormatException)
            {
                // Transfer failed? Shouldn't happen.
                spinner.Visibility = Visibility.Collapsed;
                error.Visibility = Visibility.Visible;
                btnClose.Visibility = Visibility.Visible;
                tbStatus.Text = "Transfer amount is invalid";
                return;
            }

            bool result = await wallet.RequestTransferCurrency(Application.Current.MainWindow, token, transferAmount, this.tbAddress.Text, this.tbStatus);
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
                wallet.DeductTokenAmount(token.Name, new decimal(transferAmount));
                await ((MainWindow)Application.Current.MainWindow).RefreshWalletInfoAsync();
                success.Visibility = Visibility.Visible;
                btnClose.Visibility = Visibility.Visible;
            }
        }
        #endregion

        #region Supporting Functions
        /// <summary>
        /// Verify that the data entered in this <see cref="UserControl"/> is valid.
        /// </summary>
        /// <returns></returns>
        private async Task ValidateContent()
        {
            try
            {
                Wallet? wallet = Wallet.GetConnectedWallet();
                if (!Wallet.IsValidEthereumAddress(tbAddress.Text) || wallet == null)
                {
                    btnTransfer.IsEnabled = false;
                    return;
                }
                decimal amount = decimal.Parse(tbAmount.Text);
                decimal maxAmount = await wallet.GetTokenAmountAsync((string)cbCurrency.SelectedItem);
                if (amount <= 0 || amount > maxAmount)
                {
                    btnTransfer.IsEnabled = false;
                    return;
                }
                btnTransfer.IsEnabled = true;
            }
            catch (System.Net.Http.HttpRequestException)
            {
            }
            catch (NullReferenceException)
            {
            }
            catch (FormatException)
            {
                btnTransfer.IsEnabled = false;
            }
        }
        #endregion
    }
}
