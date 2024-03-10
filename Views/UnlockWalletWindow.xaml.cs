using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GU_Exchange.Helpers;

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for UnlockWalletWindow.xaml
    /// </summary>
    public partial class UnlockWalletWindow : Window
    {
        #region Class Properties
        public enum UnlockResult { Unlock, Relock, Cancel };
        /// <summary>
        /// Used to store the result of this window.
        /// </summary>
        public UnlockResult Result { private set; get; }
        public Wallet Wallet { private set; get; }
        #endregion

        #region Default Constructor
        /// <summary>
        /// Construct a window that prompts the user to unlock their wallet.
        /// </summary>
        /// <param name="wallet"></param>
        /// <param name="relockOption"></param>
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
        #endregion

        #region Event Handlers.
        /// <summary>
        /// Handle when the unlock button is pressed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Button_Unlock(object sender, RoutedEventArgs e)
        {
            await AttemptUnlock();
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

        /// <summary>
        /// Try to unlock the wallet if enter is pressed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void TxtPassword_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await AttemptUnlock();
            }
        }
        #endregion

        #region Supporting Functions
        private async Task AttemptUnlock()
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
        #endregion
    }
}
