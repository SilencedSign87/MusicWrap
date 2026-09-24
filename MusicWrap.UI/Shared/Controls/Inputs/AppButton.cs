

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace MusicWrap.UI.Controls;

public class AppButton : Button
{
    private TextBlock? _iconBlock;
    private TextBlock? _labelBlock;
    private StackPanel? _panel;

    private readonly ContentPresenter _leadingPresenter = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        Visibility = Visibility.Collapsed
    };
    private readonly ContentPresenter _contentPresenter = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        Visibility = Visibility.Collapsed
    };
    private readonly ContentPresenter _trailingPresenter = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        Visibility = Visibility.Collapsed
    };
    private bool _isInternalContentChange;
    private object? _userContent;

    #region Dependency Properties
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(AppButton),
            new PropertyMetadata(null, OnIconChanged));
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(AppButton),
            new PropertyMetadata(null, OnTextChanged));
    public static readonly DependencyProperty IconFontSizeProperty =
        DependencyProperty.Register(nameof(IconFontSize), typeof(double), typeof(AppButton),
            new PropertyMetadata(16.0, OnVisualPropertyChanged));
    public static readonly DependencyProperty TextFontSizeProperty =
        DependencyProperty.Register(nameof(TextFontSize), typeof(double), typeof(AppButton),
            new PropertyMetadata(14.0, OnVisualPropertyChanged));
    public static readonly DependencyProperty SpacingProperty =
        DependencyProperty.Register(nameof(Spacing), typeof(double), typeof(AppButton),
            new PropertyMetadata(4.0, OnVisualPropertyChanged));
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(System.Windows.Controls.Orientation), typeof(AppButton),
            new PropertyMetadata(System.Windows.Controls.Orientation.Horizontal, OnOrientationChanged));
    public static readonly DependencyProperty IsSquareProperty =
        DependencyProperty.Register(nameof(IsSquare), typeof(bool), typeof(AppButton),
            new PropertyMetadata(false, OnVisualPropertyChanged));
    public static readonly DependencyProperty LeadingContentProperty =
        DependencyProperty.Register(nameof(LeadingContent), typeof(object), typeof(AppButton),
            new PropertyMetadata(null, OnLeadingContentChanged));
    public static readonly DependencyProperty TrailingContentProperty =
        DependencyProperty.Register(nameof(TrailingContent), typeof(object), typeof(AppButton),
            new PropertyMetadata(null, OnTrailingContentChanged));


    public static readonly DependencyProperty LeadingProperty = LeadingContentProperty;
    public static readonly DependencyProperty TrailingProperty = TrailingContentProperty;

    public object? LeadingContent
    {
        get => GetValue(LeadingContentProperty);
        set => SetValue(LeadingContentProperty, value);
    }
    public object? TrailingContent
    {
        get => GetValue(TrailingContentProperty);
        set => SetValue(TrailingContentProperty, value);
    }

    public string? Icon
    {
        get => (string?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
    public double IconFontSize
    {
        get => (double)GetValue(IconFontSizeProperty);
        set => SetValue(IconFontSizeProperty, value);
    }
    public double TextFontSize
    {
        get => (double)GetValue(TextFontSizeProperty);
        set => SetValue(TextFontSizeProperty, value);
    }
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }
    public System.Windows.Controls.Orientation Orientation
    {
        get => (System.Windows.Controls.Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }
    public bool IsSquare
    {
        get => (bool)GetValue(IsSquareProperty);
        set => SetValue(IsSquareProperty, value);
    }
    #endregion

    public AppButton()
    {
        _panel = new StackPanel { Orientation = Orientation };
        _panel.Children.Add(_leadingPresenter);
        _panel.Children.Add(_contentPresenter);
        _panel.Children.Add(_trailingPresenter);
        _isInternalContentChange = true;
        Content = _panel;
        _isInternalContentChange = false;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshIcon();
        RefreshText();
        UpdateMargins();
    }
    #region Static Callbacks
    private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((AppButton)d).RefreshIcon();
    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((AppButton)d).RefreshText();
    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((AppButton)d).ApplyVisualProperties();
    private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var btn = (AppButton)d;
        if (btn._panel is not null)
            btn._panel.Orientation = (System.Windows.Controls.Orientation)e.NewValue;
        btn.UpdateMargins();
    }
    private static void OnLeadingContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var btn = (AppButton)d;
        btn._leadingPresenter.Content = e.NewValue;
        btn._leadingPresenter.Visibility = e.NewValue is not null ? Visibility.Visible : Visibility.Collapsed;
        btn.UpdateMargins();
    }
    private static void OnTrailingContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var btn = (AppButton)d;
        btn._trailingPresenter.Content = e.NewValue;
        btn._trailingPresenter.Visibility = e.NewValue is not null ? Visibility.Visible : Visibility.Collapsed;
        btn.UpdateMargins();
    }
    #endregion

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        if (_isInternalContentChange || ReferenceEquals(newContent, _panel))
            return;

        _userContent = newContent;
        UpdateMiddleContent(_userContent ?? _labelBlock);

        _isInternalContentChange = true;
        Content = _panel;
        _isInternalContentChange = false;
    }

    #region Content Builders
    private void UpdateMiddleContent(object? content)
    {
        _contentPresenter.Content = content;
        _contentPresenter.Visibility = content is not null ? Visibility.Visible : Visibility.Collapsed;
        UpdateMargins();
    }
    private void RefreshIcon()
    {

        bool hasIcon = !string.IsNullOrEmpty(Icon);
        if (hasIcon)
        {
            if (_iconBlock is null)
            {
                _iconBlock = new TextBlock
                {
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                _iconBlock.SetBinding(TextBlock.TextProperty, new Binding(nameof(Icon)) { Source = this });
                _iconBlock.SetBinding(TextBlock.FontSizeProperty, new Binding(nameof(IconFontSize)) { Source = this });
            }
            if (LeadingContent is null || LeadingContent == _iconBlock)
                LeadingContent = _iconBlock;
        }
        else if (LeadingContent == _iconBlock)
        {
            LeadingContent = null;
            _iconBlock = null;
        }
        UpdateMargins();
    }
    private void RefreshText()
    {
        bool hasText = !string.IsNullOrEmpty(Text);
        if (hasText)
        {
            if (_labelBlock is null)
            {
                _labelBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                };
                _labelBlock.SetBinding(TextBlock.TextProperty, new Binding(nameof(Text)) { Source = this });
                _labelBlock.SetBinding(TextBlock.FontSizeProperty, new Binding(nameof(TextFontSize)) { Source = this });
            }
            if (_userContent is null)
            {
                UpdateMiddleContent(_labelBlock);
            }
        }
        else
        {
            if (_userContent is null)
            {
                UpdateMiddleContent(null);
            }
            _labelBlock = null;
        }
        UpdateMargins();
    }
    private void ApplyVisualProperties()
    {
        UpdateMargins();
        InvalidateMeasure();
    }
    private void UpdateMargins()
    {
        if (_panel is null) return;

        bool hasLeading = _leadingPresenter.Visibility == Visibility.Visible;
        bool hasContent = _contentPresenter.Visibility == Visibility.Visible;
        bool isVertical = _panel.Orientation == System.Windows.Controls.Orientation.Vertical;

        if (hasLeading && hasContent)
        {
            _contentPresenter.Margin = isVertical
                    ? new Thickness(0, Spacing, 0, 0)
                    : new Thickness(Spacing, 0, 0, 0);
        }
        else
        {
            _contentPresenter.Margin = new Thickness(0);
        }
        bool hasPreceding = hasLeading || hasContent;
        if (hasPreceding && _trailingPresenter.Visibility == Visibility.Visible)
        {
            _trailingPresenter.Margin = isVertical
                ? new Thickness(0, Spacing, 0, 0)
                : new Thickness(Spacing, 0, 0, 0);
        }
        else
        {
            _trailingPresenter.Margin = new Thickness(0);
        }
    }
    #endregion

    #region Layout
    protected override Size MeasureOverride(Size constraint)
    {
        var desired = base.MeasureOverride(constraint);
        if (IsSquare)
        {
            double side = Math.Max(desired.Width, desired.Height);
            // No exceder el espacio disponible
            if (!double.IsInfinity(constraint.Width))
                side = Math.Min(side, constraint.Width);
            if (!double.IsInfinity(constraint.Height))
                side = Math.Min(side, constraint.Height);
            side = Math.Max(0, side);
            return new Size(side, side);
        }
        return desired;
    }
    #endregion
}

