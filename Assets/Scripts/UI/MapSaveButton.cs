using Tiles;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Attach to any GameObject in MainScene.
    /// Wire the Button's OnClick() to this component's SaveMap() method.
    /// Finds TWCBridge at runtime so the DontDestroyOnLoad reference is not needed in the Inspector.
    /// </summary>
    public class MapSaveButton : MonoBehaviour
    {
        private TWCBridge _twcBridge;

        void Start()
        {
            _twcBridge = FindFirstObjectByType<TWCBridge>();

            if (_twcBridge == null)
                Debug.LogWarning("[MapSaveButton] TWCBridge not found in scene. Save button will not work.");
        }

        public void SaveMap()
        {
            if (_twcBridge == null)
            {
                Debug.LogError("[MapSaveButton] TWCBridge is null. Cannot save map.");
                return;
            }

            _twcBridge.SaveMap();
        }
    }
}
