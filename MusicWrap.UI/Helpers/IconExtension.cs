using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace MusicWrap.UI.Helpers
{
    public class IconExtension : MarkupExtension
    {
        public string Glyph { get; set; } = "e80f";
        public double FontSize { get; set; } = 16;
        public string FontFamily { get; set; } = "Segoe Fluent Icons";

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (!string.IsNullOrEmpty(Glyph))
            {
                string glyphText = Glyph;
                // Detect if Glyph is a unicode hex (e.g., "e80f" or "\xE80F")
                var hexRegex = new System.Text.RegularExpressions.Regex(@"^(?:\\x)?([0-9a-fA-F]{4,6})$");
                var match = hexRegex.Match(Glyph);
                if (match.Success)
                {
                    // Convert hex to char
                    int code = int.Parse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber);
                    glyphText = char.ConvertFromUtf32(code);
                }
                return new TextBlock
                {
                    Text = glyphText,
                    FontSize = FontSize,
                    FontFamily = new System.Windows.Media.FontFamily(FontFamily),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
            }
            return null;
        }
    }
}
