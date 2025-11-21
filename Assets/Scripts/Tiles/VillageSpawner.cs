using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tiles
{
    /// <summary>
    /// Spawns initial village buildings (VillageCore, Well, Warehouse) on suitable grass tiles.
    /// Ensures each building has clear space around it and maintains minimum distance between buildings.
    /// </summary>
    public class VillageSpawner : MonoBehaviour
    {
        private const string LogCategory = "VillageSpawner";
        
        [Header("Building Prefabs")]
        [SerializeField] private GameObject villageCorePrefab;
        [SerializeField] private GameObject wellPrefab;
        [SerializeField] private GameObject warehousePrefab;
        
        [Header("References")]
        [SerializeField] private TileGrid tileGrid;
        
        [Header("Placement Settings")]
        [SerializeField] private int villageAreaSize = 7; // Size of the cleared village area (7x7 = 49 tiles)
        [SerializeField] private float cellSize = 2f;     // Must match TileGridSpawner cellSize
        [SerializeField] private float yOffset = 0.5f;
        [SerializeField] private int minDistanceToWater = 2; // tiles outside village area
        
        // Local helper wrappers (as you use them now)
        void LogError(string msg)   => GameLog.LogError(LogCategory, msg, this);
        void LogWarning(string msg) => GameLog.LogWarning(LogCategory, msg, this);
        void LogInfo(string msg)    => GameLog.LogInfo(LogCategory, msg, this);
        void LogVerbose(string msg) => GameLog.LogVerbose(LogCategory, msg, this);
        
        /// <summary>
        /// Main entry point: spawns all initial village buildings.
        /// Call this after tile generation and navmesh baking are complete.
        /// </summary>
        public void SpawnInitialVillage()
        {
            if (tileGrid == null)
            {
                LogError("TileGrid reference is null!");
                return;
            }

            LogInfo($"Using tileGrid={tileGrid.name} (id {tileGrid.GetInstanceID()})");
            LogInfo("Starting initial village placement...");
            
            // 1. Find a suitable center location (not water, not edge)
            Vector2Int? centerPos = FindVillageCenter();
            if (!centerPos.HasValue)
            {
                LogError("Failed to find suitable center for village!");
                return;
            }
            
            LogInfo($"Village center chosen at {centerPos.Value}");
            
            // 2. Clear the village area (remove resources, make all tiles grass)
            List<Vector2Int> clearedTiles = ClearVillageArea(centerPos.Value);
            LogInfo($"Cleared {clearedTiles.Count} tiles for village");
            
            if (clearedTiles.Count < 3)
            {
                LogError("Not enough cleared tiles for buildings!");
                return;
            }
            
            // 3. Place buildings in cleared area
            ShuffleList(clearedTiles);
            
            // Place VillageCore at center
            PlaceBuilding(villageCorePrefab, centerPos.Value, ConstructionType.Hut, "VillageCore");
            LogInfo($"VillageCore placed at {centerPos.Value}");
            
            // Place Well nearby
            var wellPos = clearedTiles.FirstOrDefault(pos => pos != centerPos.Value);
            if (wellPos != Vector2Int.zero)
            {
                PlaceBuilding(wellPrefab, wellPos, ConstructionType.Well, "Well");
                LogInfo($"Well placed at {wellPos}");
            }
            
            // Place Warehouse nearby
            var warehousePos = clearedTiles.FirstOrDefault(pos => pos != centerPos.Value && pos != wellPos);
            if (warehousePos != Vector2Int.zero)
            {
                PlaceBuilding(warehousePrefab, warehousePos, ConstructionType.Warehouse, "Warehouse");
                LogInfo($"Warehouse placed at {warehousePos}");
            }
            
            LogInfo("Initial village spawning complete!");
        }
        
        /// <summary>
        /// Finds a suitable center point for the village (center of map, not water).
        /// Also ensures the surrounding area has enough non-water tiles.
        /// </summary>
        private Vector2Int? FindVillageCenter()
        {
            var allTiles = GetAllTiles();
            if (allTiles.Count == 0) return null;
            
            // Calculate map center
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            
            foreach (var tile in allTiles)
            {
                if (tile.GridPos.x < minX) minX = tile.GridPos.x;
                if (tile.GridPos.x > maxX) maxX = tile.GridPos.x;
                if (tile.GridPos.y < minY) minY = tile.GridPos.y;
                if (tile.GridPos.y > maxY) maxY = tile.GridPos.y;
            }
            
            Vector2Int mapCenter = new Vector2Int((minX + maxX) / 2, (minY + maxY) / 2);
            LogInfo($"Map center calculated at {mapCenter}");
            
            // Try tiles spiraling out from center
            for (int radius = 0; radius < 30; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue; // Only check perimeter
                        
                        Vector2Int testPos = mapCenter + new Vector2Int(dx, dy);
                        if (IsValidVillageCenter(testPos))
                        {
                            LogInfo($"Found valid center at {testPos} (radius {radius} from map center)");
                            return testPos;
                        }
                    }
                }
            }
            
            LogWarning("Could not find suitable center near map center, searching all grass tiles...");
            
            // Fallback: find any valid grass area
            var grassTiles = allTiles.Where(t => t.Archetype?.Style == TileStyle.Grass).ToList();
            ShuffleList(grassTiles);
            
            foreach (var tile in grassTiles)
            {
                if (IsValidVillageCenter(tile.GridPos))
                {
                    LogInfo($"Found valid center at {tile.GridPos} (fallback search)");
                    return tile.GridPos;
                }
            }
            
            LogError("Could not find any valid village center!");
            return null;
        }
        
        private bool IsValidVillageCenter(Vector2Int center)
        {
            if (!tileGrid.TryGet(center, out Tile centerTile))
                return false;

            if (IsWaterOrCoast(centerTile))
                return false;

            int halfSize = villageAreaSize / 2;

            // 1) Require that the entire village area contains NO water/coast
            for (int dx = -halfSize; dx <= halfSize; dx++)
            {
                for (int dy = -halfSize; dy <= halfSize; dy++)
                {
                    Vector2Int pos = center + new Vector2Int(dx, dy);

                    // If the area would go off the map, reject this center completely
                    if (!tileGrid.TryGet(pos, out Tile tile))
                        return false;

                    if (IsWaterOrCoast(tile))
                        return false;
                }
            }

            // 2) Optional: require some extra distance to water/coast around the area
            if (minDistanceToWater > 0)
            {
                int radius = halfSize + minDistanceToWater;

                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        // skip the inner village area; we already checked it
                        if (Mathf.Abs(dx) <= halfSize && Mathf.Abs(dy) <= halfSize)
                            continue;

                        Vector2Int pos = center + new Vector2Int(dx, dy);

                        if (!tileGrid.TryGet(pos, out Tile tile))
                            continue; // outside map is fine here

                        if (IsWaterOrCoast(tile))
                            return false; // too close to water/coast
                    }
                }
            }

            return true;
        }

        private static bool IsWaterOrCoast(Tile tile)
        {
            var style = tile.Archetype?.Style;
            return style == TileStyle.Water || style == TileStyle.Coast;
        }

        /// <summary>
        /// Clears a rectangular area around the center, removing resources and converting to grass.
        /// Returns list of cleared tile positions.
        /// </summary>
        private List<Vector2Int> ClearVillageArea(Vector2Int center)
        {
            var clearedTiles = new List<Vector2Int>();
            var objectsToDestroy = new List<GameObject>();
            int halfSize = villageAreaSize / 2;
            
            // Calculate world bounds for the village area
            if (!tileGrid.TryGet(center, out Tile centerTile))
            {
                LogError("Center tile not found!");
                return clearedTiles;
            }
            
            Vector3 centerWorld = centerTile.transform.position;
            float clearRadius = (villageAreaSize * cellSize) / 2f;
            
            LogInfo($"Clearing area around world pos {centerWorld} with radius {clearRadius}");
            
            // COMPREHENSIVE LAYER SEARCH - find ALL potential object layers
            var allRootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            var foundLayers = new List<Transform>();
            
            LogInfo("Searching for TWC layers...");
            
            foreach (var rootObj in allRootObjects)
            {
                if (rootObj.name.Contains("layer") || rootObj.name.Contains("Layer"))
                {
                    foundLayers.Add(rootObj.transform);
                    LogInfo($"Found root layer: '{rootObj.name}' with {rootObj.transform.childCount} children");
                }
                
                foreach (Transform child in rootObj.transform)
                {
                    if (child.name.Contains("layer") || child.name.Contains("Layer"))
                    {
                        foundLayers.Add(child);
                        LogInfo($"Found child layer: '{child.name}' with {child.childCount} children");
                    }
                    
                    foreach (Transform grandchild in child)
                    {
                        if (grandchild.name.Contains("layer") || grandchild.name.Contains("Layer"))
                        {
                            foundLayers.Add(grandchild);
                            LogInfo($"Found grandchild layer: '{grandchild.name}' with {grandchild.childCount} children");
                        }
                    }
                }
            }
            
            LogInfo($"Total layers found: {foundLayers.Count}");
            
            // Verify library has Grass archetype before starting
            if (tileGrid.TryGet(center, out Tile libTestTile))
            {
                if (libTestTile.Library != null)
                {
                    var grassArchetype = libTestTile.Library.Get(TileStyle.Grass);
                    LogInfo($"Library verification: Grass archetype = {grassArchetype?.name ?? "NULL"}");
                    if (grassArchetype == null)
                    {
                        LogError("Library does not have Grass archetype mapped!");
                    }
                }
                else
                {
                    LogError("Tiles have no Library assigned!");
                }
            }
            
            // Collect objects to destroy from all found layers
            foreach (var layer in foundLayers)
            {
                // Skip Grass_layer - we want to keep grass!
                if (layer.name.ToLower().Contains("grass"))
                    continue;
                
                int objectsInLayer = 0;
                
                foreach (Transform child in layer)
                {
                    if (child.name.Contains("Cluster"))
                    {
                        
                        foreach (Transform obj in child)
                        {
                            float distX = Mathf.Abs(obj.position.x - centerWorld.x);
                            float distZ = Mathf.Abs(obj.position.z - centerWorld.z);
                            
                            if (distX <= clearRadius && distZ <= clearRadius)
                            {
                                objectsToDestroy.Add(obj.gameObject);
                                objectsInLayer++;
                                
                                if (objectsInLayer <= 3)
                                {
                                    LogInfo($"Will destroy '{obj.name}' at ({obj.position.x:F1}, {obj.position.z:F1}) from cluster '{child.name}'");
                                }
                            }
                        }
                    }
                    else
                    {
                        float distX = Mathf.Abs(child.position.x - centerWorld.x);
                        float distZ = Mathf.Abs(child.position.z - centerWorld.z);
                        
                        if (distX <= clearRadius && distZ <= clearRadius)
                        {
                            objectsToDestroy.Add(child.gameObject);
                            objectsInLayer++;
                            
                            if (objectsInLayer <= 3)
                            {
                                LogInfo($"Will destroy '{child.name}' at ({child.position.x:F1}, {child.position.z:F1}) in layer '{layer.name}'");
                            }
                        }
                    }
                }
                
                if (objectsInLayer > 0)
                {
                    LogInfo($"Layer '{layer.name}': marking {objectsInLayer} objects for destruction");
                }
            }
            
            // Now process tiles in the area
            for (int dx = -halfSize; dx <= halfSize; dx++)
            {
                for (int dy = -halfSize; dy <= halfSize; dy++)
                {
                    Vector2Int pos = center + new Vector2Int(dx, dy);
                    
                    if (!tileGrid.TryGet(pos, out Tile tile))
                        continue;
                    
                    // Skip water and coast tiles - can't/shouldn't build there
                    if (tile.Archetype?.Style == TileStyle.Water || tile.Archetype?.Style == TileStyle.Coast)
                        continue;
                    
                    LogVerbose(
                        $"Clearing tile object={tile.name} id={tile.GetInstanceID()} gridPos={tile.GridPos} " +
                        $"before: {tile.Archetype?.name}/{tile.Archetype?.Style}"
                    );
                    
                    tile.SetStyle(TileStyle.Grass);
                    
                    LogVerbose(
                        $"AFTER SetStyle tile object={tile.name} id={tile.GetInstanceID()} gridPos={tile.GridPos} " +
                        $"after: {tile.Archetype?.name}/{tile.Archetype?.Style}"
                    );
                    
                    if (tile.HasResource)
                    {
                        tile.TrySetResource(null);
                    }
                    
                    clearedTiles.Add(pos);
                }
            }
            
            // Now destroy all collected objects
            int destroyedCount = 0;
            foreach (var obj in objectsToDestroy)
            {
                if (obj != null)
                {
                    destroyedCount++;
                    if (destroyedCount <= 5)
                    {
                        LogInfo($"Destroying '{obj.name}' at {obj.transform.position}");
                    }
                    Destroy(obj);
                }
            }
            
            LogInfo($"Cleared {clearedTiles.Count} tiles and destroyed {destroyedCount} objects");
            return clearedTiles;
        }
        
        private void PlaceBuilding(GameObject prefab, Vector2Int gridPos, ConstructionType constructionType, string buildingName)
        {
            if (!tileGrid.TryGet(gridPos, out Tile tile))
            {
                LogError($"Cannot place {buildingName}: tile at {gridPos} not found!");
                return;
            }
            
            Vector3 worldPos = tile.transform.position + new Vector3(0, yOffset, 0);
            GameObject building = Instantiate(prefab, worldPos, Quaternion.identity, transform);
            building.name = $"{buildingName}_{gridPos.x}_{gridPos.y}";
            
            var construction = new ConstructionInstance(constructionType, 0f, false);
            bool success = tile.TrySetBuilding(construction);
            
            if (!success)
            {
                LogWarning($"Failed to mark tile {gridPos} as having building {buildingName}");
            }
        }
        
        private List<Tile> GetAllTiles()
        {
            var tiles = new List<Tile>();
            
            foreach (Transform child in tileGrid.transform)
            {
                if (child.TryGetComponent<Tile>(out var tile))
                {
                    tiles.Add(tile);
                }
            }
            
            return tiles;
        }
        
        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                (list[i], list[randomIndex]) = (list[randomIndex], list[i]);
            }
        }
    }
}
