using System.Linq;

namespace BIMPlugins.Sheets.Classes
{
    /// <summary>
    /// Invisible characters at the end of a sheet number: two sheets may then show one number, "1" and "1" + one character.
    /// </summary>
    public static class InvisibleChars
    {
        // Pop directional formatting (PDF), the character used for this by hand in Revit:
        // it is a part of the sheet number for Revit, but nothing is seen of it on the sheet.
        public const char Symbol = '‬';

        public static int Count(string text) => text?.Count(c => c == Symbol) ?? 0;

        public static string Remove(string text) => text?.Replace(Symbol.ToString(), string.Empty);

        /// <summary>
        /// The mark shown next to a number in the window: "+2" for two invisible characters, null for none.
        /// </summary>
        public static string Mark(string text)
        {
            var count = Count(text);
            return count == 0 ? null : $"+{count}";
        }
    }
}
