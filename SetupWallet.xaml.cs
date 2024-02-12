using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
using static GU_Exchange.IMXlib;

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for SetupWallet.xaml
    /// </summary>
    public partial class SetupWallet : UserControl
    {
        private string _privateKey;
        private Window parent;

        public SetupWallet(Window parent)
        {
            InitializeComponent();
            this.parent = parent;
            IntPtr keyBuffer = Marshal.AllocHGlobal(67);
            string? result = IntPtrToString(eth_generate_key(keyBuffer, 67));
            Marshal.FreeHGlobal(keyBuffer);
            if (result == null)
                throw new NullReferenceException("IMXLib returned a null reference while generating an address.");
            _privateKey = result;

            IntPtr addressBuffer = Marshal.AllocHGlobal(43);
            string? address = IntPtrToString(eth_get_address(_privateKey, addressBuffer, 43));
            if (address == null)
                throw new NullReferenceException("IMXLib returned a null reference while generating an address.");
            tbAddress.Text = address;
        }

        #region Event Handlers for starting window.
        /// <summary>
        /// Close the window if the close button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            SignatureRequestServer.CancelRequests();
            parent.Close();
        }

        /// <summary>
        /// Go to the next screen to add a wallet.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (rbCreateWallet.IsChecked == true)
            {
                createGrid.Visibility = Visibility.Visible;
                optionGrid.Visibility = Visibility.Collapsed;
            }
            else if (rbImportKey.IsChecked == true)
            {
                importGrid.Visibility = Visibility.Visible;
                optionGrid.Visibility = Visibility.Collapsed;
            }
            else if (rbImportWeb.IsChecked == true)
            {
                tbLink.Text = $"http://localhost:{SignatureRequestServer.ClientPort}/";
                webImportGrid.Visibility = Visibility.Visible;
                optionGrid.Visibility = Visibility.Collapsed;
                await ImportWebWallet();
            }
        }
        #endregion

        #region Handlers for creating a new wallet.
        /// <summary>
        /// Generate a random new wallet address.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Regenerate_Click(object sender, RoutedEventArgs e)
        {
            IntPtr keyBuffer = Marshal.AllocHGlobal(67);
            string? result = IntPtrToString(eth_generate_key(keyBuffer, 67));
            Marshal.FreeHGlobal(keyBuffer);
            if (result == null)
                throw new NullReferenceException("IMXLib returned a null reference while generating an address.");
            _privateKey = result;

            IntPtr addressBuffer = Marshal.AllocHGlobal(43);
            string? address = IntPtrToString(eth_get_address(_privateKey, addressBuffer, 43));
            if (address == null)
                throw new NullReferenceException("IMXLib returned a null reference while generating an address.");
            tbAddress.Text = address;
        }

        /// <summary>
        /// Try to securely store the generated wallet using the password entered by the user.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void StoreWalletButton_Click(object sender, RoutedEventArgs e)
        {
            if (pbPassword.Password.Length < 8)
            {
                this.txtError.Text = "Password should be at least 8 characters.";
                return;
            }
            if (!pbPassword.Password.Equals(pbPasswordRepeat.Password))
            {
                this.txtError.Text = "Passwords don't match.";
                return;
            }
            txtError.Text = "";
            btnAddWallet.IsEnabled = false;
            if (!Directory.Exists("wallets"))
            {
                Directory.CreateDirectory("wallets");
            }
            Wallet wallet = new Wallet(_privateKey, pbPassword.Password);
            using (FileStream FS = new FileStream($"wallets\\{tbAddress.Text}.wlt", FileMode.Create))
            {
                Wallet.SaveWallet(wallet, FS);
                Settings.SetSetting("ConnectedWallet", tbAddress.Text);
                Settings.SaveSettings();
                await Wallet.SetConnectedWallet(wallet);
            }
            try
            {
                Wallet.wallets.Add(wallet.Address, wallet);
            }
            catch (Exception e1)
            {
                Console.WriteLine(e1.StackTrace);
            }
            await ((MainWindow)Application.Current.MainWindow).SetupWalletInfoAsync();
            parent.Close();
        }
        #endregion

        #region Handlers for importing an existing wallet.
        /// <summary>
        /// Check if a valid private key is entered.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pbPrivateKey_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _privateKey = this.pbPrivateKey.Password;
            if (_privateKey.Length == 0)
            {
                lblAddressImport.Text = "";
                txtErrorImport.Text = "";
                this.btnImportWallet.IsEnabled = false;
                return;
            }
            if (_privateKey.StartsWith("0x"))
                _privateKey = _privateKey.Substring(2);
            if (IsHexadecimal(_privateKey))
            {
                IntPtr addressBuffer = Marshal.AllocHGlobal(43);
                string? address = IntPtrToString(eth_get_address("0x" + _privateKey, addressBuffer, 43));
                if (address == null)
                    throw new NullReferenceException("IMXLib returned a null reference while generating an address.");
                lblAddressImport.Text = address;
                txtErrorImport.Text = "";
                this.btnImportWallet.IsEnabled = true;
            }
            else
            {
                lblAddressImport.Text = "";
                txtErrorImport.Text = "The provided private key is not a valid hexadecimal number.";
                this.btnImportWallet.IsEnabled = false;
            }
        }

        /// <summary>
        /// Try to import the private key entered by the user and secure it using the entered password.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void btnImportWallet_Click(object sender, RoutedEventArgs e)
        {
            if (pbPasswordImport.Password.Length < 8)
            {
                this.txtErrorImport.Text = "Password should be at least 8 characters.";
                return;
            }
            if (!pbPasswordRepeatImport.Password.Equals(pbPasswordImport.Password))
            {
                this.txtErrorImport.Text = "Passwords don't match.";
                return;
            }
            btnImportWallet.IsEnabled = false;
            if (!Directory.Exists("wallets"))
            {
                Directory.CreateDirectory("wallets");
            }
            Wallet wallet = new Wallet("0x" + _privateKey, pbPasswordImport.Password);
            using (FileStream FS = new FileStream($"wallets\\{wallet.Address}.wlt", FileMode.Create))
            {
                Wallet.SaveWallet(wallet, FS);
                Settings.SetSetting("ConnectedWallet", wallet.Address);
                Settings.SaveSettings();
                await Wallet.SetConnectedWallet(wallet);
            }
            try
            {
                Wallet.wallets.Add(wallet.Address, wallet);
            }
            catch (Exception e1)
            {
                Console.WriteLine(e1.StackTrace);
            }
            await ((MainWindow)Application.Current.MainWindow).SetupWalletInfoAsync();
            parent.Close();
        }
        #endregion

        #region Handlers for importing a web wallet.
        private async Task ImportWebWallet()
        {
            SignatureRequestServer.RequestedAddress = "*";
            SignatureRequestServer.StartServer();
            try
            {
                SignatureData data = await SignatureRequestServer.RequestSignatureAsync(IMXlib.IMX_SEED_MESSAGE);
                SignatureRequestServer.RequestedAddress = data.Address;
                WebWallet wallet = new WebWallet(data.Signature, data.Address);
                using (FileStream FS = new FileStream($"wallets\\{wallet.Address}.wlt", FileMode.Create))
                {
                    Wallet.SaveWallet(wallet, FS);
                    Settings.SetSetting("ConnectedWallet", wallet.Address);
                    Settings.SaveSettings();
                    await Wallet.SetConnectedWallet(wallet);
                }
                try
                {
                    Wallet.wallets.Add(wallet.Address, wallet);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                }
                await ((MainWindow)Application.Current.MainWindow).SetupWalletInfoAsync();
                lblWebInstructions.Content = $"Wallet '{data.Address.Substring(0, 6)}...{data.Address.Substring(data.Address.Length - 4, 4)}' imported.";
                btnEnd.Content = "Finish";
                tbLink.Visibility = Visibility.Collapsed;
                spinner.Visibility = Visibility.Collapsed;
                success.Visibility = Visibility.Visible;
            }
            catch (OperationCanceledException)
            {
                await Wallet.SetConnectedWallet(Wallet.GetConnectedWallet());
                lblWebInstructions.Content = $"Wallet import cancelled.";
                btnEnd.Content = "Close";
                tbLink.Visibility = Visibility.Collapsed;
                spinner.Visibility = Visibility.Collapsed;
                error.Visibility = Visibility.Visible;
            }
        }

        private void ClientLink_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                string url = $"http://localhost:{SignatureRequestServer.ClientPort}";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = $"/c start {url}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        #endregion

        #region Supporting functions.
        private bool IsHexadecimal(string value)
        {
            // Check if the string is a valid hexadecimal number.
            return System.Text.RegularExpressions.Regex.IsMatch(value, @"\A\b[0-9a-fA-F]+\b\Z");
        }
        #endregion
    }
}
