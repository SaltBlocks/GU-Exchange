using System.Windows;

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for SetupWallet.xaml
    /// </summary>
    public partial class SetupWalletWindow : Window
    {
        #region Default Constructor.
        /// <summary>
        /// Constructor for a window displaying a <see cref="SetupWalletControl"/>
        /// </summary>
        public SetupWalletWindow()
        {
            InitializeComponent();
            setupGrid.Children.Add(new SetupWalletControl(this));
        }
        #endregion
    }
}
