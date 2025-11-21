using UnityEngine;
using TWC;
using System.Collections.Generic;
using System.Linq;

namespace Tiles
{
    public sealed class TileGridSpawner : MonoBehaviour
    {
        private const string LogCategory = "TileGridSpawner";

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

        // Local logger helpers
        void LogError(string msg)   => GameLog.LogError(LogCategory, msg, this);
        void LogWarning(string msg) => GameLog.LogWarning(LogCategory, msg, this);
        void LogInfo(string msg)    => GameLog.LogInfo(LogCategory, msg, this);
        void LogVerbose(string msg) => GameLog.LogVerbose(LogCategory, msg, this);

        public void SpawnOrRebuild()
        {
            if (twc == null || twc.twcAsset == null)
            {
                LogError("TileWorldCreator or asset not configured.");
                return;
            }

            // Discover all active blueprint layers
            var activeLayers = GetActiveBlueprintLayers();
            if (activeLayers.Count == 0)
            {
                LogWarning("No active blueprint layers found.");
                return;
            }

            LogInfo($"Found {activeLayers.Count} active blueprint layers: {string.Join(", ", activeLayers)}");

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
                else
                {
                    LogVerbose($"Layer '{layerName}' returned no map (null).");
                }
            }

            if (refMap == null || tilePrefab == null || gridRoot == null)
            {
                LogError("No valid maps or missing prefab/grid.");
                return;
            }

            int w = refMap.GetLength(0), h = refMap.GetLength(1);
            LogInfo($"Spawning tiles for map size {w}x{h}.");

            // Clear old tiles
            if (destroyOld)
            {
                LogInfo("Destroying old tiles under gridRoot.");
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

                // Determine type using mapper priority and APPLY IT
                TileStyle tileStyle = DetermineTileStyle(x, y, maps);
                t.SetStyle(tileStyle);

                // Count for statistics
                string tileType = tileStyle.ToString();
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

        TileStyle DetermineTileStyle(int x, int y, Dictionary<string, bool[,]> maps)
        {
            if (layerMapper == null)
            {
                // Fallback: return Grass (safest default)
                LogWarning("LayerMapper is null, defaulting tile style to Grass.");
                return TileStyle.Grass;
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

            return bestMatch != null ? bestMatch.tileStyle : TileStyle.Grass;
        }

        void PrintTileTypeCounts(Dictionary<string, int> counts)
        {
            if (counts == null || counts.Count == 0)
            {
                LogInfo("No tiles spawned (tile type counts are empty).");
                return;
            }

            int totalCount = counts.Values.Sum();
            var summary = string.Join(
                ", ",
                counts
                    .OrderByDescending(kvp => kvp.Value)
                    .Select(kvp => $"{kvp.Key}={kvp.Value}")
            );

            LogInfo($"Tile type counts: {summary} (total={totalCount})");
        }

        bool[,] SafeMap(string layer)
        {
            if (string.IsNullOrEmpty(layer) || twc == null) return null;

            try
            {
                return twc.GetMapOutputFromBlueprintLayer(layer);
            }
            catch
            {
                LogWarning($"SafeMap: Exception while reading layer '{layer}'.");
                return null;
            }
        }
    }
}
