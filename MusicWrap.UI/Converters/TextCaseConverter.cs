using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace MusicWrap.UI.Converters
{
    public enum TextCase
    {
        None,
        Uppercase,
        Lowercase,
        Capitalize,
        TitleCase
    }

    public class TextCaseConverter : IValueConverter
    {
        public object Convert(
       object value,
       Type targetType,
       object parameter,
       CultureInfo culture)
        {
            if (value is not string text)
                return value;

            if (parameter is not TextCase textCase)
                return text;

            return textCase switch
            {
                TextCase.Uppercase => text.ToUpper(culture),

                TextCase.Lowercase => text.ToLower(culture),

                TextCase.Capitalize => Capitalize(text, culture),

                TextCase.TitleCase => culture.TextInfo.ToTitleCase(
                    text.ToLower(culture)),

                _ => text
            };
        }

        private static string Capitalize(string text, CultureInfo culture)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return char.ToUpper(text[0], culture) + text[1..];
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
