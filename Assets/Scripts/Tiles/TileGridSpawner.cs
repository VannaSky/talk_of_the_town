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

        [Header("Load - Resource Prefabs")]
        [Tooltip("All tree variant prefabs. One is picked at random per tree when loading a saved map.")]
        [SerializeField] List<GameObject> treePrefabs;
        [Tooltip("All stone variant prefabs. One is picked at random per stone when loading a saved map.")]
        [SerializeField] List<GameObject> stonePrefabs;
        [Tooltip("All seed/field variant prefabs. One is picked at random per seed when loading a saved map.")]
        [SerializeField] List<GameObject> seedPrefabs;

        // Local logger helpers
        void LogError(string msg)   => GameLog.LogError(LogCategory, msg, this);
        void LogWarning(string msg) => GameLog.LogWarning(LogCategory, msg, this);
        void LogInfo(string msg)    => GameLog.LogInfo(LogCategory, msg, this);
        void LogVerbose(string msg) => GameLog.LogVerbose(LogCategory, msg, this);

#if UNITY_EDITOR
        [ContextMenu("Transfer TWC Objects to Tiles (Manual)")]
        void ManualTransfer()
        {
            if (gridRoot == null)
            {
                LogError("GridRoot not assigned!");
                return;
            }
            
            gridRoot.RebuildIndex();
            TransferTWCObjectsToTiles();
        }
#endif

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
            
            // Transfer TWC objects to tiles
            TransferTWCObjectsToTiles();
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
        
        void TransferTWCObjectsToTiles()
        {
            LogInfo("Transferring TWC objects to tiles...");
            
            // TWC stores objects in cluster containers - search entire hierarchy
            var twcObjects = FindTWCObjects();
            
            if (twcObjects.Count == 0)
            {
                LogWarning("No TWC objects found to transfer! Check if layers exist.");
                return;
            }
            
            int transferred = 0;
            int notFound = 0;
            
            // Track resource groups per tile
            Dictionary<Tile, Dictionary<string, Transform>> tileResourceGroups = new Dictionary<Tile, Dictionary<string, Transform>>();
            
            foreach (var obj in twcObjects)
            {
                // Find closest tile instead of rounding
                Tile closestTile = FindClosestTile(obj.transform.position);
                
                if (closestTile != null)
                {
                    // Determine resource type and get/create the appropriate group
                    string groupName = GetResourceGroupName(obj);
                    Transform groupContainer = GetOrCreateResourceGroup(closestTile, groupName, tileResourceGroups);
                    
                    // Reparent to the group container while maintaining world position
                    obj.transform.SetParent(groupContainer, true);
                    
                    // Store as visual reference if it's a resource
                    if (IsResourceObject(obj))
                    {
                        closestTile.SetResourceVisual(obj);
                    }
                    
                    transferred++;
                    
                    if (transferred <= 5) // Log first few for verification
                    {
                        LogVerbose($"Transferred '{obj.name}' to tile {closestTile.GridPos} under group '{groupName}'");
                    }
                }
                else
                {
                    notFound++;
                    if (notFound <= 3)
                    {
                        LogWarning($"No tile found for object '{obj.name}' at world {obj.transform.position}");
                    }
                }
            }
            
            LogInfo($"Transfer complete: {transferred} objects moved to resource groups, {notFound} not found on grid");
        }
        
        Tile FindClosestTile(Vector3 worldPos)
        {
            // Calculate the grid position this world position is closest to
            Vector2Int gridPos = WorldToGrid(worldPos);
            
            // Try the calculated tile first
            if (gridRoot.TryGet(gridPos, out Tile tile))
            {
                // Compare distance to tile CENTER, not corner
                Vector3 tileCenter = GetTileCenter(tile);
                float distSq = (tileCenter - worldPos).sqrMagnitude;
                float maxDistSq = (cellSize * 0.7f) * (cellSize * 0.7f); // 70% of cell size
                
                if (distSq <= maxDistSq)
                {
                    return tile;
                }
            }
            
            // If not close enough, search immediate neighbors
            Tile closestTile = null;
            float closestDistSq = float.MaxValue;
            
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    Vector2Int neighborPos = gridPos + new Vector2Int(dx, dy);
                    if (gridRoot.TryGet(neighborPos, out Tile neighbor))
                    {
                        Vector3 neighborCenter = GetTileCenter(neighbor);
                        float distSq = (neighborCenter - worldPos).sqrMagnitude;
                        
                        if (distSq < closestDistSq)
                        {
                            closestDistSq = distSq;
                            closestTile = neighbor;
                        }
                    }
                }
            }
            
            // Return closest tile if it's within reasonable distance
            float maxSearchDistSq = (cellSize * 1.0f) * (cellSize * 1.0f);
            if (closestTile != null && closestDistSq <= maxSearchDistSq)
            {
                return closestTile;
            }
            
            return null;
        }
        
        string GetResourceGroupName(GameObject obj)
        {
            string name = obj.name.ToLower();
            
            if (name.Contains("tree")) return "Trees";
            if (name.Contains("rock") || name.Contains("stone")) return "Stones";
            if (name.Contains("seed") || name.Contains("field") || name.Contains("crop") || name.Contains("farm")) return "Seeds";
            
            // Default fallback
            return "Resources";
        }
        
        Transform GetOrCreateResourceGroup(Tile tile, string groupName, Dictionary<Tile, Dictionary<string, Transform>> tileResourceGroups)
        {
            // Get or create the dictionary for this tile
            if (!tileResourceGroups.TryGetValue(tile, out var groups))
            {
                groups = new Dictionary<string, Transform>();
                tileResourceGroups[tile] = groups;
            }
            
            // Get or create the specific group container
            if (!groups.TryGetValue(groupName, out var groupTransform))
            {
                var groupObj = new GameObject(groupName);
                groupTransform = groupObj.transform;
                groupTransform.SetParent(tile.transform, false);
                groupTransform.localPosition = Vector3.zero;
                groups[groupName] = groupTransform;
            }
            
            return groupTransform;
        }
        
        List<GameObject> FindTWCObjects()
        {
            var objects = new List<GameObject>();
            
            // TWC creates a separate GameObject in the scene (not as a child!)
            // Common names: "TileWorldCreator_Map", "TWC_World", "TWC_Objects"
            Transform mapContainer = null;
            
            // Try common TWC container names
            string[] possibleNames = {
                "TileWorldCreator_Map",
                "TWC_World",
                "TWC_Objects",
                twc.twcAsset.worldName // Use the actual worldName from asset
            };
            
            foreach (var name in possibleNames)
            {
                var found = GameObject.Find(name);
                if (found != null)
                {
                    mapContainer = found.transform;
                    LogInfo($"Found TWC map container: '{name}'");
                    break;
                }
            }
            
            if (mapContainer == null)
            {
                LogError($"Cannot find TWC map container! Tried: {string.Join(", ", possibleNames)}");
                LogInfo("Make sure TWC has generated the map first.");
                return objects;
            }
            
            // Now find layers inside the map container
            var foundLayers = new List<Transform>();
            
            foreach (Transform child in mapContainer)
            {
                if (child.name.Contains("layer") || child.name.Contains("Layer"))
                {
                    foundLayers.Add(child);
                    LogInfo($"Found layer: '{child.name}' with {child.childCount} children");
                }
            }
            
            if (foundLayers.Count == 0)
            {
                LogWarning($"No layers found in map container '{mapContainer.name}'!");
                return objects;
            }
            
            LogInfo($"Found {foundLayers.Count} layers total");
            
            // Extract objects from each layer
            foreach (var layer in foundLayers)
            {
                // Skip Grass_layer and Water_layer
                string layerNameLower = layer.name.ToLower();
                if (layerNameLower.Contains("grass") || layerNameLower.Contains("water"))
                {
                    LogVerbose($"Skipping layer: '{layer.name}'");
                    continue;
                }
                
                int objectsInLayer = 0;
                
                foreach (Transform layerChild in layer)
                {
                    if (layerChild.name.Contains("Cluster"))
                    {
                        // Objects are inside clusters
                        foreach (Transform obj in layerChild)
                        {
                            // Only add objects that have visual children or are visual themselves
                            if (obj.childCount > 0 || obj.GetComponent<MeshRenderer>() != null)
                            {
                                objects.Add(obj.gameObject);
                                objectsInLayer++;
                            }
                        }
                    }
                    else if (layerChild.GetComponent<MeshRenderer>() != null)
                    {
                        // Some objects might be directly in layer
                        objects.Add(layerChild.gameObject);
                        objectsInLayer++;
                    }
                }
                
                LogInfo($"Layer '{layer.name}': found {objectsInLayer} objects");
            }
            
            LogInfo($"Total: {objects.Count} TWC objects to transfer");
            return objects;
        }
        
        Vector2Int WorldToGrid(Vector3 worldPos)
        {
            // Tiles are positioned at their corner (lower-left), not center
            // Use floor instead of round to get the correct tile
            int x = Mathf.FloorToInt((worldPos.x - origin.x) / cellSize);
            int z = Mathf.FloorToInt((worldPos.z - origin.z) / cellSize);
            return new Vector2Int(x, z);
        }
        
        Vector3 GetTileCenter(Tile tile)
        {
            // Tile is at corner, center is offset by half cellSize
            Vector3 corner = tile.transform.position;
            return new Vector3(
                corner.x + cellSize / 2f,
                corner.y,
                corner.z + cellSize / 2f
            );
        }
        
        bool IsResourceObject(GameObject obj)
        {
            // Identify resource objects by name patterns
            string name = obj.name.ToLower();
            return name.Contains("tree") || name.Contains("rock") || name.Contains("stone") || name.Contains("ore") || name.Contains("field") || name.Contains("seed");
        }

        /// <summary>
        /// Spawns the tile grid from saved GridData instead of TWC blueprints.
        /// Used by the load path in TWCBridge. Resource visuals are instantiated from
        /// the treePrefab / stonePrefab / seedPrefab fields assigned in the Inspector.
        /// </summary>
        public void SpawnFromGridData(GridData data)
        {
            if (tilePrefab == null || gridRoot == null)
            {
                LogError("Missing tilePrefab or gridRoot — cannot spawn from saved data.");
                return;
            }

            foreach (var tileData in data.tiles)
            {
                var pos = new Vector2Int(tileData.x, tileData.y);
                var world = new Vector3(origin.x + pos.x * cellSize, origin.y, origin.z + pos.y * cellSize);

                var go = Instantiate(tilePrefab, world, Quaternion.identity, gridRoot.transform);
                go.name = $"Tile_{pos.x}_{pos.y}";

                var tile = go.GetComponent<Tile>();
                tile.Init(pos, library);

                if (System.Enum.TryParse<TileStyle>(tileData.tileStyle, out var style))
                    tile.SetStyle(style);

                if (tileData.resources == null) continue;

                foreach (var resData in tileData.resources)
                {
                    if (resData.count <= 0) continue;
                    if (!System.Enum.TryParse<ResourceType>(resData.type, out var resType)) continue;

                    List<GameObject> prefabList = resType switch
                    {
                        ResourceType.Wood  => treePrefabs,
                        ResourceType.Stone => stonePrefabs,
                        ResourceType.Seed  => seedPrefabs,
                        _ => null
                    };

                    if (prefabList == null || prefabList.Count == 0)
                    {
                        LogWarning($"No prefabs assigned for resource type '{resType}'. Assign them in TileGridSpawner Inspector.");
                        continue;
                    }

                    string groupName = resType switch
                    {
                        ResourceType.Wood  => "Trees",
                        ResourceType.Stone => "Stones",
                        ResourceType.Seed  => "Seeds",
                        _ => "Resources"
                    };

                    Transform group = GetOrCreateGroup(tile.transform, groupName);
                    for (int i = 0; i < resData.count; i++)
                    {
                        GameObject prefab = prefabList[Random.Range(0, prefabList.Count)];
                        Instantiate(prefab, group);
                    }
                }
            }

            gridRoot.RebuildIndex();
            LogInfo($"SpawnFromGridData complete: {data.tiles.Count} tiles spawned ({data.width}x{data.height}).");
        }

        Transform GetOrCreateGroup(Transform parent, string groupName)
        {
            Transform group = parent.Find(groupName);
            if (group != null) return group;

            var go = new GameObject(groupName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            return go.transform;
        }
    }
}