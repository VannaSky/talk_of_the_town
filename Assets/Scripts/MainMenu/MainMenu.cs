using UnityEditor;
using UnityEngine;

namespace MainMenu
{
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] private GameObject settingsMenu;

        [Header("Scene Transition")]
        [SerializeField] private GameObject[] objectsToDisable;
        [SerializeField] private GameObject[] objectsToEnable;
        [SerializeField] private MonoBehaviour mainMenuCameraScript;
        [SerializeField] private MonoBehaviour gameCameraScript;

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
            gameObject.SetActive(false);
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
