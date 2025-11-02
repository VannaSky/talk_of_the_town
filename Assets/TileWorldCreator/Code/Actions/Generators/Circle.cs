using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

using TWC.editor;
 
namespace TWC.Actions 
{
	[ActionCategoryAttribute(Category=ActionCategoryAttribute.CategoryTypes.Generators)]
	[ActionNameAttribute(Name="Circle")]
	public class Circle : TWCBlueprintAction, ITWCAction
	{
		public bool randomPosition;
		public int positionX, positionY;
		public int radius;
		private TWCGUILayout guiLayout;
		
		
		public ITWCAction Clone()
		{
			var _r = new Circle();
			
			_r.radius = this.radius;
			
			return _r;
		}
		
		public bool[,] Execute(bool[,] map, TileWorldCreator _twc)
		{
			
			// Make sure to set the seed from TileWorldCreator
			UnityEngine.Random.InitState(_twc.currentSeed);
			
			var _position = new Vector2Int(positionX, positionY);
			
			if (randomPosition)
			{
				_position = new Vector2Int(Random.Range(0, map.GetLength(0)), Random.Range(0, map.GetLength(1)));
			}
			
			try
			{
				map[_position.x, _position.y] = true;

				var width = map.GetLength(0);
				var height = map.GetLength(1);

				for (int x = 0; x < width; x ++)
				{
					for (int y = 0; y < height; y ++)
					{
						// Get distance to center
						var _dist = Vector2Int.Distance(new Vector2Int(x, y), _position);
						if (_dist <= radius)
						{ 
							map[x,y] = true;
						}
					}
				}
			}
			catch{}
			
			
			return map;
		}
		
		
		#if UNITY_EDITOR
		public override void DrawGUI(Rect _rect, int _layerIndex, TileWorldCreatorAsset _asset, TileWorldCreator _twc)
		{
			using (guiLayout = new TWCGUILayout(_rect))
			{
				guiLayout.Add();
				radius = EditorGUI.IntField(guiLayout.rect, "Radius", radius);
				guiLayout.Add();
				randomPosition = EditorGUI.Toggle(guiLayout.rect, "Random position", randomPosition);
				if (!randomPosition)
				{
					guiLayout.Add();
					positionX = EditorGUI.IntField(guiLayout.rect, "Position X:", positionX);
					guiLayout.Add();
					positionY = EditorGUI.IntField(guiLayout.rect, "Position Y:", positionY);
				}
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