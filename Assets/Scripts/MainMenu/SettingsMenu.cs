using UnityEngine;

namespace MainMenu
{
    public class SettingsMenu : MonoBehaviour
    {

        [SerializeField] private GameObject mainMenu;
        [SerializeField] private GameObject goalsMenu;
    
    

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
