using System.Collections.Generic;
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
        
        public void RebuildIndex()
        {
            _tiles.Clear();
            foreach (var t in GetComponentsInChildren<Tile>(includeInactive: false))
                _tiles[t.GridPos] = t;
        }
    }
}