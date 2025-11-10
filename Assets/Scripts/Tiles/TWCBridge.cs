using UnityEngine;
using TWC;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AI.Navigation;

namespace Tiles
{
    public sealed class TWCBridge : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] TileWorldCreator tileWorldCreator;
        [SerializeField] TileGrid tileGrid;
        [SerializeField] TileGridSpawner spawner;
        [SerializeField] TileArchetypeLibrary library;
        [SerializeField] TWCLayerMapper layerMapper;
        [SerializeField] NavMeshSurface navMeshSurface;

        [Header("Overlay Detection")]
        [SerializeField] bool useBuildLayerFallback = true;
        [Tooltip("Parent transforms under which TWC instantiates overlay objects (enable 'group under parent' in TWC).")]
        [SerializeField] List<OverlayBuildRoot> overlayBuildRoots = new List<OverlayBuildRoot>();
        
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

        void Awake()
        {
            if (cellSize <= 0f) cellSize = 2f;
            if (origin == default && tileWorldCreator != null)
                origin = tileWorldCreator.transform.position;
            
            // Ensure NavMeshSurface uses Physics Colliders, not Render Meshes
            if (navMeshSurface != null)
            {
                navMeshSurface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
                Debug.Log("[TWCBridge] NavMeshSurface configured to use Physics Colliders");
            }
        }

        void Start()
        {
            if (autoRunOnStart) GenerateBuildAndSync();
        }

        void OnEnable()
        {
            if (tileWorldCreator == null)
            {
                Debug.LogError("[TWCBridge] TileWorldCreator is not assigned.");
                return;
            }
            tileWorldCreator.OnBlueprintLayersComplete += OnBlueprintLayersComplete;
            tileWorldCreator.OnBuildLayersComplete += OnBuildLayersComplete;
            Debug.Log("[TWCBridge] Subscribed to TWC events.");
        }

        void OnDisable()
        {
            if (tileWorldCreator == null) return;
            tileWorldCreator.OnBlueprintLayersComplete -= OnBlueprintLayersComplete;
            tileWorldCreator.OnBuildLayersComplete -= OnBuildLayersComplete;
            Debug.Log("[TWCBridge] Unsubscribed from TWC events.");
        }

        [ContextMenu("Generate → Build → Sync")]
        public void GenerateBuildAndSync()
        {
            if (tileWorldCreator == null)
            {
                Debug.LogError("[TWCBridge] No TWC set");
                return;
            }

            if (regenerateTWCMap)
            {
                Debug.Log("[TWCBridge] ExecuteAllBlueprintLayers()");
                tileWorldCreator.ExecuteAllBlueprintLayers();
                // Events will trigger BuildLayers → Sync → NavMesh
            }
            else
            {
                Debug.Log("[TWCBridge] Using existing TWC map, skipping regeneration");

                if (!VerifyBlueprintData())
                {
                    Debug.LogError("[TWCBridge] Blueprint data not available! Generate the map manually first or enable regenerateTWCMap.");
                    return;
                }

                if (spawnTilesBeforeSync && spawner != null)
                {
                    spawner.SpawnOrRebuild();
                }
                SyncFromBlueprints();
#pragma warning disable CS4014
                RebakeNavMeshAsync(); // Fire and forget
#pragma warning restore CS4014
                Debug.Log("[TWCBridge] TWC → TileGrid sync complete (no map regen).");
            }
        }

        bool VerifyBlueprintData()
        {
            var activeLayers = GetActiveBlueprintLayers();
            if (activeLayers.Count == 0)
            {
                Debug.LogWarning("[TWCBridge] No active blueprint layers found!");
                return false;
            }

            foreach (var layerName in activeLayers)
            {
                var testMap = GetMap(layerName);
                if (testMap != null)
                {
                    Debug.Log($"[TWCBridge] Blueprint data verified: {testMap.GetLength(0)}x{testMap.GetLength(1)} from layer '{layerName}'");
                    return true;
                }
            }

            Debug.LogWarning("[TWCBridge] No valid blueprint map data found!");
            return false;
        }

        void OnBlueprintLayersComplete(TileWorldCreator twc)
        {
            Debug.Log("[TWCBridge] OnBlueprintLayersComplete → ExecuteAllBuildLayers(false)");
            tileWorldCreator.ExecuteAllBuildLayers(false);
        }

        void OnBuildLayersComplete(TileWorldCreator twc)
        {
            Debug.Log("[TWCBridge] OnBuildLayersComplete");
            if (spawnTilesBeforeSync && spawner != null)
            {
                Debug.Log("[TWCBridge] Spawning/Rebuilding tiles from blueprint maps…");
                spawner.SpawnOrRebuild();
            }
            SyncFromBlueprints();
#pragma warning disable CS4014 // Async call not awaited
            RebakeNavMeshAsync(); // Fire and forget
#pragma warning restore CS4014
            Debug.Log("[TWCBridge] TWC → TileGrid sync complete.");
        }

        async Task RebakeNavMeshAsync()
        {
            if (!rebakeNavMesh || navMeshSurface == null)
                return;

            Debug.Log($"[TWCBridge] Waiting {navMeshBakeDelay}s for physics to settle before NavMesh bake...");
            
            // Wait for specified delay (gives physics and colliders time to settle)
            await Task.Delay((int)(navMeshBakeDelay * 1000));

            // Ensure we're still in play mode
            if (!Application.isPlaying || this == null)
                return;

            Debug.Log("[TWCBridge] Baking NavMesh...");
            navMeshSurface.BuildNavMesh();
            Debug.Log("[TWCBridge] NavMesh baked successfully.");
        }

        void SyncFromBlueprints()
        {
            if (tileGrid == null)
            {
                Debug.LogError("[TWCBridge] No TileGrid set");
                return;
            }

            if (layerMapper == null)
            {
                Debug.LogError("[TWCBridge] No TWCLayerMapper set. Cannot sync without mapping configuration.");
                return;
            }

            var activeLayers = GetActiveBlueprintLayers();
            if (activeLayers.Count == 0)
            {
                Debug.LogWarning("[TWCBridge] No active blueprint layers found.");
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
                Debug.LogWarning("[TWCBridge] No valid blueprint maps found.");
                return;
            }

            int w = refMap.GetLength(0), h = refMap.GetLength(1);
            int styled = 0, resSet = 0, tilesSeen = 0;

            var allMappings = layerMapper.GetAllMappingsSortedByPriority().ToList();

            Debug.Log($"[TWCBridge] Syncing {activeLayers.Count} layers to {w}x{h} grid using {allMappings.Count} mappings");

            // Apply base terrain styles
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var p = new Vector2Int(x, y);
                if (!tileGrid.TryGet(p, out var tile)) continue;
                tilesSeen++;

                if (tile.Library == null && library != null)
                    tile.SetLibrary(library);

                // Find highest priority matching layer
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

            Debug.Log($"[TWCBridge] Tiles seen: {tilesSeen}, styled: {styled}, resources set: {resSet}");
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
                    Debug.LogWarning($"[TWCBridge] Map '{name}' returned null.");
                return m;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TWCBridge] GetMapOutputFromBlueprintLayer('{name}') failed: {e.Message}");
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

            Debug.Log($"[TWCBridge] Build-root '{root.name}' → {set.Count} overlay cells.");
            return set;
        }

        [ContextMenu("Debug: List Active Layers")]
        void DebugListActiveLayers()
        {
            var layers = GetActiveBlueprintLayers();
            Debug.Log($"[TWCBridge] Active Blueprint Layers ({layers.Count}):");
            foreach (var layer in layers)
                Debug.Log($"  - {layer}");
        }

        [ContextMenu("Debug: Validate Mapper Configuration")]
        void DebugValidateMapper()
        {
            if (layerMapper == null)
            {
                Debug.LogError("[TWCBridge] No LayerMapper assigned!");
                return;
            }

            var activeLayers = GetActiveBlueprintLayers();
            var mappedLayers = new HashSet<string>();

            layerMapper.RebuildCache();
            foreach (var mapping in layerMapper.mappings)
                mappedLayers.Add(mapping.twcLayerName);

            Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Debug.Log("  LAYER MAPPER VALIDATION");
            Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            Debug.Log($"\nMapped Layers ({mappedLayers.Count}):");
            foreach (var layer in mappedLayers)
                Debug.Log($"  ✓ {layer}");

            var unmappedLayers = activeLayers.Where(l => !mappedLayers.Contains(l)).ToList();
            if (unmappedLayers.Count > 0)
            {
                Debug.LogWarning($"\nUnmapped Active Layers ({unmappedLayers.Count}):");
                foreach (var layer in unmappedLayers)
                    Debug.LogWarning($"  ✗ {layer} (no mapping configured!)");
            }

            var inactiveMappings = mappedLayers.Where(l => !activeLayers.Contains(l)).ToList();
            if (inactiveMappings.Count > 0)
            {
                Debug.Log($"\nMapped But Inactive Layers ({inactiveMappings.Count}):");
                foreach (var layer in inactiveMappings)
                    Debug.Log($"  ○ {layer}");
            }

            Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }
    }
}