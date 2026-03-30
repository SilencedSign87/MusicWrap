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
