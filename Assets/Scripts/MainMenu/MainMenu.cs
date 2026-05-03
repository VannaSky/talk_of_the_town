using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Tiles;

namespace MainMenu
{
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] private GameObject settingsMenu;
        [SerializeField] private GameObject startMenuPanel;

        [Header("Scene Transition")]
        [SerializeField] private GameObject[] objectsToDisable;
        [SerializeField] private GameObject[] objectsToEnable;
        [SerializeField] private MonoBehaviour mainMenuCameraScript;
        [SerializeField] private MonoBehaviour gameCameraScript;

        [Header("Start Button")]
        [SerializeField] private Button startButton;

        void Start()
        {
            if (startButton != null)
                startButton.interactable = false;

            TWCBridge.OnMapLoading += OnMapLoading;
            TWCBridge.OnNavMeshReady += OnNavMeshReady;
        }

        void OnDestroy()
        {
            TWCBridge.OnMapLoading -= OnMapLoading;
            TWCBridge.OnNavMeshReady -= OnNavMeshReady;
        }

        private void OnMapLoading()
        {
            if (startButton != null)
                startButton.interactable = false;
        }

        private void OnNavMeshReady()
        {
            if (startButton != null)
                startButton.interactable = true;
        }

        public void OnStartPressed()
        {
            foreach (var obj in objectsToDisable)
                obj.SetActive(false);

            foreach (var obj in objectsToEnable)
                obj.SetActive(true);

            if (mainMenuCameraScript != null)
                mainMenuCameraScript.enabled = false;

            if (gameCameraScript != null)
                gameCameraScript.enabled = true;
        }

        public void OnSettingsPressed()
        {
            settingsMenu.SetActive(true);
            startMenuPanel.SetActive(false);
        }

        public void OnQuitPressed()
        {
#if UNITY_EDITOR
            EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }
    }
}
