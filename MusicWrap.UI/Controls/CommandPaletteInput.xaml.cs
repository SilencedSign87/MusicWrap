using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels;
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
    /// Lógica de interacción para CommandPaletteInput.xaml
    /// </summary>
    public partial class CommandPaletteInput : UserControl
    {
        private Window? _searchWindow;
        private CommandPaletteViewModel _viewModel;
        public static readonly DependencyProperty PlaceholderTextProperty =
         DependencyProperty.Register(
             nameof(PlaceholderText),
             typeof(string),
             typeof(CommandPaletteInput),
             new PropertyMetadata("Search in your library or services..."));
        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }
        public CommandPaletteInput()
        {
            InitializeComponent();
            _viewModel = App.Services.GetRequiredService<CommandPaletteViewModel>();
            DataContext = _viewModel;

            _viewModel.OpenFullSearchRequested -= _viewModel_OpenFullSearchRequested;
            _viewModel.OpenFullSearchRequested += _viewModel_OpenFullSearchRequested;
        }

        private void _viewModel_OpenFullSearchRequested(object? sender, EventArgs e)
        {
        }
    }
}
