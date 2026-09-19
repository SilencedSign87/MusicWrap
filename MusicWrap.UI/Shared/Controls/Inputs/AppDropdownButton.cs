using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MusicWrap.UI.Controls
{
    public class AppDropdownButton : AppButton
    {
        private TextBlock? _indicatorBlock;
        private readonly TranslateTransform _indicatorTransform = new();
        public AppDropdownButton()
        {
            UpdateIndicator();
        }
        #region Dependency Properties

        public ContextMenu? Menu
        {
            get { return (ContextMenu?)GetValue(MenuProperty); }
            set { SetValue(MenuProperty, value); }
        }

        public static readonly DependencyProperty MenuProperty =
            DependencyProperty.Register(
                nameof(Menu),
                typeof(ContextMenu),
                typeof(AppDropdownButton),
                new PropertyMetadata(null, OnMenuChanged));
        public static readonly DependencyProperty ShowIndicatorProperty =
            DependencyProperty.Register(
                nameof(ShowIndicator),
                typeof(bool),
                typeof(AppDropdownButton),
                new PropertyMetadata(true, OnShowIndicatorChanged));

        public bool ShowIndicator
        {
            get => (bool)GetValue(ShowIndicatorProperty);
            set => SetValue(ShowIndicatorProperty, value);
        }
        #endregion
        #region Events
        private static void OnMenuChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var button = (AppDropdownButton)d;

            button.DetachMenu(e.OldValue as ContextMenu);
            button.AttachMenu(e.NewValue as ContextMenu);
        }

        protected override void OnClick()
        {
            base.OnClick();

            if (Menu is null)
                return;

            Menu.PlacementTarget = this;
            Menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;

            Menu.IsOpen = !Menu.IsOpen;
        }

        private void OnMenuOpened(object? sender, RoutedEventArgs e)
        {
            //AnimateIndicator();
        }
        private void OnMenuClosed(object? sender, RoutedEventArgs e)
        {
            AnimateIndicator(0, 80);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            AnimateIndicator(2.5, 70);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            AnimateIndicator(0, 100);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            AnimateIndicator(0, 100);
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            AnimateIndicator(0, 100);
        }


        private static void OnShowIndicatorChanged(
                DependencyObject d,
                DependencyPropertyChangedEventArgs e)
                => ((AppDropdownButton)d).UpdateIndicator();
        #endregion
        #region Internal
        private void UpdateIndicator()
        {
            if (ShowIndicator)
            {
                _indicatorBlock ??= new TextBlock
                {
                    Text = "\xE70D",
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    RenderTransform = _indicatorTransform,
                };

                if (TrailingContent is null || TrailingContent == _indicatorBlock)
                    TrailingContent = _indicatorBlock;
            }
            else if (TrailingContent == _indicatorBlock)
            {
                TrailingContent = null;
            }
        }
        private void AnimateIndicator(double targetY, double durationMs)
        {
            var anim = new DoubleAnimation
            {
                To = targetY,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            _indicatorTransform.BeginAnimation(TranslateTransform.YProperty, anim);
        }
        private void AttachMenu(ContextMenu? menu)
        {
            if (menu is null)
                return;

            menu.PlacementTarget = this;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.StaysOpen = false;

            menu.Opened += OnMenuOpened;
        }

        private void DetachMenu(ContextMenu? menu)
        {
            if (menu is null)
                return;

            menu.Opened -= OnMenuOpened;
            menu.Closed -= OnMenuClosed;
        }

        #endregion



    }
}
