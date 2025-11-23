using UnityEngine;

namespace TWC.Utilities
{
	// Count neigbours including itself
	public static class TileWorldCreatorUtilities
	{
		
	
		public static bool[,] SubdivideMap(bool [,] _map)
		{
			int _mapSizeX = _map.GetLength(0);
			int _mapSizeY = _map.GetLength(1);

			var _subdMap = new bool[_mapSizeX * 2, _mapSizeY * 2];
				
			int xOff = 0;
			int yOff = 0;
		
					
			for (int a = 0; a < _mapSizeX; a ++)
			{
				for (int b = 0; b < _mapSizeY; b ++)
				{
					for (int _s1 = 0; _s1 < 2; _s1 ++)
					{
						for (int _s2 = 0; _s2 < 2; _s2 ++)
						{
							if (_s1 + xOff < _mapSizeX * 2 && _s2 + yOff < _mapSizeY * 2)
							{
								_subdMap[_s1 + xOff, _s2 + yOff] = _map[a,b];
							}
						}
					}
							
					yOff += 2;
				}
				yOff = 0;
				xOff += 2;
			}
			
			return _subdMap;
		}
	
	
		public static bool[,] MergeMap(bool[,] _sourceMap, bool[,] _mergeMap)
		{
			int _mapSizeX = _sourceMap.GetLength(0);
			int _mapSizeY = _sourceMap.GetLength(1);
	
			if (_mapSizeX!= _mergeMap.GetLength(0))
			{
				_mergeMap = SubdivideMap(_mergeMap);
			}
			
			for (int x = 0; x < _mapSizeX; x ++)
			{
				for (int y = 0; y < _mapSizeY; y ++)
				{
	
					if (_mergeMap[x,y])
					{
						_sourceMap[x,y] = true;
					}
				}
			}
			
			return _sourceMap;
		}
	
		public static int CountNeighbours(int x, int y, bool[,] _map)
	    {
	        int _neighbours = 0;
	        for (int i = -1; i < 2; i ++)
	        {
	            for (int j = -1; j < 2; j ++)
	            {
	               
	
	                // if ((i != 0 && j != 0) && (x+i < _map.GetLength(0) && y + j < _map.GetLength(1)) && (x+i >= 0 && y + j >= 0))
	                // {
	              try{
		              if (_map[x+i,y+j]) // && x+i != x && y+j != y)
	                    {
	                        _neighbours++;
	                    }
	              }
	              catch{}
	                // }
	            }
	        }
	
	        return _neighbours;
	    }
	
	
		public static NeighboursLocation GetNeighboursLocation(bool[,] _map, int _x, int _y, NeighboursLocation location = null)
	    {
			if (location == null)
			{
	        	location = new NeighboursLocation();
			}
	            
	        for (int x = -1; x < 2; x ++)
	        {
	            for (int y = -1; y < 2; y ++)
	            {   
	                var _pos = new Vector2Int(_x + x, _y + y);
	                   
	                // South West
		            if (x == -1 && y == -1)
	                {
	                	try{
	                    if ( _map[_pos.x, _pos.y])
	                    {
	                        location.southWest = true;
	                    }
	                	}catch{}
	                }
	                // South
	                if (x == 0 && y == -1)
	                {
	                	try{
	                    if ( _map[_pos.x, _pos.y])
	                    {
	                        location.south = true;
	                    }
	                	}catch{}
	                }
	                // South East
	                if (x == 1 && y == -1)
	                {
	                	try{
	                    if ( _map[_pos.x, _pos.y])
	                    {
	                        location.southEast = true;
	                    }
	                	}catch{}
	                }
	                // West
	                if (x == -1 && y == 0)
	                {
	                	try{
	                    if ( _map[_pos.x, _pos.y])
	                    {
	                        location.west = true;
	                    }
	                	}catch{}
	                }
	                // East
	                if (x == 1 && y == 0)
	                {
	                	try{
	                    if ( _map[_pos.x, _pos.y])
	                    {
	                        location.east = true;
	                    }
	                	}catch{}
	                }
	                // North West
	                if (x == -1 && y == 1)
	                {
	                	try{
	                    if ( _map[_pos.x, _pos.y])
	                    {
	                        location.northWest = true;
	                    }
	                	}catch{}
	                }
	                // North
	                if (x == 0 && y == 1)
	                {
	                	try{
	                    if ( _map[_pos.x, _pos.y])
	                    {
	                        location.north = true;
	                    }
	                	}catch{}
	                }
	                // North East
	                if (x == 1 && y == 1)
	                {
	                	try{
	                    if ( _map[_pos.x, _pos.y])
	                    {
	                        location.northEast = true;
	                    }
	                	}catch{}
	                }
	                    
	                
	            }
	        }
	        
	        return location;
	    }
	
	
		public static float ReturnTileRotation(NeighboursLocation _neighbourlocation)
	    {
	        float _return = 0f;

	        // CORNER
	        if (_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            !_neighbourlocation.west &&
	            _neighbourlocation.east &&
	            !_neighbourlocation.southWest &&
	            !_neighbourlocation.south && 
	            _neighbourlocation.southEast )
	        {
	            _return = 180f;
	        }
	        else if (_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            !_neighbourlocation.west &&
	            _neighbourlocation.east &&
	            !_neighbourlocation.southWest &&
	            !_neighbourlocation.south && 
	            !_neighbourlocation.southEast)
	        {
	            _return = 180f;
	        }
	        else if (!_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            !_neighbourlocation.west &&
	            _neighbourlocation.east &&
	            !_neighbourlocation.southWest &&
	            !_neighbourlocation.south && 
	            _neighbourlocation.southEast)
	        {
	            _return = 180f;
	        }
	        else if (!_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            !_neighbourlocation.west &&
	            _neighbourlocation.east &&
	            !_neighbourlocation.southWest &&
	            !_neighbourlocation.south && 
	            !_neighbourlocation.southEast)
	        {
	            _return = 180f;
	        }
	        
	        else if (_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            _neighbourlocation.west &&
	            !_neighbourlocation.east &&
	            _neighbourlocation.southWest &&
	            !_neighbourlocation.south && 
	            !_neighbourlocation.southEast)
	        {
	            _return = 90f;
	        }
	        else if (_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            _neighbourlocation.west &&
	            !_neighbourlocation.east &&
	            !_neighbourlocation.southWest &&
	            !_neighbourlocation.south && 
	            !_neighbourlocation.southEast)
	        {
	            _return = 90f;
	        }
	        else if (_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            !_neighbourlocation.northEast &&
	            _neighbourlocation.west &&
	            !_neighbourlocation.east &&
	            _neighbourlocation.southWest &&
	            !_neighbourlocation.south && 
	            !_neighbourlocation.southEast)
	        {
	            _return = 90f;
	        }
	        else if (_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            !_neighbourlocation.northEast &&
	            _neighbourlocation.west &&
	            !_neighbourlocation.east &&
	            !_neighbourlocation.southWest &&
	            !_neighbourlocation.south && 
	            !_neighbourlocation.southEast)
	        {
	            _return = 90f;
	        }
	        
	        else if (!_neighbourlocation.northWest &&
	            !_neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            !_neighbourlocation.west &&
	            _neighbourlocation.east &&
	            _neighbourlocation.southWest &&
	            _neighbourlocation.south && 
	            _neighbourlocation.southEast)
	        {
	            _return = -90f;
	        }
	        
	        else if (!_neighbourlocation.northWest &&
	            !_neighbourlocation.north &&
	            !_neighbourlocation.northEast &&
	            !_neighbourlocation.west &&
	            _neighbourlocation.east &&
	            _neighbourlocation.southWest &&
	            _neighbourlocation.south && 
	            _neighbourlocation.southEast)
	        {
	            _return = -90f;
	        }
	        else if (!_neighbourlocation.northWest &&
	            !_neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            !_neighbourlocation.west &&
	            _neighbourlocation.east &&
	            !_neighbourlocation.southWest &&
	            _neighbourlocation.south && 
	            _neighbourlocation.southEast)
	        {
	            _return = -90f;
	        }
	        else if (!_neighbourlocation.northWest &&
	            !_neighbourlocation.north &&
	            !_neighbourlocation.northEast &&
	            !_neighbourlocation.west &&
	            _neighbourlocation.east &&
	            !_neighbourlocation.southWest &&
	            _neighbourlocation.south && 
	            _neighbourlocation.southEast)
	        {
	            _return = -90f;
	        }
	        else if (_neighbourlocation.northWest &&
		        _neighbourlocation.north &&
		        !_neighbourlocation.northEast &&
		        _neighbourlocation.west &&
		        !_neighbourlocation.east &&
		        !_neighbourlocation.southWest &&
		        !_neighbourlocation.south && 
		        _neighbourlocation.southEast)
	        {
		        _return = 90;
	        }
	        else if (_neighbourlocation.northWest &&
		        !_neighbourlocation.north &&
		        !_neighbourlocation.northEast &&
		        !_neighbourlocation.west &&
		        _neighbourlocation.east &&
		        !_neighbourlocation.southWest &&
		        _neighbourlocation.south && 
		        _neighbourlocation.southEast)
	        {
		        _return = -90;
	        }
	        
		    // Interior corner
	        else if (!_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            _neighbourlocation.west &&
	            _neighbourlocation.east &&
	            _neighbourlocation.southWest &&
	            _neighbourlocation.south && 
	            _neighbourlocation.southEast)
	        {
	            _return = -90f;
	        }
	        else if (_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            _neighbourlocation.west &&
	            _neighbourlocation.east &&
	            _neighbourlocation.southWest &&
	            _neighbourlocation.south && 
	            !_neighbourlocation.southEast)
	        {
	            _return = 90f;
	        }   
	        else if (_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            _neighbourlocation.west &&
	            _neighbourlocation.east &&
	            !_neighbourlocation.southWest &&
	            _neighbourlocation.south && 
	            _neighbourlocation.southEast)
	        {
	            _return = 180f;
	        }
	        else if (!_neighbourlocation.northWest &&
		        _neighbourlocation.north &&
		        _neighbourlocation.northEast &&
		        !_neighbourlocation.west &&
		        _neighbourlocation.east &&
		        _neighbourlocation.southWest &&
		        !_neighbourlocation.south && 
		        !_neighbourlocation.southEast)
	        {
		        _return = 180f;
	        }
	        // Straight
	        else if (!_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            !_neighbourlocation.west &&
	            _neighbourlocation.east &&
	            !_neighbourlocation.southWest &&
	            _neighbourlocation.south && 
	            _neighbourlocation.southEast)
	        {
	            _return = -90;
	        }
	        else if (_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            !_neighbourlocation.west &&
	            _neighbourlocation.east &&
	            !_neighbourlocation.southWest &&
	            _neighbourlocation.south && 
	            _neighbourlocation.southEast)
	        {
	            _return = -90;
	        }
	        else if (!_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            !_neighbourlocation.west &&
	            _neighbourlocation.east &&
	            _neighbourlocation.southWest &&
	            _neighbourlocation.south && 
	            _neighbourlocation.southEast)
	        {
	            _return = -90;
	        }   
	        else if (_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            !_neighbourlocation.west &&
	            _neighbourlocation.east &&
	            _neighbourlocation.southWest &&
	            _neighbourlocation.south && 
	            _neighbourlocation.southEast)
	        {
	            _return = -90;
	        }   
	        else if (_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            !_neighbourlocation.northEast &&
	            _neighbourlocation.west &&
	            !_neighbourlocation.east &&
	            _neighbourlocation.southWest &&
	            _neighbourlocation.south && 
	            !_neighbourlocation.southEast)
	        {
	            _return = 90;
	        }
	        else if (_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            _neighbourlocation.west &&
	            !_neighbourlocation.east &&
	            _neighbourlocation.southWest &&
	            _neighbourlocation.south && 
	            !_neighbourlocation.southEast)
	        {
	            _return = 90;
	        }
	        else if (_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            !_neighbourlocation.northEast &&
	            _neighbourlocation.west &&
	            !_neighbourlocation.east &&
	            _neighbourlocation.southWest &&
	            _neighbourlocation.south && 
	            _neighbourlocation.southEast)
	        {
	            _return = 90;
	        }
	        else if (_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            _neighbourlocation.west &&
	            !_neighbourlocation.east &&
	            _neighbourlocation.southWest &&
	            _neighbourlocation.south && 
	            _neighbourlocation.southEast)
	        {
	            _return = 90;
	        }
	        else if (_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            _neighbourlocation.west &&
	            _neighbourlocation.east &&
	            !_neighbourlocation.southWest &&
	            !_neighbourlocation.south && 
	            !_neighbourlocation.southEast)
	        {
	            _return = 180;
	        }
	        else if (_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            _neighbourlocation.west &&
	            _neighbourlocation.east &&
	            _neighbourlocation.southWest &&
	            !_neighbourlocation.south && 
	            !_neighbourlocation.southEast)
	        {
	            _return = 180;
	        }
	        else if (_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            _neighbourlocation.west &&
	            _neighbourlocation.east &&
	            !_neighbourlocation.southWest &&
	            !_neighbourlocation.south && 
	            _neighbourlocation.southEast)
	        {
	            _return = 180;
	        }
	        else if (_neighbourlocation.northWest &&
	            _neighbourlocation.north &&
	            _neighbourlocation.northEast &&
	            _neighbourlocation.west &&
	            _neighbourlocation.east &&
	            _neighbourlocation.southWest &&
	            !_neighbourlocation.south && 
	            _neighbourlocation.southEast)
	        {
	            _return = 180;
	        }
			//
			else if (!_neighbourlocation.northWest &&
				!_neighbourlocation.north && 
				!_neighbourlocation.northEast &&
				_neighbourlocation.west &&
				_neighbourlocation.east &&
				!_neighbourlocation.southWest &&
				!_neighbourlocation.south && 
				!_neighbourlocation.southEast)
			{
				_return = 45;
			}
            else if (!_neighbourlocation.northWest &&
                _neighbourlocation.north &&
                !_neighbourlocation.northEast &&
                !_neighbourlocation.west &&
                !_neighbourlocation.east &&
                !_neighbourlocation.southWest &&
                _neighbourlocation.south &&
                !_neighbourlocation.southEast)
            {
                _return = 180;
            }
            else if (!_neighbourlocation.northWest &&
                _neighbourlocation.north &&
                !_neighbourlocation.northEast &&
                _neighbourlocation.west &&
                _neighbourlocation.east &&
                _neighbourlocation.southWest &&
                _neighbourlocation.south &&
                _neighbourlocation.southEast)
            {
                _return = 0;
            }
            else if (_neighbourlocation.northWest &&
               _neighbourlocation.north &&
               !_neighbourlocation.northEast &&
               _neighbourlocation.west &&
               _neighbourlocation.east &&
               _neighbourlocation.southWest &&
               _neighbourlocation.south &&
               !_neighbourlocation.southEast)
            {
                _return = 90;
            }
            else if (_neighbourlocation.northWest &&
               _neighbourlocation.north &&
               _neighbourlocation.northEast &&
               _neighbourlocation.west &&
               _neighbourlocation.east &&
               !_neighbourlocation.southWest &&
               _neighbourlocation.south &&
               !_neighbourlocation.southEast)
            {
                _return = 180;
            }
            else if (!_neighbourlocation.northWest &&
              _neighbourlocation.north &&
              _neighbourlocation.northEast &&
              _neighbourlocation.west &&
              _neighbourlocation.east &&
              !_neighbourlocation.southWest &&
              _neighbourlocation.south &&
              _neighbourlocation.southEast)
            {
                _return = -90;
            }
			else if (!_neighbourlocation.northWest &&
              !_neighbourlocation.north &&
              !_neighbourlocation.northEast &&
              !_neighbourlocation.west &&
              !_neighbourlocation.east &&
              !_neighbourlocation.southWest &&
              _neighbourlocation.south &&
              !_neighbourlocation.southEast)
			{
				_return = 180;
			}
			else if (!_neighbourlocation.northWest &&
              _neighbourlocation.north &&
              !_neighbourlocation.northEast &&
              !_neighbourlocation.west &&
              !_neighbourlocation.east &&
              !_neighbourlocation.southWest &&
              !_neighbourlocation.south &&
              !_neighbourlocation.southEast)
			{
				_return = 0;
			}
			else if (!_neighbourlocation.northWest &&
              !_neighbourlocation.north &&
              !_neighbourlocation.northEast &&
              _neighbourlocation.west &&
              !_neighbourlocation.east &&
              !_neighbourlocation.southWest &&
              !_neighbourlocation.south &&
              !_neighbourlocation.southEast)
			{
				_return = -90;
			}
			else if (!_neighbourlocation.northWest &&
              !_neighbourlocation.north &&
              !_neighbourlocation.northEast &&
              !_neighbourlocation.west &&
              _neighbourlocation.east &&
              !_neighbourlocation.southWest &&
              !_neighbourlocation.south &&
              !_neighbourlocation.southEast)
			{
				_return = 90;
			}

            return _return;
	    }
	}
}