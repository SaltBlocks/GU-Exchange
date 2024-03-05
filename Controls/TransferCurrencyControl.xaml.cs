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
    /// Interaction logic for TransferCurrencyControl.xaml
    /// </summary>
    public partial class TransferCurrencyControl : UserControl
    {
        public TransferCurrencyControl()
        {
            InitializeComponent();
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
            // Click occurred outside controlGrid, you can call your function here
            ((MainWindow)Application.Current.MainWindow).CloseOverlay();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ((MainWindow)Application.Current.MainWindow).CloseOverlay();
        }
    }
}
