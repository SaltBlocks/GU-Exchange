using GU_Exchange.Helpers;
using System.Windows;

namespace GU_Exchange
{
    /// <summary>
    /// Used to indicate the type of message to show the use, the CONFIRM type allows the user to confirm or cancel an action, INFORM only shows the provided message.
    /// </summary>
    public enum MessageType
    {
        CONFIRM, INFORM
    }

    /// <summary>
    /// Interaction logic for MessageWindow.xaml
    /// </summary>
    public partial class MessageWindow : Window
    {
        #region Class Properties.
        public bool Result { get; private set; }
        private string _askAgainOptionName;
        #endregion
        #region Default Constructor.
        /// <summary>
        /// Creates a message of the provided type with the given text and title.
        /// </summary>
        /// <param name="text">The text to display</param>
        /// <param name="title">The window title</param>
        /// <param name="type">The type of message (confirm dialog or information only)</param>
        public MessageWindow(string text, string title, MessageType type, bool askAgainOption = false, string askAgainOptionName = "")
        {
            InitializeComponent();
            tbInfo.Text = text;
            Title = title;
            _askAgainOptionName =askAgainOptionName;
            Result = false;
            
            if (type == MessageType.CONFIRM)
            {
                btnClose.Visibility = Visibility.Collapsed;
            } else
            {
                btnYes.Visibility = Visibility.Collapsed;
                btnNo.Visibility = Visibility.Collapsed;
            }

            if (askAgainOption && _askAgainOptionName != "")
            {
                chckShowAgain.Visibility = Visibility.Visible;
            }
        }
        #endregion
        #region Event Handlers.
        /// <summary>
        /// Close the window and indicate that the user wishes to continue.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            bool? dontAskAgain = chckShowAgain.IsChecked;
            if (_askAgainOptionName != "" && dontAskAgain != null && dontAskAgain == true)
            {
                Settings.SetSetting(_askAgainOptionName, "True");
                Settings.SaveSettings();
            }
            Close();
        }

        /// <summary>
        /// Close the window without indicating that the user wishes to continue.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            bool? dontAskAgain = chckShowAgain.IsChecked;
            if (_askAgainOptionName != "" && dontAskAgain != null && dontAskAgain == true)
            {
                Settings.SetSetting(_askAgainOptionName, "True");
                Settings.SaveSettings();
            }
            Close();
        }
        #endregion
    }
}
