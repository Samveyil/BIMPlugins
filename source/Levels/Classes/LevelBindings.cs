using Autodesk.Revit.DB;
using BIMPlugins.ExtStorage.Extensions;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Levels.Classes
{
    /// <summary>
    /// A level parameter of an element with the offsets measured from that level, by built-in parameters,
    /// so that it works in Revit of any language.
    /// </summary>
    internal sealed class LevelBinding(BuiltInParameter level, params BuiltInParameter[] offsets)
    {
        public BuiltInParameter Level { get; } = level;

        /// <summary>
        /// The first offset the element has and that can be changed; null if none.
        /// </summary>
        public Parameter GetOffset(Element element) => offsets
            .Select(element.get_Parameter)
            .FirstOrDefault(p => p != null && !p.IsReadOnly);

        /// <summary>
        /// Whether a new level keeps the offset, so that the element moves unless the offset changes too.
        /// An element without its own level, as most family instances on a level parameter, keeps its place by itself;
        /// stairs and groups keep the offset although their own level is not set.
        /// </summary>
        public bool KeepsOffset(Element element) =>
            element.LevelId != ElementId.InvalidElementId
            || Level is BuiltInParameter.STAIRS_BASE_LEVEL_PARAM or BuiltInParameter.GROUP_LEVEL;
    }

    internal static class LevelBindings
    {
        /// <summary>
        /// The levels elements stand on, the main ones first: walls, columns, stairs, floors, roofs,
        /// family instances, hosted instances, MEP curves and groups.
        /// </summary>
        public static readonly LevelBinding[] Bottom =
        [
            new(BuiltInParameter.WALL_BASE_CONSTRAINT, BuiltInParameter.WALL_BASE_OFFSET),
            new(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM, BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM),
            new(BuiltInParameter.STAIRS_BASE_LEVEL_PARAM, BuiltInParameter.STAIRS_BASE_OFFSET),
            new(BuiltInParameter.LEVEL_PARAM, BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM),
            new(BuiltInParameter.ROOF_BASE_LEVEL_PARAM, BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM),
            new(BuiltInParameter.FAMILY_LEVEL_PARAM, BuiltInParameter.INSTANCE_ELEVATION_PARAM,
                BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM, BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM),
            new(BuiltInParameter.INSTANCE_SCHEDULE_ONLY_LEVEL_PARAM, BuiltInParameter.INSTANCE_ELEVATION_PARAM,
                BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM, BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM),
            new(BuiltInParameter.RBS_START_LEVEL_PARAM, BuiltInParameter.RBS_OFFSET_PARAM),
            new(BuiltInParameter.GROUP_LEVEL, BuiltInParameter.GROUP_OFFSET_FROM_LEVEL)
        ];

        /// <summary>
        /// The levels the tops of elements are bound to.
        /// </summary>
        public static readonly LevelBinding[] Top =
        [
            new(BuiltInParameter.WALL_HEIGHT_TYPE, BuiltInParameter.WALL_TOP_OFFSET),
            new(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM, BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM),
            new(BuiltInParameter.STAIRS_TOP_LEVEL_PARAM, BuiltInParameter.STAIRS_TOP_OFFSET)
        ];

        /// <summary>
        /// Elements bound to the level by one of the bindings, or by their own level if <paramref name="withOwnLevel"/>.
        /// Members of groups are left out unless <paramref name="withGroupMembers"/>.
        /// </summary>
        public static List<Element> FindElements(Document doc, ElementId levelId, IEnumerable<LevelBinding> bindings, bool withOwnLevel,
            bool withGroupMembers = false)
        {
            var filters = bindings
                .Select(b => (ElementFilter)new ElementId(b.Level).CreateEqualsFilter(levelId))
                .ToList();
            if (withOwnLevel)
                filters.Add(new ElementLevelFilter(levelId));

            var filter = new LogicalOrFilter(filters);

            // Model elements only, not the plan views of the level; groups are taken on their own, as they are not among them.
            var groups = new FilteredElementCollector(doc)
                .OfClass(typeof(Group))
                .WherePasses(filter);

            return doc.ToModelElements(filter)
                .Where(e => e is not Group)
                .Concat(groups)
                .Where(e => withGroupMembers || e.GroupId == ElementId.InvalidElementId)
                .ToList();
        }

        /// <summary>
        /// Family instances on the work plane of the level: their host is the level and their height is the offset from it,
        /// "Смещение от главной модели", not a level parameter.
        /// </summary>
        public static List<FamilyInstance> FindHosted(Document doc, ElementId levelId, bool withGroupMembers) => new FilteredElementCollector(doc)
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .Where(fi => fi.Host?.Id.Equals(levelId) == true)
            .Where(fi => withGroupMembers || fi.GroupId == ElementId.InvalidElementId)
            .ToList();

        /// <summary>
        /// The offsets of a family instance from the level it stands on: the two ends of a beam,
        /// or "Смещение от главной модели" of other instances. Empty if none can be changed.
        /// </summary>
        public static List<Parameter> GetHostOffsets(Element element)
        {
            var beamEnds = new[] { BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION, BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION }
                .Select(element.get_Parameter)
                .Where(p => p is { IsReadOnly: false })
                .ToList();
            if (beamEnds.Count != 0)
                return beamEnds;

            return element.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM) is { IsReadOnly: false } offset
                ? [offset]
                : [];
        }

        /// <summary>
        /// Lowers the offset by the shift of the level; false if Revit does not take the new value.
        /// </summary>
        public static bool Shift(Parameter offset, double shift)
        {
            try
            {
                return offset.Set(offset.AsDouble() - shift);
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                return false;
            }
        }

        /// <summary>
        /// The binding whose level parameter holds the level; null if none does.
        /// </summary>
        public static LevelBinding Find(Element element, IEnumerable<LevelBinding> bindings, ElementId levelId) =>
            bindings.FirstOrDefault(b => element.get_Parameter(b.Level)?.AsElementId().Equals(levelId) == true);

        /// <summary>
        /// The element as it is shown in reports: category, name and id.
        /// </summary>
        public static string Describe(Element element) => $"{element.Category?.Name}: {element.Name} (Id {element.Id})";
    }
}
