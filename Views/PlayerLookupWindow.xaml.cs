using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GU_Exchange.Helpers;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace GU_Exchange.Views
{
    /// <summary>
    /// Interaction logic for PlayerLookupWindow.xaml
    /// </summary>
    public partial class PlayerLookupWindow : Window
    {
        #region Static Fields
        private static Dictionary<int, string> AccountCache = new();
        #endregion

        #region Class Properties
        public enum LookupResult { Select, Cancel };
        public LookupResult Result { private set; get; }
        private CancellationTokenSource? _nameTokenSource = null;
        private readonly List<string> _playerNameList;

        private string? _currentSuggestion;
        private string _currentInput = "";
        #endregion

        #region Default Constructor
        /// <summary>
        /// Constructor for a window allowing the user to find addresses associated with a specified GU player.
        /// </summary>
        public PlayerLookupWindow()
        {
            InitializeComponent();
            cbSearchType.Items.Add("Player name");
            cbSearchType.Items.Add("Apollo ID");
            cbSearchType.SelectedIndex = 0;
            _playerNameList = GameDataManager.FetchPlayerNames();
            tbSearchBar.SuggestionList = _playerNameList;
            Result = LookupResult.Cancel;
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// Update the search when the text in the searchbar is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TbSearchBar_TextChanged(object sender, EventArgs e)
        {
            if (this.cbSearchType.SelectedItem.Equals("Player name"))
            {
                SearchUserName();
            }
            else
            {
                SearchApolloID();
            }
        }

        /// <summary>
        /// Update the search when the search type is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CbSearchType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbSearchType.SelectedIndex == 0)
            {
                tbSearchBar.SuggestionList = _playerNameList;
            } else
            {
                tbSearchBar.SuggestionList = new List<string>();
            }
        }

        /// <summary>
        /// Update the displayed address when the selected apolloID is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CbApolloID_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                cbAddresses.Items.Clear();
                int apolloID = (int)cbApolloID.SelectedItem;
                if (_nameTokenSource != null)
                    _nameTokenSource.Cancel();
                _nameTokenSource = new CancellationTokenSource();
                await UpdateAccountData(apolloID, _nameTokenSource.Token);
            }
            catch (Exception ex)
            {
                if (!(ex is OperationCanceledException))
                    cbAddresses.Items.Clear();
            }
        }

        /// <summary>
        /// Close the window when cancel is pressed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Enable the select button when an address is selected.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CbAddresses_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnSelect.IsEnabled = cbAddresses.SelectedItem != null;
        }

        /// <summary>
        /// Close the window and indicate that an address was selected if select is pressed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            bool dontWarnMultiUser;
            if (!bool.TryParse(Settings.GetSetting("dont_warn_multi_user"), out dontWarnMultiUser) || !dontWarnMultiUser)
            {
                if (cbApolloID.Items.Count > 1)
                {
                    MessageWindow window = new MessageWindow($"Multiple active GU accounts use the playername you entered.\nBefore making a transfer, please make sure to verify that the apolloID {cbApolloID.SelectedItem} belongs to the intended account.", "Multiple valid accounts", MessageType.INFORM, true, "dont_warn_multi_user");
                    window.Owner = this;
                    window.ShowDialog();
                }
            }
            Result = LookupResult.Select;
            Close();
        }
        #endregion

        #region Supporting functions
        /// <summary>
        /// Fetch wallet addresses associated with the provided apolloID.
        /// </summary>
        /// <param name="apolloID"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task<string> FetchAccountData(int apolloID, CancellationToken token)
        {
            if (AccountCache.ContainsKey(apolloID))
                return AccountCache[apolloID];
            byte[] data = await ResourceManager.Client.GetByteArrayAsync($"https://apollo-auth.prod.prod.godsunchained.com/v2/account/{apolloID}", token);
            string result = System.Text.Encoding.UTF8.GetString(data);
            AccountCache.Add(apolloID, result);
            return result;
        }

        /// <summary>
        /// Update the displayed account addresses.
        /// </summary>
        /// <param name="apolloID"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task UpdateAccountData(int apolloID, CancellationToken token)
        {
            try
            {
                string accountData = await FetchAccountData(apolloID, token);
                JObject? json = (JObject?)JsonConvert.DeserializeObject(accountData);
                if (json == null)
                    return;

                string? username = (string?)json.GetValue("username");
                JToken? addresses = json.GetValue("addresses");
                if (addresses == null)
                    return;

                string? default_address = (string?)addresses.
                    Where(a => ((bool?)a["is_default"]) == true).
                    Single()["address"];
                List<string?> addressList = addresses.
                    Where(a => ((bool?)a["is_default"]) == false).
                    Select(a => (string?)a["address"]).
                    ToList();

                if (username == null)
                    return;

                token.ThrowIfCancellationRequested();
                txtPlayerName.Text = username;
                cbAddresses.Items.Clear();
                if (default_address == null)
                    return;
                cbAddresses.Items.Add(default_address);
                cbAddresses.SelectedIndex = 0;
                foreach (string? address in addressList)
                {
                    if (address != null)
                    {
                        cbAddresses.Items.Add(address);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                {
                    throw;
                }
                else
                {
                    cbAddresses.Items.Clear();
                }
            }
        }

        /// <summary>
        /// Search for the current apolloID.
        /// </summary>
        private void SearchApolloID()
        {
            var input = tbSearchBar.Text;
            try
            {
                int apolloID = int.Parse(input);
                cbApolloID.Items.Clear();
                cbApolloID.Items.Add(apolloID);
                cbApolloID.SelectedIndex = 0;
            }
            catch (FormatException) { }
        }

        /// <summary>
        /// Search for the current player name.
        /// </summary>
        private void SearchUserName()
        {
            var input = this.tbSearchBar.Text;
            if (input.Length > _currentInput.Length)
            {
                _playerNameList.FirstOrDefault(x => x.StartsWith(input));
                string? suggestedName = _playerNameList.FirstOrDefault(x => x.StartsWith(input));
                Console.WriteLine(suggestedName);
                if (suggestedName == null)
                {
                    txtPlayerName.Text = "";
                    cbApolloID.Items.Clear();
                    _currentSuggestion = "";
                    _currentInput = input;
                }
                else if (!suggestedName.Equals(_currentSuggestion))
                {
                    txtPlayerName.Text = "";
                    cbApolloID.Items.Clear();
                    cbAddresses.Items.Clear();
                    _currentSuggestion = suggestedName;
                    List<int> apolloIDs = GameDataManager.FetchApolloIDs(suggestedName);
                    if (apolloIDs.Count > 0)
                    {
                        cbApolloID.Items.Clear();
                        foreach (int apolloID in apolloIDs)
                        {
                            cbApolloID.Items.Add(apolloID);
                        }
                        cbApolloID.SelectedIndex = 0;
                        _currentInput = input;
                    }
                }
            }
            else
            {
                _currentInput = input;
            }
        }

        /// <summary>
        /// Public getter for the selected address.
        /// </summary>
        /// <returns></returns>
        public string? GetSelectedAddress()
        {
            return (string?) cbAddresses.SelectedItem;
        }
        #endregion
    }
}
