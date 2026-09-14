using System.Collections.Generic;

namespace BIMPlugins.Families.Classes
{
    public class ParametersTestSet(int index)
    {
        public Dictionary<string, object> ParameterValues { get; set; } = new();
        public int Index { get; set; } = index;
    }
}
