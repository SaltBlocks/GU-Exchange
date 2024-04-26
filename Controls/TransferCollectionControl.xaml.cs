using GU_Exchange.Helpers;
using GU_Exchange.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
    /// Interaction logic for TransferCollectionControl.xaml
    /// </summary>
    public partial class TransferCollectionControl : UserControl
    {
        public TransferCollectionControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Adjust the size of the CardControl when the window size is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double maxWidth = 1400;
            double maxHeight = 800;
            double width = Math.Min(ActualWidth, maxWidth);
            double height = width / 1.75;
            if (height > ActualHeight)
            {
                height = Math.Min(ActualHeight, maxHeight);
                width = height * 1.75;
            }
            controlGrid.Height = height - 10;
            controlGrid.Width = width - 10;
        }

        /// <summary>
        /// Close the window when the user clicks on the greyed out background.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Get the position of the mouse click relative to the controlGrid
            Point clickPoint = e.GetPosition(controlGrid);

            // Check if the click occurred on the controlGrid
            if (clickPoint.X >= 0 && clickPoint.X < controlGrid.ActualWidth &&
                clickPoint.Y >= 0 && clickPoint.Y < controlGrid.ActualHeight)
            {
                return;
            }
            // Click occurred outside controlGrid, close the overlay.
            if (btnClose.IsEnabled)
            {
                ((MainWindow)Application.Current.MainWindow).CloseOverlay();
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            ((MainWindow)Application.Current.MainWindow).CloseOverlay();
        }

        /// <summary>
        /// Allow the user to lookup an address belonging to a specific player.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnLookup_Click(object sender, RoutedEventArgs e)
        {
            PlayerLookupWindow window = new PlayerLookupWindow();
            window.Owner = Application.Current.MainWindow;
            window.ShowDialog();
            if (window.Result == PlayerLookupWindow.LookupResult.Select)
                tbAddress.Text = window.GetSelectedAddress();
        }
    }
}
