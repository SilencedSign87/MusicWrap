using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Features.Settings.ViewModels;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace MusicWrap.UI.Features.Settings.Views
{
    public partial class SettingsGeneralPage : UserControl
    {
        private readonly SettingsGeneralViewModel _viewModel;
        public SettingsGeneralPage()
        {
            Resources["TrayPosToHAlign"] = new TrayPopupPositionToHorizontalAlignmentConverter();
            Resources["TrayPosToVAlign"] = new TrayPopupPositionToVerticalAlignmentConverter();

            InitializeComponent();

            _viewModel = App.Services.GetRequiredService<SettingsGeneralViewModel>();
            DataContext = _viewModel;

        }

        private class TrayPopupPositionToHorizontalAlignmentConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is TrayPopupPosition pos)
                {
                    return pos switch
                    {
                        TrayPopupPosition.TopLeft or TrayPopupPosition.BottomLeft => HorizontalAlignment.Left,
                        TrayPopupPosition.TopCenter or TrayPopupPosition.BottomCenter => HorizontalAlignment.Center,
                        TrayPopupPosition.TopRight or TrayPopupPosition.BottomRight => HorizontalAlignment.Right,
                        _ => HorizontalAlignment.Right
                    };
                }
                return HorizontalAlignment.Right;
            }
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => throw new NotSupportedException();
        }
        private class TrayPopupPositionToVerticalAlignmentConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is TrayPopupPosition pos)
                {
                    return pos switch
                    {
                        TrayPopupPosition.TopLeft or TrayPopupPosition.TopCenter or TrayPopupPosition.TopRight => VerticalAlignment.Top,
                        TrayPopupPosition.BottomLeft or TrayPopupPosition.BottomCenter or TrayPopupPosition.BottomRight => VerticalAlignment.Bottom,
                        _ => VerticalAlignment.Bottom
                    };
                }
                return VerticalAlignment.Bottom;
            }
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => throw new NotSupportedException();
        }
    }
}


