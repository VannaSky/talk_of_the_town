using ollama;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

public class VillagerLLMTest : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text promptDisplay;
    [SerializeField] private TMP_Text responseDisplay;
    [SerializeField] private TMP_Text reasoningDisplay;  // NEU!
    [SerializeField] private TMP_Text actionDisplay;     // NEU!
    [SerializeField] private TMP_Dropdown modelDropdown;
    
    [Header("Test Values")]
    [SerializeField] private int hunger = 35;
    [SerializeField] private int thirst = 80;
    [SerializeField] private int sleep = 90;
    [SerializeField] private int villageFood = 8;
    
    [Header("Prompt Options")]
    [SerializeField] private bool requestReasoning = true;
    
    private List<string> modelNames;
    
    void Awake() { Ollama.Launch(); }
    
    async void Start()
    {
        var models = await Ollama.List();
        modelNames = new List<string>();
        
        foreach (var model in models)
            modelNames.Add(model.name);
        
        modelDropdown.AddOptions(modelNames);
        Ollama.InitChat();
    }
    
    public async void OnTestDecision()
    {
        string prompt = BuildPrompt();
        
        promptDisplay.text = prompt;
        responseDisplay.text = "Thinking...";
        reasoningDisplay.text = "...";
        actionDisplay.text = "...";
        
        string selectedModel = modelNames[modelDropdown.value];
        string response = await Ollama.Chat(selectedModel, prompt);
        
        // Vollständige Response anzeigen
        responseDisplay.text = response;
        
        // Parse und zeige Reasoning + Action separat
        ParseResponse(response);
    }
    
    private string BuildPrompt()
    {
        string prompt = $@"You are Anna, a villager.

YOUR STATUS:
Hunger: {hunger}/100 {GetUrgencyTag(hunger)}
Thirst: {thirst}/100 {GetUrgencyTag(thirst)}
Sleep: {sleep}/100 {GetUrgencyTag(sleep)}

VILLAGE:
Food available: {villageFood} units

AVAILABLE ACTIONS:
- EAT: Consume food (restores Hunger)
- DRINK: Get water (restores Thirst)
- SLEEP: Rest (restores Sleep)
- GATHER_FOOD: Hunt/forage for food
- GATHER_WOOD: Collect wood from trees

INSTRUCTIONS:
1. Analyze: Which need is most critical? (lowest value = most urgent!)
2. Explain: Why is this your priority?
3. Decide: State your action

Format:
REASONING: <your analysis>
ACTION: <your choice>";
    
        return prompt;
    }
    
    private string GetUrgencyTag(int value)
    {
        // Auch hier: KEINE TextMeshPro Tags im Prompt!
        if (value < 20) return "[CRITICAL]";
        if (value < 40) return "[LOW]";
        return "";
    }
    
    private void ParseResponse(string response)
    {
        // Parse ohne Formatierung
        var reasoningMatch = Regex.Match(response, @"REASONING:\s*(.+?)(?=ACTION:|$)", RegexOptions.Singleline);
        if (reasoningMatch.Success)
        {
            string reasoning = reasoningMatch.Groups[1].Value.Trim();
            // Formatierung NUR für Display hinzufügen
            reasoningDisplay.text = $"<b>REASONING:</b>\n{reasoning}";
        }
        else
        {
            reasoningDisplay.text = "<color=grey>No reasoning found</color>";
        }
    
        var actionMatch = Regex.Match(response, @"ACTION:\s*(\w+)", RegexOptions.IgnoreCase);
        if (actionMatch.Success)
        {
            string action = actionMatch.Groups[1].Value.ToUpper();
            bool isCorrect = ValidateAction(action);
        
            if (isCorrect)
            {
                // Formatierung NUR für Display
                actionDisplay.text = $"<b><color=green>{action}</color></b>";
            }
            else
            {
                actionDisplay.text = $"<b><color=red>{action}</color></b>\n<size=80%>Should be EAT</size>";
            }
        }
        else
        {
            actionDisplay.text = "<color=red>Parse failed</color>";
        }
    }
    
    private bool ValidateAction(string action)
    {
        // Einfache Validierung: Niedrigster Wert sollte priorisiert werden
        int lowestNeed = Mathf.Min(hunger, Mathf.Min(thirst, sleep));
        
        if (lowestNeed == hunger && hunger < 40)
            return action == "EAT";
        if (lowestNeed == thirst && thirst < 40)
            return action == "DRINK";
        if (lowestNeed == sleep && sleep < 40)
            return action == "SLEEP";
        
        // Sonst ist jede Action "okay" (keine kritischen Needs)
        return true;
    }
}