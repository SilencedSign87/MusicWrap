using System;
using System.Collections.Generic;
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
    /// Lógica de interacción para CompactInputField.xaml
    /// </summary>
    public partial class CompactInputField : UserControl
    {
        public CompactInputField()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(CompactInputField),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(
                nameof(PlaceholderText),
                typeof(string),
                typeof(CompactInputField),
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
                typeof(CompactInputField),
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
                typeof(CompactInputField),
                new PropertyMetadata(null));

        public object AfterContent
        {
            get => GetValue(AfterContentProperty);
            set => SetValue(AfterContentProperty, value);
        }

        public static readonly RoutedEvent EnterPressedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(EnterPressed),
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(CompactInputField));

        public event RoutedEventHandler EnterPressed
        {
            add => AddHandler(EnterPressedEvent, value);
            remove => RemoveHandler(EnterPressedEvent, value);
        }

        // Custom CLR events for text input handling
        public event TextChangedEventHandler? TextBoxTextChanged;
        public event TextCompositionEventHandler? TextBoxPreviewTextInput;
        public event DataObjectPastingEventHandler? TextBoxPasting;
        public event KeyboardFocusChangedEventHandler? TextBoxLostKeyboardFocus;
        public event KeyEventHandler? TextBoxPreviewKeyDown;

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            RaiseEvent(new RoutedEventArgs(EnterPressedEvent, this));
            e.Handled = true;
        }

        private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBoxTextChanged?.Invoke(this, e);
        }

        private void InputBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBoxPreviewTextInput?.Invoke(this, e);
        }

        private void InputBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            TextBoxPasting?.Invoke(this, e);
        }

        private void InputBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            TextBoxLostKeyboardFocus?.Invoke(this, e);
        }

        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            TextBoxPreviewKeyDown?.Invoke(this, e);
        }

    }
}


