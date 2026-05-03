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
        private const string LogCategory = "MapSaveButton";
        void LogError(string msg)   => GameLog.LogError(LogCategory, msg, this);
        void LogWarning(string msg) => GameLog.LogWarning(LogCategory, msg, this);
        void LogEvent(string msg)   => GameLog.LogEvent(LogCategory, msg, this);
        void LogInfo(string msg)    => GameLog.LogInfo(LogCategory, msg, this);
        void LogVerbose(string msg) => GameLog.LogVerbose(LogCategory, msg, this);

        [Tooltip("Optional: MapLoadButton to refresh its dropdown after saving.")]
        [SerializeField] private MapLoadButton mapLoadButton;

        private TWCBridge _twcBridge;

        void Start()
        {
            _twcBridge = FindFirstObjectByType<TWCBridge>();

            if (_twcBridge == null)
                LogWarning("TWCBridge not found in scene. Save button will not work.");
        }

        public void SaveMap()
        {
            if (_twcBridge == null)
            {
                LogError("TWCBridge is null. Cannot save map.");
                return;
            }

            _twcBridge.SaveMap();

            if (mapLoadButton != null)
                mapLoadButton.RefreshFileList();
        }
    }
}
