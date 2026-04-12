using MusicWrap.Core.Metadata;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MusicWrap.UI.Controls
{
    /// <summary>
    /// Autocomplete input control for metadata editing
    /// Provides suggestions based on existing values in the music library
    /// </summary>
    public partial class AutocompleteMetadataInput : UserControl
    {
        private readonly IMetadataAutocompleteService _autocompleteService;
        private bool _isUpdating;
        private bool _hasUserInteraction;

        public AutocompleteMetadataInput()
        {
            InitializeComponent();

            // Get service from DI container
            _autocompleteService = App.Services.GetRequiredService<IMetadataAutocompleteService>();

            // Initialize suggestions collection
            Suggestions = new ObservableCollection<string>();
        }

        #region Dependency Properties

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                "Text",
                typeof(string),
                typeof(AutocompleteMetadataInput),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(
                "IsOpen",
                typeof(bool),
                typeof(AutocompleteMetadataInput),
                new PropertyMetadata(false));

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public static readonly DependencyProperty SelectedSuggestionProperty =
            DependencyProperty.Register(
                "SelectedSuggestion",
                typeof(string),
                typeof(AutocompleteMetadataInput),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedSuggestionChanged));

        public string SelectedSuggestion
        {
            get => (string)GetValue(SelectedSuggestionProperty);
            set => SetValue(SelectedSuggestionProperty, value);
        }

        private static void OnSelectedSuggestionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is string suggestion && !string.IsNullOrEmpty(suggestion))
            {
                var control = (AutocompleteMetadataInput)d;
                control._isUpdating = true;
                control.Text = suggestion;
                control.IsOpen = false;
                control._isUpdating = false;
            }
        }

        public static readonly DependencyProperty SuggestionsProperty =
            DependencyProperty.Register(
                "Suggestions",
                typeof(ObservableCollection<string>),
                typeof(AutocompleteMetadataInput),
                new PropertyMetadata(null));

        public ObservableCollection<string> Suggestions
        {
            get => (ObservableCollection<string>)GetValue(SuggestionsProperty);
            set => SetValue(SuggestionsProperty, value);
        }

        public static readonly DependencyProperty MetadataTypeProperty =
            DependencyProperty.Register(
                "MetadataType",
                typeof(MetadataType),
                typeof(AutocompleteMetadataInput),
                new PropertyMetadata(MetadataType.ArtistName));

        public MetadataType MetadataType
        {
            get => (MetadataType)GetValue(MetadataTypeProperty);
            set => SetValue(MetadataTypeProperty, value);
        }

        public static readonly DependencyProperty SuggestionLimitProperty =
            DependencyProperty.Register(
                "SuggestionLimit",
                typeof(int),
                typeof(AutocompleteMetadataInput),
                new PropertyMetadata(15));

        public int SuggestionLimit
        {
            get => (int)GetValue(SuggestionLimitProperty);
            set => SetValue(SuggestionLimitProperty, value);
        }

        #endregion

        #region Event Handlers

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;

            if (!_hasUserInteraction)
            {
                CloseSuggestionsPopup();
                return;
            }

            UpdateSuggestions();
        }

        private void SearchTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            _hasUserInteraction = true;
        }

        private void SearchTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            _hasUserInteraction = true;
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && SuggestionsListBox.Items.Count > 0)
            {
                if (SuggestionsListBox.SelectedIndex < 0)
                    SuggestionsListBox.SelectedIndex = 0;
                else if (SuggestionsListBox.SelectedIndex < SuggestionsListBox.Items.Count - 1)
                    SuggestionsListBox.SelectedIndex++;

                SuggestionsListBox.ScrollIntoView(SuggestionsListBox.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.Up && SuggestionsListBox.Items.Count > 0)
            {
                if (SuggestionsListBox.SelectedIndex > 0)
                    SuggestionsListBox.SelectedIndex--;
                else if (SuggestionsListBox.SelectedIndex < 0)
                    SuggestionsListBox.SelectedIndex = SuggestionsListBox.Items.Count - 1;

                SuggestionsListBox.ScrollIntoView(SuggestionsListBox.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.Return && SuggestionsListBox.SelectedItem is string suggestion)
            {
                _isUpdating = true;
                Text = suggestion;
                IsOpen = false;
                _isUpdating = false;
                SearchTextBox.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                IsOpen = false;
                e.Handled = true;
            }
        }

        private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Back or Key.Delete or Key.Space)
            {
                _hasUserInteraction = true;
            }

            // Suppress arrow key sounds when navigating suggestions
            if (e.Key == Key.Up || e.Key == Key.Down)
            {
                e.Handled = false; // Allow the KeyDown event to handle it
            }
        }

        private void SearchTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            CloseSuggestionsPopup();
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                return;
            }

            CloseSuggestionsPopup();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            CloseSuggestionsPopup();
        }

        private void CloseSuggestionsPopup()
        {
            _isUpdating = true;
            IsOpen = false;
            _isUpdating = false;
        }

        private void SuggestionsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SuggestionsListBox.SelectedItem is string suggestion)
            {
                _isUpdating = true;
                Text = suggestion;
                IsOpen = false;
                _isUpdating = false;
            }
        }

        #endregion

        #region Private Methods

        private void UpdateSuggestions()
        {
            try
            {
                var suggestions = _autocompleteService.GetSuggestions(
                    MetadataType,
                    Text,
                    SuggestionLimit);

                Suggestions.Clear();
                foreach (var suggestion in suggestions)
                {
                    Suggestions.Add(suggestion);
                }

                IsOpen = suggestions.Count > 0;
            }
            catch
            {
                // If suggestions fail, just close popup
                Suggestions.Clear();
                IsOpen = false;
            }
        }

        #endregion
    }
}


