namespace TWC.Actions
{
	[ActionCategoryAttribute(Category=ActionCategoryAttribute.CategoryTypes.Modifiers)]
	[ActionNameAttribute(Name="Invert")]
	public class Invert : TWCBlueprintAction, ITWCAction
	{
		
		public override bool ShowFoldout
		{
			get{ return false;}
		}
		
		public ITWCAction Clone()
		{
			var _r = new Invert();
			return _r;
		}
		
		
		public bool[,] Execute(bool[,] map, TileWorldCreator _twc)
		{
			int _mapSizeX = map.GetLength(0);
			int _mapSizeY = map.GetLength(1);

	        for (int x = 0; x < _mapSizeX; x ++)
	        {
	            for (int y = 0; y < _mapSizeY; y ++)
	            {
	                map[x,y] = !map[x,y];
	            }
	        }
	
	        return map;
		}
	    
		public float GetGUIHeight(){ return 0; }
	}
}