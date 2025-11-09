using UnityEngine;
using TWC;
using System.Collections.Generic;

namespace Tiles
{
    public sealed class TWCBridge : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] TileWorldCreator tileWorldCreator;
        [SerializeField] TileGrid tileGrid;
        [SerializeField] TileGridSpawner spawner;            // optional but recommended
        [SerializeField] TileArchetypeLibrary library;

        [Header("Blueprint layer names")]
        [SerializeField] string islandLayer   = "Island";
        [SerializeField] string grassLayer    = "Grass";
        [SerializeField] string forestLayer   = "Forest";
        [SerializeField] string mountainLayer = "Mountain";
        [SerializeField] string waterLayer    = "Water";

        [Header("Overlays (Object Build Layers)")]
        [SerializeField] string woodLayer     = "Wood";      // optional blueprint name (often null)
        [SerializeField] string stoneLayer    = "Stone";     // optional blueprint name (often null)

        [Header("Overlay Fallback (scan built objects)")]
        [SerializeField] bool useBuildLayerFallback = true;
        [Tooltip("Parent transforms under which TWC instantiates Wood/Stone objects (enable 'group under parent' in TWC).")]
        [SerializeField] Transform woodBuildRoot;
        [SerializeField] Transform stoneBuildRoot;
        [Tooltip("Must match your grid spacing in world units (X/Z).")]
        [SerializeField] float cellSize = 1f;
        [Tooltip("World-space origin of cell (0,0).")]
        [SerializeField] Vector3 origin = Vector3.zero;

        [Header("Run options")]
        [SerializeField] bool autoRunOnStart = true;
        [SerializeField] bool regenerateTWCMap = true;
        [SerializeField] bool spawnTilesBeforeSync = true;   // create tiles if missing
        
        void Awake()
        {
            // Sensible defaults from your screenshot
            if (cellSize <= 0f) cellSize = 2f;
            if (origin == default && tileWorldCreator != null)
                origin = tileWorldCreator.transform.position;
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
            tileWorldCreator.OnBuildLayersComplete     += OnBuildLayersComplete;
            Debug.Log("[TWCBridge] Subscribed to TWC events.");
        }

        void OnDisable()
        {
            if (tileWorldCreator == null) return;
            tileWorldCreator.OnBlueprintLayersComplete -= OnBlueprintLayersComplete;
            tileWorldCreator.OnBuildLayersComplete     -= OnBuildLayersComplete;
            Debug.Log("[TWCBridge] Unsubscribed from TWC events.");
        }

        [ContextMenu("Generate → Build → Sync")]
        public void GenerateBuildAndSync()
        {
            if (tileWorldCreator == null) { Debug.LogError("[TWCBridge] No TWC set"); return; }
    
            if (regenerateTWCMap)
            {
                Debug.Log("[TWCBridge] ExecuteAllBlueprintLayers()");
                tileWorldCreator.ExecuteAllBlueprintLayers();
                // Events will trigger BuildLayers → Sync
            }
            else
            {
                Debug.Log("[TWCBridge] Using existing TWC map, skipping regeneration");
        
                // Important: Verify blueprint data is actually available
                if (!VerifyBlueprintData())
                {
                    Debug.LogError("[TWCBridge] Blueprint data not available! Generate the map manually first or enable regenerateTWCMap.");
                    return;
                }
        
                // Skip directly to syncing from existing blueprint data
                if (spawnTilesBeforeSync && spawner != null)
                {
                    spawner.SpawnOrRebuild();
                    tileGrid.RebuildIndex();
                }
                SyncFromBlueprints();
                Debug.Log("[TWCBridge] TWC → TileGrid sync complete (no map regen).");
            }
        }

        bool VerifyBlueprintData()
        {
            var testMap = Map(islandLayer) ?? Map(grassLayer) ?? Map(forestLayer) ?? 
                Map(mountainLayer) ?? Map(waterLayer);
    
            if (testMap == null)
            {
                Debug.LogWarning("[TWCBridge] No blueprint map data found!");
                return false;
            }
    
            Debug.Log($"[TWCBridge] Blueprint data verified: {testMap.GetLength(0)}x{testMap.GetLength(1)}");
            return true;
        }

        // -------- Event handlers --------
        void OnBlueprintLayersComplete(TileWorldCreator _)
        {
            Debug.Log("[TWCBridge] OnBlueprintLayersComplete → ExecuteAllBuildLayers(false)");
            tileWorldCreator.ExecuteAllBuildLayers(false);
        }

        void OnBuildLayersComplete(TileWorldCreator _)
        {
            Debug.Log("[TWCBridge] OnBuildLayersComplete");
            if (spawnTilesBeforeSync && spawner != null)
            {
                Debug.Log("[TWCBridge] Spawning/Rebuilding tiles from blueprint maps…");
                spawner.SpawnOrRebuild();
                tileGrid.RebuildIndex();  // <<< make sure dictionary is fresh
            }
            SyncFromBlueprints();
            Debug.Log("[TWCBridge] TWC → TileGrid sync complete.");
        }

        // -------- Core sync --------
        void SyncFromBlueprints()
        {
            if (tileGrid == null) { Debug.LogError("[TWCBridge] No TileGrid set"); return; }
            if (library == null)  { Debug.LogWarning("[TWCBridge] No TileArchetypeLibrary set (Tiles must already have one)."); }

            var island   = Map(islandLayer);
            var mountain = Map(mountainLayer);
            var forest   = Map(forestLayer);
            var grass    = Map(grassLayer);
            var water    = Map(waterLayer);
            var refMap = island ?? mountain ?? forest ?? grass ?? water;


            if (refMap == null)
            {
                Debug.LogWarning("[TWCBridge] No blueprint maps found (check layer names and that blueprints executed).");
                return;
            }

            int w = refMap.GetLength(0), h = refMap.GetLength(1);
            int styled = 0, resSet = 0, tilesSeen = 0;

            // Base: Mountain > Forest > Grass > Water
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var p = new Vector2Int(x, y);
                if (!tileGrid.TryGet(p, out var tile)) continue;
                tilesSeen++;

                if (tile.Library == null && library != null) tile.SetLibrary(library);

                // Outside island => always Water
                if (island != null && !island[x, y]) { tile.SetStyle(TileStyle.Water); styled++; continue; }

                // Inside island: Mountain > Forest > Grass
                if (mountain != null && mountain[x, y])      { tile.SetStyle(TileStyle.Mountain); styled++; continue; }
                if (forest   != null && forest[x, y])        { tile.SetStyle(TileStyle.Forest);   styled++; continue; }
                if (grass    != null && grass[x, y])         { tile.SetStyle(TileStyle.Grass);    styled++; continue; }

                // Unlabeled *inside island* => treat as Water (lakes/rivers not explicitly tagged)
                tile.SetStyle(TileStyle.Water); styled++;
            }

            // ---- OVERLAYS ----
            // Do NOT ask blueprint for objects unless explicitly enabled.
            HashSet<Vector2Int> woodCells  = null;
            HashSet<Vector2Int> stoneCells = null;

            if (useBuildLayerFallback)
            {
                woodCells  = CellsFromBuildRoot(woodBuildRoot);
                stoneCells = CellsFromBuildRoot(stoneBuildRoot);
            }

            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var p = new Vector2Int(x, y);
                if (!tileGrid.TryGet(p, out var tile)) continue;

                tile.TrySetResource(null);

                bool woodHere  = woodCells  != null && woodCells.Contains(p);
                bool stoneHere = stoneCells != null && stoneCells.Contains(p);

                if (woodHere  && tile.Archetype != null && tile.Archetype.Style == TileStyle.Forest)
                    tile.TrySetResource(new ResourceInstance(ResourceType.Wood, 1));

                if (stoneHere && tile.Archetype != null && tile.Archetype.Style == TileStyle.Mountain)
                    tile.TrySetResource(new ResourceInstance(ResourceType.Stone, 1));
            }

            Debug.Log($"[TWCBridge] Tiles seen: {tilesSeen}, styled: {styled}, resources set: {resSet}");
        }

        // -------- Helpers --------
        bool[,] Map(string name)
        {
            if (string.IsNullOrEmpty(name) || tileWorldCreator == null) return null;
            try
            {
                var m = tileWorldCreator.GetMapOutputFromBlueprintLayer(name);
                if (m == null) Debug.LogWarning($"[TWCBridge] Map '{name}' returned null.");
                else Debug.Log($"[TWCBridge] Map '{name}' size: {m.GetLength(0)}x{m.GetLength(1)}");
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

            // Traverse all descendants (built objects may be nested)
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
    }
}
