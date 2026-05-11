using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Controls
{
    /// <summary>
    /// Lógica de interacción para SectionTitleSeparator.xaml
    /// </summary>
    public partial class SectionTitleSeparator : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(SectionTitleSeparator),
            new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty IconGlyphProperty =
            DependencyProperty.Register(
            nameof(IconGlyph),
            typeof(string),
            typeof(SectionTitleSeparator),
            new PropertyMetadata("\uE7E8"));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string IconGlyph
        {
            get => (string)GetValue(IconGlyphProperty);
            set => SetValue(IconGlyphProperty, value);
        }
        public SectionTitleSeparator()
        {
            InitializeComponent();
        }
    }
}


