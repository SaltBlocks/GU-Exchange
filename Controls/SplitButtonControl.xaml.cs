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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GU_Exchange.Controls
{
    /// <summary>
    /// Interaction logic for SplitButtonControl.xaml
    /// </summary>
    public partial class SplitButtonControl : UserControl
    {
        // Define a DependencyProperty for 'Title'
        public static readonly DependencyProperty MainContentProperty =
            DependencyProperty.Register("MainContent", typeof(string), typeof(SplitButtonControl), new PropertyMetadata(string.Empty));

        // Define an event for 'ButtonClick'
        public event RoutedEventHandler? MainButtonClick;

        // Define the CLR property wrapper for 'Title'
        public string MainContent
        {
            get { return (string)GetValue(MainContentProperty); }
            set { SetValue(MainContentProperty, value); }
        }

        public SplitButtonControl()
        {
            InitializeComponent();
        }

        public void AddContextItem(string itemName, RoutedEventHandler handler)
        {
            // Create a new MenuItem
            MenuItem newMenuItem = new MenuItem
            {
                Header = itemName // Set the display text
            };

            // Attach the event handler for the Click event
            newMenuItem.Click += handler;

            // Add the new MenuItem to the ContextMenu
            menu.Items.Add(newMenuItem);
        }

        // Internal Button click event handler that raises the external ButtonClick event
        private void MainButton_Click(object sender, RoutedEventArgs e)
        {
            // Raise the custom ButtonClick event (if subscribed)
            MainButtonClick?.Invoke(this, e);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button? button = sender as Button;
            if (button != null && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true;
            }
        }
    }
}
