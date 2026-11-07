using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace MonitoringTools.UI
{
    /// <summary>
    /// Cards in equal columns, each row as tall as its tallest card. It always takes the full width it is offered, so
    /// it is laid out at the same width it was measured at and a card never ends up narrower, and taller, than its row.
    /// With a minimum column width it drops columns on a narrow page instead of squeezing them.
    /// </summary>
    public sealed class CardGrid : Panel
    {
        private int _columns = 2;
        private double _minColumnWidth;

        /// <summary>How many cards sit side by side, at most.</summary>
        public int Columns
        {
            get => _columns;
            set { value = Math.Max(1, value); if (value == _columns) return; _columns = value; InvalidateMeasure(); }
        }

        /// <summary>The narrowest a column may get before the grid uses fewer columns. 0 keeps <see cref="Columns"/> always.</summary>
        public double MinColumnWidth
        {
            get => _minColumnWidth;
            set { if (value == _minColumnWidth) return; _minColumnWidth = Math.Max(0, value); InvalidateMeasure(); }
        }

        private int ColumnsFor(double width) =>
            _minColumnWidth <= 0 || double.IsInfinity(width) ? _columns : Math.Max(1, Math.Min(_columns, (int)Math.Floor(width / _minColumnWidth)));

        protected override Size MeasureOverride(Size constraint)
        {
            bool open = double.IsInfinity(constraint.Width);
            int cols = ColumnsFor(constraint.Width);
            double colW = open ? double.PositiveInfinity : constraint.Width / cols;
            double total = 0, rowH = 0, widest = 0;
            int i = 0;
            foreach (UIElement c in InternalChildren)
            {
                if (c == null) continue;
                c.Measure(new Size(colW, double.PositiveInfinity));
                rowH = Math.Max(rowH, c.DesiredSize.Height);
                widest = Math.Max(widest, c.DesiredSize.Width);
                if (++i % cols == 0) { total += rowH; rowH = 0; }
            }
            total += rowH;
            return new Size(open ? widest * cols : constraint.Width, total);
        }

        protected override Size ArrangeOverride(Size final)
        {
            int cols = ColumnsFor(final.Width);
            double colW = final.Width / cols, y = 0, rowH = 0;
            var row = new List<UIElement>();
            void Place()
            {
                for (int k = 0; k < row.Count; k++) row[k].Arrange(new Rect(k * colW, y, colW, rowH));
                y += rowH; rowH = 0; row.Clear();
            }
            foreach (UIElement c in InternalChildren)
            {
                if (c == null) continue;
                row.Add(c);
                rowH = Math.Max(rowH, c.DesiredSize.Height);
                if (row.Count == cols) Place();
            }
            if (row.Count > 0) Place();
            return final;
        }
    }
}
