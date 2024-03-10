using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using GU_Exchange.Helpers;

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for LinkAccount.xaml
    /// </summary>
    public partial class LinkAccountControl : UserControl
    {
        #region Class Properties
        private Window parent;
        #endregion

        #region Default Constructor
        /// <summary>
        /// Initialize window and load player name suggestions.
        /// </summary>
        /// <param name="parent">The window this UserControl is shown on.</param>
        public LinkAccountControl(Window parent)
        {
            InitializeComponent();
            this.parent = parent;
            this.stbUsername.SuggestionList = GameDataManager.FetchPlayerNames();
        }
        #endregion

        #region Lookup Handlers
        /// <summary>
        /// Attempts to look up a user by their apolloID and fills out the form using the players data.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Button_ApolloLookup(object sender, RoutedEventArgs e)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            try
            {
                string username = await GameDataManager.FetchPlayerNameAsync(int.Parse(this.txtApolloID.Text), cancellationTokenSource.Token);
                this.stbUsername.Text = username;
                this.btnLink.IsEnabled = true;
                this.txtError.Text = String.Empty;
            }
            catch (Exception)
            {
                this.txtError.Text = "Failed to find account.";
            }
        }

        /// <summary>
        /// Attempts to lookup a user using their playername and fills out the form using the players data.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_UserLookup(object sender, RoutedEventArgs e)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            try
            {
                List<int> apolloIDs = GameDataManager.FetchApolloIDs(this.stbUsername.Text);
                if (apolloIDs.Count() == 0)
                {
                    this.txtError.Text = "Failed to find account.";
                    return;
                }
                if (apolloIDs.Count() > 1)
                {
                    this.txtError.Text = "Multiple active users use this username, please link using ApolloID.";
                    return;
                }
                this.txtApolloID.Text = apolloIDs[0].ToString();
                this.btnLink.IsEnabled = true;
                this.txtError.Text = String.Empty;
            }
            catch (Exception)
            {
                this.txtError.Text = "Failed to find account.";
            }
        }
        #endregion

        #region Link user account
        /// <summary>
        /// Link the selected user to GU Exchange.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BtnLink_Click(object sender, RoutedEventArgs e)
        {
            Settings.SetApolloID(int.Parse(this.txtApolloID.Text));
            Settings.SaveSettings();
            btnCancel.IsEnabled = false;
            btnLink.IsEnabled = false;
            btnLookup1.IsEnabled = false;
            btnLookup2.IsEnabled = false;
            await ((MainWindow)Application.Current.MainWindow).SetupInventoryAsync();
            parent.Close();
        }
        #endregion

        #region Other Event Handlers
        /// <summary>
        /// Close the parent window.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Close(object sender, RoutedEventArgs e)
        {
            parent.Close();
        }
        
        /// <summary>
        /// Disable link button if user edits the apolloID.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtApolloIDChanged(object sender, TextChangedEventArgs e)
        {
            this.btnLink.IsEnabled = false;
        }

        /// <summary>
        /// Disable link button if user edits the playername.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtUsernameChanged(object sender, EventArgs e)
        {
            this.btnLink.IsEnabled = false;
        }
        #endregion
    }
}
