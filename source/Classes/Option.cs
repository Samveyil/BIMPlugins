namespace BIMPlugins.Classes
{
    /// <summary>A value for a combo box with the text it shows.</summary>
    public sealed class Option<T>(T value, string text)
    {
        public T Value { get; } = value;
        public string Text { get; } = text;
    }
}
