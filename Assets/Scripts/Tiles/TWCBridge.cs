using UnityEngine;
using TWC;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AI.Navigation;
using UnityEngine.Serialization;

namespace Tiles
{
    public sealed class TWCBridge : MonoBehaviour
    {
        private const string LogCategory = "TWCBridge";

        /// <summary>
        /// Set this to a map filename (e.g. "map_01.json") before loading MainScene
        /// to trigger the load path instead of procedural generation.
        /// Cleared automatically after use.
        /// </summary>
        public static string MapFileToLoad = null;

        [Header("Refs")]
        [SerializeField] TileWorldCreator tileWorldCreator;
        [SerializeField] private GameObject tileWorldCreatorMap;
        [SerializeField] TileGrid tileGrid;
        [SerializeField] TileGridSpawner spawner;
        [SerializeField] TileArchetypeLibrary library;
        [SerializeField] TWCLayerMapper layerMapper;
        [SerializeField] NavMeshSurface navMeshSurface;
        [SerializeField] VillageSpawner villageSpawner;
        [SerializeField] bool dontDestroyOnLoad = true;
        [Header("Overlay Detection")]
        [SerializeField] bool useBuildLayerFallback = true;
        [Tooltip("Parent transforms under which TWC instantiates overlay objects (enable 'group under parent' in TWC).")]
        [SerializeField] List<OverlayBuildRoot> overlayBuildRoots = new List<OverlayBuildRoot>();
        
        [Header("Village Placement Retry")]
        [SerializeField] private int maxVillagePlacementRetries = 5;
        private int currentRetryCount = 0;
        
        [System.Serializable]
        public class OverlayBuildRoot
        {
            public Transform root;
            public ResourceType resourceType;
        }

        [Tooltip("Must match your grid spacing in world units (X/Z).")]
        [SerializeField] float cellSize = 2f;
        [Tooltip("World-space origin of cell (0,0).")]
        [SerializeField] Vector3 origin = Vector3.zero;

        [Header("Run options")]
        [SerializeField] bool autoRunOnStart = true;
        [SerializeField] bool regenerateTWCMap = true;
        [SerializeField] bool spawnTilesBeforeSync = true;
        [SerializeField] bool rebakeNavMesh = true;
        [SerializeField] float navMeshBakeDelay = 0.5f; // Time to wait before baking

        // Local logger helpers (same pattern as VillageSpawner)
        void LogError(string msg)   => GameLog.LogError(LogCategory, msg, this);
        void LogWarning(string msg) => GameLog.LogWarning(LogCategory, msg, this);
        void LogInfo(string msg)    => GameLog.LogInfo(LogCategory, msg, this);
        void LogVerbose(string msg) => GameLog.LogVerbose(LogCategory, msg, this);

        void Awake()
        {
            if (cellSize <= 0f) cellSize = 2f;
            if (origin == default && tileWorldCreator != null)
            {
                origin = tileWorldCreator.transform.position;
                if (dontDestroyOnLoad)
                {
                    DontDestroyOnLoad(tileWorldCreatorMap);
                    DontDestroyOnLoad(tileGrid.gameObject);
                    DontDestroyOnLoad(gameObject);
             
                }
                    
            }
                
            // Ensure NavMeshSurface uses Physics Colliders, not Render Meshes
            if (navMeshSurface != null)
            {
                navMeshSurface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
                LogInfo("NavMeshSurface configured to use Physics Colliders");
            }
        }

        void Start()
        {
            if (!string.IsNullOrEmpty(MapFileToLoad))
            {
                string fileToLoad = MapFileToLoad;
                MapFileToLoad = null; // Clear before loading so retries don't re-trigger load
                LoadFromFile(fileToLoad);
            }
            else if (autoRunOnStart)
            {
                GenerateBuildAndSync();
            }
        }

        void OnEnable()
        {
            if (tileWorldCreator == null)
            {
                LogError("TileWorldCreator is not assigned.");
                return;
            }

            tileWorldCreator.OnBlueprintLayersComplete += OnBlueprintLayersComplete;
            tileWorldCreator.OnBuildLayersComplete += OnBuildLayersComplete;
            LogInfo("Subscribed to TWC events.");
        }

        void OnDisable()
        {
            if (tileWorldCreator == null) return;
            tileWorldCreator.OnBlueprintLayersComplete -= OnBlueprintLayersComplete;
            tileWorldCreator.OnBuildLayersComplete -= OnBuildLayersComplete;
            LogInfo("Unsubscribed from TWC events.");
        }

        [ContextMenu("Generate → Build → Sync")]
        public void GenerateBuildAndSync()
        {
            if (tileWorldCreator == null)
            {
                LogError("No TileWorldCreator set.");
                return;
            }

            if (regenerateTWCMap)
            {
                // Clean up before regenerating
                CleanupBeforeRegeneration();
        
                LogInfo("ExecuteAllBlueprintLayers()");
                currentRetryCount = 0; // Reset retry counter for new generation
                tileWorldCreator.ExecuteAllBlueprintLayers();
                // Events will trigger BuildLayers → Sync → NavMesh
            }
            else
            {
                LogInfo("Using existing TWC map, skipping regeneration.");

                if (!VerifyBlueprintData())
                {
                    LogError("Blueprint data not available! Generate the map manually first or enable regenerateTWCMap.");
                    return;
                }

                if (spawnTilesBeforeSync && spawner != null)
                {
                    spawner.SpawnOrRebuild();
                }

                // 1) Sync terrain from existing blueprints
                SyncFromBlueprints();

                // 2) Spawn village (with retry, but regeneration is disabled)
                if (villageSpawner != null)
                {
                    LogInfo("Spawning village after blueprint sync (no map regen)...");
                    bool success = villageSpawner.SpawnInitialVillage();
                    if (!success)
                    {
                        LogWarning("Village placement failed, but regenerateTWCMap is disabled. Enable it to allow automatic retries.");
                    }
                }
                else
                {
                    LogWarning("VillageSpawner reference is null. Skipping village spawning (no map regen).");
                }

                // 3) Register resources
                RegisterExistingResources();

#pragma warning disable CS4014
                RebakeNavMeshAsync();
#pragma warning restore CS4014

                LogInfo("TWC → TileGrid sync complete (no map regen).");
            }
        }

        bool VerifyBlueprintData()
        {
            var activeLayers = GetActiveBlueprintLayers();
            if (activeLayers.Count == 0)
            {
                LogWarning("No active blueprint layers found.");
                return false;
            }

            foreach (var layerName in activeLayers)
            {
                var testMap = GetMap(layerName);
                if (testMap != null)
                {
                    LogInfo($"Blueprint data verified: {testMap.GetLength(0)}x{testMap.GetLength(1)} from layer '{layerName}'");
                    return true;
                }
            }

            LogWarning("No valid blueprint map data found.");
            return false;
        }

        void OnBlueprintLayersComplete(TileWorldCreator twc)
        {
            LogInfo("OnBlueprintLayersComplete → ExecuteAllBuildLayers(false)");
            tileWorldCreator.ExecuteAllBuildLayers(false);
        }

        void OnBuildLayersComplete(TileWorldCreator twc)
        {
            LogInfo("OnBuildLayersComplete");

            if (spawnTilesBeforeSync && spawner != null)
            {
                LogInfo("Spawning/Rebuilding tiles from blueprint maps…");
                spawner.SpawnOrRebuild();
            }

            // 1) Sync all terrain styles from blueprints
            SyncFromBlueprints();

            // 2) Try to spawn village (with automatic retry if it fails)
            TrySpawnVillageWithRetry();

            // 3) Register resources after village is placed
            RegisterExistingResources();

#pragma warning disable CS4014
            RebakeNavMeshAsync();
#pragma warning restore CS4014

            LogInfo("TWC → TileGrid sync complete (with village).");
        }

        async Task RebakeNavMeshAsync()
        {
            if (!rebakeNavMesh || navMeshSurface == null)
                return;

            LogInfo($"Waiting {navMeshBakeDelay}s for physics to settle before NavMesh bake...");
            
            await Task.Delay((int)(navMeshBakeDelay * 1000));

            if (!Application.isPlaying || this == null)
                return;

            LogInfo("Baking NavMesh...");
            navMeshSurface.BuildNavMesh();
            LogInfo("NavMesh baked successfully.");
        }

        void SyncFromBlueprints()
        {
            if (tileGrid == null)
            {
                LogError("No TileGrid set.");
                return;
            }

            if (layerMapper == null)
            {
                LogError("No TWCLayerMapper set. Cannot sync without mapping configuration.");
                return;
            }

            var activeLayers = GetActiveBlueprintLayers();
            if (activeLayers.Count == 0)
            {
                LogWarning("No active blueprint layers found.");
                return;
            }

            // Load all maps
            var maps = new Dictionary<string, bool[,]>();
            bool[,] refMap = null;

            foreach (var layerName in activeLayers)
            {
                var map = GetMap(layerName);
                if (map != null)
                {
                    maps[layerName] = map;
                    if (refMap == null) refMap = map;
                }
            }

            if (refMap == null)
            {
                LogWarning("No valid blueprint maps found.");
                return;
            }

            int w = refMap.GetLength(0), h = refMap.GetLength(1);
            int styled = 0, resSet = 0, tilesSeen = 0;

            var allMappings = layerMapper.GetAllMappingsSortedByPriority().ToList();

            LogInfo($"Syncing {activeLayers.Count} layers to {w}x{h} grid using {allMappings.Count} mappings");

            // Apply base terrain styles
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var p = new Vector2Int(x, y);
                if (!tileGrid.TryGet(p, out var tile)) continue;
                tilesSeen++;

                if (tile.Library == null && library != null)
                    tile.SetLibrary(library);

                TWCLayerMapper.LayerMapping bestMatch = null;
                int highestPriority = int.MinValue;

                foreach (var mapping in allMappings)
                {
                    if (maps.TryGetValue(mapping.twcLayerName, out var map) && map[x, y])
                    {
                        if (mapping.priority > highestPriority)
                        {
                            highestPriority = mapping.priority;
                            bestMatch = mapping;
                        }
                    }
                }

                if (bestMatch != null)
                {
                    tile.SetStyle(bestMatch.tileStyle);
                    styled++;
                }
            }

            // Apply overlays (resources)
            if (useBuildLayerFallback)
            {
                resSet = ApplyOverlaysFromBuildRoots(w, h);
            }

            LogInfo($"Tiles seen: {tilesSeen}, styled: {styled}, resources set: {resSet}");
        }

        int ApplyOverlaysFromBuildRoots(int w, int h)
        {
            if (overlayBuildRoots.Count == 0)
                return 0;

            var overlayCells = new Dictionary<ResourceType, HashSet<Vector2Int>>();

            foreach (var buildRoot in overlayBuildRoots)
            {
                if (buildRoot.root == null) continue;

                var cells = CellsFromBuildRoot(buildRoot.root);
                if (!overlayCells.ContainsKey(buildRoot.resourceType))
                    overlayCells[buildRoot.resourceType] = new HashSet<Vector2Int>();

                foreach (var cell in cells)
                    overlayCells[buildRoot.resourceType].Add(cell);
            }

            int resSet = 0;
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var p = new Vector2Int(x, y);
                if (!tileGrid.TryGet(p, out var tile)) continue;

                tile.TrySetResource(null);

                foreach (var kvp in overlayCells)
                {
                    if (kvp.Value.Contains(p))
                    {
                        bool canPlace = false;

                        switch (kvp.Key)
                        {
                            case ResourceType.Wood:
                                canPlace = tile.Archetype?.Style == TileStyle.Forest;
                                break;
                            case ResourceType.Stone:
                                canPlace = tile.Archetype?.Style == TileStyle.Mountain;
                                break;
                        }

                        if (canPlace)
                        {
                            tile.TrySetResource(new ResourceInstance(kvp.Key, 1));
                            resSet++;
                            break;
                        }
                    }
                }
            }

            return resSet;
        }

        List<string> GetActiveBlueprintLayers()
        {
            var layers = new List<string>();
            if (tileWorldCreator?.twcAsset?.mapBlueprintLayers == null)
                return layers;

            foreach (var layer in tileWorldCreator.twcAsset.mapBlueprintLayers)
            {
                if (layer.active && !string.IsNullOrEmpty(layer.layerName))
                    layers.Add(layer.layerName);
            }

            return layers;
        }

        bool[,] GetMap(string name)
        {
            if (string.IsNullOrEmpty(name) || tileWorldCreator == null)
                return null;

            try
            {
                var m = tileWorldCreator.GetMapOutputFromBlueprintLayer(name);
                if (m == null)
                    LogWarning($"Map '{name}' returned null.");
                return m;
            }
            catch (System.Exception e)
            {
                LogWarning($"GetMapOutputFromBlueprintLayer('{name}') failed: {e.Message}");
                return null;
            }
        }

        HashSet<Vector2Int> CellsFromBuildRoot(Transform root)
        {
            var set = new HashSet<Vector2Int>();
            if (root == null) return set;

            var all = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == root) continue;

                var local = t.position - origin;
                int x = Mathf.RoundToInt(local.x / Mathf.Max(0.0001f, cellSize));
                int y = Mathf.RoundToInt(local.z / Mathf.Max(0.0001f, cellSize));
                set.Add(new Vector2Int(x, y));
            }

            LogInfo($"Build-root '{root.name}' → {set.Count} overlay cells.");
            return set;
        }

        [ContextMenu("Debug: List Active Layers")]
        void DebugListActiveLayers()
        {
            var layers = GetActiveBlueprintLayers();
            LogInfo($"Active Blueprint Layers ({layers.Count}):");
            foreach (var layer in layers)
                LogInfo($"  - {layer}");
        }

        [ContextMenu("Debug: Validate Mapper Configuration")]
        void DebugValidateMapper()
        {
            if (layerMapper == null)
            {
                LogError("No LayerMapper assigned!");
                return;
            }

            var activeLayers = GetActiveBlueprintLayers();
            var mappedLayers = new HashSet<string>();

            layerMapper.RebuildCache();
            foreach (var mapping in layerMapper.mappings)
                mappedLayers.Add(mapping.twcLayerName);

            LogInfo("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            LogInfo("  LAYER MAPPER VALIDATION");
            LogInfo("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            LogInfo($"\nMapped Layers ({mappedLayers.Count}):");
            foreach (var layer in mappedLayers)
                LogInfo($"  ✓ {layer}");

            var unmappedLayers = activeLayers.Where(l => !mappedLayers.Contains(l)).ToList();
            if (unmappedLayers.Count > 0)
            {
                LogWarning($"\nUnmapped Active Layers ({unmappedLayers.Count}):");
                foreach (var layer in unmappedLayers)
                    LogWarning($"  ✗ {layer} (no mapping configured!)");
            }

            var inactiveMappings = mappedLayers.Where(l => !activeLayers.Contains(l)).ToList();
            if (inactiveMappings.Count > 0)
            {
                LogInfo($"\nMapped But Inactive Layers ({inactiveMappings.Count}):");
                foreach (var layer in inactiveMappings)
                    LogInfo($"  ○ {layer}");
            }

            LogInfo("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }
        
        /// <summary>
        /// Scans all tiles for existing resource group children and registers their counts
        /// </summary>
        [ContextMenu("Register Existing Resources")]
        public void RegisterExistingResources()
        {
            if (tileGrid == null)
            {
                LogError("TileGrid is null, cannot register resources");
                return;
            }

            int tilesProcessed = 0;
            int totalResources = 0;

            // Get all tiles
            foreach (Transform tileTransform in tileGrid.transform)
            {
                if (!tileTransform.TryGetComponent<Tile>(out var tile))
                    continue;

                tilesProcessed++;

                // Check for Trees group
                Transform treesGroup = tileTransform.Find("Trees");
                if (treesGroup != null && treesGroup.childCount > 0)
                {
                    tile.AddResource(ResourceType.Wood, treesGroup.childCount);
                    totalResources += treesGroup.childCount;
                }

                // Check for Stones group
                Transform stonesGroup = tileTransform.Find("Stones");
                if (stonesGroup != null && stonesGroup.childCount > 0)
                {
                    tile.AddResource(ResourceType.Stone, stonesGroup.childCount);
                    totalResources += stonesGroup.childCount;
                }

                // Check for Seeds group
                Transform seedsGroup = tileTransform.Find("Seeds");
                if (seedsGroup != null && seedsGroup.childCount > 0)
                {
                    tile.AddResource(ResourceType.Seed, seedsGroup.childCount);
                    totalResources += seedsGroup.childCount;
                }
            }

            LogInfo($"Resource registration complete: {tilesProcessed} tiles processed, {totalResources} total resources registered");
        }
        
        
        [ContextMenu("Export Grid to JSON")]
        public void ExportGridToJson()
        {
            if (tileGrid == null)
            {
                LogError("TileGrid is null, cannot export");
                return;
            }

            string filename = $"tilegrid_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
            tileGrid.SaveToFile(filename);
            LogInfo($"Grid exported to: {filename}");
        }

        [ContextMenu("Print LLM Summary")]
        public void PrintLLMSummary()
        {
            if (tileGrid == null)
            {
                LogError("TileGrid is null, cannot generate summary");
                return;
            }

            string summary = tileGrid.GetLLMSummary();
            LogInfo($"\n{summary}");
        }
        
        [ContextMenu("Export Grid to JSON (Compact for LLM)")]
        public void ExportGridToJsonCompact()
        {
            if (tileGrid == null)
            {
                LogError("TileGrid is null, cannot export");
                return;
            }

            string filename = $"tilegrid_compact_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
            tileGrid.SaveToFileCompact(filename);
            LogInfo($"Compact grid exported to: {filename}");
        }

        /// <summary>
        /// Saves the current map with an auto-incremented filename (map_01.json, map_02.json, ...).
        /// Call this manually via GUI button after approving a generated map.
        /// </summary>
        [ContextMenu("Save Map")]
        public void SaveMap()
        {
            if (tileGrid == null)
            {
                LogError("TileGrid is null, cannot save map");
                return;
            }

            string filename = GetNextMapFilename();
            tileGrid.SaveToFile(filename);
            LogInfo($"Map saved as: {filename} (path: {System.IO.Path.Combine(Application.persistentDataPath, filename)})");
        }

        private string GetNextMapFilename()
        {
            string dir = Application.persistentDataPath;
            int highest = 0;

            foreach (string file in System.IO.Directory.GetFiles(dir, "map_??.json"))
            {
                string numberPart = System.IO.Path.GetFileNameWithoutExtension(file).Substring(4); // "map_01" → "01"
                if (int.TryParse(numberPart, out int n))
                    highest = Mathf.Max(highest, n);
            }

            return $"map_{(highest + 1):D2}.json";
        }

        /// <summary>
        /// Loads a saved map from file and rebuilds the tile grid without running TWC generation.
        /// VillageSpawner still runs normally for a fresh game start on the loaded map.
        /// </summary>
        public void LoadFromFile(string filename)
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath, filename);

            if (!System.IO.File.Exists(path))
            {
                LogError($"Map file not found: {path}");
                return;
            }

            LogInfo($"Loading map from: {filename}");

            string json = System.IO.File.ReadAllText(path);
            GridData data = JsonUtility.FromJson<GridData>(json);

            if (data == null || data.tiles == null || data.tiles.Count == 0)
            {
                LogError("Failed to deserialize map data or map is empty.");
                return;
            }

            // 1. Clear any existing tiles
            tileGrid.DestroyAllTiles();

            // 2. Spawn tiles and resource visuals from saved data (no TWC)
            spawner.SpawnFromGridData(data);

            // 3. Count resources from the spawned hierarchy into tile data
            RegisterExistingResources();

            // 4. Spawn village fresh (no retry/regeneration — map is fixed)
            if (villageSpawner != null)
            {
                bool success = villageSpawner.SpawnInitialVillage();
                if (!success)
                    LogWarning("Village placement failed on loaded map. The saved map may not have a valid village area.");
            }

            // 5. Bake NavMesh
#pragma warning disable CS4014
            RebakeNavMeshAsync();
#pragma warning restore CS4014

            LogInfo($"Map loaded successfully: {filename} ({data.width}x{data.height}, {data.tiles.Count} tiles)");
        }
        
        private void TrySpawnVillageWithRetry()
        {
            if (villageSpawner == null)
            {
                LogWarning("VillageSpawner reference is null. Skipping village spawning.");
                return;
            }

            LogInfo($"Attempting village spawn (attempt {currentRetryCount + 1}/{maxVillagePlacementRetries})...");
            bool success = villageSpawner.SpawnInitialVillage();

            if (!success)
            {
                currentRetryCount++;
        
                if (currentRetryCount >= maxVillagePlacementRetries)
                {
                    LogError($"Failed to place village after {maxVillagePlacementRetries} attempts. Giving up.");
                    currentRetryCount = 0; // Reset for next manual attempt
                    return;
                }

                LogWarning($"Village placement failed. Regenerating map (retry {currentRetryCount}/{maxVillagePlacementRetries})...");
        
                // Regenerate the entire map and try again
                RegenerateMapForVillageRetry();
            }
            else
            {
                LogInfo("Village placed successfully!");
                currentRetryCount = 0; // Reset counter on success
            }
        }

        private void RegenerateMapForVillageRetry()
        {
            LogInfo("Starting map regeneration for village retry...");
    
            // Clean up everything before regenerating
            CleanupBeforeRegeneration();
    
            // Regenerate TWC map with new seed
            if (tileWorldCreator != null)
            {
                tileWorldCreator.ExecuteAllBlueprintLayers();
                // This will trigger OnBlueprintLayersComplete → OnBuildLayersComplete
                // which will eventually call TrySpawnVillageWithRetry again
            }
            else
            {
                LogError("Cannot regenerate: TileWorldCreator is null");
            }
        }
        
        /// <summary>
        /// Cleans up our tile grid before TWC regenerates
        /// </summary>
        private void CleanupBeforeRegeneration()
        {
            LogInfo("Cleaning up tile grid before regeneration...");
    
            // Clean up our TileGrid (TWC will handle its own objects)
            if (tileGrid != null)
            {
                tileGrid.DestroyAllTiles();
                LogInfo("TileGrid cleared");
            }
    
            // Clear NavMesh
            if (navMeshSurface != null)
            {
                navMeshSurface.RemoveData();
                LogVerbose("NavMesh data cleared");
            }
    
            LogInfo("Cleanup complete - TWC will regenerate its objects");
        }
    }
}
