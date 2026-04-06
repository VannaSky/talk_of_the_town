using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tiles;
using UnityEngine;
using ollama;

/// <summary>
/// LLM Controller with batch decision-making for all villagers at once.
/// Prevents race conditions by coordinating assignments in a single prompt.
/// Now tracks token usage and performance metrics.
/// </summary>
public class LLMController : MonoBehaviour
{
    [Header("Model Settings")]
    [SerializeField] private string defaultModel = "gpt-oss:120b-cloud";
    [SerializeField] private int keepAliveSeconds = 600;

    [Header("Prompt Settings")]
    [SerializeField] private int maxResourceLocationsToShow = 6;
    [SerializeField] private bool includeThinkingPrompt = true;

    [Header("Batch Decision Settings")]
    [Tooltip("How often to make batch decisions for all villagers")]
    [SerializeField] private float batchDecisionInterval = 10f;
    [SerializeField] private bool useBatchDecisions = true;

    [Header("Debug")]
    [SerializeField] private bool logPrompts = true;
    [SerializeField] private bool logResponses = true;
    [SerializeField] private bool logErrors = true;
    [SerializeField] private bool logTokenUsage = true;

    [Header("Metrics Tracking")]
    [SerializeField] private bool trackMetrics = true;
    [SerializeField] private bool exportMetricsToFile = false;
    [SerializeField] private string metricsFilePath = "LLM_Metrics.json";

    public static LLMController Instance { get; private set; }

    public event Action<string> OnModelLoaded;
    public event Action<JobDecision> OnDecisionMade;
    public event Action<Dictionary<string, JobDecision>> OnBatchDecisionMade;
    public event Action<string> OnError;
    public event Action<LLMMetrics> OnMetricsRecorded;

    private List<string> _availableModels = new List<string>();
    public IReadOnlyList<string> AvailableModels => _availableModels;

    public bool IsReady { get; private set; }
    public string CurrentModel => defaultModel;
    public bool UseBatchDecisions => useBatchDecisions;

    // Batch decision state
    private bool _isBatchProcessing;
    private Dictionary<string, JobDecision> _latestBatchDecisions = new ();
    private float _lastBatchDecisionTime;

    public bool IsBatchProcessing => _isBatchProcessing;
    public float TimeSinceLastBatch => Time.time - _lastBatchDecisionTime;

    // Metrics tracking
    private List<LLMMetrics> _metricsHistory = new ();
    private LLMMetrics _lastMetrics;
    private LLMSessionStats _sessionStats = new ();

    public LLMMetrics LastMetrics => _lastMetrics;
    public LLMSessionStats SessionStats => _sessionStats;
    public IReadOnlyList<LLMMetrics> MetricsHistory => _metricsHistory;

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

            await ApplyModelFromGlobalSettings();

            IsReady = true;
            OnModelLoaded?.Invoke(defaultModel);

            if (logPrompts)
                Debug.Log($"[LLM] Controller ready with model: {defaultModel}");

            if (useBatchDecisions)
            {
                StartCoroutine(BatchDecisionLoop());
            }
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

    private async Task ApplyModelFromGlobalSettings()
    {
        var globalSettings = FindObjectOfType<GlobalSettings>();
        if (globalSettings == null || string.IsNullOrEmpty(globalSettings.LLMModel))
            return;

        if (_availableModels.Contains(globalSettings.LLMModel))
        {
            defaultModel = globalSettings.LLMModel;
            if (logPrompts)
                Debug.Log($"[LLM] Applied model from GlobalSettings: {defaultModel}");
        }
        else
        {
            Debug.Log($"[LLM] Model '{globalSettings.LLMModel}' not found locally. Attempting to pull...");

            bool success = await Ollama.Pull(globalSettings.LLMModel, (status, progress) =>
            {
                if (logPrompts)
                    Debug.Log($"[LLM] Pull '{globalSettings.LLMModel}': {status} ({progress:F1}%)");
            });

            if (success)
            {
                await LoadAvailableModels();
                defaultModel = globalSettings.LLMModel;
                Debug.Log($"[LLM] Successfully pulled and applied model: {defaultModel}");
            }
            else
            {
                Debug.LogWarning($"[LLM] Failed to pull model '{globalSettings.LLMModel}'. Using default: {defaultModel}");
            }
        }
    }

    #region Batch Decision Loop

    private IEnumerator BatchDecisionLoop()
    {
        yield return new WaitForSeconds(2f);

        while (true)
        {
            if (IsReady && VillageState.Instance != null && VillageState.Instance.Villagers.Count > 0)
            {
                yield return RequestBatchDecisions();
            }

            yield return new WaitForSeconds(batchDecisionInterval);
        }
    }

    public void RequestImmediateBatchDecision()
    {
        if (!_isBatchProcessing && IsReady)
        {
            StartCoroutine(RequestBatchDecisions());
        }
    }

    private IEnumerator RequestBatchDecisions()
    {
        if (_isBatchProcessing) yield break;

        _isBatchProcessing = true;

        var villagers = VillageState.Instance.Villagers;
        var jobNames = GetAvailableJobNames();

        if (villagers.Count == 0 || jobNames.Count == 0)
        {
            _isBatchProcessing = false;
            yield break;
        }

        var task = RequestBatchJobDecisions(villagers, jobNames);

        while (!task.IsCompleted)
            yield return null;

        _latestBatchDecisions = task.Result;
        _lastBatchDecisionTime = Time.time;
        _isBatchProcessing = false;

        OnBatchDecisionMade?.Invoke(_latestBatchDecisions);

        if (logPrompts)
            Debug.Log($"[LLM] Batch decisions made for {_latestBatchDecisions.Count} villagers");
    }

    private List<string> GetAvailableJobNames()
    {
        var jobTypes = Resources.LoadAll<JobType>("");
        var names = new List<string>();
        foreach (var jt in jobTypes)
        {
            if (jt != null)
                names.Add(jt.JobName);
        }
        return names;
    }

    public JobDecision GetLatestDecision(string villagerName)
    {
        if (_latestBatchDecisions.TryGetValue(villagerName, out var decision))
            return decision;
        return null;
    }

    #endregion

    #region Batch Prompt Building

    private string BuildBatchSystemPrompt(List<string> availableJobs, int villagerCount)
    {
        string jobList = string.Join(", ", availableJobs);

        string jsonExample = @"{
    ""assignments"": [
        { ""villager"": ""<NAME>"", ""job"": ""<JOB>"", ""targetX"": <X>, ""targetY"": <Y>, ""reason"": ""<why>"" },
        { ""villager"": ""<NAME>"", ""job"": ""<JOB>"", ""targetX"": <X>, ""targetY"": <Y>, ""reason"": ""<why>"" }
    ]
}";

        return $@"You are an AI coordinator for a village simulation. You must assign jobs to ALL {villagerCount} villagers at once, ensuring they work efficiently and don't cluster together.

AVAILABLE JOBS: {jobList}, IDLE

JOB DESCRIPTIONS:
- Lumberjack: Chops trees for wood. Assign to TREE locations.
- Miner: Mines stone deposits. Assign to STONE locations.
- Builder: Constructs buildings. Needs wood+stone in inventory.
- Farmer: Works at farms to produce food. Needs seeds.
- SeedGatherer: Collects seeds from seed nodes (pumpkins, wheat, etc.)
- IDLE: Rest.

CRITICAL COORDINATION RULES:
1. SPREAD VILLAGERS OUT: Each villager must go to a DIFFERENT location!
2. NEVER assign two villagers to the same coordinates!
3. BALANCE JOBS: Don't assign everyone to the same job unless necessary.
4. MATCH RESOURCES TO NEEDS: Check inventory, gather what's scarce.
5. USE DIFFERENT RESOURCE NODES: If both need wood, send them to different tree clusters!

RESPONSE FORMAT (JSON only, assign ALL villagers):
{jsonExample}

Respond ONLY with valid JSON containing assignments for ALL {villagerCount} villagers.";
    }

    private string BuildBatchContext(IReadOnlyList<Villager> villagers, List<string> availableJobs)
    {
        var sb = new System.Text.StringBuilder();

        if (VillageGoals.Instance != null)
        {
            sb.AppendLine("=== VILLAGE GOALS ===");
            sb.AppendLine(VillageGoals.Instance.GetGoalsForPrompt());
            sb.AppendLine();
        }

        if (VillageState.Instance != null)
        {
            sb.AppendLine("=== VILLAGE INVENTORY ===");
            sb.AppendLine($"Wood: {VillageState.Instance.Wood}");
            sb.AppendLine($"Stone: {VillageState.Instance.Stone}");
            sb.AppendLine($"Seeds: {VillageState.Instance.Seeds}");
            sb.AppendLine($"Food: {VillageState.Instance.Food}");
            sb.AppendLine();
        }

        sb.AppendLine("=== VILLAGERS TO ASSIGN ===");
        foreach (var v in villagers)
        {
            if (v == null) continue;
            var d = v.GetData();
            sb.AppendLine($"- {d.name}: currently at ({d.x},{d.y}), Job={d.currentJob}, Status=\"{d.jobStatus}\"");
        }
        sb.AppendLine();

        sb.AppendLine("=== AVAILABLE RESOURCES (assign villagers to DIFFERENT locations!) ===");
        var resourceLocations = GetAllResourceLocations();

        if (resourceLocations.treeLocations.Count > 0)
        {
            sb.Append("TREES: ");
            sb.AppendLine(FormatLocationsSimple(resourceLocations.treeLocations));
        }

        if (resourceLocations.stoneLocations.Count > 0)
        {
            sb.Append("STONE: ");
            sb.AppendLine(FormatLocationsSimple(resourceLocations.stoneLocations));
        }

        if (resourceLocations.seedLocations.Count > 0)
        {
            sb.Append("SEEDS: ");
            sb.AppendLine(FormatLocationsSimple(resourceLocations.seedLocations));
        }

        if (resourceLocations.buildingLocations.Count > 0)
        {
            sb.Append("UNFINISHED BUILDINGS: ");
            sb.AppendLine(FormatLocationsSimple(resourceLocations.buildingLocations));
        }

        if (resourceLocations.farmLocations.Count > 0)
        {
            sb.Append("FARMS: ");
            sb.AppendLine(FormatLocationsSimple(resourceLocations.farmLocations));
        }

        return sb.ToString();
    }

    private string FormatLocationsSimple(List<Vector2Int> locations)
    {
        var parts = new List<string>();
        int count = Mathf.Min(locations.Count, maxResourceLocationsToShow);

        for (int i = 0; i < count; i++)
        {
            var loc = locations[i];
            parts.Add($"({loc.x},{loc.y})");
        }

        string result = string.Join(", ", parts);
        if (locations.Count > maxResourceLocationsToShow)
            result += $" ...+{locations.Count - maxResourceLocationsToShow} more";

        return result;
    }

    #endregion

    #region Batch Decision Making

    public async Task<Dictionary<string, JobDecision>> RequestBatchJobDecisions(
        IReadOnlyList<Villager> villagers,
        List<string> availableJobs)
    {
        var results = new Dictionary<string, JobDecision>();

        if (!IsReady || villagers.Count == 0)
            return results;

        string systemPrompt = BuildBatchSystemPrompt(availableJobs, villagers.Count);
        string context = BuildBatchContext(villagers, availableJobs);

        string fullPrompt = $"{systemPrompt}\n\n{context}\nAssign jobs and locations to ALL villagers:";

        if (includeThinkingPrompt)
            fullPrompt += "\n/think";

        if (logPrompts)
            Debug.Log($"[LLM] Batch Prompt:\n{fullPrompt}");

        // Start metrics tracking
        var metrics = new LLMMetrics
        {
            timestamp = DateTime.Now,
            modelUsed = defaultModel,
            requestType = "Batch",
            villagerCount = villagers.Count,
            promptLength = fullPrompt.Length
        };

        var startTime = DateTime.Now;

        try
        {
            // Use the extension method to get full metadata
            var chatResponse = await OllamaExtensions.ChatWithMetadataExt(defaultModel, fullPrompt, keepAliveSeconds);
            
            metrics.responseTime = (DateTime.Now - startTime).TotalSeconds;
            metrics.responseLength = chatResponse.content.Length;
            
            // Extract ACTUAL token counts from metadata
            metrics.promptEvalCount = chatResponse.promptEvalCount;
            metrics.evalCount = chatResponse.evalCount;
            metrics.totalTokens = chatResponse.TotalTokens;
            
            // Extract timing data
            metrics.promptEvalDuration = chatResponse.PromptEvalSeconds;
            metrics.evalDuration = chatResponse.EvalSeconds;
            metrics.totalDuration = chatResponse.TotalSeconds;
            metrics.loadDuration = chatResponse.LoadSeconds;

            if (logResponses)
                Debug.Log($"[LLM] Batch Response:\n{chatResponse.content}");

            results = ParseBatchDecisions(chatResponse.content, villagers);
            
            metrics.success = true;
            metrics.decisionsCount = results.Count;
        }
        catch (Exception e)
        {
            metrics.success = false;
            metrics.errorMessage = e.Message;
            metrics.responseTime = (DateTime.Now - startTime).TotalSeconds;
            
            if (logErrors)
                Debug.LogError($"[LLM] Batch Error: {e.Message}");

            OnError?.Invoke(e.Message);

            foreach (var v in villagers)
            {
                if (v != null)
                    results[v.villagerName] = JobDecision.Idle($"Error: {e.Message}");
            }
        }

        // Record metrics
        RecordMetrics(metrics);

        return results;
    }

    private Dictionary<string, JobDecision> ParseBatchDecisions(string response, IReadOnlyList<Villager> villagers)
    {
        var results = new Dictionary<string, JobDecision>();

        try
        {
            response = Regex.Replace(response, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase).Trim();

            var match = Regex.Match(response, @"\{[\s\S]*\}");
            if (!match.Success)
            {
                Debug.LogWarning("[LLM] No JSON found in batch response");
                foreach (var v in villagers)
                    if (v != null) results[v.villagerName] = JobDecision.Idle("No JSON");
                return results;
            }

            var raw = JsonUtility.FromJson<RawBatchDecision>(match.Value);

            if (raw.assignments != null)
            {
                foreach (var assignment in raw.assignments)
                {
                    var decision = new JobDecision
                    {
                        jobName = assignment.job ?? "IDLE",
                        reason = assignment.reason ?? "",
                        success = true,
                        hasTargetArea = assignment.targetX != 0 || assignment.targetY != 0,
                        targetX = assignment.targetX,
                        targetY = assignment.targetY
                    };

                    results[assignment.villager] = decision;

                    if (logPrompts)
                        Debug.Log($"[LLM] Parsed: {assignment.villager} -> {decision.jobName} at ({decision.targetX},{decision.targetY})");
                }
            }

            foreach (var v in villagers)
            {
                if (v != null && !results.ContainsKey(v.villagerName))
                {
                    results[v.villagerName] = JobDecision.Idle("Not in response");
                    Debug.LogWarning($"[LLM] Villager {v.villagerName} not in batch response");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LLM] Batch parse error: {e.Message}");
            foreach (var v in villagers)
                if (v != null) results[v.villagerName] = JobDecision.Idle($"Parse error: {e.Message}");
        }

        return results;
    }

    #endregion

    #region Single Villager Decision (fallback)

    public async Task<JobDecision> RequestJobDecision(Villager villager, List<string> availableJobs)
    {
        if (useBatchDecisions)
        {
            var existing = GetLatestDecision(villager.villagerName);
            if (existing != null && TimeSinceLastBatch < batchDecisionInterval)
                return existing;

            RequestImmediateBatchDecision();

            float timeout = 30f;
            float elapsed = 0f;
            while (_isBatchProcessing && elapsed < timeout)
            {
                await Task.Delay(100);
                elapsed += 0.1f;
            }

            return GetLatestDecision(villager.villagerName) ?? JobDecision.Idle("Batch timeout");
        }

        return await RequestSingleJobDecision(villager, availableJobs);
    }

    private async Task<JobDecision> RequestSingleJobDecision(Villager villager, List<string> availableJobs)
    {
        if (!IsReady)
            return JobDecision.Idle("Controller not ready");

        if (villager == null)
            return JobDecision.Idle("Villager null");

        string systemPrompt = BuildSingleSystemPrompt(availableJobs);
        string context = BuildSingleContext(villager);

        string fullPrompt = $"{systemPrompt}\n\n{context}\nDecide job AND target for {villager.villagerName}:";

        if (includeThinkingPrompt)
            fullPrompt += "\n/think";

        if (logPrompts)
            Debug.Log($"[LLM] Single Prompt:\n{fullPrompt}");

        var metrics = new LLMMetrics
        {
            timestamp = DateTime.Now,
            modelUsed = defaultModel,
            requestType = "Single",
            villagerCount = 1,
            promptLength = fullPrompt.Length,
            villagerName = villager.villagerName
        };

        var startTime = DateTime.Now;

        try
        {
            var chatResponse = await OllamaExtensions.ChatWithMetadataExt(defaultModel, fullPrompt, keepAliveSeconds);

            metrics.responseTime = (DateTime.Now - startTime).TotalSeconds;
            metrics.responseLength = chatResponse.content.Length;
            
            metrics.promptEvalCount = chatResponse.promptEvalCount;
            metrics.evalCount = chatResponse.evalCount;
            metrics.totalTokens = chatResponse.TotalTokens;
            
            metrics.promptEvalDuration = chatResponse.PromptEvalSeconds;
            metrics.evalDuration = chatResponse.EvalSeconds;
            metrics.totalDuration = chatResponse.TotalSeconds;
            metrics.loadDuration = chatResponse.LoadSeconds;

            if (logResponses)
                Debug.Log($"[LLM] Response:\n{chatResponse.content}");

            var decision = ParseSingleDecision(chatResponse.content);
            
            metrics.success = decision.success;
            metrics.decisionsCount = 1;
            
            RecordMetrics(metrics);
            
            return decision;
        }
        catch (Exception e)
        {
            metrics.success = false;
            metrics.errorMessage = e.Message;
            metrics.responseTime = (DateTime.Now - startTime).TotalSeconds;
            
            RecordMetrics(metrics);
            
            if (logErrors)
                Debug.LogError($"[LLM] Error: {e.Message}");

            return JobDecision.Idle($"Error: {e.Message}");
        }
    }

    private string BuildSingleSystemPrompt(List<string> availableJobs)
    {
        string jobList = string.Join(", ", availableJobs);
        string jsonExample = @"{ ""job"": ""<JOB>"", ""targetX"": <X>, ""targetY"": <Y>, ""reason"": ""<why>"" }";

        return $@"You control a villager. Pick a job and location.
JOBS: {jobList}, IDLE
Response format: {jsonExample}
JSON only.";
    }

    private string BuildSingleContext(Villager targetVillager)
    {
        var sb = new System.Text.StringBuilder();
        var data = targetVillager.GetData();

        if (VillageState.Instance != null)
        {
            sb.AppendLine($"Inventory: Wood={VillageState.Instance.Wood}, Stone={VillageState.Instance.Stone}");
        }

        sb.AppendLine($"Villager: {data.name} at ({data.x},{data.y}), Status={data.jobStatus}");

        if (VillageState.Instance != null)
        {
            foreach (var v in VillageState.Instance.Villagers)
            {
                if (v == null || v == targetVillager) continue;
                var d = v.GetData();
                sb.AppendLine($"Other: {d.name} at ({d.x},{d.y}) doing {d.currentJob}");
            }
        }

        var resources = GetAllResourceLocations();
        if (resources.treeLocations.Count > 0)
            sb.AppendLine($"Trees: {FormatLocationsSimple(resources.treeLocations)}");
        if (resources.stoneLocations.Count > 0)
            sb.AppendLine($"Stone: {FormatLocationsSimple(resources.stoneLocations)}");

        return sb.ToString();
    }

    private JobDecision ParseSingleDecision(string response)
    {
        try
        {
            response = Regex.Replace(response, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase).Trim();

            var match = Regex.Match(response, @"\{[\s\S]*\}");
            if (!match.Success)
                return JobDecision.Idle("No JSON");

            var raw = JsonUtility.FromJson<RawSingleAssignment>(match.Value);

            return new JobDecision
            {
                jobName = raw.job ?? "IDLE",
                reason = raw.reason ?? "",
                success = true,
                hasTargetArea = raw.targetX != 0 || raw.targetY != 0,
                targetX = raw.targetX,
                targetY = raw.targetY
            };
        }
        catch (Exception e)
        {
            return JobDecision.Idle($"Parse error: {e.Message}");
        }
    }

    #endregion

    #region Metrics Tracking

    private void RecordMetrics(LLMMetrics metrics)
    {
        if (!trackMetrics) return;

        _lastMetrics = metrics;
        _metricsHistory.Add(metrics);

        _sessionStats.totalRequests++;
        if (metrics.success)
            _sessionStats.successfulRequests++;
        else
            _sessionStats.failedRequests++;

        _sessionStats.totalPromptTokens += metrics.promptEvalCount;
        _sessionStats.totalResponseTokens += metrics.evalCount;
        _sessionStats.totalTokens += metrics.totalTokens;
        _sessionStats.totalResponseTime += metrics.responseTime;
        _sessionStats.totalDecisions += metrics.decisionsCount;

        if (metrics.responseTime > _sessionStats.maxResponseTime)
            _sessionStats.maxResponseTime = metrics.responseTime;

        if (metrics.responseTime < _sessionStats.minResponseTime || _sessionStats.minResponseTime == 0)
            _sessionStats.minResponseTime = metrics.responseTime;

        OnMetricsRecorded?.Invoke(metrics);

        if (logTokenUsage)
        {
            Debug.Log($"[LLM Metrics] Type={metrics.requestType}, " +
                     $"Tokens={metrics.totalTokens} (prompt={metrics.promptEvalCount}, response={metrics.evalCount}), " +
                     $"Time={metrics.responseTime:F2}s (eval={metrics.evalDuration:F2}s), Success={metrics.success}");
        }

        if (exportMetricsToFile && _metricsHistory.Count % 10 == 0)
        {
            ExportMetricsToFile();
        }
    }

    public void ExportMetricsToFile()
    {
        try
        {
            var export = new MetricsExport
            {
                sessionStats = _sessionStats,
                metricsHistory = _metricsHistory
            };

            string json = JsonUtility.ToJson(export, true);
            string path = System.IO.Path.Combine(Application.persistentDataPath, metricsFilePath);
            System.IO.File.WriteAllText(path, json);

            Debug.Log($"[LLM] Metrics exported to: {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LLM] Failed to export metrics: {e.Message}");
        }
    }

    public void ClearMetricsHistory()
    {
        _metricsHistory.Clear();
        _sessionStats = new LLMSessionStats();
        Debug.Log("[LLM] Metrics history cleared");
    }

    public string GetMetricsSummary()
    {
        if (_sessionStats.totalRequests == 0)
            return "No metrics recorded yet.";

        double avgResponseTime = _sessionStats.totalResponseTime / _sessionStats.totalRequests;
        double successRate = (_sessionStats.successfulRequests / (double)_sessionStats.totalRequests) * 100.0;
        int avgTokensPerRequest = _sessionStats.totalRequests > 0 ? _sessionStats.totalTokens / _sessionStats.totalRequests : 0;

        return $@"=== LLM Session Metrics ===
Total Requests: {_sessionStats.totalRequests}
Success Rate: {successRate:F1}%
Total Decisions Made: {_sessionStats.totalDecisions}

Token Usage (ACTUAL from API):
  Total: {_sessionStats.totalTokens:N0}
  Prompt: {_sessionStats.totalPromptTokens:N0}
  Response: {_sessionStats.totalResponseTokens:N0}
  Avg per Request: {avgTokensPerRequest:N0}

Response Times:
  Average: {avgResponseTime:F2}s
  Min: {_sessionStats.minResponseTime:F2}s
  Max: {_sessionStats.maxResponseTime:F2}s";
    }

    #endregion

    #region Resource Location Helpers

    private ResourceLocations GetAllResourceLocations()
    {
        var result = new ResourceLocations();

        var resourceNodes = UnityEngine.Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        foreach (var node in resourceNodes)
        {
            if (node == null || node.isReserved) continue;

            var pos = WorldToGrid(node.transform.position);

            switch (node.resourceType)
            {
                case ResourceNode.ResourceType.Tree:
                    result.treeLocations.Add(pos);
                    break;
                case ResourceNode.ResourceType.Stone:
                    result.stoneLocations.Add(pos);
                    break;
                case ResourceNode.ResourceType.Seed:
                    result.seedLocations.Add(pos);
                    break;
            }
        }

        var buildings = UnityEngine.Object.FindObjectsByType<Buildings.Building>(FindObjectsSortMode.None);
        foreach (var b in buildings)
        {
            if (b == null || b.IsReserved) continue;

            var pos = WorldToGrid(b.transform.position);

            if (!b.IsFinished())
                result.buildingLocations.Add(pos);
            else if (b.buildingData != null && b.buildingData.buildingType == Buildings.BuildingType.Farm)
                result.farmLocations.Add(pos);
        }

        return result;
    }

    private Vector2Int WorldToGrid(Vector3 worldPos, float cellSize = 2f)
    {
        int x = Mathf.FloorToInt(worldPos.x / cellSize);
        int z = Mathf.FloorToInt(worldPos.z / cellSize);
        return new Vector2Int(x, z);
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
    }

    public void SetBatchMode(bool enabled)
    {
        useBatchDecisions = enabled;
        if (enabled && !_isBatchProcessing)
        {
            StopAllCoroutines();
            StartCoroutine(BatchDecisionLoop());
        }
    }

    #endregion

#if UNITY_EDITOR
    [ContextMenu("Force Batch Decision Now")]
    private void EditorForceBatch()
    {
        if (Application.isPlaying)
            RequestImmediateBatchDecision();
    }

    [ContextMenu("Log Resource Locations")]
    private void LogResources()
    {
        var resources = GetAllResourceLocations();
        Debug.Log($"Trees: {resources.treeLocations.Count}, Stone: {resources.stoneLocations.Count}");
    }

    [ContextMenu("Show Metrics Summary")]
    private void EditorShowMetrics()
    {
        if (Application.isPlaying)
            Debug.Log(GetMetricsSummary());
    }

    [ContextMenu("Export Metrics to File")]
    private void EditorExportMetrics()
    {
        if (Application.isPlaying)
            ExportMetricsToFile();
    }

    [ContextMenu("Clear Metrics History")]
    private void EditorClearMetrics()
    {
        if (Application.isPlaying)
            ClearMetricsHistory();
    }
#endif
}

#region Data Classes

[Serializable]
public class RawBatchDecision
{
    public List<RawSingleAssignment> assignments;
}

[Serializable]
public class RawSingleAssignment
{
    public string villager;
    public string job;
    public int targetX;
    public int targetY;
    public string reason;
}

[Serializable]
public class JobDecision
{
    public string jobName;
    public string reason;
    public bool success;
    public bool hasTargetArea;
    public int targetX;
    public int targetY;

    public bool IsIdle => string.IsNullOrEmpty(jobName) ||
                          jobName.Equals("IDLE", StringComparison.OrdinalIgnoreCase);

    public Vector2Int TargetPosition => new Vector2Int(targetX, targetY);

    public static JobDecision Idle(string reason) => new JobDecision
    {
        jobName = "IDLE",
        reason = reason,
        success = false,
        hasTargetArea = false
    };
}

[Serializable]
public class LLMResponse
{
    public bool success;
    public string content;
    public string error;
    public string model;
}

[Serializable]
public class LLMMetrics
{
    public DateTime timestamp;
    public string modelUsed;
    public string requestType;
    public int villagerCount;
    public string villagerName;
    
    public int promptLength;
    public int responseLength;
    
    // ACTUAL token counts from Ollama API
    public int promptEvalCount;
    public int evalCount;
    public int totalTokens;
    
    // Timing data (in seconds)
    public double responseTime;
    public double promptEvalDuration;
    public double evalDuration;
    public double totalDuration;
    public double loadDuration;
    
    public bool success;
    public int decisionsCount;
    public string errorMessage;
}

[Serializable]
public class LLMSessionStats
{
    public int totalRequests;
    public int successfulRequests;
    public int failedRequests;
    
    public int totalPromptTokens;
    public int totalResponseTokens;
    public int totalTokens;
    
    public double totalResponseTime;
    public double minResponseTime;
    public double maxResponseTime;
    
    public int totalDecisions;
}

[Serializable]
public class MetricsExport
{
    public LLMSessionStats sessionStats;
    public List<LLMMetrics> metricsHistory;
}

public class ResourceLocations
{
    public List<Vector2Int> treeLocations = new List<Vector2Int>();
    public List<Vector2Int> stoneLocations = new List<Vector2Int>();
    public List<Vector2Int> seedLocations = new List<Vector2Int>();
    public List<Vector2Int> buildingLocations = new List<Vector2Int>();
    public List<Vector2Int> farmLocations = new List<Vector2Int>();
}

#endregion