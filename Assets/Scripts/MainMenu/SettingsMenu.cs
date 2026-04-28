using UnityEngine;
using UnityEngine.UI;

namespace MainMenu
{
    public class SettingsMenu : MonoBehaviour
    {
        [SerializeField] private GameObject mainMenu;
        [SerializeField] private GameObject goalsMenu;
        [SerializeField] private Toggle cavemanPromptToggle;

        private void OnEnable()
        {
            if (cavemanPromptToggle != null && GlobalSettings.Instance != null)
                cavemanPromptToggle.SetIsOnWithoutNotify(GlobalSettings.Instance.UseCavemanPrompt);
        }

        public void OnCavemanPromptToggleChanged(bool value)
        {
            if (GlobalSettings.Instance != null)
                GlobalSettings.Instance.UseCavemanPrompt = value;
        }

        public void OnBackPressed()
        {
            gameObject.SetActive(false);
            mainMenu.SetActive(true);
        }

        public void OnGoalsPressed()
        {
            gameObject.SetActive(false);
            goalsMenu.SetActive(true);
        }
    }
}
