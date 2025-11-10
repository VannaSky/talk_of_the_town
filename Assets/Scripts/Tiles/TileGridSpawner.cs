using UnityEngine;
using TWC;
using System.Collections.Generic;
using System.Linq;

namespace Tiles
{
    public sealed class TileGridSpawner : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] TileWorldCreator twc;
        [SerializeField] TileGrid gridRoot;
        [SerializeField] Tile tilePrefab;
        [SerializeField] TWCLayerMapper layerMapper;

        [Header("Placement")]
        [SerializeField] float cellSize = 2f;
        [SerializeField] Vector3 origin = Vector3.zero;
        [SerializeField] bool destroyOld = true;
        
        [SerializeField] TileArchetypeLibrary library;

        public void SpawnOrRebuild()
        {
            if (twc == null || twc.twcAsset == null)
            {
                Debug.LogError("[Spawner] TWC or asset not configured.");
                return;
            }

            // Discover all active blueprint layers
            var activeLayers = GetActiveBlueprintLayers();
            if (activeLayers.Count == 0)
            {
                Debug.LogWarning("[Spawner] No active blueprint layers found.");
                return;
            }

            Debug.Log($"[Spawner] Found {activeLayers.Count} active blueprint layers: {string.Join(", ", activeLayers)}");

            // Get all maps
            var maps = new Dictionary<string, bool[,]>();
            bool[,] refMap = null;
            
            foreach (var layerName in activeLayers)
            {
                var map = SafeMap(layerName);
                if (map != null)
                {
                    maps[layerName] = map;
                    if (refMap == null) refMap = map;
                }
            }

            if (refMap == null || tilePrefab == null || gridRoot == null)
            {
                Debug.LogError("[Spawner] No valid maps or missing prefab/grid.");
                return;
            }

            int w = refMap.GetLength(0), h = refMap.GetLength(1);

            // Clear old tiles
            if (destroyOld)
            {
                foreach (Transform c in gridRoot.transform)
                    DestroyImmediate(c.gameObject);
            }

            // Count tile types
            Dictionary<string, int> tileTypeCounts = new Dictionary<string, int>();

            // Create tiles where ANY layer has data
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                bool isLand = maps.Values.Any(map => map[x, y]);
                if (!isLand) continue;

                var world = new Vector3(origin.x + x * cellSize, origin.y, origin.z + y * cellSize);
                var t = Instantiate(tilePrefab, world, Quaternion.identity, gridRoot.transform);
                t.name = $"Tile_{x}_{y}";
                t.Init(new Vector2Int(x, y), library);

                // Determine type using mapper priority
                string tileType = DetermineTileType(x, y, maps);
                if (tileTypeCounts.ContainsKey(tileType))
                    tileTypeCounts[tileType]++;
                else
                    tileTypeCounts[tileType] = 1;
            }

            gridRoot.RebuildIndex();
            PrintTileTypeCounts(tileTypeCounts);
        }

        List<string> GetActiveBlueprintLayers()
        {
            var layers = new List<string>();
            if (twc?.twcAsset?.mapBlueprintLayers == null) return layers;

            foreach (var layer in twc.twcAsset.mapBlueprintLayers)
            {
                if (layer.active && !string.IsNullOrEmpty(layer.layerName))
                    layers.Add(layer.layerName);
            }
            return layers;
        }

        string DetermineTileType(int x, int y, Dictionary<string, bool[,]> maps)
        {
            if (layerMapper == null)
            {
                // Fallback: return first layer that has this cell
                foreach (var kvp in maps)
                    if (kvp.Value[x, y])
                        return kvp.Key;
                return "Unknown";
            }

            // Use mapper with priority
            TWCLayerMapper.LayerMapping bestMatch = null;
            int highestPriority = int.MinValue;

            foreach (var kvp in maps)
            {
                if (!kvp.Value[x, y]) continue;

                if (layerMapper.TryGetMapping(kvp.Key, out var mapping))
                {
                    if (mapping.priority > highestPriority)
                    {
                        highestPriority = mapping.priority;
                        bestMatch = mapping;
                    }
                }
            }

            return bestMatch != null ? bestMatch.tileStyle.ToString() : "Unknown";
        }

        void PrintTileTypeCounts(Dictionary<string, int> counts)
        {
            int totalCount = 0;
            Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Debug.Log("     TILE TYPE COUNTS");
            Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var kvp in counts.OrderByDescending(k => k.Value))
            {
                Debug.Log($"  {kvp.Key,-12}: {kvp.Value,5}");
                totalCount += kvp.Value;
            }
            
            Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Debug.Log($"  {"TOTAL",-12}: {totalCount,5}");
            Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }

        bool[,] SafeMap(string layer)
        {
            if (string.IsNullOrEmpty(layer) || twc == null) return null;
            try { return twc.GetMapOutputFromBlueprintLayer(layer); }
            catch { return null; }
        }
    }
}