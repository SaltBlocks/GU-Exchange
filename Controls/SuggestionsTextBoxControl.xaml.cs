﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for SuggestionsTextBoxControl.xaml
    /// </summary>
    public partial class SuggestionsTextBoxControl : UserControl
    {
        #region Class properties. 
        /// <summary>  
        /// Suggestion list property.  
        /// </summary>  
        private List<string> suggestionList = new List<string>();

        /// <summary>
        /// Event handler for when the text of the textbox is changed.
        /// </summary>
        public event EventHandler? TextChanged;
        #endregion

        #region Default Constructor
        /// <summary>
        /// Construct a <see cref="UserControl"/> for a textbox with a dropdown box for suggestions.
        /// </summary>
        public SuggestionsTextBoxControl()
        {
            InitializeComponent();
        }
        #endregion

        #region Getters and Setters
        /// <summary>  
        /// Gets or sets the list of possible suggestions.  
        /// </summary>  
        public List<string> SuggestionList
        {
            get { return this.suggestionList; }
            set { this.suggestionList = value; }
        }

        /// <summary>
        /// Gets or sets the text being displayed.
        /// </summary>
        public string Text
        {
            get { return this.textInput.Text; }
            set {
                this.textInput.Text = value;
                this.CloseAutoSuggestionBox();
            }
        }
        #endregion

        #region Methods for opening and closing the suggestions popup.
        /// <summary>  
        ///  Open Auto Suggestion box method  
        /// </summary>  
        private void OpenAutoSuggestionBox()
        {
            this.listPopup.Visibility = Visibility.Visible;
            this.listPopup.IsOpen = true;
            this.lbSuggestions.Visibility = Visibility.Visible;
        }

        /// <summary>  
        ///  Close Auto Suggestion box method  
        /// </summary>  
        private void CloseAutoSuggestionBox()
        {
            this.listPopup.Visibility = Visibility.Collapsed;
            this.listPopup.IsOpen = false;
            this.lbSuggestions.Visibility = Visibility.Collapsed;
        }
        #endregion

        #region Event handlers.
        /// <summary>  
        ///  Update the suggestions and whether or not the popup is shown. 
        /// </summary>  
        /// <param name="sender">Sender parameter</param>  
        /// <param name="e">Event parameter</param>  
        private void TextInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Raise the TextChanged event
            OnTextChanged(EventArgs.Empty);

            if (string.IsNullOrEmpty(this.textInput.Text))
            {
                this.CloseAutoSuggestionBox();
                return;
            }
            List<string> suggestions = this.suggestionList.Where(p => p.ToLower().Contains(this.textInput.Text.ToLower()))
                .OrderBy(p => !p.ToLower().StartsWith(this.textInput.Text.ToLower())).ToList();
            if (suggestions.Count > 0)
                this.OpenAutoSuggestionBox();
            else
                this.CloseAutoSuggestionBox();
            this.lbSuggestions.ItemsSource = suggestions;
        }

        // Helper method to raise the TextChanged event
        protected virtual void OnTextChanged(EventArgs e)
        {
            TextChanged?.Invoke(this, e);
        }

        /// <summary>  
        ///  Respond to clicks on the suggested items.
        /// </summary>  
        /// <param name="sender">Sender parameter</param>  
        /// <param name="e">Event parameter</param>  
        private void SuggestionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.lbSuggestions.SelectedIndex <= -1)
            {
                this.CloseAutoSuggestionBox();
                return;
            }
            this.CloseAutoSuggestionBox();

            this.textInput.Text = this.lbSuggestions.SelectedItem.ToString();
            this.lbSuggestions.SelectedIndex = -1;
        }

        /// <summary>
        /// Update the width of the textbox.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SuggestionsTextBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.textInput.Width = this.Width;
            this.textInput.Height = this.Height;
            this.lbSuggestions.Width = this.Width;
        }
        #endregion
    }
}
