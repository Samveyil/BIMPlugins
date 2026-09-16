using System;

namespace BIMPlugins.Families.Classes
{
    public static class RandomValueGenerator
    {
        private static readonly Random _random = new Random();

        public static int GenerateRandomInt(int min, int max, int step)
        {
            int count = (max - min) / step;

            return min + _random.Next(count + 1) * step;
        }
        public static int GenerateRandomBool() => _random.Next(2);

        public static string GetRandomValue(string values)
        {
            var items = values.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);

            return items[_random.Next(items.Length)];
        }
    }
}
