using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MusicWrap.UI.Controls
{
    public class HiddenPanel : Border
    {
        public HiddenPanel()
        {
            Background = Brushes.Transparent;

            Loaded += HiddenPanel_Loaded;
        }

        private void HiddenPanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (Child is UIElement child)
            {
                child.Opacity = 0;
                child.IsHitTestVisible = false;
            }
            Loaded -= HiddenPanel_Loaded;
        }

        protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            if (Child is UIElement child)
            {
                child.Opacity = 1;
                child.IsHitTestVisible = true;
            }
        }
        protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (Child is UIElement child)
            {
                child.Opacity = 0;
                child.IsHitTestVisible = false;
            }
        }
    }
}
