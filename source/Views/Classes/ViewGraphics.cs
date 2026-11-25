using Autodesk.Revit.DB;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using System;
using System.Linq;

namespace BIMPlugins.Views.Classes
{
    /// <summary>What the coloured filters and the view range shapes of the Views panel share.</summary>
    public static class ViewGraphics
    {
        private const double GoldenRatioConjugate = 0.618033988749895;
        private static double _currentHue;

        /// <summary>
        /// A colour well apart from the previous ones: each hue is moved by the golden ratio, so that neighbouring colours
        /// do not look alike however many are taken.
        /// </summary>
        public static Color NextDistinctColor()
        {
            _currentHue = (_currentHue + GoldenRatioConjugate) % 1.0;

            var hue = _currentHue * 360;
            const double saturation = 0.7;
            const double value = 0.9;

            var c = value * saturation;
            var x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
            var m = value - c;

            var (r, g, b) = hue switch
            {
                < 60 => (c, x, 0.0),
                < 120 => (x, c, 0.0),
                < 180 => (0.0, c, x),
                < 240 => (0.0, x, c),
                < 300 => (x, 0.0, c),
                _ => (c, 0.0, x)
            };

            return new Color((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
        }

        /// <summary>The solid fill pattern of the document, found by the pattern itself and not by its name, which depends on the Revit language.</summary>
        public static ElementId GetSolidFillPatternId(Document doc) =>
            doc.ToElements<FillPatternElement>()
                .FirstOrDefault(p => p.GetFillPattern().IsSolidFill)?
                .Id;

        /// <summary>The active view if it is a 3D view; otherwise the user's own default 3D view, made active.</summary>
        public static View3D GetUser3DView(Document doc)
        {
            if (doc.ActiveView is View3D view3D)
                return view3D;

            var viewName = doc.IsWorkshared
                ? "{3D - " + RevitAPI.Application.Username + "}"
                : "{3D}";

            return doc.GetView3D(viewName);
        }
    }
}
