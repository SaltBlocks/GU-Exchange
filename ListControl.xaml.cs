using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for ListControl.xaml
    /// </summary>
    public partial class ListControl : UserControl
    {
        public ListControl(CardControl parent, ImageSource image)
        {
            InitializeComponent();
            List<string> items = new List<string> { "ETH", "GODS", "IMX" };
            cbCurrency1.ItemsSource = items;
            cbCurrency1.SelectedIndex = 0;
            DataContext = new ListCardViewModel(parent.tbCardName.Text, (string)parent.cbQuality.SelectedItem, image);
        }

        private void cbCurrency1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            tbCurrency.Text = cbCurrency1.SelectedItem.ToString();
        }
    }

    /// <summary>
    /// Class used to display details about the order to the user.
    /// </summary>
    public class ListCardViewModel : INotifyPropertyChanged
    {
        #region Class Properties
        private string cardName;
        private string cardQuality;
        private ImageSource cardImageSource;
        #endregion

        #region Default Constructor
        public ListCardViewModel(string cardName, string cardQuality, ImageSource image)
        {
            this.cardName = cardName;
            this.cardQuality = cardQuality;
            cardImageSource = image;
        }
        #endregion

        #region Getters and Setters
        public string CardName
        {
            get { return cardName; }
            set
            {
                cardName = value;
                OnPropertyChanged(nameof(CardName));
            }
        }

        public string CardQuality
        {
            get { return cardQuality; }
            set
            {
                cardQuality = value;
                OnPropertyChanged(nameof(CardQuality));
            }
        }

        public ImageSource CardImageSource
        {
            get { return cardImageSource; }
            set
            {
                cardImageSource = value;
                OnPropertyChanged(nameof(CardImageSource));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
