using System.Windows;

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for UnlockWindow.xaml
    /// </summary>
    public partial class UnlockWindow : Window
    {
        /// <summary>
        /// Used to store the result of this window.
        /// </summary>
        public MessageBoxResult result { private set; get; }
        public UnlockWindow(string address)
        {
            InitializeComponent();
            this.lblAddress.Content = address;
            result = MessageBoxResult.Cancel;
        }

        /// <summary>
        /// Get the password entered into the window.
        /// </summary>
        /// <returns></returns>
        public string getEnteredPassword()
        {
            return this.txtPassword.Password;
        }

        /// <summary>
        /// Handle when the unlock button is pressed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Unlock(object sender, RoutedEventArgs e)
        {
            result = MessageBoxResult.OK;
            this.Close();
        }

        /// <summary>
        /// Handle when the cancel button is pressed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Cancel(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        
    }
}
