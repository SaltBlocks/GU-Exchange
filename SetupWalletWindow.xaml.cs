using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for SetupWallet.xaml
    /// </summary>
    public partial class SetupWalletWindow : Window
    {
        #region Default Constructor.
        public SetupWalletWindow()
        {
            InitializeComponent();
            setupGrid.Children.Add(new SetupWallet(this));
        }
        #endregion
    }
}
