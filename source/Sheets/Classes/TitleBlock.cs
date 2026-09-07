using Autodesk.Revit.DB;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;

namespace BIMPlugins.Sheets.Classes
{
    public class TitleBlock(Element element, double width, double height)
    {
        public double Width { get; } = width.ToMillimeters();
        public double Height { get; } = height.ToMillimeters();
        public double OffsetX { get; set; } = 0;
        public double OffsetY { get; set; } = 0;
        public Element Element { get; } = element;
        public XYZ Location { get; } = element.ToPoint();
        public PageOrientationType PageOrientation { get; } = height > width ? PageOrientationType.Portrait : PageOrientationType.Landscape;
        public PaperSize RevitPaperSize { get; set; }
    }
}