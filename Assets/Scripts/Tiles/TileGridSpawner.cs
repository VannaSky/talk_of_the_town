using UnityEngine;
using TWC;
using System.Collections.Generic;

namespace Tiles
{
    public sealed class TileGridSpawner : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] TileWorldCreator twc;
        [SerializeField] TileGrid gridRoot;
        [SerializeField] Tile tilePrefab;

        [Header("Blueprint layer names")]
        [SerializeField] string islandLayer   = "Island";
        [SerializeField] string grassLayer    = "Grass";
        [SerializeField] string forestLayer   = "Forest";
        [SerializeField] string mountainLayer = "Mountain";
        [SerializeField] string waterLayer    = "Water";

        [Header("Placement")]
        [SerializeField] float cellSize = 2f;
        [SerializeField] Vector3 origin = Vector3.zero;
        [SerializeField] bool destroyOld = true;
        
        [SerializeField] TileArchetypeLibrary library;

        public void SpawnOrRebuild()
        {
            var refMap = SafeMap(islandLayer) ?? SafeMap(grassLayer) ??
                SafeMap(forestLayer) ?? SafeMap(mountainLayer) ?? SafeMap(waterLayer);
            if (refMap == null || tilePrefab == null || gridRoot == null)
            { Debug.LogError("[Spawner] Not configured."); return; }

            int w = refMap.GetLength(0), h = refMap.GetLength(1);

            var island   = SafeMap(islandLayer);
            var grass    = SafeMap(grassLayer);
            var forest   = SafeMap(forestLayer);
            var mountain = SafeMap(mountainLayer);
            var water    = SafeMap(waterLayer);

            // Clear old tiles
            foreach (Transform c in gridRoot.transform) DestroyImmediate(c.gameObject);

            // Dictionary to count tile types
            Dictionary<string, int> tileTypeCounts = new Dictionary<string, int>();

            // Create one Tile per *land* cell
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                bool isLand =
                    (island != null && island[x,y]) ||
                    (grass  != null && grass[x,y])  ||
                    (forest != null && forest[x,y]) ||
                    (mountain != null && mountain[x,y]) ||
                    (water != null && !water[x,y]);

                if (!isLand) continue;

                var world = new Vector3(origin.x + x * cellSize, origin.y, origin.z + y * cellSize);
                var t = Instantiate(tilePrefab, world, Quaternion.identity, gridRoot.transform);
                t.name = $"Tile_{x}_{y}";
                t.Init(new Vector2Int(x, y), library);

                // Count tile type
                string tileType = DetermineTileType(x, y, island, grass, forest, mountain, water);
                if (tileTypeCounts.ContainsKey(tileType))
                    tileTypeCounts[tileType]++;
                else
                    tileTypeCounts[tileType] = 1;
            }

            gridRoot.RebuildIndex();

            // Print counts
            PrintTileTypeCounts(tileTypeCounts);
        }

        void PrintTileTypeCounts(Dictionary<string, int> counts)
        {
            int totalCount = 0;
            Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Debug.Log("     TILE TYPE COUNTS");
            Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var kvp in counts)
            {
                Debug.Log($"  {kvp.Key,-12}: {kvp.Value,5}");
                totalCount += kvp.Value;
            }
            
            Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Debug.Log($"  {"TOTAL",-12}: {totalCount,5}");
            Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }

        string DetermineTileType(int x, int y, bool[,] island, bool[,] grass, 
                                 bool[,] forest, bool[,] mountain, bool[,] water)
        {
            if (water != null && water[x, y]) return "Water";
            if (mountain != null && mountain[x, y]) return "Mountain";
            if (forest != null && forest[x, y]) return "Forest";
            if (grass != null && grass[x, y]) return "Grass";
            if (island != null && island[x, y]) return "Island";
            return "Unknown";
        }

        bool[,] SafeMap(string layer)
        {
            if (string.IsNullOrEmpty(layer) || twc == null) return null;
            try { return twc.GetMapOutputFromBlueprintLayer(layer); } catch { return null; }
        }
    }
}