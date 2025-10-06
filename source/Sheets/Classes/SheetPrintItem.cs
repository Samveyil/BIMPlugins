using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace BIMPlugins.Sheets.Classes
{
    public partial class SheetPrintItem(ViewSheet viewSheet) : SheetItem(viewSheet)
    {
        public List<TitleBlock> TitleBlocks { get; set; } = [];
    }
}