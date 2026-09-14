using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace BIMPlugins.Families.Classes
{
    public class ErrorSkipper : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
        {
            IList<FailureMessageAccessor> failures = accessor.GetFailureMessages();
            foreach (FailureMessageAccessor f in failures)
            {
                var failureSeverity = accessor.GetSeverity();
                if (failureSeverity == FailureSeverity.Error)
                {
                    return FailureProcessingResult.ProceedWithRollBack;
                }
                else if (failureSeverity == FailureSeverity.Warning)
                {
                    accessor.DeleteWarning(f);
                    return FailureProcessingResult.ProceedWithRollBack;
                }
            }

            return FailureProcessingResult.Continue;
        }
    }
}
