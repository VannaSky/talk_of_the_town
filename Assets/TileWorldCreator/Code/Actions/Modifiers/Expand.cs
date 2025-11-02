
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

using TWC.Utilities;

namespace TWC.Actions
{
	[ActionCategoryAttribute(Category=ActionCategoryAttribute.CategoryTypes.Modifiers)]
	[ActionNameAttribute(Name="Expand")]
	public class Expand : TWCBlueprintAction, ITWCAction
	{
		
		public override bool ShowFoldout
		{
			get{ return false;}
		}
		
		public ITWCAction Clone()
		{
			var _r = new Expand();
			return _r;
		}
		
		
		public bool[,] Execute(bool[,] map, TileWorldCreator _twc)
		{
			int _mapSizeX = map.GetLength(0);
			int _mapSizeY = map.GetLength(1);

			List<Vector2Int> borderTiles = new List<Vector2Int>();
	        for (int x = 0; x < _mapSizeX; x ++)
	        {
	            for (int y = 0; y < _mapSizeY; y ++)
	            {
	                
		            var _neighbours = TileWorldCreatorUtilities.CountNeighbours(x, y, map);
		            if (_neighbours < 9 && map[x,y])
	                {
		            	borderTiles.Add(new Vector2Int(x, y));
	                }
	            }
	        }
	        
	        
			for (int c = 0; c < borderTiles.Count; c++)
			{
				for (int x = -1; x < 2; x ++)
				{
					for (int y = -1; y < 2; y ++)
					{
						var _p = new Vector2Int(borderTiles[c].x + x, borderTiles[c].y + y);
						if (_p.x >= 0 && _p.x < _mapSizeX && _p.y >= 0 && _p.y < _mapSizeY)
						{
							map[_p.x, _p.y] = true;
						}
					}
				}
			}
	
	        return map;
	    }
	
	
	    #if UNITY_EDITOR
		public override void DrawGUI(Rect _rect, int _layerIndex, TileWorldCreatorAsset _asset, TileWorldCreator _twc) {}
		#endif
		public float GetGUIHeight(){ return 18; }
		
	}
}