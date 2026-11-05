using System;
using System.Linq;

namespace MonitoringTools.UI
{
    /// <summary>The Material 3 type scale. Helpers that take a font size pass it through <see cref="Snap"/> so a caller
    /// asking for 13 or 15 still lands on a size the scale defines.</summary>
    internal static class TypeScale
    {
        public static readonly double[] Sizes = { 11, 12, 14, 16, 22, 24, 28, 32, 36, 45, 57 };

        /// <summary>The nearest size on the scale; halfway between two sizes rounds down (13 becomes 12, 15 becomes 14).</summary>
        public static double Snap(double size) =>
            Sizes.OrderBy(s => Math.Abs(s - size)).ThenBy(s => s).First();
    }
}
