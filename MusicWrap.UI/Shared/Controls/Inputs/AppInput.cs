using MusicWrap.UI.Controls;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MusicWrap.UI.Controls
{
    public class AppInput : TextBox
    {
        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(
                nameof(PlaceholderText),
                typeof(string),
                typeof(AppInput),
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
                typeof(AppInput),
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
                typeof(AppInput),
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
                typeof(AppInput));
        public event RoutedEventHandler EnterPressed
        {
            add => AddHandler(EnterPressedEvent, value);
            remove => RemoveHandler(EnterPressedEvent, value);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Enter)
            {
                RaiseEvent(new RoutedEventArgs(EnterPressedEvent, this));
                e.Handled = true;
            }
        }
    }
}
