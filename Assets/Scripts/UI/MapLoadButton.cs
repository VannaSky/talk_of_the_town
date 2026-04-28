using Tiles;
using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Populates a TMP_Dropdown with saved .twcmap files from persistentDataPath.
    /// Wire the Button's OnClick() to LoadMap(). The dropdown selection determines which file to load.
    /// </summary>
    public class MapLoadButton : MonoBehaviour
    {
        [Tooltip("Dropdown that lists available saved maps.")]
        [SerializeField] TMP_Dropdown mapDropdown;

        void Start()
        {
            RefreshFileList();
        }

        /// <summary>
        /// Scans persistentDataPath for .twcmap files and populates the dropdown.
        /// </summary>
        public void RefreshFileList()
        {
            if (mapDropdown == null)
            {
                Debug.LogWarning("[MapLoadButton] No dropdown assigned.");
                return;
            }

            mapDropdown.ClearOptions();

            string dir = Application.persistentDataPath;
            if (!System.IO.Directory.Exists(dir)) return;

            var files = System.IO.Directory.GetFiles(dir, "*.twcmap");
            if (files.Length == 0)
            {
                mapDropdown.AddOptions(new System.Collections.Generic.List<string> { "No saved maps" });
                mapDropdown.interactable = false;
                return;
            }

            mapDropdown.interactable = true;
            var options = new System.Collections.Generic.List<string>();
            foreach (var file in files)
            {
                options.Add(System.IO.Path.GetFileName(file));
            }

            options.Sort();
            mapDropdown.AddOptions(options);
        }

        public void LoadMap()
        {
            if (mapDropdown == null || mapDropdown.options.Count == 0)
            {
                Debug.LogError("[MapLoadButton] No map selected.");
                return;
            }

            string selected = mapDropdown.options[mapDropdown.value].text;
            if (selected == "No saved maps") return;

            var bridge = FindObjectOfType<TWCBridge>();
            if (bridge == null)
            {
                Debug.LogError("[MapLoadButton] No TWCBridge found in scene.");
                return;
            }

            bridge.LoadFromFile(selected);
        }
    }
}
