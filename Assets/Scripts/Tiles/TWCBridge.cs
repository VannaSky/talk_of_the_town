using TWC;
using UnityEngine;

// TileWorldCreator namespace

namespace Tiles
{
    public sealed class TWCBridge : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] TileWorldCreator tileWorldCreator; // TWC root component
        [SerializeField] TileGrid tileGrid;                 // your grid (already placed in scene)

        [Header("Blueprint layer names (must match TWC)")]
        [SerializeField] string grassLayer   = "Grass";
        [SerializeField] string waterLayer   = "Water";
        [SerializeField] string mountainLayer= "Mountain";
        [SerializeField] string woodLayer    = "Wood";
        [SerializeField] string rockLayer    = "Rock";

        [Header("Grid mapping")]
        [Tooltip("World cell size, only needed if you create Tiles procedurally. If your grid already exists by (x,y) indices, ignore.")]
        [SerializeField] float cellSize = 1f;

        void OnEnable()
        {
            if (tileWorldCreator != null)
            {
                tileWorldCreator.OnBlueprintLayersComplete += OnBlueprintDone;
                tileWorldCreator.OnBuildLayersComplete     += OnBuildDone;
            }
        }

        void OnDisable()
        {
            if (tileWorldCreator != null)
            {
                tileWorldCreator.OnBlueprintLayersComplete -= OnBlueprintDone;
                tileWorldCreator.OnBuildLayersComplete     -= OnBuildDone;
            }
        }

        // Public entry point if you want a one-button bootstrap
        [ContextMenu("Generate & Build & Sync")]
        public void GenerateBuildAndSync()
        {
            if (tileWorldCreator == null) { Debug.LogError("No TileWorldCreator set"); return; }
            tileWorldCreator.ExecuteAllBlueprintLayers();        // async-ish; OnBlueprintDone will fire
            // When blueprints complete we’ll call ExecuteAllBuildLayers(false) in OnBlueprintDone
            // and then sync in OnBuildDone.
        }

        void OnBlueprintDone(TileWorldCreator _)
        {
            // Kick the actual build; you can set true to force a rebuild
            tileWorldCreator.ExecuteAllBuildLayers(false);
        }

        void OnBuildDone(TileWorldCreator _)
        {
            // Now read blueprint outputs and push into your runtime grid
            SyncFromBlueprints();
            Debug.Log("TWC → TileGrid sync complete.");
        }

        void SyncFromBlueprints()
        {
            if (tileGrid == null) { Debug.LogError("No TileGrid set"); return; }

            // 1) Pull raw maps from TWC (true = tile present)
            var grassMap    = SafeMap(grassLayer);
            var waterMap    = SafeMap(waterLayer);
            var mountainMap = SafeMap(mountainLayer);
            var woodMap     = SafeMap(woodLayer);
            var rockMap     = SafeMap(rockLayer);

            if (grassMap == null && waterMap == null && mountainMap == null)
            { Debug.LogWarning("No base maps found."); return; }

            // Use the largest available as reference (width = x, height = y)
            var reference = mountainMap ?? waterMap ?? grassMap;
            int width  = reference.GetLength(0);
            int height = reference.GetLength(1);

            // 2) Base style priority: Mountain > Water > Grass
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                var pos = new Vector2Int(x, y);
                if (!tileGrid.TryGet(pos, out var tile)) continue;

                if (mountainMap != null && mountainMap[x, y]) { SetStyle(tile, TileStyle.Mountain); }
                else if (waterMap != null && waterMap[x, y])  { SetStyle(tile, TileStyle.Water); }
                else if (grassMap != null && grassMap[x, y])  { SetStyle(tile, TileStyle.Grass); }
                // else: leave as-is (void tile)
            }

            // 3) Overlays → resources (on top of base)
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                var pos = new Vector2Int(x, y);
                if (!tileGrid.TryGet(pos, out var tile)) continue;

                // Clear first (optional)
                tile.TrySetResource(null);

                if (woodMap != null && woodMap[x, y])
                    tile.TrySetResource(new ResourceInstance(ResourceType.Wood, amount: 1));

                if (rockMap != null && rockMap[x, y])
                    tile.TrySetResource(new ResourceInstance(ResourceType.Stone, amount: 1)); // your enum
            }
        }

        bool[,] SafeMap(string layerName)
        {
            if (string.IsNullOrEmpty(layerName)) return null;
            try { return tileWorldCreator.GetMapOutputFromBlueprintLayer(layerName); }
            catch { return null; }
        }

        void SetStyle(Tile tile, TileStyle style)
        {
            // If your Tile exposes a setter or style-swapping archetype, call it here.
            // Example if you add this to Tile:
            // tile.SetStyle(style);
            // For now, assume archetype carries style and visuals already match from TWC build.
        }
    }
}
