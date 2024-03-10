using System;
using System.Collections.Generic;
using System.Linq;
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
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Windows.Markup;

namespace GU_Exchange.Views
{
    /// <summary>
    /// Interaction logic for PlayerLookupWindow.xaml
    /// </summary>
    public partial class PlayerLookupWindow : Window
    {
        private static Dictionary<int, string> AccountCache = new();
        public enum LookupResult { Select, Cancel };

        private List<string> _playerNameList;
        private string? _currentSuggestion;
        private CancellationTokenSource? _nameTokenSource = null;


        private string _currentInput = "";
        
        private string _currentText = "";

        private int _selectionStart;
        private int _selectionLength;
        public LookupResult Result { private set; get; }

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

        private async Task<string> FetchAccountData(int apolloID, CancellationToken token)
        {
            if (AccountCache.ContainsKey(apolloID))
                return AccountCache[apolloID];
            byte[] data = await ResourceManager.Client.GetByteArrayAsync($"https://apollo-auth.prod.prod.godsunchained.com/v2/account/{apolloID}", token);
            string result = System.Text.Encoding.UTF8.GetString(data);
            AccountCache.Add(apolloID, result);
            return result;
        }

        private async Task UpdateAccountData(int apolloID, CancellationToken token)
        {
            try {
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

        private void tbSearchBar_TextChanged(object sender, EventArgs e)
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

        private void cbSearchType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbSearchType.SelectedIndex == 0)
            {
                tbSearchBar.SuggestionList = _playerNameList;
            } else
            {
                tbSearchBar.SuggestionList = new List<string>();
            }
        }

        private async void cbApolloID_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void cbAddresses_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnSelect.IsEnabled = cbAddresses.SelectedItem != null;
        }

        private void btnSelect_Click(object sender, RoutedEventArgs e)
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

        public string? GetSelectedAddress()
        {
            return (string?) cbAddresses.SelectedItem;
        }
    }
}
