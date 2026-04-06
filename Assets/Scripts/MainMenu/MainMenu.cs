using UnityEditor;
using UnityEngine;

namespace MainMenu
{
    public class MainMenu : MonoBehaviour
    {
        [SerializeField]
        private string gameScene = "GameScene";
        
        [SerializeField] private GameObject settingsMenu;
        
        public void OnStartPressed()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameScene);
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
