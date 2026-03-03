using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Helpers
{
    public static class ColumnsHelper
    {
        public static int GetColumns(DependencyObject obj)
        {
            return (int)obj.GetValue(ColumnsProperty);
        }

        public static void SetColumns(DependencyObject obj, int value)
        {
            obj.SetValue(ColumnsProperty, value);
        }

        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.RegisterAttached(
                "Columns",
                typeof(int),
                typeof(ColumnsHelper),
                new PropertyMetadata(1));

        public static int GetMinimumItemsForMultiColumn(DependencyObject obj)
        {
            return (int)obj.GetValue(MinimumItemsForMultiColumnProperty);
        }

        public static void SetMinimumItemsForMultiColumn(DependencyObject obj, int value)
        {
            obj.SetValue(MinimumItemsForMultiColumnProperty, value);
        }

        public static readonly DependencyProperty MinimumItemsForMultiColumnProperty =
            DependencyProperty.RegisterAttached(
                "MinimumItemsForMultiColumn",
                typeof(int),
                typeof(ColumnsHelper),
                new PropertyMetadata(10));

        public static double GetMinimumWidthForMultiColumn(DependencyObject obj)
        {
            return (double)obj.GetValue(MinimumWidthForMultiColumnProperty);
        }

        public static void SetMinimumWidthForMultiColumn(DependencyObject obj, double value)
        {
            obj.SetValue(MinimumWidthForMultiColumnProperty, value);
        }

        public static readonly DependencyProperty MinimumWidthForMultiColumnProperty =
            DependencyProperty.RegisterAttached(
                "MinimumWidthForMultiColumn",
                typeof(double),
                typeof(ColumnsHelper),
                new PropertyMetadata(800.0));
    }
}
