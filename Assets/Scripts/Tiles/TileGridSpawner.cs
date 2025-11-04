using UnityEngine;
using TWC;

namespace Tiles
{
    public sealed class TileGridSpawner : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] TileWorldCreator twc;
        [SerializeField] TileGrid gridRoot;
        [SerializeField] Tile tilePrefab;

        [Header("Blueprint layer names")]
        [SerializeField] string islandLayer   = "Island";   // preferred mask
        [SerializeField] string grassLayer    = "Grass";
        [SerializeField] string forestLayer   = "Forest";
        [SerializeField] string mountainLayer = "Mountain";
        [SerializeField] string waterLayer    = "Water";

        [Header("Placement")]
        [SerializeField] float cellSize = 2f;
        [SerializeField] Vector3 origin = Vector3.zero;
        [SerializeField] bool destroyOld = true;
        
        [SerializeField] TileArchetypeLibrary library; // NEW

        

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

            // Create one Tile per *land* cell
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                bool isLand =
                    (island != null && island[x,y]) ||
                    (grass  != null && grass[x,y])  ||
                    (forest != null && forest[x,y]) ||
                    (mountain != null && mountain[x,y]) ||
                    (water != null && !water[x,y]); // fallback

                if (!isLand) continue;

                var world = new Vector3(origin.x + x * cellSize, origin.y, origin.z + y * cellSize);
                var t = Instantiate(tilePrefab, world, Quaternion.identity, gridRoot.transform);
                t.name = $"Tile_{x}_{y}";
                t.Init(new Vector2Int(x, y), library);     // ← set GridPos + Library at runtime
            }

            gridRoot.RebuildIndex();                       // ← make TileGrid lookups work
        }

        bool[,] SafeMap(string layer)
        {
            if (string.IsNullOrEmpty(layer) || twc == null) return null;
            try { return twc.GetMapOutputFromBlueprintLayer(layer); } catch { return null; }
        }
    }
}
