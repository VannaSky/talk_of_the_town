using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tiles
{
    /// <summary>Serializable representation of a single tile for JSON export</summary>
    [Serializable]
    public class TileData
    {
        public int x;
        public int y;
        public string tileStyle;
        public List<ResourceData> resources;
        public string construction;
        public bool isWalkable;
        
        public TileData(Tile tile)
        {
            x = tile.GridPos.x;
            y = tile.GridPos.y;
            tileStyle = tile.Archetype?.Style.ToString() ?? "Unknown";
            
            // Convert resource dictionary to list
            resources = new List<ResourceData>();
            foreach (var kvp in tile.GetAllResourceCounts())
            {
                if (kvp.Value > 0)
                {
                    resources.Add(new ResourceData 
                    { 
                        type = kvp.Key.ToString(), 
                        count = kvp.Value 
                    });
                }
            }
            
            construction = tile.Construction?.Type.ToString() ?? "None";
            isWalkable = tile.IsWalkable;
        }
    }
    
    [Serializable]
    public class ResourceData
    {
        public string type;
        public int count;
    }
    
    /// <summary>Complete grid data for JSON export</summary>
    [Serializable]
    public class GridData
    {
        public int width;   // Map dimensions for LLM context
        public int height;
        public List<TileData> tiles;
        
        public GridData()
        {
            tiles = new List<TileData>();
        }
    }
}