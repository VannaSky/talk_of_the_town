using UnityEngine;

public class GlobalSettings : MonoBehaviour
{
    public static GlobalSettings Instance { get; private set; }

    [SerializeField] private LogLevel logLevel = LogLevel.Warning;
    [SerializeField] private string llmModel = "";
    [SerializeField] private bool useCavemanPrompt = false;

    public string LLMModel
    {
        get => llmModel;
        set => llmModel = value;
    }

    public bool UseCavemanPrompt
    {
        get => useCavemanPrompt;
        set => useCavemanPrompt = value;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        GameLog.GlobalLevel = logLevel;
    }
}