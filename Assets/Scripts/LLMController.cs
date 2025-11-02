using ollama;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Singleton LLM Controller - verwaltet alle Ollama-Interaktionen
/// </summary>
public class LLMController : MonoBehaviour
{
    [Header("Model Settings")]
    [SerializeField] private string defaultModel = "qwen3:8b";
    [SerializeField] private int keepAliveSeconds = 600;
    
    [Header("Debug")]
    [SerializeField] private bool logPrompts = false;
    [SerializeField] private bool logResponses = false;
    [SerializeField] private bool logErrors = true;
    
    // Singleton
    public static LLMController Instance { get; private set; }
    
    // Events für andere Scripts
    public event Action<string> OnModelLoaded;
    public event Action<string> OnDecisionMade;
    public event Action<string> OnError;
    
    // Verfügbare Modelle
    private List<string> _availableModels = new List<string>();
    public IReadOnlyList<string> AvailableModels => _availableModels;
    
    // Status
    public bool IsReady { get; private set; }
    public string CurrentModel => defaultModel;
    
    private void Awake()
    {
        // Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private async void Initialize()
    {
        try
        {
            Ollama.Launch();
            
            // Chat-System initialisieren
            Ollama.InitChat();
            
            // Verfügbare Modelle laden
            await LoadAvailableModels();
            
            IsReady = true;
            OnModelLoaded?.Invoke(defaultModel);
            
            if (logPrompts)
                Debug.Log($"[LLM] Controller ready with model: {defaultModel}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LLM] Initialization failed: {e.Message}");
            OnError?.Invoke(e.Message);
        }
    }
    
    private async Task LoadAvailableModels()
    {
        var models = await Ollama.List();
        _availableModels.Clear();
        
        foreach (var model in models)
            _availableModels.Add(model.name);
        
        if (logPrompts)
            Debug.Log($"[LLM] Loaded {_availableModels.Count} models");
    }
    
    /// <summary>
    /// Haupt-Methode: Sendet Prompt und erhält Response
    /// </summary>
    public async Task<LLMResponse> GetResponse(string prompt, string modelOverride = null)
    {
        if (!IsReady)
        {
            Debug.LogWarning("[LLM] Controller not ready yet!");
            return new LLMResponse { success = false, error = "Controller not ready" };
        }
        
        string model = modelOverride ?? defaultModel;
        
        if (logPrompts)
            Debug.Log($"[LLM] Sending prompt to {model}:\n{prompt}");
        
        try
        {
            string response = await Ollama.Chat(model, prompt, keepAliveSeconds);
            
            if (logResponses)
                Debug.Log($"[LLM] Response:\n{response}");
            
            OnDecisionMade?.Invoke(response);
            
            return new LLMResponse
            {
                success = true,
                content = response,
                model = model
            };
        }
        catch (Exception e)
        {
            if (logErrors)
                Debug.LogError($"[LLM] Error: {e.Message}");
            
            OnError?.Invoke(e.Message);
            
            return new LLMResponse
            {
                success = false,
                error = e.Message
            };
        }
    }
    
    /// <summary>
    /// Chat zurücksetzen (neue Conversation starten)
    /// </summary>
    public void ResetChat()
    {
        Ollama.InitChat();
        
        if (logPrompts)
            Debug.Log("[LLM] Chat reset");
    }
    
    /// <summary>
    /// Modell wechseln
    /// </summary>
    public void SetModel(string modelName)
    {
        if (_availableModels.Contains(modelName))
        {
            defaultModel = modelName;
            ResetChat(); // Neue Chat-Session für neues Modell
            OnModelLoaded?.Invoke(modelName);
        }
        else
        {
            Debug.LogWarning($"[LLM] Model {modelName} not available!");
        }
    }
}

/// <summary>
/// Response-Datenstruktur
/// </summary>
[System.Serializable]
public class LLMResponse
{
    public bool success;
    public string content;
    public string error;
    public string model;
}