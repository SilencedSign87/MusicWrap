using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Controls
{
    public class TokenizingPanel : Panel
    {
        private readonly List<Rect> _rectangles = new();

        protected override Size MeasureOverride(Size availableSize)
        {
            _rectangles.Clear();

            if (InternalChildren.Count == 0)
                return new Size();

            double availableWidth = availableSize.Width;

            if (double.IsInfinity(availableWidth))
                availableWidth = 1000;

            availableWidth = Math.Max(0, availableWidth);

            double x = 0;
            double y = 0;
            double rowHeight = 0;

            for (int i = 0; i < InternalChildren.Count; i++)
            {
                UIElement child = InternalChildren[i];

                bool isLast = i == InternalChildren.Count - 1;

                if (isLast && child is FrameworkElement input)
                {
                    double minWidth = input.MinWidth;

                    double remaining = availableWidth - x;

                    if (x > 0 && remaining < minWidth)
                    {
                        y += rowHeight;
                        x = 0;
                        rowHeight = 0;
                        remaining = availableWidth;
                    }

                    double inputWidth = Math.Max(minWidth, remaining);

                    child.Measure(new Size(inputWidth, availableSize.Height));

                    double height = child.DesiredSize.Height;

                    _rectangles.Add(
                        new Rect(x, y, inputWidth, height));

                    rowHeight = Math.Max(rowHeight, height);

                    continue;
                }

                child.Measure(
                    new Size(availableWidth, availableSize.Height));

                Size desired = child.DesiredSize;

                if (x > 0 && x + desired.Width > availableWidth)
                {
                    y += rowHeight;
                    x = 0;
                    rowHeight = 0;
                }

                _rectangles.Add(
                    new Rect(x, y, desired.Width, desired.Height));

                x += desired.Width;
                rowHeight = Math.Max(rowHeight, desired.Height);
            }

            double totalHeight = y + rowHeight;

            return new Size(
                double.IsInfinity(availableSize.Width)
                    ? availableWidth
                    : availableSize.Width,
                totalHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double availableWidth = Math.Max(0, finalSize.Width);

            if (InternalChildren.Count == 0)
                return finalSize;

            double x = 0;
            double y = 0;
            double rowHeight = 0;

            for (int i = 0; i < InternalChildren.Count; i++)
            {
                UIElement child = InternalChildren[i];

                Rect rect = i < _rectangles.Count
                    ? _rectangles[i]
                    : new Rect();

                bool isLast = i == InternalChildren.Count - 1;

                if (isLast && child is FrameworkElement input)
                {
                    double minWidth = input.MinWidth;
                    double remaining = availableWidth - x;

                    if (x > 0 && remaining < minWidth)
                    {
                        y += rowHeight;
                        x = 0;
                        rowHeight = 0;
                        remaining = availableWidth;
                    }

                    double width = Math.Max(minWidth, remaining);

                    child.Arrange(new Rect(
                        x,
                        y,
                        width,
                        rect.Height));

                    rowHeight = Math.Max(rowHeight, rect.Height);

                    continue;
                }

                if (x > 0 && x + rect.Width > availableWidth)
                {
                    y += rowHeight;
                    x = 0;
                    rowHeight = 0;
                }

                child.Arrange(new Rect(
                    x,
                    y,
                    rect.Width,
                    rect.Height));

                x += rect.Width;
                rowHeight = Math.Max(rowHeight, rect.Height);
            }

            return finalSize;
        }
    }
}
