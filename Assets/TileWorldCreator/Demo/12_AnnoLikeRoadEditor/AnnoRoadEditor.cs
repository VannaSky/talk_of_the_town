using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using TWC;

/*
	BASIC Anno like road editor
	----------------------------
	
	Simple editor for showcasing "Anno" like road building, switchting between different road types and build prioritization

*/

namespace TWC.Demo
{
	public class AnnoRoadEditor : MonoBehaviour
	{	
		
		[Header("Editor objects")]
		public TileWorldCreator tileWorldCreator;
		public Camera editorCamera;
		public GameObject roadPreview;
		public GameObject cursorObject;
		public GameObject removeCellObject;
		
		[Header("Layers")]
		public List<string> layers;
		
		private string selectedLayer;
		private int selectedLayerIndex;
		private List<GameObject> paintedCells = new List<GameObject>();
		private int cellSize = 1;
		private Vector3 currentPosition;
		private Vector3 lastPosition;
		private bool cellularAutomataGeneration;
		private float cameraZoom = 12f;
		
		
		private int xDistanceAbs;
		private float xDistance;
		private int yDistanceAbs;
		private float yDistance;
		private Vector2Int startGridPosition;
		
		
		void OnEnable()
		{
			tileWorldCreator.OnBlueprintLayersComplete += BuildMap;
		}
		 
		void OnDisable()
		{
			tileWorldCreator.OnBlueprintLayersComplete -= BuildMap;
		}
		
		void Start()
		{
			selectedLayer = layers[0];
		}
		
		// Draw basic functionality
		void OnGUI()
		{
			using (new GUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Clear"))		
				{
					for (int i = 0; i < layers.Count; i ++)
					{
						// Clear paint map
						tileWorldCreator.FillMap(layers[i], false);
					}
					
					// Rebuild map
					tileWorldCreator.ExecuteAllBlueprintLayers();
				}		
			}
			
			var _layerRect = new Rect(Screen.width - 200, Screen.height - 200, 200, 200);
			GUI.Box(_layerRect, "");
			
			
			
			
			using (new GUILayout.AreaScope(_layerRect))
			{
				GUILayout.Label("Zoom");
				
				cameraZoom = GUILayout.HorizontalSlider(cameraZoom, 8, 25);
				editorCamera.orthographicSize = cameraZoom;
				
				GUILayout.Label("Layers");
				
				
				for (int i = 0; i < layers.Count; i ++)
				{
					if (layers[i] == selectedLayer)
					{
						GUI.color = Color.green;
					}
					else
					{
						GUI.color = Color.white;
					}
					
					if (GUILayout.Button(layers[i]))
					{
						selectedLayer = layers[i];
						selectedLayerIndex = i;
					}
				}
			}
		}
		
	    void Update()
	    {
		    Ray _ray = editorCamera.ScreenPointToRay(Input.mousePosition);
		    RaycastHit _hit = new RaycastHit();
	
		    if (Physics.Raycast(_ray, out _hit, 1000))
		    {
		    	
		    	var _xPos = (Mathf.FloorToInt(_hit.point.x / cellSize) * cellSize) + 1;
		    	var _zPos = (Mathf.FloorToInt(_hit.point.z / cellSize) * cellSize) + 1;
		    	var _mouseGridPosition  = new Vector2Int(_xPos, _zPos);
		  
		    	var _cellPosition = new Vector3(_xPos + 0.5f, 0.2f, _zPos + 0.5f);
		    	cursorObject.transform.position = _cellPosition;
			    currentPosition = _cellPosition;
			    
			    
			    if (Input.GetMouseButtonDown(0))
			    {
			    	startGridPosition = _mouseGridPosition;
			    }
			    
			    
			    if (Input.GetMouseButton(0))
			    {
				    xDistanceAbs = Mathf.Abs(_mouseGridPosition.x - startGridPosition.x);
				    yDistanceAbs = Mathf.Abs(_mouseGridPosition.y - startGridPosition.y);
				    xDistance = _mouseGridPosition.x - startGridPosition.x;
				    yDistance = _mouseGridPosition.y - startGridPosition.y;
		
		
				    
				    if (xDistanceAbs > yDistanceAbs)
				    {
			
					    var _gridPosition = new Vector2Int(_mouseGridPosition.x, startGridPosition.y);
				

					    if (xDistance > 0)
					    {
						    roadPreview.transform.rotation = Quaternion.Euler(0, -90, 0);
						    roadPreview.transform.position = new Vector3(startGridPosition.x, 0f, startGridPosition.y + .5f);
				
					    }	
					    else
					    {
						    roadPreview.transform.rotation = Quaternion.Euler(0, 90, 0);
						    roadPreview.transform.position = new Vector3(startGridPosition.x + 1, 0f, startGridPosition.y + 0.5f);
				
					    }
				
					    roadPreview.transform.localScale = new Vector3(1, 0.2f, xDistanceAbs);
				    }
			
				    if (yDistanceAbs > xDistanceAbs)
				    {
					    var _gridPosition = new Vector2Int(startGridPosition.x, _mouseGridPosition.y);


					    if (yDistance > 0)
					    {
						    roadPreview.transform.rotation = Quaternion.Euler(0, 180, 0);
						    roadPreview.transform.position = new Vector3(startGridPosition.x + .5f, 0f, startGridPosition.y);
				
					    }	
					    else
					    {
						    roadPreview.transform.rotation = Quaternion.Euler(0, 0, 0);
						    roadPreview.transform.position = new Vector3(startGridPosition.x + .5f, 0f, startGridPosition.y + 1);
					    }
				
					    roadPreview.transform.localScale = new Vector3(1, 0.2f, yDistanceAbs);
				    }
		
		
			    	
			    }
			    
			    if (Input.GetMouseButton(1))
			    {
			    	
			    	// remove all tiles from all layers
				    for (int i = 0; i < layers.Count; i ++)
				    {
				    	tileWorldCreator.ModifyMap(layers[i], (int)_xPos, (int)_zPos, false);				    	
				    }
			    	
				    if (currentPosition != lastPosition)
				    {
				    	lastPosition = currentPosition;
					
					    var _cellObject = Instantiate(removeCellObject, currentPosition, Quaternion.identity);
				    	paintedCells.Add(_cellObject);
				    }
			    }
			    
			    if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))
			    {
			    	
			    	roadPreview.transform.localScale = Vector3.zero;
			    	
			    	
			    	for (int i = 0; i < paintedCells.Count; i ++)
			    	{
			    		Destroy(paintedCells[i]);
			    	}
			    	
			    	
				    
				    
			    	
			    	// Modify currently selected map and set value to true
				    //tileWorldCreator.ModifyMap(selectedLayer, (int)(_xPos), (int)(_zPos), true);
				    
				    // Assign cells to tileworldcreator
				    if (xDistanceAbs > yDistanceAbs)
				    {
					    if (xDistance > 0)
					    {
						    for (int x = 0; x < xDistanceAbs; x ++)
						    {
							    
								    tileWorldCreator.ModifyMap(selectedLayer, startGridPosition.x + x, startGridPosition.y, true);
							
							    
							    // Loop through all other non selected layers and set the cell value to false
							    // to make sure we can override the tiles with the currently selected one.
							    for (int i = 0; i < layers.Count; i ++)
							    {
								    if (layers[i] != selectedLayer)
								    {
									    tileWorldCreator.ModifyMap(layers[i], startGridPosition.x + x, startGridPosition.y, false);
								    }
							    }
						    }
					    }
					    else
					    {
						    for (int x = 0; x < xDistanceAbs; x ++)
						    {
							    
								    tileWorldCreator.ModifyMap(selectedLayer, startGridPosition.x - x, startGridPosition.y, true);
							
							        
							    // Loop through all other non selected layers and set the cell value to false
							    // to make sure we can override the tiles with the currently selected one.
							    for (int i = 0; i < layers.Count; i ++)
							    {
								    if (layers[i] != selectedLayer)
								    {
									    tileWorldCreator.ModifyMap(layers[i], startGridPosition.x - x, startGridPosition.y, false);
								    }
							    }
						    }
					    }
				    }
			
				    if (yDistanceAbs > xDistanceAbs)
				    {
					    if (yDistance > 0)
					    {
						    for (int x = 0; x < yDistanceAbs; x ++)
						    {
							   
								    tileWorldCreator.ModifyMap(selectedLayer, startGridPosition.x, startGridPosition.y  + x, true);
							
								        
							    // Loop through all other non selected layers and set the cell value to false
							    // to make sure we can override the tiles with the currently selected one.
							    for (int i = 0; i < layers.Count; i ++)
							    {
								    if (layers[i] != selectedLayer)
								    {
									    tileWorldCreator.ModifyMap(layers[i], startGridPosition.x, startGridPosition.y  + x, false);
								    }
							    }
						    }
					    }
					    else
					    {
						    for (int x = 0; x < yDistanceAbs; x ++)
						    {
							   
								    tileWorldCreator.ModifyMap(selectedLayer, startGridPosition.x, startGridPosition.y  - x, true);
							
								    
							    // Loop through all other non selected layers and set the cell value to false
							    // to make sure we can override the tiles with the currently selected one.
							    for (int i = 0; i < layers.Count; i ++)
							    {
								    if (layers[i] != selectedLayer)
								    {
									    tileWorldCreator.ModifyMap(layers[i], startGridPosition.x, startGridPosition.y  - x, false);
								    }
							    }
						    }
					    }
				    }
				    
				    
				  
				    
			    	
			    	tileWorldCreator.ExecuteAllBlueprintLayers();
				}
		    }
	    }
	    
	    
		// Build map after loading is complete or map generation is complete
		void BuildMap(TileWorldCreator _twc)
		{
			// Build the map and set the selected layer as priority
			// because the build layers have the same name as the blueprint layers we can simply
			// assign the selected layer
			_twc.ExecuteAllBuildLayers(selectedLayer, false);
		}
	}
}