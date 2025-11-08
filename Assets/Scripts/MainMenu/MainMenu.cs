using UnityEngine;

public class MainMenu : MonoBehaviour
{
    [SerializeField]
    private string gameScene = "GameScene";

    public void OnStartPressed()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(gameScene);
    }

    public void OnSettingsPressed()
    {
        Debug.Log("Settings button pressed - functionality not implemented yet.");
    }

    public void OnQuitPressed()
    {
        Application.Quit();
    }
}
