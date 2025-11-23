using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tiles
{
    /// <summary>Compact tile data for LLM consumption - minimal tokens</summary>
    [Serializable]
    public class TileDataCompact
    {
        public int x;
        public int y;
        public string t;  // terrain: G=Grass, F=Forest, M=Mountain, W=Water, C=Coast
        
        // Only include if non-empty
        public List<ResourceDataCompact> r;  // resources
        
        // Only include if present
        public string b;  // building: H=Hut, Mi=Mill, Mn=Mine, W=Well, Wh=Warehouse
        
        // Only include if false (assume true by default)
        public bool? w;  // walkable (null = true, false = blocked)
        
        public TileDataCompact(Tile tile)
        {
            x = tile.GridPos.x;
            y = tile.GridPos.y;
            
            // Convert terrain to single letter
            t = tile.Archetype?.Style switch
            {
                TileStyle.Grass => "G",
                TileStyle.Forest => "F",
                TileStyle.Mountain => "M",
                TileStyle.Water => "W",
                TileStyle.Coast => "C",
                TileStyle.Field => "Fi",
                _ => "?"
            };
            
            // Only include resources if present
            var resourceCounts = tile.GetAllResourceCounts();
            if (resourceCounts.Count > 0)
            {
                r = new List<ResourceDataCompact>();
                foreach (var kvp in resourceCounts)
                {
                    if (kvp.Value > 0)
                    {
                        r.Add(new ResourceDataCompact 
                        { 
                            t = kvp.Key switch
                            {
                                ResourceType.Wood => "Wd",
                                ResourceType.Stone => "St",
                                ResourceType.Iron => "Ir",
                                ResourceType.Seed => "Sd",
                                _ => "?"
                            },
                            c = kvp.Value 
                        });
                    }
                }
            }
            
            // Only include building if present
            if (tile.Construction != null && tile.Construction.Type != ConstructionType.None)
            {
                b = tile.Construction.Type switch
                {
                    ConstructionType.Hut => "H",
                    ConstructionType.Mill => "Mi",
                    ConstructionType.Mine => "Mn",
                    ConstructionType.Well => "We",
                    ConstructionType.Warehouse => "Wh",
                    _ => "?"
                };
            }
            
            // Only include walkable if false (assume true by default)
            if (!tile.IsWalkable)
            {
                w = false;
            }
        }
    }
    
    [Serializable]
    public class ResourceDataCompact
    {
        public string t;  // type
        public int c;     // count
    }
    
    /// <summary>Compact grid data for LLM - includes legend</summary>
    [Serializable]
    public class GridDataCompact
    {
        // IMPORTANT: ADDITIONS NEED TO BE NAMED HERE AS WELL
        public string legend = "Terrain: G=Grass F=Forest M=Mountain W=Water C=Coast Fi=Field | Resources: Wd=Wood St=Stone Ir=Iron Sd=Seed | Buildings: H=Hut Mi=Mill Mn=Mine We=Well Wh=Warehouse";
        public int w;  // width
        public int h;  // height
        public List<TileDataCompact> tiles;
        
        public GridDataCompact()
        {
            tiles = new List<TileDataCompact>();
        }
    }
}