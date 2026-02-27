namespace BIMPlugins.Test2dRebar.Classes
{
    public class RebarGroupKey(string type, bool isAbove)
    {
        public string Type { get; private set; } = type;
        public bool IsAbove { get; private set; } = isAbove;

        public override bool Equals(object obj)
        {
            if (obj is RebarGroupKey other)
            {
                return Type == other.Type && IsAbove == other.IsAbove;
            }
            return false;
        }

        // Переопределяем GetHashCode - обязательно вместе с Equals
        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                hash = hash * 23 + (Type?.GetHashCode() ?? 0);
                hash = hash * 23 + IsAbove.GetHashCode();
                return hash;
            }
        }
    }
}