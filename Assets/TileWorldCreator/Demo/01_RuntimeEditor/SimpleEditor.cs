using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using TWC;

/*
	BASIC RUNTIME EDITOR
	--------------------
	
	This is a simple demonstration on how to modify and build a map at runtime.
	It also shows basic functionality like saving/loading, selecting different layers and fill or clear a map


	// Modify map
	ModifyMap(string layerName, int x, int y, bool value);
	// Execute all generation layers after modification
	ExecuteAllGenerationLayers();
	// Execute all instantiation layers.
	ExecuteAllInstantiationLayers();

*/

namespace TWC.Demo
{
	public class SimpleEditor : MonoBehaviour
	{
		public TileWorldCreator tileWorldCreator;
		public Camera editorCamera;
		
		[Header("Editor objects")]
		public GameObject cursorObject;
		public GameObject addCellObject;
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
				if (GUILayout.Button("Clear Layer"))		
				{
					// Clear paint map of selected layer
					tileWorldCreator.FillMap(selectedLayer, false);
					// Rebuild map
					tileWorldCreator.ExecuteAllBlueprintLayers();
				}
				
				if (GUILayout.Button("Fill Layer"))
				{
					// Fill pain map of selected layer
					tileWorldCreator.FillMap(selectedLayer, true);
					// Rebuild map
					tileWorldCreator.ExecuteAllBlueprintLayers();
				}
				
				// SAVE AND LOAD
				// We're using the streaming assets folder. 
				// Please make sure a streaming assets folder exists in your project
				if (GUILayout.Button("Save Map"))
				{
					if (!System.IO.Directory.Exists(Application.streamingAssetsPath))
					{
						Debug.LogError("Directory does not exist. Please create a new Streaming Assets folder");
						return;
					}
					
					var _path = Application.streamingAssetsPath + "/twc_demo_saveMap.json";
					tileWorldCreator.SaveBlueprintStack(_path);
				}
				
				if (GUILayout.Button("Load Map"))
				{
					var _path = Application.streamingAssetsPath + "/twc_demo_saveMap.json";
					if (System.IO.File.Exists(_path))
					{
						// Load layer stack and execute it
						// Build map when OnGenerationComplete event has been called
						tileWorldCreator.LoadBlueprintStackAndExecute(_path);
					}
				}
				
				if (GUILayout.Button("Enable cellular automata and regenerate"))
				{
					cellularAutomataGeneration = true;
					// Get the cellular automata action from the layer by using its stack index.
					var _action = tileWorldCreator.GetAction("PaintLayer1", 0);
					// Enable cellular automata generator
					_action.active = true;
					// Clear the paint map
					tileWorldCreator.FillMap("PaintLayer1", false);
					// Execute all generation layers including the newly enabled cellular automata generator
					tileWorldCreator.ExecuteAllBlueprintLayers();
					// Disable cellular automata. This is important otherwise it will re-generate the whole map after user modifies the map.
					_action.active = false;
					
					// After generation is complete we copy the map output to the paint modifier, so that the user can modify it.
					// This is done in the BuildMap method
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
		    	
		  
		    	cursorObject.transform.position = new Vector3(_xPos + 0.5f, 0.2f, _zPos + 0.5f);
			    currentPosition = new Vector3(_xPos + 0.5f, (selectedLayerIndex * 1) + 0.1f, _zPos + 0.5f);
			    
			    
			    if (Input.GetMouseButton(0))
			    {
			    	
				    //tileWorldCreator.ModifyMap(selectedLayer, (int)(_xPos/2), (int)(_zPos/2), true);
				    tileWorldCreator.ModifyMap(selectedLayer, (int)(_xPos), (int)(_zPos), true);
				    
				    if (currentPosition != lastPosition)
				    {
				    	lastPosition = currentPosition;
					
					    var _cellObject = Instantiate(addCellObject, currentPosition, Quaternion.identity);
				    	paintedCells.Add(_cellObject);
				    }
			    	
			    }
			    
			    if (Input.GetMouseButton(1))
			    {
			    	//tileWorldCreator.ModifyMap(selectedLayer, (int)(_xPos/2), (int)(_zPos/2), false);
				    tileWorldCreator.ModifyMap(selectedLayer, (int)(_xPos), (int)(_zPos), false);
			    	
				    if (currentPosition != lastPosition)
				    {
				    	lastPosition = currentPosition;
					
					    var _cellObject = Instantiate(removeCellObject, currentPosition, Quaternion.identity);
				    	paintedCells.Add(_cellObject);
				    }
			    }
			    
			    if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))
			    {
			    	for (int i = 0; i < paintedCells.Count; i ++)
			    	{
			    		Destroy(paintedCells[i]);
			    	}
			    	
			    	tileWorldCreator.ExecuteAllBlueprintLayers();
				}
		    }
	    }
	    
	    
		// Build map after loading is complete or map generation is complete
		void BuildMap(TileWorldCreator _twc)
		{
			if (cellularAutomataGeneration)
			{
				// After generation is done we can copy the map which was generated by the cellular automata to our paint modifier
				tileWorldCreator.CopyMap("PaintLayer1");
				
				cellularAutomataGeneration = false;
			}
			
			// Build the map, force a complete rebuild
			_twc.ExecuteAllBuildLayers(false);
		}
	}
}