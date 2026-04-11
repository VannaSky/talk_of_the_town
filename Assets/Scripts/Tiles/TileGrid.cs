using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Tiles
{
    public sealed class TileGrid : MonoBehaviour
    {
        private readonly Dictionary<Vector2Int, Tile> _tiles = new();

        void Awake()
        {
            foreach (var t in GetComponentsInChildren<Tile>(includeInactive: false))
                _tiles[t.GridPos] = t;
        }

        public bool TryGet(Vector2Int pos, out Tile tile) => _tiles.TryGetValue(pos, out tile);

        public IEnumerable<Tile> Neighbors4(Vector2Int pos)
        {
            var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            foreach (var d in dirs) if (_tiles.TryGetValue(pos + d, out var n)) yield return n;
        }
        
        /// <summary>Export grid state to JSON</summary>
    public string ExportToJson()
    {
        GridData data = new GridData();
        
        // Calculate grid bounds
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;
        
        foreach (var tile in _tiles.Values)
        {
            if (tile.GridPos.x < minX) minX = tile.GridPos.x;
            if (tile.GridPos.x > maxX) maxX = tile.GridPos.x;
            if (tile.GridPos.y < minY) minY = tile.GridPos.y;
            if (tile.GridPos.y > maxY) maxY = tile.GridPos.y;
            
            data.tiles.Add(new TileData(tile));
        }
        
        data.width = maxX - minX + 1;
        data.height = maxY - minY + 1;
        
        return JsonUtility.ToJson(data, true);
    }
    
    /// <summary>Save grid to JSON file</summary>
    public void SaveToFile(string filename = "tilegrid.json")
    {
        string json = ExportToJson();
        string path = Path.Combine(Application.persistentDataPath, filename);
        File.WriteAllText(path, json);
    }
    
    /// <summary>Export compact grid state to JSON for LLM consumption</summary>
    public string ExportToJsonCompact()
    {
        GridDataCompact data = new GridDataCompact();
    
        // Calculate grid bounds
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;
    
        foreach (var tile in _tiles.Values)
        {
            if (tile.GridPos.x < minX) minX = tile.GridPos.x;
            if (tile.GridPos.x > maxX) maxX = tile.GridPos.x;
            if (tile.GridPos.y < minY) minY = tile.GridPos.y;
            if (tile.GridPos.y > maxY) maxY = tile.GridPos.y;
        
            data.tiles.Add(new TileDataCompact(tile));
        }
    
        data.w = maxX - minX + 1;
        data.h = maxY - minY + 1;
    
        // No pretty print for minimal size
        return JsonUtility.ToJson(data, false);
    }

    /// <summary>Save compact grid to JSON file</summary>
    public void SaveToFileCompact(string filename = "tilegrid_compact.json")
    {
        string json = ExportToJsonCompact();
        string path = Path.Combine(Application.persistentDataPath, filename);
        File.WriteAllText(path, json);
        Debug.Log($"Compact grid saved to: {path}");
    }
    
    /// <summary>Get compact summary for LLM (smaller token count)</summary>
    public string GetLLMSummary()
    {
        var summary = new System.Text.StringBuilder();
        summary.AppendLine("=== TILE GRID SUMMARY ===");
        
        foreach (var tile in _tiles.Values)
        {
            if (tile.Archetype?.Style == TileStyle.Water) continue; // Skip water tiles
            
            var resources = tile.GetAllResourceCounts();
            if (resources.Count == 0 && tile.Construction == null) continue; // Skip empty tiles
            
            summary.Append($"[{tile.GridPos.x},{tile.GridPos.y}] {tile.Archetype?.Style}");
            
            if (resources.Count > 0)
            {
                summary.Append(" | Resources: ");
                foreach (var res in resources)
                {
                    summary.Append($"{res.Key}x{res.Value} ");
                }
            }
            
            if (tile.Construction != null)
            {
                summary.Append($" | Building: {tile.Construction.Type}");
            }
            
            if (tile.IsOccupied)
            {
                summary.Append($" | Occupied by: {tile.Occupant.name}");
            }
            
            summary.AppendLine();
        }
        
        return summary.ToString();
    }
        
        /// <summary>
        /// Finds the nearest tile to a world position by checking all tiles.
        /// Returns null if the grid is empty.
        /// </summary>
        public Tile FindNearestTile(Vector3 worldPos)
        {
            Tile nearest = null;
            float nearestSqr = float.MaxValue;
            foreach (var tile in _tiles.Values)
            {
                float sqr = (tile.transform.position - worldPos).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = tile;
                }
            }
            return nearest;
        }

        public List<Tile> FindTilesInRadius(Vector2Int center, int radius, System.Func<Tile, bool> predicate)
        {
            var results = new List<Tile>();
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    var pos = center + new Vector2Int(dx, dy);
                    if (_tiles.TryGetValue(pos, out var tile) && predicate(tile))
                        results.Add(tile);
                }
            }
            return results;
        }

        public void RebuildIndex()
        {
            _tiles.Clear();
            foreach (var t in GetComponentsInChildren<Tile>(includeInactive: false))
                _tiles[t.GridPos] = t;
        }
        
        /// <summary>
        /// Destroys all tile GameObjects and clears the dictionary
        /// </summary>
        public void DestroyAllTiles()
        {
            // Collect all children
            var children = new List<Transform>();
            foreach (Transform child in transform)
            {
                children.Add(child);
            }
    
            // Destroy them all
            foreach (var child in children)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(child.gameObject);
                else
                    Destroy(child.gameObject);
#else
        Destroy(child.gameObject);
#endif
            }
    
            // Clear the dictionary
            _tiles.Clear();
        }
    }
    
    
}