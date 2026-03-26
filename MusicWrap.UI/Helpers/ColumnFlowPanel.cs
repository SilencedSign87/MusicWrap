using System;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Helpers
{

    public class ColumnFlowPanel : Panel
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
                typeof(ColumnFlowPanel),
                new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        protected override Size MeasureOverride(Size availableSize)
        {
            //int columns = GetColumns(this);
            //if (columns <= 0) columns = 1;

            //int itemCount = InternalChildren.Count;
            //int rows = itemCount > 0 ? (int)Math.Ceiling((double)itemCount / columns) : 0;

            //// mesure child
            //foreach (UIElement child in InternalChildren)
            //{
            //    child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            //}

            //// calc max item size
            //double maxWidth = 0;
            //double maxHeight = 0;

            //foreach (UIElement child in InternalChildren)
            //{
            //    maxWidth = Math.Max(maxWidth, child.DesiredSize.Width);
            //    maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
            //}

            //double totalWidth = maxWidth * columns;
            //double totalHeight = maxHeight * rows;

            //return new Size(
            //    double.IsInfinity(availableSize.Width) ? totalWidth : availableSize.Width,
            //    double.IsInfinity(availableSize.Height) ? totalHeight : availableSize.Height
            //);
            int columns = GetColumns(this);
            if (columns <= 0) columns = 1;

            int itemCount = InternalChildren.Count;
            int rows = itemCount > 0 ? (int)Math.Ceiling((double)itemCount / columns) : 0;

            // Calcular ancho disponible por columna
            double itemWidth = availableSize.Width / columns;

            // Medir hijos con restricción de ancho
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(itemWidth, double.PositiveInfinity));
            }

            // calc max item size
            double maxWidth = 0;
            double maxHeight = 0;

            foreach (UIElement child in InternalChildren)
            {
                maxWidth = Math.Max(maxWidth, child.DesiredSize.Width);
                maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
            }

            double totalWidth = maxWidth * columns;
            double totalHeight = maxHeight * rows;

            return new Size(
                double.IsInfinity(availableSize.Width) ? totalWidth : availableSize.Width,
                double.IsInfinity(availableSize.Height) ? totalHeight : availableSize.Height
            );
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int columns = GetColumns(this);
            if (columns <= 0) columns = 1;

            int itemCount = InternalChildren.Count;
            int rows = itemCount > 0 ? (int)Math.Ceiling((double)itemCount / columns) : 0;

            if (rows == 0) return finalSize;

            double itemWidth = finalSize.Width / columns;
            double itemHeight = finalSize.Height / rows;

            for (int index = 0; index < InternalChildren.Count; index++)
            {
                int row = index % rows;
                int col = index / rows;

                double x = col * itemWidth;
                double y = row * itemHeight;

                InternalChildren[index].Arrange(new Rect(x, y, itemWidth, itemHeight));
            }

            return finalSize;
        }
    }
}
