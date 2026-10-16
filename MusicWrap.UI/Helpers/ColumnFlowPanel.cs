using System;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Helpers
{
    public class ColumnFlowPanel : Panel
    {

        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.RegisterAttached(
                "Columns",
                typeof(int),
                typeof(ColumnFlowPanel),
                new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));
        public static int GetColumns(DependencyObject obj)
        {
            return (int)obj.GetValue(ColumnsProperty);
        }

        public static void SetColumns(DependencyObject obj, int value)
        {
            obj.SetValue(ColumnsProperty, value);
        }

        // fixed height for each item
        public double ItemHeight
        {
            get { return (double)GetValue(ItemHeightProperty); }
            set { SetValue(ItemHeightProperty, value); }
        }

        public static readonly DependencyProperty ItemHeightProperty =
            DependencyProperty.Register(
                nameof(ItemHeight),
                typeof(double),
                typeof(ColumnFlowPanel),
                new FrameworkPropertyMetadata(
                    double.NaN,
                    FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange
                    ),
                value =>
                {
                    double height = (double)value;
                    return double.IsNaN(height) ||
                          (!double.IsInfinity(height) && height > 0);
                }
                );


        protected override Size MeasureOverride(Size availableSize)
        {
            int count = InternalChildren.Count;
            if (count == 0) return new Size(0, 0);

            int columns = Math.Max(1, GetColumns(this));
            int rows = (count + columns - 1) / columns;

            double itemWidth = double.IsInfinity(availableSize.Width)
                ? double.PositiveInfinity
                : availableSize.Width / columns;

            double fixedHeight = ItemHeight;
            bool hasFixedHeight = !double.IsNaN(fixedHeight);

            Size constraint = new(
                itemWidth,
                hasFixedHeight ? fixedHeight : double.PositiveInfinity
                );

            double maxDesiredHeight = 0;
            double maxDesiredWidth = 0;

            foreach (UIElement child in InternalChildren)
            {
                child.Measure(constraint);

                maxDesiredWidth = Math.Max(maxDesiredWidth, child.DesiredSize.Width);
                maxDesiredHeight = Math.Max(maxDesiredHeight, child.DesiredSize.Height);
            }

            double rowHeight = hasFixedHeight ? fixedHeight : maxDesiredHeight;
            double desiredWidth = double.IsInfinity(availableSize.Width) ? maxDesiredWidth * columns : availableSize.Width;

            double desiredHeight = rowHeight * rows;

            return new Size(desiredWidth, desiredHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int count = InternalChildren.Count;

            if (count == 0) return finalSize;

            int columns = Math.Max(1, GetColumns(this));
            int rows = (count + columns - 1) / columns;

            double itemWidth = finalSize.Width / columns;

            double itemHeight = !double.IsNaN(ItemHeight) ? ItemHeight : finalSize.Height / rows;

            for (int index = 0; index < count; index++)
            {
                // columns top to bottom, left to right
                int row = index % rows;
                int column = index / rows;

                double x = column * itemWidth;
                double y = row * itemHeight;

                InternalChildren[index].Arrange(new Rect(x, y, itemWidth, itemHeight));
            }

            return finalSize;
        }
    }
}
