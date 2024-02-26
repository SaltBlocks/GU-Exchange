using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
        #endregion
        #region Default Constructor.
        /// <summary>
        /// Creates a message of the provided type with the given text and title.
        /// </summary>
        /// <param name="text">The text to display</param>
        /// <param name="title">The window title</param>
        /// <param name="type">The type of message (confirm dialog or information only)</param>
        public MessageWindow(string text, string title, MessageType type)
        {
            InitializeComponent();
            tbInfo.Text = text;
            Title = title;
            Result = false;
            
            if (type == MessageType.CONFIRM)
            {
                btnClose.Visibility = Visibility.Collapsed;
            } else
            {
                btnYes.Visibility = Visibility.Collapsed;
                btnNo.Visibility = Visibility.Collapsed;
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
            Close();
        }
        #endregion
    }
}
