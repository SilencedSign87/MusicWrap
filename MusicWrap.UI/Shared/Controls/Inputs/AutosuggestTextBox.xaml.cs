using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MusicWrap.UI.Controls
{
    /// <summary>
    /// Lógica de interacción para AutosuggestTextBox.xaml
    /// </summary>
    public partial class AutosuggestTextBox : UserControl
    {
        private bool _isSyncingText = false;
        private const int MaxSuggestions = 15;
        public AutosuggestTextBox()
        {
            InitializeComponent();
            Input.LostKeyboardFocus += Input_LostKeyboardFocus;
        }

        private void Input_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (SuggestionsList.IsKeyboardFocusWithin) return;

            IsSuggestionsListOpen = false;
        }

        #region Dependency Properties

        public bool IsSuggestionsListOpen
        {
            get { return (bool)GetValue(IsSuggestionsListOpenProperty); }
            set { SetValue(IsSuggestionsListOpenProperty, value); }
        }
        public static readonly DependencyProperty IsSuggestionsListOpenProperty =
            DependencyProperty.Register(nameof(IsSuggestionsListOpen), typeof(bool), typeof(AutosuggestTextBox), new PropertyMetadata(false));


        public string DisplayMemberPath
        {
            get => (string)GetValue(DisplayMemberPathProperty);
            set => SetValue(DisplayMemberPathProperty, value);
        }
        public static readonly DependencyProperty DisplayMemberPathProperty =
          DependencyProperty.Register(
              nameof(DisplayMemberPath),
              typeof(string),
              typeof(AutosuggestTextBox),
              new PropertyMetadata(string.Empty));


        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(AutosuggestTextBox),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));
        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (AutosuggestTextBox)d;
            if (control.Input is null || control._isSyncingText) return;

            control._isSyncingText = true;
            try
            {
                control.Input.Text = e.NewValue?.ToString() ?? string.Empty;
            }
            finally
            {
                control._isSyncingText = false;
            }

            control.RaiseTextChanged(
                control.Input.Text,
                AutosuggestTextChangedReason.ProgrammaticChange
                );
        }

        public string PlaceholderText
        {
            get { return (string)GetValue(PlaceholderTextProperty); }
            set { SetValue(PlaceholderTextProperty, value); }
        }
        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(AutosuggestTextBox), new PropertyMetadata(string.Empty));


        public bool IsReadonly
        {
            get { return (bool)GetValue(IsReadonlyProperty); }
            set { SetValue(IsReadonlyProperty, value); }
        }
        public static readonly DependencyProperty IsReadonlyProperty =
            DependencyProperty.Register(nameof(IsReadonly), typeof(bool), typeof(AutosuggestTextBox), new PropertyMetadata(false));



        public IEnumerable<string> SuggestionsSource
        {
            get { return (IEnumerable<string>)GetValue(SuggestionsSourceProperty); }
            set { SetValue(SuggestionsSourceProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SuggestionsSource.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SuggestionsSourceProperty =
            DependencyProperty.Register(nameof(SuggestionsSource), typeof(IEnumerable<string>), typeof(AutosuggestTextBox), new PropertyMetadata(null, OnSuggestionsSourceChanged));

        private static void OnSuggestionsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (AutosuggestTextBox)d;

            control.RefreshSuggestions();
        }

        public static readonly RoutedEvent TextChangedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(TextChanged),
                RoutingStrategy.Bubble,
                typeof(EventHandler<AutosuggestTextChangedEventArgs>),
                typeof(AutosuggestTextBox));

        public event EventHandler<AutosuggestTextChangedEventArgs> TextChanged
        {
            add => AddHandler(TextChangedEvent, value);
            remove => RemoveHandler(TextChangedEvent, value);
        }

        #endregion

        #region Events
        private void Input_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingText) return;
            _isSyncingText = true;
            try
            {
                SetCurrentValue(TextProperty, Input.Text);
            }
            finally
            {
                _isSyncingText = false;
            }
            RaiseTextChanged(
                Input.Text,
                AutosuggestTextChangedReason.UserInput
                );

            RefreshSuggestions();
        }

        private void SuggestionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void SuggestionsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SuggestionsList.SelectedItem is object suggestion)
            {
                CommitSuggestion(suggestion);
            }
        }

        private void SuggestionsList_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && SuggestionsList.SelectedItem is object suggestion)
            {
                CommitSuggestion(suggestion);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                IsSuggestionsListOpen = false;
                e.Handled = true;
            }
        }

        #endregion
        #region Internal
        private void CommitSuggestion(object suggestion)
        {
            if (IsReadonly) return;

            string suggestionText = suggestion?.ToString() ?? string.Empty;

            Text = suggestionText;
            IsSuggestionsListOpen = false;
        }
        private void RefreshSuggestions()
        {
            if (SuggestionsList is null) return;

            string query = (Input?.Text ?? string.Empty).Trim();

            string[] matches = string.IsNullOrEmpty(query) || SuggestionsSource is null
                            ? []
                            : SuggestionsSource
                                .Where(s => s.Contains(query, StringComparison.OrdinalIgnoreCase))
                                .Take(MaxSuggestions)
                                .ToArray();

            SuggestionsList.ItemsSource = matches;
            SuggestionsList.SelectedIndex = -1;

            IsSuggestionsListOpen = !IsReadonly
                && (Input?.IsKeyboardFocusWithin ?? false)
                && matches.Length > 0;
        }
        private void RaiseTextChanged(string text, AutosuggestTextChangedReason reason)
        {
            RaiseEvent(
                new AutosuggestTextChangedEventArgs(
                    TextChangedEvent,
                    this,
                    text,
                    reason
                    )
                );
        }
        #endregion
    }
    #region Event Args
    public enum AutosuggestTextChangedReason
    {
        UserInput,
        SuggestionChosen,
        ProgrammaticChange
    }
    public class AutosuggestTextChangedEventArgs : RoutedEventArgs
    {
        public AutosuggestTextChangedEventArgs(RoutedEvent routedEvent, object source, string text, AutosuggestTextChangedReason reason) : base(routedEvent, source)
        {
            Text = text;
            Reason = reason;
        }
        public string Text { get; }
        public AutosuggestTextChangedReason Reason { get; }
    }
    #endregion
}
