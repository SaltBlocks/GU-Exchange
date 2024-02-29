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
            Console.WriteLine(result);
            AccountCache.Add(apolloID, result);
            return result;
        }

        private async void SearchApolloID()
        {
            /*var input = this.tbSearchBar.Text;
            try
            {
                int apolloID = int.Parse(input);
                this.cbApolloID.Items.Clear();
                this.cbApolloID.Items.Add(apolloID);
                this.cbApolloID.SelectedIndex = 0;

                if (_nameTokenSource != null)
                    _nameTokenSource.Cancel();
                _nameTokenSource = new CancellationTokenSource();

                string data = await FetchAccountData(apolloID, _nameTokenSource.Token);
                JObject? json = (JObject?)JsonConvert.DeserializeObject(data);
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

                Console.WriteLine("Username: " + username);
                Console.WriteLine($"Default address: {default_address}");
                Console.WriteLine("Addresses:");
                foreach (var address in addressList)
                {
                    Console.WriteLine(address);
                }

                if (username == null)
                    return;
                this.txtPlayerName.Text = username;
                this.cbApolloID.Items.Clear();
                this.cbApolloID.Items.Add(apolloID);
                this.cbApolloID.SelectedIndex = 0;


                this.cbAddresses.Items.Clear();
                if (default_address == null)
                    return;
                this.cbAddresses.Items.Add(default_address);
                this.cbAddresses.SelectedIndex = 0;

                foreach (string? address in addressList)
                {
                    if (address != null)
                    {
                        this.cbAddresses.Items.Add(address);
                    }
                }
            }
            catch (FormatException) { }
            catch (OperationCanceledException) { }
            catch (InvalidOperationException) { }
            catch (HttpRequestException) { }*/
        }

        private void SearchUserName()
        {
            var input = this.tbSearchBar.Text;
            if (input.Length > _currentInput.Length)
            {
                _playerNameList.FirstOrDefault(x => x.StartsWith(input));
                string? suggestedName = _playerNameList.FirstOrDefault(x => x.StartsWith(input));
                if (suggestedName == null)
                {
                    txtPlayerName.Text = "";
                    cbApolloID.Items.Clear();
                    _currentSuggestion = "";
                }
                else if (!suggestedName.Equals(_currentSuggestion))
                {
                    _currentSuggestion = suggestedName;
                    txtPlayerName.Text = suggestedName;
                    List<int> apolloIDs = GameDataManager.FetchApolloIDs(suggestedName);
                    if (apolloIDs.Count > 0)
                    {
                        cbApolloID.Items.Clear();
                        foreach (int apolloID in apolloIDs)
                        {
                            cbApolloID.Items.Add(apolloID);
                        }
                        cbApolloID.SelectedIndex = 0;

                        Console.WriteLine("Should fetch");
                    }
                }
            }
            _currentInput = input;
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
    }
}
