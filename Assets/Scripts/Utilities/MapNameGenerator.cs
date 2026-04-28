using UnityEngine;

namespace Utilities
{
    public static class MapNameGenerator
    {
        static readonly string[] Prefixes =
        {
            "Shadow", "Golden", "Iron", "Misty", "Storm", "Silver", "Ember",
            "Frost", "Thorn", "Hollow", "Ashen", "Crimson", "Dusk", "Dawn",
            "Moss", "Raven", "Wolf", "Amber", "Cobalt", "Ivory", "Rusty",
            "Wild", "Stone", "Briar", "Elder", "Copper", "Wither", "Bitter",
            "Fallen", "Silent", "Lonely", "Broken", "Ancient", "Lost", "Sunken"
        };

        static readonly string[] Suffixes =
        {
            "vale", "hollow", "reach", "keep", "haven", "stead", "moor",
            "wick", "ford", "glen", "ridge", "march", "fall", "gate",
            "brook", "thorn", "wood", "field", "crest", "barrow", "peak",
            "watch", "cross", "end", "bury", "dale", "mere", "hold",
            "bridge", "well", "ton", "shore", "grove", "cliff", "mouth"
        };

        public static string Generate()
        {
            string prefix = Prefixes[Random.Range(0, Prefixes.Length)];
            string suffix = Suffixes[Random.Range(0, Suffixes.Length)];
            return prefix + suffix;
        }
    }
}
