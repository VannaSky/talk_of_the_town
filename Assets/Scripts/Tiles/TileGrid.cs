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
        Debug.Log($"Grid saved to: {path}");
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
        
        public void RebuildIndex()
        {
            _tiles.Clear();
            foreach (var t in GetComponentsInChildren<Tile>(includeInactive: false))
                _tiles[t.GridPos] = t;
        }
    }
}