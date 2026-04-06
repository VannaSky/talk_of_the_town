using UnityEngine;

public class GlobalSettings : MonoBehaviour
{
    [SerializeField] private LogLevel logLevel = LogLevel.Warning;
    [SerializeField] private string llmModel = "";

    public string LLMModel
    {
        get => llmModel;
        set => llmModel = value;
    }


    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        GameLog.GlobalLevel = logLevel;
        
        
       
    }
}