using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using TWC.editor;

namespace TWC.Actions
{
	[ActionCategoryAttribute(Category=ActionCategoryAttribute.CategoryTypes.Modifiers)]
	[ActionNameAttribute(Name="Offset")]
	[System.Serializable]
	public class Offset : TWCBlueprintAction, ITWCAction
	{
		[SerializeField]
		public Vector2Int offset;
		// serializable values
		public int x;
		public int y;

		TWCGUILayout guiLayout;
		
		public ITWCAction Clone()
		{
			var _r = new Offset();
			
			_r.offset = this.offset;
			
			return _r;
		}
		
		
		public bool[,] Execute(bool[,] map, TileWorldCreator _twc)
		{
			int _mapSizeX = map.GetLength(0);
			int _mapSizeY = map.GetLength(1);

			List<Vector2Int> _modified = new List<Vector2Int>();
			bool[,] _copiedMap = new bool[_mapSizeX, _mapSizeY];
			
			System.Array.Copy(map, _copiedMap, map.Length);
			
			for (int x = 0; x < _mapSizeX; x ++)
			{
				for (int y = 0; y < _mapSizeY; y ++)
				{
					if (map[x,y])
					{
						try{
							
							_copiedMap[x + offset.x, y + offset.y] = true;
							_modified.Add(new Vector2Int(x,y));
							
						}
						catch
						{
						}
					}
				}
			}
			
			for (int x = 0; x < _modified.Count; x ++)
			{
				_copiedMap[_modified[x].x, _modified[x].y] = false;
			}
	
			return _copiedMap;
		}
		
		#if UNITY_EDITOR
		public override void DrawGUI(Rect _rect, int _layerIndex, TileWorldCreatorAsset _asset, TileWorldCreator _twc)
		{
			if (offset == Vector2Int.zero)
			{
				offset = new Vector2Int(x, y);
			}

			using (guiLayout = new TWCGUILayout(_rect))
			{
				guiLayout.Add();
				offset = EditorGUI.Vector2IntField(guiLayout.rect, "Offset", offset);

				x = offset.x;
				y = offset.y;
			}
		}
	    #endif
	    
		public float GetGUIHeight()
		{
			if (guiLayout != null)
			{
				return guiLayout.height;
			}
			else
			{
				return 18;
			}
		}
		
	}
}