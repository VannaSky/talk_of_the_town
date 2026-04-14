using Tiles;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UI
{
    /// <summary>
    /// Attach to the Load button (or any GameObject) in the Menu scene.
    /// Wire the Button's OnClick() to this component's LoadMap() method.
    /// Set mapFilename in the Inspector to choose which saved map to load.
    /// </summary>
    public class MapLoadButton : MonoBehaviour
    {
        [Tooltip("Name of the scene to load (must match exactly as in Build Settings).")]
        [SerializeField] string mainSceneName = "MainScene";

        [Tooltip("Saved map file to load, e.g. map_01.json")]
        [SerializeField] string mapFilename = "map_01.json";

        public void LoadMap()
        {
            if (string.IsNullOrEmpty(mapFilename))
            {
                Debug.LogError("[MapLoadButton] mapFilename is not set.");
                return;
            }

            TWCBridge.MapFileToLoad = mapFilename;
            SceneManager.LoadScene(mainSceneName);
        }
    }
}
