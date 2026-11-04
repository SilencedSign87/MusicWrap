using AngleSharp.Text;
using MusicWrap.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MusicWrap.UI.Controls
{
    /// <summary>
    /// Lógica de interacción para TokenizingTextBox.xaml
    /// </summary>
    public partial class TokenizingTextBox : UserControl
    {
        private bool _isRebuilding;
        private bool _isSynchronizingText;
        private bool _isUserInput;
        private const int MaxSuggestions = 50;


        public TokenizingTextBox()
        {
            InitializeComponent();

            ItemsSource = new ObservableCollection<string>();

            RebuildTokens();
        }

        #region Dependency Properties

        public IEnumerable<string>? AutocompleteSource
        {
            get { return (IEnumerable<string>?)GetValue(AutocompleteSourceProperty); }
            set { SetValue(AutocompleteSourceProperty, value); }
        }

        // Using a DependencyProperty as the backing store for AutocompleteSource.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty AutocompleteSourceProperty =
            DependencyProperty.Register(nameof(AutocompleteSource), typeof(IEnumerable<string>), typeof(TokenizingTextBox), new PropertyMetadata(null, OnAutocompleteSourceChanged));

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(TokenizingTextBox),
                new FrameworkPropertyMetadata(
                    string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnTextChanged));


        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty DisplayMemberPathProperty =
            DependencyProperty.Register(
                nameof(DisplayMemberPath),
                typeof(string),
                typeof(TokenizingTextBox),
                new PropertyMetadata(string.Empty));

        public string DisplayMemberPath
        {
            get => (string)GetValue(DisplayMemberPathProperty);
            set => SetValue(DisplayMemberPathProperty, value);
        }

        public static readonly DependencyProperty IsSuggestionListOpenProperty =
            DependencyProperty.Register(
                nameof(IsSuggestionListOpen),
                typeof(bool),
                typeof(TokenizingTextBox),
                new PropertyMetadata(false));

        public bool IsSuggestionListOpen
        {
            get => (bool)GetValue(IsSuggestionListOpenProperty);
            set => SetValue(IsSuggestionListOpenProperty, value);
        }

        public static readonly RoutedEvent TextChangedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(TextChanged),
                RoutingStrategy.Bubble,
                typeof(EventHandler<TokenizingTextChangedEventArgs>),
                typeof(TokenizingTextBox));

        public event EventHandler<TokenizingTextChangedEventArgs> TextChanged
        {
            add => AddHandler(TextChangedEvent, value);
            remove => RemoveHandler(TextChangedEvent, value);
        }

        public static readonly RoutedEvent SuggestionChosenEvent =
            EventManager.RegisterRoutedEvent(
                nameof(SuggestionChosen),
                RoutingStrategy.Bubble,
                typeof(EventHandler<TokenizingSuggestionChosenEventArgs>),
                typeof(TokenizingTextBox));

        public event EventHandler<TokenizingSuggestionChosenEventArgs> SuggestionChosen
        {
            add => AddHandler(SuggestionChosenEvent, value);
            remove => RemoveHandler(SuggestionChosenEvent, value);
        }

        public ObservableCollection<String> ItemsSource
        {
            get { return (ObservableCollection<String>)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(ObservableCollection<String>),
                typeof(TokenizingTextBox),
                 new PropertyMetadata(null, OnItemsSourceChanged));


        public int MaximumTokens
        {
            get => (int)GetValue(MaximumTokensProperty);
            set => SetValue(MaximumTokensProperty, value);
        }
        public static readonly DependencyProperty MaximumTokensProperty =
        DependencyProperty.Register(
            nameof(MaximumTokens),
            typeof(int),
            typeof(TokenizingTextBox),
            new PropertyMetadata(int.MaxValue),
            (value) => value is int && (int)value >= 0);


        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }
        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(
                nameof(PlaceholderText),
                typeof(string),
                typeof(TokenizingTextBox),
                new PropertyMetadata(""));


        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }
        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(
                nameof(IsReadOnly),
                typeof(bool),
                typeof(TokenizingTextBox),
                new PropertyMetadata(false, OnIsReadOnlyChanged));


        public DataTemplate TokenTemplate
        {
            get => (DataTemplate)GetValue(TokenTemplateProperty);
            set => SetValue(TokenTemplateProperty, value);
        }
        public static readonly DependencyProperty TokenTemplateProperty =
            DependencyProperty.Register(
                nameof(TokenTemplate),
                typeof(DataTemplate),
                typeof(TokenizingTextBox),
                new PropertyMetadata(null, OnTokenTemplateChanged));

        #endregion
        #region Property Callbacks
        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TokenizingTextBox control)
                return;

            if (e.OldValue is ObservableCollection<string> oldCollection)
            {
                oldCollection.CollectionChanged -=
                    control.ItemsSource_CollectionChanged;
            }

            if (e.NewValue is ObservableCollection<string> newCollection)
            {
                newCollection.CollectionChanged +=
                    control.ItemsSource_CollectionChanged;
            }

            control.RebuildTokens();
        }
        private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TokenizingTextBox control)
            {
                control.UpdateInputState();
                control.RebuildTokens();
            }
        }

        private static void OnAutocompleteSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (TokenizingTextBox)d;

            //if (control.SuggestionsList is null)
            //    return;
            //control.SuggestionsList.SelectedIndex = -1;

            //control.IsSuggestionListOpen =
            //    !string.IsNullOrWhiteSpace(control.Text) &&
            //    control.SuggestionsList.HasItems;

            control.RefreshSuggestions();
        }
        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (TokenizingTextBox)d;

            if (control.Input is null || control._isSynchronizingText)
                return;

            control._isSynchronizingText = true;

            try
            {
                control.Input.Text = (string?)e.NewValue ?? string.Empty;
            }
            finally
            {
                control._isSynchronizingText = false;
            }

            control.RaiseTextChanged(
                (string?)e.NewValue ?? string.Empty,
                TokenizingTextChangedReason.ProgrammaticChange);
        }
        private static void OnTokenTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TokenizingTextBox control)
            {
                control.RebuildTokens();
            }
        }
        private void ItemsSource_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildTokens();
        }
        #endregion
        #region Token Management
        private void RebuildTokens()
        {
            if (TokenPanel is null || Input is null || _isRebuilding)
                return;

            _isRebuilding = true;

            try
            {
                TokenPanel.Children.Clear();

                if (ItemsSource is not null)
                {
                    foreach (string item in ItemsSource)
                        TokenPanel.Children.Add(CreateToken(item));
                }

                UpdateInputState();

                if (!IsReadOnly &&
                    (ItemsSource?.Count ?? 0) < MaximumTokens)
                {
                    TokenPanel.Children.Add(Input);
                }
            }
            finally
            {
                _isRebuilding = false;
            }
        }

        private UIElement CreateToken(string value)
        {
            var presenter = new ContentPresenter
            {
                Content = value,
                ContentTemplate = TokenTemplate ??
                (DataTemplate)Resources["DefaultTokenTemplate"]
            };

            return presenter;
        }


        private void RemoveTokenButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsReadOnly || sender is not FrameworkElement element)
                return;

            if (element.DataContext is string value)
            {
                ItemsSource?.Remove(value);
                return;
            }

            if (ItemsSource is null)
                return;

            var presenter = FindAncestor<ContentPresenter>(element);

            if (presenter?.Content is string token)
            {
                ItemsSource.Remove(token);
            }
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current is not null)
            {
                if (current is T result)
                    return result;

                current = System.Windows.Media.VisualTreeHelper
                    .GetParent(current);
            }

            return null;
        }
        #endregion
        #region Input
        private void UpdateInputState()
        {
            if (Input is null) return;
            Input.IsReadOnly = IsReadOnly;
            //Input.PlaceholderText = PlaceholderText;
            bool canAdd = !IsReadOnly && (ItemsSource?.Count ?? 0) < MaximumTokens;

            Input.IsEnabled = canAdd;
        }
        private void Input_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSynchronizingText)
                return;

            _isSynchronizingText = true;

            try
            {
                SetCurrentValue(TextProperty, Input.Text);
            }
            finally
            {
                _isSynchronizingText = false;
            }

            RaiseTextChanged(
                Input.Text,
                TokenizingTextChangedReason.UserInput);

            RefreshSuggestions();
        }

        private void RefreshSuggestions()
        {
            if (SuggestionsList is null)
                return;

            string query = (Input?.Text ?? string.Empty).Trim();

            string[] matches = string.IsNullOrEmpty(query) || AutocompleteSource is null
                ? []
                : AutocompleteSource
                    .Where(s => s.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Take(MaxSuggestions)
                    .ToArray();

            SuggestionsList.ItemsSource = matches;
            SuggestionsList.SelectedIndex = -1;

            IsSuggestionListOpen = !IsReadOnly && matches.Length > 0;

        }

        private void Input_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Down:
                    MoveSuggestionIndex(1);
                    e.Handled = true;
                    break;
                case Key.Up:
                    MoveSuggestionIndex(-1);
                    e.Handled = true;
                    break;
                case Key.Escape:
                    IsSuggestionListOpen = false;
                    e.Handled = true;
                    break;
                case Key.Tab:
                    if (IsSuggestionListOpen &&
                        SuggestionsList.SelectedItem is object suggestion)
                    {
                        CommitSuggestion(suggestion);
                        e.Handled = true;
                    }
                    break;
                    // enter is handled by the Input_EnterPressed event
            }
        }

        private void Input_EnterPressed(object sender, RoutedEventArgs e)
        {
            if (IsReadOnly)
                return;

            if (IsSuggestionListOpen &&
                SuggestionsList.SelectedItem is object suggestion)
            {
                CommitSuggestion(suggestion);
                return;
            }

            CommitText();
        }
        #endregion

        private void RaiseTextChanged(
            string text,
            TokenizingTextChangedReason reason)
        {
            RaiseEvent(new TokenizingTextChangedEventArgs(
                TextChangedEvent,
                this,
                text,
                reason));
        }

        private void MoveSuggestionIndex(int direction)
        {
            if (SuggestionsList is null || SuggestionsList.Items.Count == 0)
                return;

            int newIndex = SuggestionsList.SelectedIndex + direction;

            if (newIndex < 0)
                newIndex = SuggestionsList.Items.Count - 1;
            else if (newIndex >= SuggestionsList.Items.Count)
                newIndex = 0;

            SuggestionsList.SelectedIndex = newIndex;
            SuggestionsList.ScrollIntoView(SuggestionsList.SelectedItem);
        }

        private void SuggestionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void SuggestionsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SuggestionsList.SelectedItem is object suggestion)
                CommitSuggestion(suggestion);
        }

        private void SuggestionsList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter &&
                SuggestionsList.SelectedItem is object suggestion)
            {
                CommitSuggestion(suggestion);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                IsSuggestionListOpen = false;
                Input.Focus();
                e.Handled = true;
            }
        }

        private void CommitSuggestion(object suggestion)
        {
            if (IsReadOnly || ItemsSource is null)
                return;

            if (ItemsSource.Count >= MaximumTokens)
                return;

            string displayText = GetSuggestionText(suggestion);

            if (string.IsNullOrWhiteSpace(displayText))
                return;

            if (ItemsSource.Contains(displayText))
            {
                IsSuggestionListOpen = false;
                Input.Focus();
                return;
            }

            ItemsSource.Add(displayText);

            SetCurrentValue(TextProperty, string.Empty);

            IsSuggestionListOpen = false;

            RaiseEvent(new TokenizingSuggestionChosenEventArgs(
                SuggestionChosenEvent,
                this,
                suggestion));

            Input.Focus();
        }

        private void CommitText()
        {
            string value = Text.Trim();

            if (string.IsNullOrWhiteSpace(value) ||
                IsReadOnly ||
                ItemsSource is null ||
                ItemsSource.Count >= MaximumTokens)
            {
                return;
            }

            if (!ItemsSource.Contains(value))
                ItemsSource.Add(value);

            SetCurrentValue(TextProperty, string.Empty);

            IsSuggestionListOpen = false;
            Input.Focus();
        }
        private string GetSuggestionText(object suggestion)
        {
            if (string.IsNullOrWhiteSpace(DisplayMemberPath))
                return suggestion.ToString() ?? string.Empty;

            object? value = suggestion.GetType()
                .GetProperty(DisplayMemberPath)?
                .GetValue(suggestion);

            return value?.ToString() ?? string.Empty;
        }
    }

    #region Event Args

    public enum TokenizingTextChangedReason
    {
        UserInput,
        SuggestionChosen,
        ProgrammaticChange
    }
    public sealed class TokenizingTextChangedEventArgs : RoutedEventArgs
    {
        public TokenizingTextChangedEventArgs(
            RoutedEvent routedEvent,
            object source,
            string text,
            TokenizingTextChangedReason reason)
            : base(routedEvent, source)
        {
            Text = text;
            Reason = reason;
        }

        public string Text { get; }

        public TokenizingTextChangedReason Reason { get; }
    }

    public sealed class TokenizingSuggestionChosenEventArgs : RoutedEventArgs
    {
        public TokenizingSuggestionChosenEventArgs(
            RoutedEvent routedEvent,
            object source,
            object selectedItem)
            : base(routedEvent, source)
        {
            SelectedItem = selectedItem;
        }

        public object SelectedItem { get; }
    }
    #endregion

}
