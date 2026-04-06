using UnityEngine;

namespace MainMenu
{
    public class GoalsMenu : MonoBehaviour
    {
    
        [SerializeField] private GameObject settingsMenu;
        // Start is called once before the first execution of Update after the MonoBehaviour is created


        public void OnBackPressed()
        {
            gameObject.SetActive(false);
            settingsMenu.SetActive(true);
        }

        // Update is called once per frame

    }
}
