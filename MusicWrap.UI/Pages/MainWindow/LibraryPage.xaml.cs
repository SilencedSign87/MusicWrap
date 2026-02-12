using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels.Library;
using System;
using System.Collections.Generic;
using System.Linq;
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

namespace MusicWrap.UI.Pages.MainWindow
{
    /// <summary>
    /// Lógica de interacción para LibraryPage.xaml
    /// </summary>
    public partial class LibraryPage : UserControl
    {
        public LibraryViewModel vm;
        public LibraryPage()
        {
            InitializeComponent();

            vm = App.Services.GetRequiredService<LibraryViewModel>();
            DataContext = vm;
        }

        private void EntriesListView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key >= Key.A && e.Key <= Key.Z)
            {
                char keyChar = (char)('A' + (e.Key - Key.A));
                ScrollToGroup(keyChar.ToString());
                e.Handled = true;
            }
            else if (e.Key >= Key.D0 && e.Key <= Key.D9)
            {
                ScrollToGroup("#");
                e.Handled = true;
            }
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
            {
                ScrollToGroup("#");
                e.Handled = true;
            }
        }

        private void ScrollToGroup(string groupKey)
        {
            var view = EntriesListView.Items;
            
            if (view.Groups == null || view.Groups.Count == 0)
                return;

            foreach (CollectionViewGroup group in view.Groups)
            {
                if (group.Name?.ToString()?.Equals(groupKey, StringComparison.OrdinalIgnoreCase) == true)
                {
                    if (group.ItemCount > 0)
                    {
                        var firstItem = group.Items[0];
                        
                        EntriesListView.ScrollIntoView(firstItem);
                        
                        EntriesListView.SelectedItem = firstItem;
                        
                        break;
                    }
                }
            }
        }
    }
}

