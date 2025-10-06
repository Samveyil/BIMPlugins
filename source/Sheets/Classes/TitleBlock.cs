using Autodesk.Revit.DB;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Methods;

namespace BIMPlugins.Sheets.Classes
{
    public class TitleBlock(Element element, double width, double height)
    {
        public double Width { get; } = UnitUtils.ConvertFromInternalUnits(width, ParameterMethods.GetUnitType());
        public double Height { get; } = UnitUtils.ConvertFromInternalUnits(height, ParameterMethods.GetUnitType());
        public double OffsetX { get; set; } = 0;
        public double OffsetY { get; set; } = 0;
        public Element Element { get; } = element;
        public XYZ Location { get; } = element.ToLocationCoordinates();
        public PageOrientationType PageOrientation { get; } = height > width ? PageOrientationType.Portrait : PageOrientationType.Landscape;
        public PaperSize RevitPaperSize { get; set; }
    }
}