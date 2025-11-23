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
    List<Vector2Int> clearedTiles = new List<Vector2Int>();
    int halfSize = villageAreaSize / 2;
    
    LogInfo($"Clearing {villageAreaSize}x{villageAreaSize} area centered at {center}");
    
    int objectsDestroyed = 0;
    
    // Process tiles in the area
    for (int dx = -halfSize; dx <= halfSize; dx++)
    {
        for (int dy = -halfSize; dy <= halfSize; dy++)
        {
            Vector2Int pos = center + new Vector2Int(dx, dy);
            
            if (!tileGrid.TryGet(pos, out Tile tile))
                continue;
            
            // Skip water and coast tiles
            if (tile.Archetype?.Style == TileStyle.Water || tile.Archetype?.Style == TileStyle.Coast)
                continue;
            
            // Delete only resource groups (Trees, Stones, Seeds)
            string[] resourceGroups = { "Trees", "Stones", "Seeds", "Resources" };
            
            foreach (var groupName in resourceGroups)
            {
                Transform groupTransform = tile.transform.Find(groupName);
                if (groupTransform != null)
                {
                    int childCount = groupTransform.childCount;
                    if (childCount > 0 && objectsDestroyed < 5)
                    {
                        LogInfo($"Destroying resource group '{groupName}' with {childCount} objects on tile {pos}");
                    }
                    objectsDestroyed += childCount;
                    Destroy(groupTransform.gameObject);
                }
            }
            
            // Clear the tile's resource tracking
            tile.ClearResources();
            
            // Clear the tile's resource visual reference (legacy)
            if (tile.ResourceVisual != null)
            {
                tile.SetResourceVisual(null);
            }
            
            // Set style to grass
            tile.SetStyle(TileStyle.Grass);
            
            // Clear resource data (legacy - now handled by ClearResources above)
            if (tile.HasResource)
            {
                tile.TrySetResource(null);
            }
            
            clearedTiles.Add(pos);
        }
    }
    
    LogInfo($"Cleared {clearedTiles.Count} tiles and destroyed {objectsDestroyed} objects");
    return clearedTiles;
}
        
        private void PlaceBuilding(GameObject prefab, Vector2Int gridPos, ConstructionType constructionType, string buildingName)
        {
            if (!tileGrid.TryGet(gridPos, out Tile tile))
            {
                LogError($"Cannot place {buildingName}: tile at {gridPos} not found!");
                return;
            }
    
            Vector3 tileCenter = tile.transform.position + new Vector3(cellSize / 2f, yOffset, cellSize / 2f);
    
            // Create parent at tile center
            GameObject buildingParent = new GameObject(buildingName);
            buildingParent.transform.position = tileCenter;
    
            // Instantiate actual building as child
            GameObject building = Instantiate(prefab, buildingParent.transform);
    
            // Center the child on X and Z only, keep Y at 0
            if (building.TryGetComponent<Renderer>(out var renderer))
            {
                Vector3 center = renderer.bounds.center;
                Vector3 offset = building.transform.position - center;
                // Only apply X and Z offset, keep Y at 0 (so parent's yOffset is maintained)
                building.transform.localPosition = new Vector3(offset.x, 0f, offset.z);
            }
    
            // Find or create "Building" container under the tile
            Transform buildingContainer = tile.transform.Find("Building");
            if (buildingContainer == null)
            {
                GameObject container = new GameObject("Building");
                container.transform.SetParent(tile.transform);
                container.transform.localPosition = Vector3.zero;
                buildingContainer = container.transform;
            }
    
            // Move the building parent under the Building container (maintains world position)
            buildingParent.transform.SetParent(buildingContainer);
    
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