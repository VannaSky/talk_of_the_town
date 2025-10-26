using ollama;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class VillagerLLMTest : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text promptDisplay;
    [SerializeField] private TMP_Text responseDisplay;
    [SerializeField] private TMP_Dropdown modelDropdown;
    
    [Header("Test Values")]
    [SerializeField] private int hunger = 35;
    [SerializeField] private int thirst = 80;
    [SerializeField] private int sleep = 90;
    [SerializeField] private int villageFood = 8;
    
    private List<string> modelNames;
    
    void Awake()
    {
        Ollama.Launch();
    }
    
    async void Start()
    {
        var models = await Ollama.List();
        modelNames = new List<string>();
        
        foreach (var model in models)
            modelNames.Add(model.name);
        
        modelDropdown.AddOptions(modelNames);
        
        // Chat initialisieren (wichtig für Chat-Methoden!)
        Ollama.InitChat();
    }
    
    public async void OnTestDecision()
    {
        string prompt = $@"You are Anna, a villager.

YOUR STATUS:
Hunger: {hunger}/100
Thirst: {thirst}/100  
Sleep: {sleep}/100

VILLAGE:
Food available: {villageFood} units

DECISION:
Choose ONE action: EAT, DRINK, SLEEP, GATHER_FOOD, GATHER_WOOD

Respond with: ACTION: <your choice>";

        promptDisplay.text = prompt;
        responseDisplay.text = "Thinking...";
        
        string selectedModel = modelNames[modelDropdown.value];
        
        // Chat (ohne Stream) - wartet auf komplette Antwort
        string response = await Ollama.Chat(selectedModel, prompt);
        
        responseDisplay.text = response;
    }
}