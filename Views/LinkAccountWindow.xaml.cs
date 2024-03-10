using System.Windows;
using GU_Exchange.Helpers;

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for Setup.xaml
    /// </summary>
    public partial class LinkAccountWindow : Window
    {
        #region Default Constructor.
        
        /// <summary>
        /// Setup the window and text message.
        /// </summary>
        public LinkAccountWindow()
        {
            InitializeComponent();
            txtAccountInfo.Text = "You can link your Gods Unchained account to show your in-game inventory while browsing the market.\n\nWould you like to link your account now?";
        }
        #endregion

        #region Event Handlers.
        /// <summary>
        /// Close the window.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void No_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Modify the settings so this prompt will not be shown again.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Never_Click(object sender, RoutedEventArgs e)
        {
            Settings.SetSetting("dont_ask_link", "True");
            Settings.SaveSettings();
            this.Close();
        }

        /// <summary>
        /// Redirect the user to a <see cref="LinkAccountControl"/>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            this.childGrid.Visibility = Visibility.Collapsed;
            LinkAccountControl link = new LinkAccountControl(this);
            link.Margin = new Thickness(0, 0, 0, 0);
            this.setupGrid.Children.Add(link);
        }
        #endregion
    }
}
