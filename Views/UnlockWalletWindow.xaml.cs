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
using System.Windows.Shapes;
using GU_Exchange.Helpers;

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for UnlockWalletWindow.xaml
    /// </summary>
    public partial class UnlockWalletWindow : Window
    {
        public enum UnlockResult { Unlock, Relock, Cancel };
        /// <summary>
        /// Used to store the result of this window.
        /// </summary>
        public UnlockResult Result { private set; get; }
        public Wallet Wallet { private set; get; }

        public UnlockWalletWindow(Wallet wallet, bool relockOption = true)
        {
            InitializeComponent();
            Wallet = wallet;
            Result = UnlockResult.Cancel;
            lblAddress.Content = wallet.Address;
            if (!relockOption)
            {
                chkRelock.IsChecked = false;
                lblRelock.Visibility = Visibility.Collapsed;
                chkRelock.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Handle when the unlock button is pressed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Button_Unlock(object sender, RoutedEventArgs e)
        {
            await attemptUnlock();
        }

        /// <summary>
        /// Handle when the cancel button is pressed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Cancel(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void TxtPassword_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await attemptUnlock();
            }
        }

        private async Task attemptUnlock()
        {
            bool result;
            if (Wallet.IsLocked())
            {
                result = await Wallet.UnlockWallet(txtPassword.Password);
            }
            else
            {
                result = await Wallet.CheckPassword(txtPassword.Password);
            }
            if (!result)
            {
                lblTop.Content = "Incorrect password for wallet";
                lblTop.Foreground = Brushes.Red;
                lblAddress.Foreground = Brushes.Red;
                lblBottom.Content = "Please try again.";
                lblBottom.Foreground = Brushes.Red;
                return;
            }
            if (chkRelock.IsChecked == true)
                Result = UnlockResult.Relock;
            else
                Result = UnlockResult.Unlock;
            Close();
        }
    }
}
