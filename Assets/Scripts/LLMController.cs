using ollama;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tiles;
using UnityEngine;

/// <summary>
/// LLM Controller for villager job assignment decisions.
/// Uses VillageState for complete context including map, resources, and all villagers.
/// </summary>
public class LLMController : MonoBehaviour
{
    [Header("Model Settings")]
    [SerializeField] private string defaultModel = "qwen3:8b";
    [SerializeField] private int keepAliveSeconds = 600;
    
    [Header("Prompt Settings")]
    [SerializeField] private bool includeFullMap = false;  // Full map is large, use nearby tiles instead
    [SerializeField] private int nearbyTileRadius = 5;     // Tiles to analyze around villager
    [SerializeField] private bool includeThinkingPrompt = true;
    
    [Header("Debug")]
    [SerializeField] private bool logPrompts = true;
    [SerializeField] private bool logResponses = true;
    [SerializeField] private bool logErrors = true;
    
    public static LLMController Instance { get; private set; }
    
    public event Action<string> OnModelLoaded;
    public event Action<JobDecision> OnDecisionMade;
    public event Action<string> OnError;
    
    private List<string> _availableModels = new List<string>();
    public IReadOnlyList<string> AvailableModels => _availableModels;
    
    public bool IsReady { get; private set; }
    public string CurrentModel => defaultModel;
    
    private void Awake()
    {
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
            Ollama.InitChat();
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
    }

    #region Prompt Building
    
    private string BuildSystemPrompt(List<string> availableJobs)
    {
        string jobList = string.Join(", ", availableJobs);
        
        // JSON example as separate string to avoid escaping issues
        string jsonExample = "{\n    \"job\": \"<JOB_NAME>\",\n    \"reason\": \"<brief explanation>\"\n}";
        
        return $@"You are an AI controlling a villager in a village-building simulation. You decide which JOB the villager should perform based on their POSITION and the village's NEEDS.

AVAILABLE JOBS: {jobList}, IDLE

JOB DESCRIPTIONS:
- Lumberjack: Chops trees to gather wood. Works best when near FOREST tiles with trees.
- Miner: Mines stone deposits. Works best when near MOUNTAIN tiles.
- Builder: Constructs buildings. Requires wood and stone in inventory.
- Farmer: Works on completed farms to produce food. Requires seeds.
- IDLE: Stop working, rest.

TERRAIN KEY: G=Grass, F=Forest, M=Mountain, W=Water, C=Coast, Fi=Field

DECISION RULES:
1. POSITION MATTERS: Assign jobs based on what's NEAR the villager!
   - Many forest tiles nearby -> Lumberjack is efficient
   - Many mountain tiles nearby -> Miner is efficient
   - Near unfinished buildings -> Builder (if resources available)
2. RESOURCE BALANCE: Check village inventory. Gather what's scarce.
3. SPREAD OUT: Villagers should NOT work in the same area! If another villager is nearby doing the same job, assign a DIFFERENT job or the target villager should move elsewhere. Check the distance to other villagers!
4. DON'T OVER-ASSIGN: If other villagers already do a job, consider variety.
5. EFFICIENCY: Don't switch jobs if villager is actively working (check their status).
6. GOALS: Prioritize active village goals!

RESPONSE FORMAT (JSON only):
{jsonExample}

Respond ONLY with valid JSON. No other text.";
    }
    
    private string BuildFullContext(Villager targetVillager)
    {
        var sb = new System.Text.StringBuilder();
        
        // Village goals (strategic direction)
        if (VillageGoals.Instance != null)
        {
            sb.AppendLine("=== VILLAGE GOALS ===");
            sb.AppendLine(VillageGoals.Instance.GetGoalsForPrompt());
            sb.AppendLine();
        }
        
        // Village resources
        if (VillageState.Instance != null)
        {
            sb.AppendLine("=== VILLAGE INVENTORY ===");
            sb.AppendLine($"Wood: {VillageState.Instance.Wood}");
            sb.AppendLine($"Stone: {VillageState.Instance.Stone}");
            sb.AppendLine($"Seeds: {VillageState.Instance.Seeds}");
            sb.AppendLine($"Iron: {VillageState.Instance.Iron}");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("=== VILLAGE INVENTORY ===");
            sb.AppendLine("(No VillageState found - inventory unknown)");
            sb.AppendLine();
        }
        
        // All villagers with distances
        sb.AppendLine("=== ALL VILLAGERS ===");
        var targetData = targetVillager.GetData();
        var targetPos = new Vector2Int(targetData.x, targetData.y);
        
        if (VillageState.Instance != null && VillageState.Instance.Villagers.Count > 0)
        {
            foreach (var v in VillageState.Instance.Villagers)
            {
                if (v == null) continue;
                var d = v.GetData();
                
                if (v == targetVillager)
                {
                    sb.AppendLine($"- {d.name} [TARGET - DECIDE FOR THIS ONE]: at ({d.x},{d.y}) on {d.tileType}, Job={d.currentJob} (Lvl {d.jobLevel}), Status=\"{d.jobStatus}\"");
                }
                else
                {
                    // Calculate distance to target villager
                    var otherPos = new Vector2Int(d.x, d.y);
                    int distance = Mathf.Abs(targetPos.x - otherPos.x) + Mathf.Abs(targetPos.y - otherPos.y);
                    string proximity = distance < 5 ? " [VERY CLOSE!]" : distance < 10 ? " [NEARBY]" : "";
                    sb.AppendLine($"- {d.name}: at ({d.x},{d.y}), Job={d.currentJob}, Distance={distance} tiles{proximity}");
                }
            }
        }
        else
        {
            sb.AppendLine($"- {targetData.name} [TARGET]: at ({targetData.x},{targetData.y}) on {targetData.tileType}, Job={targetData.currentJob}, Status=\"{targetData.jobStatus}\"");
        }
        sb.AppendLine();
        
        // Nearby tiles analysis - THIS IS THE KEY FOR POSITION-AWARE DECISIONS
        if (VillageState.Instance != null)
        {
            var nearby = VillageState.Instance.GetNearbyTilesInfo(targetPos, nearbyTileRadius);
            sb.AppendLine($"=== TILES NEAR {targetData.name} (radius {nearbyTileRadius}) ===");
            sb.AppendLine($"Forest tiles: {nearby.forestCount} (can chop trees here)");
            sb.AppendLine($"Mountain tiles: {nearby.mountainCount} (can mine stone here)");
            sb.AppendLine($"Grass tiles: {nearby.grassCount}");
            sb.AppendLine($"Buildings nearby: {nearby.buildingCount}");
            sb.AppendLine($"Trees with wood nearby: {nearby.nearbyWood}");
            sb.AppendLine($"Stone deposits nearby: {nearby.nearbyStone}");
            sb.AppendLine();
        }
        
        // Optional: full map data (very large, disabled by default)
        if (includeFullMap && VillageState.Instance?.TileGrid != null)
        {
            sb.AppendLine("=== FULL MAP (compact) ===");
            sb.AppendLine(VillageState.Instance.GetMapContextJson());
        }
        
        return sb.ToString();
    }
    
    #endregion

    #region Decision Making
    
    /// <summary>
    /// Request a job decision for a villager using full village context
    /// </summary>
    public async Task<JobDecision> RequestJobDecision(Villager villager, List<string> availableJobs)
    {
        if (!IsReady)
        {
            Debug.LogWarning("[LLM] Controller not ready!");
            return JobDecision.Idle("Controller not ready");
        }
        
        if (villager == null)
        {
            Debug.LogError("[LLM] Villager is null!");
            return JobDecision.Idle("Villager null");
        }
        
        string systemPrompt = BuildSystemPrompt(availableJobs);
        string context = BuildFullContext(villager);
        
        string fullPrompt = $"{systemPrompt}\n\n{context}\nWhat job should {villager.villagerName} do next?";
        
        if (includeThinkingPrompt)
            fullPrompt += "\n/think";
        
        if (logPrompts)
            Debug.Log($"[LLM] Prompt:\n{fullPrompt}");
        
        try
        {
            string response = await Ollama.Chat(defaultModel, fullPrompt, keepAliveSeconds);
            
            if (logResponses)
                Debug.Log($"[LLM] Response:\n{response}");
            
            var decision = ParseJobDecision(response);
            OnDecisionMade?.Invoke(decision);
            return decision;
        }
        catch (Exception e)
        {
            if (logErrors)
                Debug.LogError($"[LLM] Error: {e.Message}");
            
            OnError?.Invoke(e.Message);
            return JobDecision.Idle($"Error: {e.Message}");
        }
    }
    
    // Backwards compatibility overload
    public Task<JobDecision> RequestJobDecision(Villager villager, List<string> availableJobs, List<Villager> _)
    {
        return RequestJobDecision(villager, availableJobs);
    }
    
    private JobDecision ParseJobDecision(string response)
    {
        try
        {
            // Strip <think>...</think> blocks (for qwen3, etc.)
            response = Regex.Replace(response, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase).Trim();
            
            // Extract JSON
            var match = Regex.Match(response, @"\{[\s\S]*\}");
            if (!match.Success)
            {
                Debug.LogWarning("[LLM] No JSON found in response");
                return JobDecision.Idle("No JSON in response");
            }
            
            var raw = JsonUtility.FromJson<RawJobDecision>(match.Value);
            
            return new JobDecision
            {
                jobName = raw.job ?? "IDLE",
                reason = raw.reason ?? "",
                success = true
            };
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LLM] Parse error: {e.Message}");
            return JobDecision.Idle($"Parse error: {e.Message}");
        }
    }
    
    #endregion

    #region Utility
    
    public void ResetChat()
    {
        Ollama.InitChat();
        if (logPrompts) Debug.Log("[LLM] Chat reset");
    }
    
    public void SetModel(string modelName)
    {
        if (_availableModels.Contains(modelName))
        {
            defaultModel = modelName;
            ResetChat();
            OnModelLoaded?.Invoke(modelName);
        }
        else
        {
            Debug.LogWarning($"[LLM] Model {modelName} not available!");
        }
    }
    
    /// <summary>
    /// Raw prompt without game context (for testing)
    /// </summary>
    public async Task<LLMResponse> GetResponse(string prompt, string modelOverride = null)
    {
        if (!IsReady)
            return new LLMResponse { success = false, error = "Not ready" };
        
        string model = modelOverride ?? defaultModel;
        
        try
        {
            string response = await Ollama.Chat(model, prompt, keepAliveSeconds);
            return new LLMResponse { success = true, content = response, model = model };
        }
        catch (Exception e)
        {
            return new LLMResponse { success = false, error = e.Message };
        }
    }
    
    #endregion

#if UNITY_EDITOR
    [ContextMenu("Test Decision (First Villager)")]
    private async void TestDecision()
    {
        var villager = FindFirstObjectByType<Villager>();
        if (villager == null)
        {
            Debug.LogError("No Villager in scene!");
            return;
        }
        
        var jobs = new List<string> { "Lumberjack", "Builder", "Farmer" };
        var decision = await RequestJobDecision(villager, jobs);
        Debug.Log($"[LLM] Decision: {decision.jobName} - {decision.reason}");
    }
    
    [ContextMenu("Log Current Context")]
    private void LogContext()
    {
        var villager = FindFirstObjectByType<Villager>();
        if (villager != null)
            Debug.Log(BuildFullContext(villager));
        else
            Debug.LogError("No villager found");
    }
#endif
}

#region Data Classes

[System.Serializable]
public class RawJobDecision
{
    public string job;
    public string reason;
}

[System.Serializable]
public class JobDecision
{
    public string jobName;
    public string reason;
    public bool success;
    
    public bool IsIdle => string.IsNullOrEmpty(jobName) || 
                          jobName.Equals("IDLE", StringComparison.OrdinalIgnoreCase);
    
    public static JobDecision Idle(string reason) => new JobDecision
    {
        jobName = "IDLE",
        reason = reason,
        success = false
    };
}

[System.Serializable]
public class LLMResponse
{
    public bool success;
    public string content;
    public string error;
    public string model;
}

#endregion