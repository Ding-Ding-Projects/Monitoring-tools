using System;
using System.Windows;
using System.Windows.Controls;

namespace MonitoringTools.UI
{
    /// <summary>
    /// A text field with a preferred width: it keeps that width while there is room and narrows to the room it is given
    /// when there is not (a narrow window, large text), instead of running out of its card. In a wrap row the buttons
    /// beside it move to the next line first.
    /// </summary>
    public class FieldBox : TextBox
    {
        static FieldBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FieldBox), new FrameworkPropertyMetadata(typeof(TextBox)));
        }

        /// <summary>The width the field is designed at. NaN sizes it like any text box.</summary>
        public double PreferredWidth { get; set; } = double.NaN;

        protected override Size MeasureOverride(Size constraint)
        {
            if (double.IsNaN(PreferredWidth)) return base.MeasureOverride(constraint);
            double w = Math.Max(0, Math.Min(PreferredWidth, constraint.Width));
            var d = base.MeasureOverride(new Size(w, constraint.Height));
            return new Size(w, d.Height);
        }
    }
}
