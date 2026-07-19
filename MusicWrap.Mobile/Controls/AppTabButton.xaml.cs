using System.Diagnostics;
using System.Windows.Input;


namespace MusicWrap.Mobile.Controls;

public partial class AppTabButton : ContentView
{
    public AppTabButton()
    {
        InitializeComponent();

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += OnTapped;
        GestureRecognizers.Add(tapGesture);

        IconLabel.InputTransparent = true;
        Indicator.InputTransparent = true;
        TextLabel.InputTransparent = true;
    }

    #region Bindable Properties
    public static readonly BindableProperty GlyphProperty =
       BindableProperty.Create(nameof(Glyph), typeof(string), typeof(AppTabButton),
           default(string), propertyChanged: OnVisualChanged);
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(AppTabButton),
            default(string));
    public static readonly BindableProperty IsSelectedProperty =
        BindableProperty.Create(nameof(IsSelected), typeof(bool), typeof(AppTabButton),
            false, propertyChanged: OnVisualChanged);
    public static readonly BindableProperty IconFontFamilyProperty =
        BindableProperty.Create(nameof(IconFontFamily), typeof(string), typeof(AppTabButton),
            "SegoeFluentIcons");
    public static readonly BindableProperty SelectedColorProperty =
        BindableProperty.Create(nameof(SelectedColor), typeof(Color), typeof(AppTabButton),
            Colors.White);
    public static readonly BindableProperty UnselectedColorProperty =
        BindableProperty.Create(nameof(UnselectedColor), typeof(Color), typeof(AppTabButton),
            Color.FromArgb("#888888"));
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(AppTabButton));
    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(AppTabButton));
    #endregion
    #region Properties
    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }
    public string IconFontFamily
    {
        get => (string)GetValue(IconFontFamilyProperty);
        set => SetValue(IconFontFamilyProperty, value);
    }
    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }
    public Color UnselectedColor
    {
        get => (Color)GetValue(UnselectedColorProperty);
        set => SetValue(UnselectedColorProperty, value);
    }
    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }
    #endregion
    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
    }
    private static void OnVisualChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((AppTabButton)bindable).UpdateVisualState();
    }
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsSelected))
            UpdateVisualState();
    }
    private void UpdateVisualState()
    {
        var color = IsSelected ? SelectedColor : UnselectedColor;
        IconLabel.TextColor = color;
        IconLabel.Text = Glyph;
        TextLabel.TextColor = color;
        Indicator.Opacity = IsSelected ? 1.0f : 0.0f;
    }
}