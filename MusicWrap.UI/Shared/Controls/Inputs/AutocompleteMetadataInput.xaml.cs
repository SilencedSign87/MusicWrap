using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Metadata;
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

            // Subscribe to CompactInputField events after initialization
            Loaded += (s, e) =>
            {
                if (CompactInput != null)
                {
                    CompactInput.TextBoxTextChanged += SearchTextBox_TextChanged;
                    CompactInput.TextBoxPreviewTextInput += SearchTextBox_PreviewTextInput;
                    CompactInput.TextBoxPasting += SearchTextBox_Pasting;
                    CompactInput.TextBoxLostKeyboardFocus += SearchTextBox_LostKeyboardFocus;
                    CompactInput.TextBoxPreviewKeyDown += SearchTextBox_PreviewKeyDown;
                }
            };
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
            //if (e.NewValue is string suggestion && !string.IsNullOrEmpty(suggestion))
            //{
            //    ((AutocompleteMetadataInput)d).CommitSuggestion(suggestion);
            //}
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

        public static readonly DependencyProperty IsMultipleValueEnabledProperty =
            DependencyProperty.Register(
                nameof(IsMultipleValueEnabled),
                typeof(bool),
                typeof(AutocompleteMetadataInput),
                new PropertyMetadata(false));
        public bool IsMultipleValueEnabled
        {
            get => (bool)GetValue(IsMultipleValueEnabledProperty);
            set => SetValue(IsMultipleValueEnabledProperty, value);
        }

        public static readonly DependencyProperty MultipleValueSeparatorProperty =
           DependencyProperty.Register(
               nameof(MultipleValueSeparator),
               typeof(string),
               typeof(AutocompleteMetadataInput),
               new PropertyMetadata(","));
        public string MultipleValueSeparator
        {
            get => (string)GetValue(MultipleValueSeparatorProperty);
            set => SetValue(MultipleValueSeparatorProperty, value);
        }

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(
                nameof(PlaceholderText),
                typeof(string),
                typeof(AutocompleteMetadataInput),
                new PropertyMetadata(string.Empty));
        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        public static readonly DependencyProperty BeforeContentProperty =
            DependencyProperty.Register(
                nameof(BeforeContent),
                typeof(object),
                typeof(AutocompleteMetadataInput),
                new PropertyMetadata(null));
        public object BeforeContent
        {
            get => GetValue(BeforeContentProperty);
            set => SetValue(BeforeContentProperty, value);
        }

        public static readonly DependencyProperty AfterContentProperty =
            DependencyProperty.Register(
                nameof(AfterContent),
                typeof(object),
                typeof(AutocompleteMetadataInput),
                new PropertyMetadata(null));
        public object AfterContent
        {
            get => GetValue(AfterContentProperty);
            set => SetValue(AfterContentProperty, value);
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

        }

        private void SearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Back or Key.Delete or Key.Space)
            {
                _hasUserInteraction = true;
            }

            if (!IsOpen || SuggestionsListBox.Items.Count == 0)
            {
                return;
            }

            if (e.Key == Key.Down)
            {
                if (SuggestionsListBox.SelectedIndex < 0)
                    SuggestionsListBox.SelectedIndex = 0;
                else if (SuggestionsListBox.SelectedIndex < SuggestionsListBox.Items.Count - 1)
                    SuggestionsListBox.SelectedIndex++;

                SuggestionsListBox.ScrollIntoView(SuggestionsListBox.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (SuggestionsListBox.SelectedIndex > 0)
                    SuggestionsListBox.SelectedIndex--;
                else if (SuggestionsListBox.SelectedIndex >= 0)
                    SuggestionsListBox.SelectedIndex = SuggestionsListBox.Items.Count - 1;

                SuggestionsListBox.ScrollIntoView(SuggestionsListBox.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.Return && SuggestionsListBox.SelectedIndex >= 0)
            {
                var suggestion = SuggestionsListBox.SelectedItem as string;
                if (suggestion != null)
                {
                    CommitSuggestion(suggestion);
                    CompactInput?.Focus();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                IsOpen = false;
                e.Handled = true;
            }
        }

        private void SearchTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Popup light-dismiss is handled by StaysOpen=False.
            _hasUserInteraction = false;
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
            var suggestion = SuggestionsListBox.SelectedItem as string;

            if (string.IsNullOrEmpty(suggestion) && e.OriginalSource is FrameworkElement element)
            {
                suggestion = element.DataContext as string;
            }

            if (!string.IsNullOrEmpty(suggestion))
            {
                CommitSuggestion(suggestion);
            }
        }

        #endregion

        #region Private Methods

        private void UpdateSuggestions()
        {
            try
            {
                string term;
                if (IsMultipleValueEnabled)
                {
                    var segment = GetActiveSegment(Text, GetCaretIndex());
                    term = segment.Value;
                }
                else
                {
                    term = Text ?? string.Empty;
                }

                var suggestions = _autocompleteService.GetSuggestions(MetadataType, term, SuggestionLimit);

                Suggestions.Clear();
                foreach (var suggestion in suggestions)
                {
                    Suggestions.Add(suggestion);
                }
                // Only open popup if user interacted and there are suggestions
                IsOpen = Suggestions.Count > 0 && _hasUserInteraction;
            }
            catch
            {
                Suggestions.Clear();
                IsOpen = false;
            }
        }

        private char GetSeparatorChar()
        {
            if (string.IsNullOrWhiteSpace(MultipleValueSeparator))
            {
                return ',';
            }

            return MultipleValueSeparator[0];
        }
        private SegmentInfo GetActiveSegment(string? text, int caretIndex)
        {
            string value = text ?? string.Empty;
            int caret = Math.Clamp(caretIndex, 0, value.Length);
            char sep = GetSeparatorChar();

            int start = value.LastIndexOf(sep, Math.Max(0, caret - 1));
            start = start < 0 ? 0 : start + 1;

            int end = value.IndexOf(sep, caret);
            end = end < 0 ? value.Length : end;

            string segment = value.Substring(start, end - start).Trim();

            return new SegmentInfo(start, end, segment);
        }

        private int GetCaretIndex()
        {
            var inputBox = CompactInput?.FindName("InputBox") as TextBox;
            return inputBox?.CaretIndex ?? 0;
        }

        private void SetCaretIndex(int index)
        {
            var inputBox = CompactInput?.FindName("InputBox") as TextBox;
            if (inputBox != null)
            {
                inputBox.CaretIndex = Math.Clamp(index, 0, Text.Length);
            }
        }
        private string NormalizeCommaSpaces(string input)
        {
            if (!IsMultipleValueEnabled)
            {
                return input;
            }
            char sep = GetSeparatorChar();
            var parts = input
                        .Split(sep)
                        .Select(p => p.Trim())
                        .ToArray();
            return string.Join($"{sep} ", parts);
        }
        private void CommitSuggestion(string suggestion)
        {
            if (string.IsNullOrEmpty(suggestion))
            {
                return;
            }
            _isUpdating = true;
            try
            {
                if (!IsMultipleValueEnabled)
                {
                    Text = suggestion;
                    IsOpen = false;
                    return;
                }
                string current = Text ?? string.Empty;
                var segment = GetActiveSegment(current, GetCaretIndex());

                string before = current[..segment.Start];
                string after = current[segment.End..];
                string mergued = before + suggestion.Trim() + after;
                mergued = NormalizeCommaSpaces(mergued);
                int newCaret = (before + suggestion.Trim()).Length;
                Text = mergued;
                SetCaretIndex(newCaret);

                IsOpen = false;
                CompactInput?.Focus();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        #endregion

        private readonly record struct SegmentInfo(int Start, int End, string Value);
    }
}


