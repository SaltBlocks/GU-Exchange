using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace GU_Exchange
{
    /// <summary>
    /// Class used for storing the settings for the application.
    /// </summary>
    internal class Settings
    {
        /// <summary>
        /// Reference to the global settings object.
        /// </summary>
        public static Settings globalSettings = loadSettings();
        /// <summary>
        /// THe apolloID linked to the application.
        /// </summary>
        public int apolloID { get; set; }
        /// <summary>
        /// The wallet address currently linked to the application.
        /// </summary>
        public string walletAddress { get; set; }

        /// <summary>
        /// Constructs a new settings object with the given apolloID and wallet address.
        /// </summary>
        /// <param name="apolloID">The apollo ID</param>
        /// <param name="walletAddress">The wallet address</param>
        public Settings(int apolloID, string walletAddress)
        {
            this.apolloID = apolloID;
            this.walletAddress = walletAddress;
        }

        /// <summary>
        /// Loads the settings from the disk.
        /// </summary>
        /// <returns>The settings object stored on the disk.</returns>
        public static Settings loadSettings()
        {
            int apolloID = 0;
            string walletAddress = "";
            try
            {
                StreamReader sr = new StreamReader("settings.txt");
                string? line = sr.ReadLine();
                //Continue to read until you reach end of file
                while (line != null)
                {
                    Console.WriteLine(line);
                    //write the line to console window
                    if (line.ToLower().StartsWith("apolloid"))
                    {
                        apolloID = int.Parse(line.Split("=")[1]);
                    }
                    else if (line.ToLower().StartsWith("wallet"))
                    {
                        walletAddress = line.Split("=")[1];
                    }
                    //Read the next line
                    line = sr.ReadLine();
                }
                //close the file
                sr.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
            }
            return new Settings(apolloID, walletAddress);
        }

        /// <summary>
        /// Saves the programs settings to the disk.
        /// </summary>
        /// <param name="settings"></param>
        public static void saveSettings(Settings settings)
        {
            try
            {
                StreamWriter sw = new StreamWriter("settings.txt");
                sw.WriteLine($"apolloID={settings.apolloID}");
                sw.WriteLine($"Wallet={settings.walletAddress}");
                sw.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message);
            }
        }
    }

        /// <summary>
        /// Interaction logic for SetupWindow.xaml
        /// </summary>
        public partial class SetupWindow : Window
    {
        /// <summary>
        /// Creates a new window that the user can use to change the application settings.
        /// </summary>
        public SetupWindow()
        {
            InitializeComponent();
            this.txtApolloID.Text = Settings.globalSettings.apolloID.ToString();
            this.cbWallets.Items.Add("None");
            foreach (string walletAddress in Wallet.wallets.Keys)
            {
                Console.WriteLine(walletAddress);
                this.cbWallets.Items.Add(walletAddress);
            }
            this.cbWallets.Items.Add("Add wallet");
            if (Wallet.connectedWallet == null)
            {
                Console.WriteLine("No connected wallet");
                this.cbWallets.SelectedIndex = 0;
                return;
            }
            for (int i = 0; i < this.cbWallets.Items.Count; i++)
            {
                Console.WriteLine(this.cbWallets.Items.GetItemAt(i));
                Console.WriteLine(Wallet.connectedWallet.address);
                Console.WriteLine(this.cbWallets.Items.GetItemAt(i).Equals(Wallet.connectedWallet.address));
                if (this.cbWallets.Items.GetItemAt(i).Equals(Wallet.connectedWallet.address))
                {
                    Console.WriteLine(i);
                    this.cbWallets.SelectedIndex = i;
                    break;
                }
            }
            
        }

        /// <summary>
        /// Update the address to the ETH address associated with the provided private key in the txtETHKey <see cref="PasswordBox"/>.
        /// </summary>
        private void updateAddress()
        {
            string text = this.txtETHKey.Password;
            if (!text.StartsWith("0x"))
            {
                text = "0x" + text;
            }
            bool isHex = System.Text.RegularExpressions.Regex.IsMatch(text.Substring(2), @"\A\b[0-9a-fA-F]+\b\Z");
            if (isHex)
            {
                char[] address = new char[43];
                Wallet.eth_get_address(text.ToCharArray(), address, address.Length);
                this.txtAddress.Text = new string(address);
            }
            else
            {
                if (this.txtAddress != null)
                {
                    this.txtAddress.Clear();
                }
            }
        }

        /// <summary>
        /// Used to generate random wallets and delete saved wallets.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Wallet_Button_Click(object sender, RoutedEventArgs e)
        {
            string selectedItem = (string)this.cbWallets.SelectedItem;
            if (selectedItem.Equals("Add wallet"))
            {
                char[] data = new char[65];
                Wallet.eth_generate_key(data, data.Length);
                this.txtETHKey.Password = new string(data).Trim('\0');
                updateAddress();
            }
            else
            {
                MessageBoxResult res = MessageBox.Show($"Delete wallet '{selectedItem}'?", "Delete wallet", MessageBoxButton.YesNo);
                if (res == MessageBoxResult.No)
                {
                    return;
                }
                Console.WriteLine(selectedItem);
                Console.WriteLine(Wallet.wallets.Remove(selectedItem));
                this.cbWallets.Items.Remove(selectedItem);
                this.cbWallets.SelectedIndex = 0;
                if (Wallet.connectedWallet != null && Wallet.connectedWallet.address.Equals(selectedItem))
                {
                    Wallet.connectedWallet = null;
                }
                string[] walletFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.wlt");
                foreach (string file in walletFiles)
                {
                    try
                    {
                        Wallet wlt = Wallet.loadWallet(File.Open(file, FileMode.Open));
                        if (wlt.address.Equals(selectedItem))
                        {
                            File.Delete(file);
                            break;
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }
        }

        /// <summary>
        /// Update the address field when a characted is typed in the ETH key <see cref="PasswordBox"/>.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtETHKey_PasswordChanged(object sender, RoutedEventArgs e)
        {
            updateAddress();
        }

        /// <summary>
        /// Save the current settings.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Button_Save(object sender, RoutedEventArgs e)
        {
            bool isIntString = txtApolloID.Text.All(char.IsDigit);
            int apolloID = Settings.globalSettings.apolloID;
            try
            {
                apolloID = int.Parse(txtApolloID.Text);
            }
            catch (FormatException)
            {
                Console.WriteLine("Invalid ApolloID entered, kept previous ID.");
                this.txtApolloID.Text = Settings.globalSettings.apolloID.ToString();
                // Invalid ID entered.
                return;
            }
            string walletAddress = Settings.globalSettings.walletAddress;
            if (this.cbWallets.SelectedItem.Equals("None"))
            {
                walletAddress = "";
                Wallet.connectedWallet = null;
            }
            else if (this.cbWallets.SelectedItem.Equals("Add wallet"))
            {
                string ethKey = this.txtETHKey.Password;
                if (ethKey.Length == 0)
                {
                    Console.WriteLine("No key entered to add.");
                    return;
                }
                if (!ethKey.StartsWith("0x"))
                {
                    ethKey = "0x" + ethKey;
                }
                bool isHex = System.Text.RegularExpressions.Regex.IsMatch(ethKey.Substring(2), @"\A\b[0-9a-fA-F]+\b\Z");
                if (!isHex)
                {
                    Console.WriteLine("Invalid private key entered.");
                    return;
                }

                string password = this.txtPassword.Password;
                string passwordRepeat = this.txtPasswordRepeat.Password;
                if (!password.Equals(passwordRepeat))
                {
                    Console.WriteLine("Passwords don't match.");
                    return;
                }
                if (password.Length == 0)
                {
                    Console.WriteLine("Not using a password.");
                    //No password warning.
                }
                if (password.Length < 8)
                {
                    Console.WriteLine("Using a short password.");
                    //Short password warning.
                }
                

                Wallet wallet = new Wallet(ethKey, password);
                walletAddress = wallet.address.ToString();
                this.cbWallets.Items.Insert(this.cbWallets.Items.Count - 1, walletAddress);
                Console.WriteLine(walletAddress);
                Wallet.saveWallet(wallet, File.Open($"wallet_{walletAddress}.wlt", FileMode.Create));
                Wallet.connectedWallet = wallet;
                Wallet.wallets.Add(walletAddress, wallet);
            }
            else
            {
                walletAddress = (string) this.cbWallets.SelectedItem;
                Wallet.connectedWallet = Wallet.wallets[walletAddress];
            }

            await Inventory.connectedInventory.setApolloID(apolloID);
            Settings.globalSettings = new Settings(apolloID, walletAddress);
            Settings.saveSettings(Settings.globalSettings);
            this.lblMessage.Content = "Saved.";
        }

        /// <summary>
        /// React to the selected wallet being changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cbWallets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.txtAddress.Clear();
            this.txtETHKey.Clear();
            this.txtPassword.Clear();
            this.txtPasswordRepeat.Clear();
            this.btnWalletAction.IsEnabled = true;
            if (this.cbWallets.SelectedIndex == this.cbWallets.Items.Count - 1)
            {
                this.txtETHKey.IsEnabled = true;
                this.txtPassword.IsEnabled = true;
                this.txtPasswordRepeat.IsEnabled = true;
                this.btnWalletAction.Content = "Random key";
                Console.WriteLine("Add wallet selected");
            }
            else
            {
                this.txtETHKey.IsEnabled = false;
                this.txtPassword.IsEnabled = false;
                this.txtPasswordRepeat.IsEnabled = false;
                this.btnWalletAction.Content = "Delete wallet";
                Console.WriteLine("Add wallet deselected");
                if (this.cbWallets.SelectedIndex == 0)
                    this.btnWalletAction.IsEnabled = false;
            }
        }
    }
}
