using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using TWC;

/*
	This demo shows you how to set a texture to the texture generator by script,
	and generate the map. 
	Simply assign any texture to the texture field in the inspector of this script.
*/

namespace TWC.Demo
{
	public class AssignTexture : MonoBehaviour
	{
		public TileWorldCreator tileWorldCreator;
		public Texture2D texture;
		
		public float grayscaleRangeMin = 0.1f;
		public float grayscaleRangeMax = 1f;
		
		
		void OnEnable()
		{
			tileWorldCreator.OnBlueprintLayersComplete += BuildTiles;
		}
		
		void OnDisable()
		{
			tileWorldCreator.OnBlueprintLayersComplete -= BuildTiles;
		}
	
		void OnGUI()
		{
			GUILayout.Label("Grayscale range min:");
			grayscaleRangeMin = GUILayout.HorizontalSlider(grayscaleRangeMin, 0f, 1f);
			GUILayout.Label("Grayscale range max:");
			grayscaleRangeMax = GUILayout.HorizontalSlider(grayscaleRangeMax, 0f, 1f);
			
			
			if (GUILayout.Button("Generate from Texture"))
			{
				// 1. Get the first action (Texture) from the blueprint layer called island
				var _textureGenerator = tileWorldCreator.GetAction("Island", 0) as TWC.Actions.Texture;
				
				// 2. Set the texture
				_textureGenerator.SetTexture(texture);
				
				// 3. Set grayscale range
				_textureGenerator.SetGrayscaleRange(grayscaleRangeMin, grayscaleRangeMax);
				
				// 4. Execute all blueprint layers
				tileWorldCreator.ExecuteAllBlueprintLayers();
			}
		}
		
		
		void BuildTiles(TileWorldCreator _twc)
		{
			// 5. After blueprint layers have been executed, execute build layers
			tileWorldCreator.ExecuteAllBuildLayers(true);
		}
	}
}