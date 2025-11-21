using UnityEngine;

namespace Tiles
{
    public enum TileStyle { Grass, Forest, Mountain, Water, Coast, Field }
    public enum ResourceType { None, Wood, Stone, Iron, Seed}
    public enum ConstructionType { None, Hut, Mill, Mine, Well, Warehouse }
    
    public static class TransformExtensions
    {
        public static string GetHierarchyPath(this Transform t)
        {
            var path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}