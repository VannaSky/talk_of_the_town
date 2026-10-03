using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Benchmark;
using Environment.Resources;
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
    
    private const string LogCategory = "LLMController";
    [Header("Model Settings")]
    [SerializeField] private string defaultModel = "gpt-oss:120b-cloud";
    [SerializeField] private int keepAliveSeconds = 600;

    [Header("Prompt Settings")]
    [SerializeField] private int maxResourceLocationsToShow = 6;

    [Header("Batch Decision Settings")]
    [Tooltip("Fallback interval for batch decisions when no events fire (game seconds)")]
    [SerializeField] private float batchDecisionInterval = 60f;
    [SerializeField] private bool useBatchDecisions = true;
    [Tooltip("Game seconds to wait after an event before triggering a decision (collects multiple events into one call)")]
    [SerializeField] private float decisionDebounceDelay = 1f;

    [Header("Game Pause Settings")]
    [Tooltip("Freeze game time while waiting for the LLM to respond, so game state doesn't change based on stale data.")]
    [SerializeField] private bool pauseGameDuringLLM = true;

    [Header("Context Settings")]
    [Tooltip("Context window size (tokens). 0 = use model default. Check your model's actual limit.")]
    [SerializeField] private int contextSize = 0;

    [Header("Reasoning Control")]
    [Tooltip("Controls reasoning/thinking for models that support it (e.g. gpt-oss).")]
    [SerializeField] private ThinkMode thinkMode = ThinkMode.ModelDefault;

    [Header("Structured Output")]
    [Tooltip("Force JSON output via Ollama's format constraint. Prevents malformed JSON from weaker models.")]
    [SerializeField] private bool forceJsonFormat = false;

    [Header("Generation Limits")]
    [Tooltip("Max output tokens (num_predict). 0 = Ollama default. Set to 2048+ for reasoning models that use tokens for internal thinking.")]
    [SerializeField] private int maxOutputTokens = 0;

    [Header("Memory Settings")]
    [Tooltip("Number of past user/assistant message pairs to retain. 0 = stateless.")]
    [SerializeField] private int memoryPairs = 2;
    [SerializeField] private bool useConversationMemory = true;
    [Tooltip("Pinned system message sent on every call. Leave empty to omit.")]
    [SerializeField, TextArea(2, 6)] private string pinnedSystemMessage = "";

    [Header("Logging")]
    [Tooltip("Include full prompt and response text in log events. Disable to keep logs readable during debugging.")]
    [SerializeField] private bool logFullPrompts = true;

    [Header("Metrics Tracking")]
    [SerializeField] private bool trackMetrics = true;
    [SerializeField] private bool exportMetricsToFile = false;
    [SerializeField] private string metricsFilePath = "LLM_Metrics.json";

    public static LLMController Instance { get; private set; }

    public event Action<string> OnModelLoaded;
    public event Action<Dictionary<string, JobDecision>> OnBatchDecisionMade;
    public event Action<string> OnError;
    public event Action<LLMMetrics> OnMetricsRecorded;
    public event Action<Benchmark.BatchDecisionLog> OnBatchDecisionLogged;

    private List<string> _availableModels = new List<string>();
    public IReadOnlyList<string> AvailableModels => _availableModels;

    public bool IsReady { get; private set; }
    public string CurrentModel => defaultModel;
    public bool UseBatchDecisions => useBatchDecisions;
    public bool UseConversationMemory => useConversationMemory;
    public ThinkMode CurrentThinkMode { get => thinkMode; set => thinkMode = value; }
    public bool ForceJsonFormat { get => forceJsonFormat; set => forceJsonFormat = value; }
    public int MaxOutputTokens { get => maxOutputTokens; set => maxOutputTokens = value; }
    public int ContextSize { get => contextSize; set => contextSize = value; }
    /// <summary>Game seconds to collect further triggers before a batch call fires.</summary>
    public float DecisionDebounceDelay { get => decisionDebounceDelay; set => decisionDebounceDelay = value; }
    /// <summary>Game seconds without any decision before the fallback call fires.</summary>
    public float BatchDecisionInterval { get => batchDecisionInterval; set => batchDecisionInterval = value; }
    
    
    void LogError(string msg)   => GameLog.LogError(LogCategory, msg, this);
    void LogWarning(string msg) => GameLog.LogWarning(LogCategory, msg, this);
    void LogEvent(string msg)   => GameLog.LogEvent(LogCategory, msg, this);
    void LogInfo(string msg)    => GameLog.LogInfo(LogCategory, msg, this);
    void LogVerbose(string msg) => GameLog.LogVerbose(LogCategory, msg, this);


    // Batch decision state
    private bool _isBatchProcessing;
    private float _preLLMTimeScale;
    private bool _triggerPendingAfterBatch;
    private string _pendingTriggerReason;
    private string _currentTriggerReason = "startup";
    private Dictionary<string, JobDecision> _latestBatchDecisions = new ();
    private float _lastBatchDecisionTime;
    private float _lastBatchStartTime = float.NegativeInfinity;
    private Coroutine _pendingDecisionCoroutine;

    // State tracking for delta context
    private int _lastWood = -1, _lastStone = -1, _lastSeeds = -1, _lastFood = -1;
    private Dictionary<string, string> _lastAssignedJob = new();
    private int _decisionCount;
    private const int FullSnapshotInterval = 10;

    // Recent events buffer — injected into every prompt for causal context
    private readonly struct RecentEvent
    {
        public readonly float Time;
        public readonly string Message;
        public RecentEvent(float time, string message) { Time = time; Message = message; }
    }
    private readonly Queue<RecentEvent> _recentEvents = new();
    private const int MaxRecentEvents = 8;

    public bool IsBatchProcessing => _isBatchProcessing;
    /// <summary>Game-time seconds since the last batch decision (freezes while paused).</summary>
    public float TimeSinceLastBatch => Time.time - _lastBatchDecisionTime;

    /// <summary>Number of batch requests in a row that failed (HTTP error, timeout, exception). Reset on success.</summary>
    public int ConsecutiveFailures { get; private set; }

    // Conversation memory
    private ollama.ConversationHistory _conversation;

    // Metrics tracking
    private List<LLMMetrics> _metricsHistory = new ();
    private LLMMetrics _lastMetrics;
    [SerializeField] private LLMSessionStats _sessionStats = new ();

    public LLMMetrics LastMetrics => _lastMetrics;
    public LLMSessionStats SessionStats => _sessionStats;
    public IReadOnlyList<LLMMetrics> MetricsHistory => _metricsHistory;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
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

            _conversation = new ollama.ConversationHistory(memoryPairs);
            if (!string.IsNullOrWhiteSpace(pinnedSystemMessage))
                _conversation.SetSystemMessage(pinnedSystemMessage);

            IsReady = true;
            _sessionStats.sessionStartRealtime = Time.realtimeSinceStartup;
            _sessionStats.sessionStartGameTime = Time.time;
            OnModelLoaded?.Invoke(defaultModel);

            LogEvent($"Controller ready with model: {defaultModel}");

            if (useBatchDecisions)
            {
                StartCoroutine(BatchDecisionLoop());
            }
        }
        catch (Exception e)
        {
            LogError($"Initialization failed: {e.Message}");
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
        var globalSettings = FindFirstObjectByType<GlobalSettings>();
        if (globalSettings == null || string.IsNullOrEmpty(globalSettings.LLMModel))
            return;

        if (_availableModels.Contains(globalSettings.LLMModel))
        {
            defaultModel = globalSettings.LLMModel;
            LogEvent($"Applied model from GlobalSettings: {defaultModel}");
        }
        else
        {
            LogWarning($"Model '{globalSettings.LLMModel}' not found locally. Attempting to pull...");

            bool success = await Ollama.Pull(globalSettings.LLMModel, (status, progress) =>
            {
                LogWarning($"Pull '{globalSettings.LLMModel}': {status} ({progress:F1}%)");
            });

            if (success)
            {
                await LoadAvailableModels();
                defaultModel = globalSettings.LLMModel;
                LogWarning($"Successfully pulled and applied model: {defaultModel}");
            }
            else
            {
                LogError($"Failed to pull model '{globalSettings.LLMModel}'. Using default: {defaultModel}");
            }
        }
    }

    private void Start()
    {
        if (VillageGoals.Instance != null)
        {
            VillageGoals.Instance.OnGoalCompleted += OnVillageGoalCompleted;
            VillageGoals.Instance.OnGoalAdded += OnVillageGoalAdded;
        }

        if (VillageState.Instance != null)
        {
            VillageState.Instance.OnVillagerRegistered += SubscribeToVillager;
            VillageState.Instance.OnVillagerUnregistered += UnsubscribeFromVillager;

            foreach (var villager in VillageState.Instance.Villagers)
                SubscribeToVillager(villager);
        }
    }

    private void OnDestroy()
    {
        if (VillageGoals.Instance != null)
        {
            VillageGoals.Instance.OnGoalCompleted -= OnVillageGoalCompleted;
            VillageGoals.Instance.OnGoalAdded -= OnVillageGoalAdded;
        }

        if (VillageState.Instance != null)
        {
            VillageState.Instance.OnVillagerRegistered -= SubscribeToVillager;
            VillageState.Instance.OnVillagerUnregistered -= UnsubscribeFromVillager;
        }
    }

    private void SubscribeToVillager(Villager villager)
    {
        var jh = villager.GetComponent<JobHandler>();
        if (jh != null) jh.OnBecameIdle += OnVillagerBecameIdle;
    }

    private void UnsubscribeFromVillager(Villager villager)
    {
        var jh = villager.GetComponent<JobHandler>();
        if (jh != null) jh.OnBecameIdle -= OnVillagerBecameIdle;
    }

    private void OnVillagerBecameIdle(JobHandler handler)
    {
        string status = handler.ActiveJobLogic?.GetCurrentStatus() ?? "no work";

        // Job logics also pass through their idle state while working normally (a Farmer re-checks its
        // growing fields every second). Only a stuck status is worth asking the LLM about.
        if (!IsStuckStatus(status))
        {
            LogInfo($"Ignoring idle event for {handler.gameObject.name} (not stuck: {status})");
            return;
        }

        // Same villager, same status, nothing changed in the village since its last trigger:
        // a new call would get the same input and most likely the same answer, which re-triggers
        // the same idle — a loop. Leave it to the next real change or the fallback interval.
        string snapshot = BuildWorldSnapshot();
        string key = handler.gameObject.name;
        if (_lastIdleTrigger.TryGetValue(key, out var last) && last.status == status && last.snapshot == snapshot)
        {
            LogInfo($"Skipping repeated idle trigger for {key} (unchanged: {status})");
            return;
        }
        _lastIdleTrigger[key] = (status, snapshot);

        TriggerDecision($"{key} idle: {status}");
    }

    private readonly Dictionary<string, (string status, string snapshot)> _lastIdleTrigger = new();

    /// <summary>Coarse village state used to detect whether anything changed between two idle triggers.</summary>
    private string BuildWorldSnapshot()
    {
        var vs = VillageState.Instance;
        if (vs == null) return "";
        string goals = VillageGoals.Instance != null
            ? string.Join(",", VillageGoals.Instance.ActiveGoals.Select(g => g.description))
            : "";
        return $"{vs.Wood}:{vs.Stone}:{vs.Seeds}:{vs.Food}:{vs.InventoryCapacity}:{vs.Villagers.Count}:{CountFinishedBuildings()}:{goals}";
    }

    /// <summary>
    /// Village state that can change a decision, compared between batches. Raw resource amounts are
    /// left out on purpose: working gatherers change them all the time, so every fallback would fire.
    /// Affordability is tracked separately in <see cref="GetNewlyAffordable"/>.
    /// </summary>
    private string BuildDecisionSnapshot()
    {
        var vs = VillageState.Instance;
        if (vs == null) return "";
        string goals = VillageGoals.Instance != null
            ? string.Join(",", VillageGoals.Instance.ActiveGoals.Select(g => g.description))
            : "";
        return $"{vs.InventoryCapacity}:{vs.Villagers.Count}:{CountFinishedBuildings()}:{goals}";
    }

    private HashSet<Buildings.BuildingType> GetAffordableTypes() =>
        new(GetAffordability().Where(a => a.missing.Count == 0).Select(a => a.type));

    /// <summary>
    /// Building types that are affordable now but were not at the last batch. Only this direction opens
    /// a new option; a building dropping to unaffordable is usually the villagers' own spending.
    /// </summary>
    private List<Buildings.BuildingType> GetNewlyAffordable() =>
        GetAffordableTypes()
            // A Stockpile (10 wood) is affordable almost all the time; waking the LLM for it only invited
            // building spare Stockpiles. A full store shows up as a stuck "Storage full" gatherer anyway.
            .Where(t => t != Buildings.BuildingType.Stockpile && !_lastBatchAffordable.Contains(t))
            .ToList();

    private static int CountAvailableTrees()
    {
        int count = 0;
        foreach (var n in UnityEngine.Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None))
            if (n != null && n.resourceType == ResourceNode.ResourceType.Tree && n.IsMature && n.resourceAmount > 0)
                count++;
        return count;
    }

    /// <summary>Missing resources per building type for a new foundation (empty list = affordable now).</summary>
    private List<(Buildings.BuildingType type, List<string> missing)> GetAffordability()
    {
        var result = new List<(Buildings.BuildingType, List<string>)>();
        var vs = VillageState.Instance;
        if (vs == null) return result;
        foreach (var data in Resources.LoadAll<BuildingData>(""))
        {
            if (data.levels == null || data.levels.Count == 0) continue;
            var level = data.levels[0];
            var missing = new List<string>();
            if (vs.Wood < level.woodCost) missing.Add($"{level.woodCost - vs.Wood} wood");
            if (vs.Stone < level.stoneCost) missing.Add($"{level.stoneCost - vs.Stone} stone");
            if (vs.Food < level.foodCost) missing.Add($"{level.foodCost - vs.Food} food");
            result.Add((data.buildingType, missing));
        }
        return result;
    }

    /// <summary>Job statuses that mean the villager has nothing useful to do and needs a new assignment.</summary>
    private static bool IsStuckStatus(string status) =>
        status == "Idle"
        || status.StartsWith("Need ") // builder blocked by missing resources
        || status.Contains("Waiting")
        || status.Contains("No ")
        || status.Contains("not found")
        // "Looking for work." is only the initial status of a freshly assigned job, not a stuck one
        || status.Contains("already completed")
        || status.Contains("Storage full") // gatherer cannot deposit — needs a Stockpile or another job
        || status.Contains("Failed");

    private static bool IsBusyBuilder(VillagerData d) => d.currentJob == "Builder" && d.jobStatus.StartsWith("Building ");

    // Snapshot of the village when the last batch was built, to tell whether a fallback would see anything new
    private string _lastBatchSnapshot = "";
    private HashSet<Buildings.BuildingType> _lastBatchAffordable = new();
    private int _lastBatchAvailableTrees = -1;

    // Villager states the last batch prompt showed, so a villager stuck in the same state is not re-sent
    // to the LLM again and again (e.g. a Lumberjack re-assigned while every tree is regrowing)
    private readonly Dictionary<string, string> _lastSeenStatus = new();

    /// <summary>Raised when an event trigger is dropped without a call, so brains waiting on it can re-arm.</summary>
    public event Action OnTriggerDropped;

    private static string SeenKey(VillagerData d) => d.restTarget > 0 ? "[resting]" : d.jobStatus;

    /// <summary>
    /// Called when a decision actually switched the villager to another job. If it later falls back into
    /// the state the LLM saw (e.g. Idle → Builder → exhausted → Idle), that is a new situation, not a repeat.
    /// </summary>
    public void ForgetSeenStatus(string villagerName) => _lastSeenStatus.Remove(villagerName);

    private void RememberSeenStatuses()
    {
        _lastSeenStatus.Clear();
        foreach (var v in VillageState.Instance.Villagers)
        {
            if (v == null) continue;
            var d = v.GetData();
            _lastSeenStatus[d.name] = SeenKey(d);
        }
    }

    // A fallback is skipped while nothing changed, but at most this many intervals in a row while
    // villagers are working — a safety net in case a job hangs in a state that is not detected as stuck.
    private const float FallbackMaxSkipIntervals = 4f;

    /// <summary>True if at least one villager could act on a decision (not resting, not busy building).</summary>
    private bool AnyAssignableVillager()
    {
        foreach (var v in VillageState.Instance.Villagers)
        {
            if (v == null) continue;
            var d = v.GetData();
            if (d.restTarget == 0 && !IsBusyBuilder(d)) return true;
        }
        return false;
    }

    /// <summary>
    /// Decides whether a batch would see anything new. Used for the fallback interval and for triggers
    /// queued while a batch was running (the batch already covered every villager). A call is only
    /// useful if a villager can act on it and either needs a job or the village changed since the last
    /// batch. Resting villagers and busy builders end their state on their own and trigger a decision then.
    /// </summary>
    private bool IsBatchUseful(bool allowHeartbeat, out string reason)
    {
        if (!AnyAssignableVillager()) { reason = "all villagers resting or building"; return false; }

        foreach (var v in VillageState.Instance.Villagers)
        {
            if (v == null) continue;
            var d = v.GetData();
            if (d.restTarget > 0 || IsBusyBuilder(d)) continue;
            if (!IsStuckStatus(d.jobStatus)) continue;
            // The LLM already decided on this villager in exactly this state — asking again gets the same answer
            if (_lastSeenStatus.TryGetValue(d.name, out var seen) && seen == SeenKey(d)) continue;
            reason = $"{d.name} needs assignment ({d.jobStatus})";
            return true;
        }

        var newlyAffordable = GetNewlyAffordable();
        if (newlyAffordable.Count > 0) { reason = $"now affordable: {string.Join(", ", newlyAffordable)}"; return true; }

        // Trees regrew after the last batch saw none: villagers that were told to wait can work again
        if (_lastBatchAvailableTrees == 0 && CountAvailableTrees() > 0) { reason = "trees available again"; return true; }

        if (BuildDecisionSnapshot() != _lastBatchSnapshot) { reason = "buildings, capacity or goals changed"; return true; }

        if (allowHeartbeat && TimeSinceLastBatch >= batchDecisionInterval * FallbackMaxSkipIntervals)
        {
            reason = $"no call for {TimeSinceLastBatch:F0}s (safety heartbeat)";
            return true;
        }

        reason = "all villagers working, nothing changed";
        return false;
    }

    /// <summary>Same check the fallback and event triggers use, for callers outside the controller (benchmark idle detection).</summary>
    public bool WouldBatchBeUseful(out string reason) => IsBatchUseful(false, out reason);

    private void OnVillageGoalCompleted(VillageGoal goal)
    {
        TriggerDecision($"Goal completed: {goal.description}");
    }

    private void OnVillageGoalAdded(VillageGoal goal)
    {
        if (goal.priority >= GoalPriority.High)
            TriggerDecision($"Urgent goal added: {goal.description}");
    }

    public void AddRecentEvent(string message)
    {
        while (_recentEvents.Count >= MaxRecentEvents)
            _recentEvents.Dequeue();
        _recentEvents.Enqueue(new RecentEvent(Time.realtimeSinceStartup, message));
    }

    public void TriggerDecision(string reason)
    {
        if (!IsReady) { LogWarning($"TriggerDecision skipped (not ready): {reason}"); return; }
        AddRecentEvent(reason);
        if (_isBatchProcessing)
        {
            _triggerPendingAfterBatch = true;
            _pendingTriggerReason = reason;
            return;
        }
        // If a debounce is already scheduled, don't reset it — the batch will fire soon.
        // Resetting on every incoming trigger causes a storm where it never fires.
        if (_pendingDecisionCoroutine != null) return;
        LogEvent($"TriggerDecision: scheduling batch in {decisionDebounceDelay}s ({reason})");
        _pendingDecisionCoroutine = StartCoroutine(DebouncedDecision(reason));
    }

    private IEnumerator DebouncedDecision(string reason)
    {
        float scheduledAt = Time.time;
        // Game time, so the debounce costs the same number of sim-ticks at any game speed
        yield return new WaitForSeconds(decisionDebounceDelay);
        _pendingDecisionCoroutine = null;
        if (_isBatchProcessing)
        {
            // A batch started while we were waiting — queue for after it finishes.
            _triggerPendingAfterBatch = true;
            _pendingTriggerReason = reason;
            yield break;
        }
        // Drop the trigger if the call would show the LLM nothing new: nobody can act (resting / building
        // end on their own), a batch during the debounce already covered it, or the villager is stuck in
        // the same state the LLM already decided on
        if (!IsBatchUseful(false, out string usefulReason))
        {
            _sessionStats.skippedTriggers++;
            string covered = _lastBatchStartTime >= scheduledAt ? ", a batch ran during the debounce" : "";
            LogInfo($"Event trigger dropped ({usefulReason}{covered}): {reason}");
            OnTriggerDropped?.Invoke();
            yield break;
        }
        LogEvent($"Event-triggered decision: {reason}");
        _currentTriggerReason = reason;
        yield return RequestBatchDecisions();
    }

    #region Batch Decision Loop

    private IEnumerator BatchDecisionLoop()
    {
        yield return new WaitForSecondsRealtime(2f);

        // Fire once immediately at startup so the game doesn't wait the full interval —
        // unless a batch already ran (the benchmark runner requests one as soon as all villagers are idle).
        if (IsReady && VillageState.Instance != null && VillageState.Instance.Villagers.Count > 0
            && float.IsNegativeInfinity(_lastBatchStartTime))
        {
            LogEvent("Startup batch decision.");
            _currentTriggerReason = "startup";
            yield return RequestBatchDecisions();
        }

        while (true)
        {
            // Game time, so the fallback fires after the same number of sim-ticks at any game speed
            yield return new WaitForSeconds(batchDecisionInterval);

            if (!IsReady || VillageState.Instance == null || VillageState.Instance.Villagers.Count == 0)
                continue;

            // Skip if a decision happened recently (event-triggered or benchmark-triggered)
            if (TimeSinceLastBatch < batchDecisionInterval * 0.9f)
                continue;

            if (!IsBatchUseful(true, out string fallbackReason))
            {
                _sessionStats.skippedFallbacks++;
                LogInfo($"Fallback interval skipped: {fallbackReason}");
                continue;
            }

            LogEvent($"Fallback interval triggered batch decision ({fallbackReason}).");
            _currentTriggerReason = $"fallback_interval: {fallbackReason}";
            yield return RequestBatchDecisions();
        }
    }

    /// <summary>Starts a batch decision right away. Returns false if one is already running or the controller is not ready.</summary>
    public bool RequestImmediateBatchDecision(string reason = "immediate_request")
    {
        if (_isBatchProcessing || !IsReady) return false;
        _currentTriggerReason = reason;
        StartCoroutine(RequestBatchDecisions());
        return true;
    }

    private IEnumerator RequestBatchDecisions()
    {
        if (_isBatchProcessing) yield break;

        _isBatchProcessing = true;
        _lastBatchStartTime = Time.time;
        RememberSeenStatuses();
        if (pauseGameDuringLLM)
        {
            _preLLMTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        var villagers = VillageState.Instance.Villagers;
        var jobNames = GetAvailableJobNames();

        if (villagers.Count == 0 || jobNames.Count == 0)
        {
            if (pauseGameDuringLLM) RestoreGameSpeed();
            _isBatchProcessing = false;
            yield break;
        }

        var task = RequestBatchJobDecisions(villagers, jobNames);

        while (!task.IsCompleted)
            yield return null;

        _latestBatchDecisions = task.Result;
        // Taken after the batch so the goals it just set do not count as a change next time
        _lastBatchSnapshot = BuildDecisionSnapshot();
        _lastBatchAffordable = GetAffordableTypes();
        _lastBatchAvailableTrees = CountAvailableTrees();
        _lastBatchDecisionTime = Time.time;
        if (pauseGameDuringLLM) RestoreGameSpeed();
        _isBatchProcessing = false;

        OnBatchDecisionMade?.Invoke(_latestBatchDecisions);

        // Re-fire any trigger that was queued while the batch was running.
        // The batch just answered for every villager, so the trigger is only worth a call if
        // someone still needs a job or the village changed since the batch was built.
        if (_triggerPendingAfterBatch)
        {
            _triggerPendingAfterBatch = false;
            if (IsBatchUseful(false, out string usefulReason))
                TriggerDecision($"{_pendingTriggerReason} ({usefulReason})");
            else
            {
                _sessionStats.skippedTriggers++;
                LogInfo($"Queued trigger dropped, batch already covered it ({usefulReason}): {_pendingTriggerReason}");
                OnTriggerDropped?.Invoke();
            }
        }

    }

    private void RestoreGameSpeed()
    {
        // If the game was already paused (manual pause) before the LLM ran, keep it paused.
        // Otherwise restore via VillageState so the slider and label stay in sync.
        if (_preLLMTimeScale == 0f)
        {
            Time.timeScale = 0f;
        }
        else if (VillageState.Instance != null)
        {
            VillageState.Instance.SetGameSpeed(VillageState.Instance.GameSpeed);
        }
        else
        {
            Time.timeScale = _preLLMTimeScale;
        }
    }

    public List<string> GetAvailableJobNames()
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
        var energyRates = GetEnergyRates();
        var buildingCosts = GetBuildingCostsString();
        var style = GlobalSettings.Instance != null ? GlobalSettings.Instance.PromptStyle : PromptStyle.Normal;
        string prompt = style switch
        {
            PromptStyle.Caveman    => LLMPromptCaveman.BuildBatchSystemPrompt(availableJobs, villagerCount, energyRates, buildingCosts),
            PromptStyle.Lean       => LLMPromptLean.BuildBatchSystemPrompt(availableJobs, villagerCount, energyRates, buildingCosts),
            PromptStyle.CavemanOld => LLMPromptCavemanOld.BuildBatchSystemPrompt(availableJobs, villagerCount, energyRates),
            PromptStyle.NormalOld  => LLMPromptNormalOld.BuildBatchSystemPrompt(availableJobs, villagerCount, energyRates, buildingCosts),
            _                      => LLMPromptNormal.BuildBatchSystemPrompt(availableJobs, villagerCount, energyRates, buildingCosts),
        };

        // Inject reasoning level directive for models that read it from the system prompt (e.g. gpt-oss)
        // Only for actual levels — Off/On are plain think:false/true and need no prompt text
        if (thinkMode is ThinkMode.Low or ThinkMode.Medium or ThinkMode.High)
        {
            prompt = $"Reasoning: {thinkMode.ToString().ToLower()}\n{prompt}";
        }

        return prompt;
    }

    public string GetBuildingCostsString()
    {
        var jobTypes = Resources.LoadAll<JobType>("Villagers/Jobs");
        foreach (var jt in jobTypes)
        {
            if (jt?.JobLogic is BuilderLogic builder && builder.buildableTypes.Count > 0)
            {
                var parts = new List<string>();
                foreach (var bd in builder.buildableTypes)
                {
                    if (bd == null || bd.levels.Count == 0) continue;
                    var level = bd.levels[0];
                    string foodPart = level.foodCost > 0 ? $" + {level.foodCost} food" : "";
                    parts.Add($"{bd.buildingType} (costs {level.woodCost} wood + {level.stoneCost} stone{foodPart})");
                }
                return string.Join(", ", parts);
            }
        }
        return "";
    }

    private (float drain, float walkDrain, float recovery) GetEnergyRates()
    {
        var villagers = VillageState.Instance?.Villagers;
        if (villagers != null && villagers.Count > 0 && villagers[0] != null)
        {
            var v = villagers[0];
            return (v.EnergyDrainRate, v.EnergyWalkDrainRate, v.EnergyRecoveryRate);
        }
        return (1f, 0.3f, 2f);
    }

    private string BuildBatchContext(IReadOnlyList<Villager> villagers, List<string> availableJobs)
    {
        var sb = new System.Text.StringBuilder();

        if (GlobalGoals.Instance != null && GlobalGoals.Instance.HasGoals)
        {
            sb.AppendLine("=== RESEARCHER GOALS (FINAL OBJECTIVES) ===");
            sb.AppendLine(GlobalGoals.Instance.GetGoalsForPrompt());
            sb.AppendLine();
        }

        if (VillageGoals.Instance != null)
        {
            sb.AppendLine("=== VILLAGE GOALS ===");
            sb.AppendLine(VillageGoals.Instance.GetGoalsForPrompt());
            sb.AppendLine();
        }

        if (VillageState.Instance != null)
        {
            int wood = VillageState.Instance.Wood;
            int stone = VillageState.Instance.Stone;
            int seeds = VillageState.Instance.Seeds;
            int food = VillageState.Instance.Food;

            int cap = VillageState.Instance.InventoryCapacity;
            sb.AppendLine("=== VILLAGE INVENTORY ===");
            sb.AppendLine($"Capacity: {cap} (build Stockpile to increase)");
            sb.AppendLine($"Wood: {wood}/{cap}{GatherTag("Wood", wood, cap, 50, "Lumberjack", verbose: true)}");
            sb.AppendLine($"Stone: {stone}/{cap}{GatherTag("Stone", stone, cap, 40, "Miner", verbose: true)}");
            bool seedsFull = seeds >= cap;
            bool foodFull  = food >= cap;
            sb.AppendLine($"Seeds: {seeds}/{cap}{(seedsFull ? " [FULL - no more SeedGatherers]" : seeds >= 10 ? SufficientSeedsTag(seeds) : " [LOW - need SeedGatherer]")}");
            bool foodNearFull = food >= cap * 0.8f;
            sb.AppendLine($"Food: {food}/{cap}{FoodGoalTag(food)}{(foodFull ? " [FULL - harvest is wasted until Stockpile is built or food is consumed]" : foodNearFull ? " [NEARLY FULL - do NOT build more Farms, avoid excess Farmers]" : IsFoodSurplus(food) ? " [SURPLUS - no more Farmers needed]" : food < 10 ? " [LOW - farming urgently needed!]" : "")}");
            if (foodFull && seedsFull)
                sb.AppendLine("⚠ FARMING BLOCKED: both Food and Seeds are at capacity — do NOT assign Farmers or SeedGatherers. Build a Stockpile to increase capacity.");
            else if (foodFull)
                sb.AppendLine("⚠ Food storage full: farmers can still plant (seeds are consumed) but harvested food will overflow. Build a Stockpile soon.");
            sb.AppendLine();
        }

        AppendRecentEvents(sb);

        sb.AppendLine("=== VILLAGERS TO ASSIGN ===");
        foreach (var v in villagers)
        {
            if (v == null) continue;
            var d = v.GetData();

            // Builders actively constructing are not assignable — they will finish automatically
            if (d.currentJob == "Builder" && d.jobStatus.StartsWith("Building "))
            {
                sb.AppendLine($"- {d.name} [BUSY]: {d.jobStatus} — will finish automatically, do NOT reassign");
                continue;
            }

            if (d.restTarget > 0)
            {
                sb.AppendLine($"- {d.name} [RESTING]: Idle at ({d.x},{d.y}), resting until {d.restTarget}% energy (now {d.energy}%) — will request a new job automatically, do NOT reassign");
                continue;
            }

            bool isStuck = IsStuckStatus(d.jobStatus);
            string tag = isStuck ? "[NEEDS ASSIGNMENT]" : "[KEEP]";
            // Below 10% a villager hits the 5% hard stop within seconds, so any job is wasted — flag it as exhausted
            string energyTag = d.energy < 10 ? $" [EXHAUSTED — {d.energy}%, stops at 5%, must rest!]" : d.energy < 30 ? $" [TIRED — working at {d.energy}% speed]" : "";

            // Add explicit error feedback when the last assignment failed
            string errorTag = "";
            if (d.jobStatus.Contains("already completed"))
                errorTag = " !! PREVIOUS ASSIGNMENT FAILED: building at that location is already finished. Assign a DIFFERENT location or job !!";
            else if (d.jobStatus.Contains("Waiting for resources") || (d.currentJob == "Builder" && d.jobStatus.StartsWith("Need ")))
                errorTag = " !! PREVIOUS ASSIGNMENT FAILED: not enough resources to build. Assign a gathering job until the cost is covered !!";
            else if (d.jobStatus.Contains("No farm"))
                errorTag = " !! PREVIOUS ASSIGNMENT FAILED: no completed Farm exists. Build a Farm first !!";
            else if (d.jobStatus.Contains("field cap") || d.jobStatus.Contains("Field limit"))
                errorTag = " !! PREVIOUS ASSIGNMENT FAILED: field capacity reached. Build another Farm or assign a different job !!";
            else if (d.jobStatus.Contains("Failed to place"))
                errorTag = " !! LAST BUILD FAILED: tile occupied or invalid. Pick a DIFFERENT FREE BUILD SITE !!";
            else if (d.jobStatus.Contains("No building tasks"))
                errorTag = " !! BUILDER HAS NOTHING TO DO: no valid build site found. Reassign to a different job !!";
            else if (d.jobStatus.StartsWith("No ") && d.jobStatus.Contains(" found"))
                errorTag = " !! NOTHING LEFT TO GATHER: every node of this resource is used up or regrowing. Assign a different USEFUL job, or IDLE to wait until it regrows (see TREES line) — do not build things nobody needs !!";
            else if (d.jobStatus.Contains("no buildingType"))
                errorTag = " !! BUILDER GOT NO buildingType: a NEW building needs buildingType (Farm/House/Stockpile) !!";
            else if (d.jobStatus.Contains("Storage full"))
                errorTag = " !! STORAGE FULL: this resource is at capacity. Build a Stockpile or assign a DIFFERENT job !!";

            sb.AppendLine($"- {d.name} {tag}: at ({d.x},{d.y}), Job={d.currentJob}, Status=\"{d.jobStatus}\", Energy={d.energy}%{energyTag}{errorTag}");
        }
        sb.AppendLine();

        // Build occupied-position map from villagers who are actively working ([KEEP])
        var takenPositions = new Dictionary<Vector2Int, string>();
        foreach (var v in villagers)
        {
            if (v == null) continue;
            var d = v.GetData();
            if (!IsStuckStatus(d.jobStatus))
                takenPositions[new Vector2Int(d.x, d.y)] = d.name;
        }

        var resourceLocations = GetAllResourceLocations();
        AppendBuildingSummary(sb, resourceLocations);

        sb.AppendLine("=== AVAILABLE RESOURCES (assign villagers to DIFFERENT locations!) ===");

        if (resourceLocations.treeLocations.Count > 0)
        {
            sb.Append("TREES: ");
            sb.Append(FormatLocationsWithTaken(SortByNearestVillager(resourceLocations.treeLocations, villagers), takenPositions));
        }
        else
            sb.Append("TREES: none available");
        sb.AppendLine(RegrowingTreesNote());

        if (resourceLocations.stoneLocations.Count > 0)
        {
            sb.Append("STONE: ");
            sb.AppendLine(FormatLocationsWithTaken(SortByNearestVillager(resourceLocations.stoneLocations, villagers), takenPositions));
        }

        if (resourceLocations.mineLocations.Count > 0)
        {
            sb.Append("MINE SHAFT (infinite stone, VERY slow — prefer regular STONE, but assign 1 permanent miner here at 10+ villagers): ");
            sb.AppendLine(FormatLocationsWithTaken(SortByNearestVillager(resourceLocations.mineLocations, villagers), takenPositions));
        }

        if (resourceLocations.seedLocations.Count > 0)
        {
            sb.Append("SEEDS: ");
            sb.AppendLine(FormatLocationsWithTaken(SortByNearestVillager(resourceLocations.seedLocations, villagers), takenPositions));
        }

        if (resourceLocations.buildingLocations.Count > 0)
        {
            sb.Append("UNFINISHED BUILDINGS: ");
            sb.AppendLine(FormatLocationsWithTaken(SortByNearestVillager(resourceLocations.buildingLocations, villagers), takenPositions));
        }

        if (resourceLocations.farmLocations.Count > 0)
        {
            sb.Append("FARM BUILDINGS (these are building tiles — farmers plant on empty grass tiles NEAR them, not on the building itself): ");
            sb.AppendLine(FormatLocationsWithTaken(SortByNearestVillager(resourceLocations.farmLocations, villagers), takenPositions));
        }

        if (resourceLocations.cropLocations.Count > 0)
        {
            sb.Append("MATURE CROPS: ");
            sb.AppendLine(FormatLocationsWithTaken(SortByNearestVillager(resourceLocations.cropLocations, villagers), takenPositions));
        }

        if (VillageState.Instance != null)
        {
            var core = VillageState.Instance.GetVillageCore();
            sb.AppendLine($"Village core (center of existing buildings): ({core.x},{core.y})");
            sb.AppendLine("  → When assigning a Builder, set targetX/targetY close to the village core.");
            sb.AppendLine();
        }

        if (VillageState.Instance != null)
        {
            sb.AppendLine();
            sb.AppendLine("=== VILLAGE POPULATION ===");
            int pop = VillageState.Instance.Villagers.Count;
            int popCap = VillageState.Instance.PopulationCap;
            int freeSlots = VillageState.Instance.GetAvailableHouseSlots();
            int completedHouses = VillageState.Instance.CompletedHouseCount;
            sb.AppendLine($"Population: {pop}/{popCap}");
            sb.AppendLine($"Completed houses: {completedHouses} | Free slots: {freeSlots}");
            sb.AppendLine("Villagers spawn automatically when a house finishes.");
            if (freeSlots >= 2)
                sb.AppendLine($"[{freeSlots} free slots already — consider building Stockpile or Farm instead of more Houses]");
            else if (freeSlots == 0)
                sb.AppendLine("No free slots — build a House to grow population.");
        }

        return sb.ToString();
    }

    private List<Vector2Int> SortByNearestVillager(List<Vector2Int> locations, IReadOnlyList<Villager> villagers)
    {
        if (villagers == null || villagers.Count == 0) return locations.Distinct().ToList();

        return locations
            .Distinct()
            .OrderBy(loc => villagers
                .Where(v => v != null)
                .Min(v => Mathf.Abs(v.GridPosition.x - loc.x) + Mathf.Abs(v.GridPosition.y - loc.y)))
            .ToList();
    }

    private string FormatLocationsWithTaken(List<Vector2Int> locations, Dictionary<Vector2Int, string> taken)
    {
        var parts = new List<string>();
        int count = Mathf.Min(locations.Count, maxResourceLocationsToShow);

        for (int i = 0; i < count; i++)
        {
            var loc = locations[i];
            if (taken != null && taken.TryGetValue(loc, out string occupant))
                parts.Add($"({loc.x},{loc.y})[TAKEN by {occupant}]");
            else
                parts.Add($"({loc.x},{loc.y})");
        }

        return string.Join(", ", parts);
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

    private bool ShouldUseFullSnapshot()
    {
        if (_decisionCount == 0) return true;
        // Delta context only makes sense with conversation memory — without it the LLM
        // has no prior state to compare against, so always send the full snapshot.
        if (!useConversationMemory) return true;
        if (_decisionCount % FullSnapshotInterval == 0) return true;
        // Only crisis-trigger if food was previously healthy and just dropped — not early game zero
        if (VillageState.Instance != null && _lastFood >= 5 && VillageState.Instance.Food < 5) return true;
        return false;
    }

    private void AppendBuildingSummary(System.Text.StringBuilder sb, ResourceLocations resourceLocations)
    {
        var counts = resourceLocations.completedBuildingCounts;
        int freeSlots = VillageState.Instance?.GetAvailableHouseSlots() ?? 0;

        counts.TryGetValue(Buildings.BuildingType.House,     out int houses);
        counts.TryGetValue(Buildings.BuildingType.Stockpile, out int stockpiles);
        counts.TryGetValue(Buildings.BuildingType.Farm,      out int farms);

        sb.AppendLine("=== EXISTING BUILDINGS ===");
        bool canSpawn = VillageState.Instance?.CanSpawnVillager() ?? false;
        string spawnNote = freeSlots > 0
            ? (canSpawn
                ? " [spawn ready — villager will appear automatically]"
                : " [slot available but spawn BLOCKED: need 5 wood + 5 stone + 5 seeds + 10 food]")
            : "";
        string housePos = resourceLocations.houseLocations.Count > 0
            ? $" at {FormatLocationsSimple(resourceLocations.houseLocations)}" : "";
        sb.AppendLine($"Houses: {houses} completed ({freeSlots} free slot(s)){spawnNote}{housePos}");
        string stockpilePos = resourceLocations.stockpileLocations.Count > 0
            ? $" at {FormatLocationsSimple(resourceLocations.stockpileLocations)}" : "";
        sb.AppendLine($"Stockpiles: {stockpiles}{stockpilePos}");

        int fieldCap = VillageState.Instance?.FieldCapacity ?? 0;
        int currentCrops = CountAllCrops();
        int currentFood  = VillageState.Instance?.Food ?? 0;
        int invCap       = VillageState.Instance?.InventoryCapacity ?? 1;
        bool foodHigh    = currentFood >= invCap * 0.8f;
        string farmNote;
        if (farms == 0)
            farmNote = " [REQUIRED — farmers cannot plant any fields without a Farm building!]";
        else if (fieldCap > 0 && currentCrops >= fieldCap && foodHigh)
            farmNote = $" [field limit reached: {currentCrops}/{fieldCap} — but food is high, do NOT build more Farms]";
        else if (fieldCap > 0 && currentCrops >= fieldCap)
            farmNote = $" [field limit reached: {currentCrops}/{fieldCap} — build another Farm only if food production is needed]";
        else if (farms >= 3)
            farmNote = $" [SUFFICIENT — crops regrow within each farm's radius. Avoid building more unless field limit is hit.]";
        else
            farmNote = " [each Farm lets farmers plant fields within its radius — build near where you want fields]";
        sb.AppendLine($"Farms: {farms}{farmNote}");
        if (fieldCap > 0)
            sb.AppendLine($"Fields: {currentCrops}/{fieldCap} planted (capacity from Farm bonuses)");

        AppendUnderConstruction(sb);

        AppendBuildingCosts(sb);
        AppendFreeBuildLocations(sb);
        sb.AppendLine();
    }

    private void AppendFreeBuildLocations(System.Text.StringBuilder sb)
    {
        if (VillageState.Instance?.TileGrid == null) return;

        var core = VillageState.Instance.GetVillageCore();
        var candidates = VillageState.Instance.TileGrid.FindTilesInRadius(core, 5, tile =>
            tile.Archetype != null
            && tile.Archetype.Style == TileStyle.Grass
            && !tile.HasBuilding
            && !tile.HasResource);

        if (candidates.Count == 0) return;

        // Sort by distance to core, pick up to 6
        candidates.Sort((a, b) =>
        {
            float da = Vector2Int.Distance(a.GridPos, core);
            float db = Vector2Int.Distance(b.GridPos, core);
            return da.CompareTo(db);
        });

        int count = Mathf.Min(candidates.Count, 6);
        sb.Append("FREE BUILD SITES (use these for Builder targetX/targetY): ");
        for (int i = 0; i < count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"({candidates[i].GridPos.x},{candidates[i].GridPos.y})");
        }
        sb.AppendLine();
    }

    /// <summary>
    /// Lists every unfinished building with its progress and payment state. Costs are spent when the
    /// foundation is placed, so a stalled site only needs a Builder — without this the model reads the
    /// cost table and gathers the full cost again.
    /// </summary>
    private void AppendUnderConstruction(System.Text.StringBuilder sb)
    {
        var buildings = UnityEngine.Object.FindObjectsByType<Buildings.Building>(FindObjectsSortMode.None);
        var lines = new List<string>();
        foreach (var b in buildings)
        {
            if (b == null || b.buildingData == null || b.IsFinished()) continue;
            var tile = b.GetComponentInParent<Tiles.Tile>();
            string pos = tile != null ? $" ({tile.GridPos.x},{tile.GridPos.y})" : "";
            string state = b.IsReserved
                ? "a Builder is working on it"
                : b.resourcesPaidForCurrentLevel
                    ? "ALREADY PAID, NO resources needed — STALLED, assign 1 Builder with targetX/targetY = this coordinate to finish it"
                    : "STALLED, cost is spent when a Builder resumes — assign 1 Builder with targetX/targetY = this coordinate";
            lines.Add($"  {b.buildingData.buildingType}{pos} {b.GetProgressPercent()}% — {state}");
        }
        if (lines.Count == 0) return;

        sb.AppendLine($"Under construction: {lines.Count} building(s)");
        foreach (var line in lines) sb.AppendLine(line);
    }

    private void AppendBuildingCosts(System.Text.StringBuilder sb)
    {
        var allData = Resources.LoadAll<BuildingData>("");
        if (allData.Length == 0) return;

        sb.AppendLine("Building costs (resources required to start a NEW construction — not needed to finish one under construction):");
        foreach (var data in allData)
        {
            if (data.levels == null || data.levels.Count == 0) continue;
            var level = data.levels[0];
            string foodPart = level.foodCost > 0 ? $", {level.foodCost} food" : "";
            string bonusPart = "";
            if (level.bonuses != null)
            {
                foreach (var bonus in level.bonuses)
                {
                    if (bonus.type == BuildingBonusType.InventoryCapacity)
                        bonusPart += $" → +{bonus.value} inventory capacity";
                    else if (bonus.type == BuildingBonusType.FieldCapacity)
                        bonusPart += $" → +{bonus.value} field slots";
                }
            }
            sb.AppendLine($"  {data.buildingType}: {level.woodCost} wood, {level.stoneCost} stone{foodPart}{bonusPart}");
        }

        // Precomputed so the model does not have to compare inventory and costs itself
        var parts = GetAffordability().Select(a => a.missing.Count == 0
            ? $"{a.type} ✓"
            : $"{a.type} ✗ (need {string.Join(", ", a.missing)} more)").ToList();
        if (parts.Count > 0)
            sb.AppendLine($"AFFORDABLE NOW: {string.Join(" | ", parts)}");
    }

    private void AppendRecentEvents(System.Text.StringBuilder sb)
    {
        if (_recentEvents.Count == 0) return;

        float now = Time.realtimeSinceStartup;
        sb.AppendLine("=== RECENT EVENTS (what just happened — use this to guide your decisions) ===");
        foreach (var ev in _recentEvents)
        {
            int secsAgo = Mathf.RoundToInt(now - ev.Time);
            sb.AppendLine($"  [{secsAgo}s ago] {ev.Message}");
        }
        sb.AppendLine();
    }

    private string BuildDeltaContext(IReadOnlyList<Villager> villagers, List<string> availableJobs)
    {
        var sb = new System.Text.StringBuilder();

        if (GlobalGoals.Instance != null && GlobalGoals.Instance.HasGoals)
        {
            sb.AppendLine("=== RESEARCHER GOALS (FINAL OBJECTIVES) ===");
            sb.AppendLine(GlobalGoals.Instance.GetGoalsForPrompt());
            sb.AppendLine();
        }

        if (VillageGoals.Instance != null)
        {
            sb.AppendLine("=== VILLAGE GOALS ===");
            sb.AppendLine(VillageGoals.Instance.GetGoalsForPrompt());
            sb.AppendLine();
        }

        if (VillageState.Instance != null)
        {
            int wood = VillageState.Instance.Wood;
            int stone = VillageState.Instance.Stone;
            int seeds = VillageState.Instance.Seeds;
            int food = VillageState.Instance.Food;

            int cap = VillageState.Instance.InventoryCapacity;
            sb.AppendLine($"=== RESOURCE CHANGES (capacity: {cap}) ===");
            sb.AppendLine(FormatDelta("Wood", wood, _lastWood, GatherTag("Wood", wood, cap, 50, "Lumberjack", verbose: false)));
            sb.AppendLine(FormatDelta("Stone", stone, _lastStone, GatherTag("Stone", stone, cap, 40, "Miner", verbose: false)));
            sb.AppendLine(FormatDelta("Seeds", seeds, _lastSeeds, seeds >= cap ? " [FULL]" : seeds >= 10 ? " [SUFFICIENT]" : " [LOW]"));
            sb.AppendLine(FormatDelta("Food", food, _lastFood, FoodGoalTag(food) + (food >= cap ? " [FULL]" : food >= cap * 0.8f ? " [NEARLY FULL]" : IsFoodSurplus(food) ? " [SURPLUS]" : food < 10 ? " [LOW]" : "")));
            sb.AppendLine();
        }

        AppendRecentEvents(sb);

        sb.AppendLine("=== VILLAGERS ===");
        foreach (var v in villagers)
        {
            if (v == null) continue;
            var d = v.GetData();

            // Builders actively constructing are not assignable — they will finish automatically
            if (d.currentJob == "Builder" && d.jobStatus.StartsWith("Building "))
            {
                sb.AppendLine($"- {d.name} [BUSY]: {d.jobStatus} — will finish automatically, do NOT reassign");
                continue;
            }

            if (d.restTarget > 0)
            {
                sb.AppendLine($"- {d.name} [RESTING]: Idle at ({d.x},{d.y}), resting until {d.restTarget}% energy (now {d.energy}%) — will request a new job automatically, do NOT reassign");
                continue;
            }

            bool isStuck = IsStuckStatus(d.jobStatus);
            string tag = isStuck ? "[NEEDS ASSIGNMENT]" : "[KEEP]";
            string previousJob = _lastAssignedJob.TryGetValue(d.name, out var prev) && prev != d.currentJob
                ? $", was {prev}"
                : "";
            // Below 10% a villager hits the 5% hard stop within seconds, so any job is wasted — flag it as exhausted
            string energyTag = d.energy < 10 ? $" [EXHAUSTED — {d.energy}%, stops at 5%, must rest!]" : d.energy < 30 ? $" [TIRED — working at {d.energy}% speed, assign IDLE to recover]" : "";

            // Add explicit error feedback when the last assignment failed
            string errorTag = "";
            if (d.jobStatus.Contains("already completed"))
                errorTag = " !! PREVIOUS ASSIGNMENT FAILED: building at that location is already finished. Assign a DIFFERENT location or job !!";
            else if (d.jobStatus.Contains("Waiting for resources") || (d.currentJob == "Builder" && d.jobStatus.StartsWith("Need ")))
                errorTag = " !! PREVIOUS ASSIGNMENT FAILED: not enough resources to build. Assign a gathering job until the cost is covered !!";
            else if (d.jobStatus.Contains("No farm"))
                errorTag = " !! PREVIOUS ASSIGNMENT FAILED: no completed Farm exists. Build a Farm first !!";
            else if (d.jobStatus.Contains("field cap") || d.jobStatus.Contains("Field limit"))
                errorTag = " !! PREVIOUS ASSIGNMENT FAILED: field capacity reached. Build another Farm or assign a different job !!";
            else if (d.jobStatus.Contains("Failed to place"))
                errorTag = " !! LAST BUILD FAILED: tile occupied or invalid. Pick a DIFFERENT FREE BUILD SITE !!";
            else if (d.jobStatus.Contains("No building tasks"))
                errorTag = " !! BUILDER HAS NOTHING TO DO: no valid build site found. Reassign to a different job !!";
            else if (d.jobStatus.StartsWith("No ") && d.jobStatus.Contains(" found"))
                errorTag = " !! NOTHING LEFT TO GATHER: every node of this resource is used up or regrowing. Assign a different USEFUL job, or IDLE to wait until it regrows (see TREES line) — do not build things nobody needs !!";
            else if (d.jobStatus.Contains("no buildingType"))
                errorTag = " !! BUILDER GOT NO buildingType: a NEW building needs buildingType (Farm/House/Stockpile) !!";
            else if (d.jobStatus.Contains("Storage full"))
                errorTag = " !! STORAGE FULL: this resource is at capacity. Build a Stockpile or assign a DIFFERENT job !!";

            sb.AppendLine($"- {d.name} {tag}: {d.currentJob} at ({d.x},{d.y}){previousJob}, Status=\"{d.jobStatus}\", Energy={d.energy}%{energyTag}{errorTag}");
        }
        sb.AppendLine();

        if (VillageState.Instance != null)
        {
            var core = VillageState.Instance.GetVillageCore();
            sb.AppendLine($"Village core: ({core.x},{core.y}) — Builder targets should be near here.");
            sb.AppendLine();
        }

        var resourceLocations = GetAllResourceLocations();
        AppendBuildingSummary(sb, resourceLocations);

        sb.AppendLine("=== AVAILABLE RESOURCES ===");
        string trees = resourceLocations.treeLocations.Count > 0
            ? FormatLocationsSimple(SortByNearestVillager(resourceLocations.treeLocations, villagers))
            : "none available";
        sb.AppendLine($"TREES: {trees}{RegrowingTreesNote()}");
        if (resourceLocations.stoneLocations.Count > 0)
            sb.AppendLine($"STONE: {FormatLocationsSimple(SortByNearestVillager(resourceLocations.stoneLocations, villagers))}");
        if (resourceLocations.seedLocations.Count > 0)
            sb.AppendLine($"SEEDS: {FormatLocationsSimple(SortByNearestVillager(resourceLocations.seedLocations, villagers))}");
        if (resourceLocations.buildingLocations.Count > 0)
            sb.AppendLine($"UNFINISHED BUILDINGS: {FormatLocationsSimple(SortByNearestVillager(resourceLocations.buildingLocations, villagers))}");
        if (resourceLocations.farmLocations.Count > 0)
            sb.AppendLine($"FARMS: {FormatLocationsSimple(SortByNearestVillager(resourceLocations.farmLocations, villagers))}");
        if (resourceLocations.cropLocations.Count > 0)
            sb.AppendLine($"MATURE CROPS: {FormatLocationsSimple(SortByNearestVillager(resourceLocations.cropLocations, villagers))}");

        return sb.ToString();
    }

    /// <summary>
    /// " | 14 regrowing, next ready in ~45s" — lets the model weigh waiting for wood against other work
    /// instead of only seeing an empty TREES list. Seconds are game seconds, like the energy rates.
    /// </summary>
    private static string RegrowingTreesNote()
    {
        int count = 0;
        float next = float.MaxValue;
        foreach (var n in UnityEngine.Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None))
        {
            if (n == null || n.resourceType != ResourceNode.ResourceType.Tree || !n.canRegrow || n.IsMature || n.isMineShaft) continue;
            count++;
            // Seedling still has to pass the Growing stage, each stage takes growthTime
            float remaining = n.growthStage == ResourceNode.GrowthStage.Seedling
                ? 2f * n.growthTime - n.currentGrowthTimer
                : n.growthTime - n.currentGrowthTimer;
            next = Mathf.Min(next, remaining);
        }
        return count > 0 ? $" | {count} regrowing, next ready in ~{Mathf.CeilToInt(next)}s" : "";
    }

    /// <summary>Open researcher "Gather X" goal for this resource (enum name, e.g. "Wood"), or null.</summary>
    private static GlobalGoal OpenResourceGoal(string resource)
    {
        if (GlobalGoals.Instance == null) return null;
        foreach (var g in GlobalGoals.Instance.Goals)
            if (g.type == GlobalGoalType.ResourceAmount && !g.isCompleted && g.targetResource.ToString() == resource)
                return g;
        return null;
    }

    /// <summary>
    /// Inventory tag for a gathered resource. An open researcher goal for it wins over [SURPLUS]: the goal counts
    /// the stock on hand, so "no more gatherers needed" and spending it on spare buildings work against it.
    /// </summary>
    private static string GatherTag(string resource, int amount, int cap, int surplusAt, string gatherer, bool verbose)
    {
        if (amount >= cap) return verbose ? " [FULL - gatherers are BLOCKED, build Stockpile!]" : " [FULL - gatherers BLOCKED]";
        var goal = OpenResourceGoal(resource);
        if (goal != null)
            return $" [RESEARCHER GOAL {amount}/{goal.targetAmount} - keep {gatherer}s on it; spending it on buildings undoes progress]";
        if (amount >= cap * 0.8f) return " [NEARLY FULL - a Stockpile is useful now]";
        if (amount > surplusAt) return verbose ? $" [SURPLUS - no more {gatherer}s needed]" : " [SURPLUS]";
        if (amount < 10) return verbose ? $" [LOW - need {gatherer}]" : " [LOW]";
        return "";
    }

    private static string FoodGoalTag(int food)
    {
        var goal = OpenResourceGoal("Food");
        return goal != null ? $" [RESEARCHER GOAL {food}/{goal.targetAmount} - keep farming]" : "";
    }

    /// <summary>
    /// Same idea as the wood/stone [SURPLUS] tag. Without it, food had no tag between its goal and [FULL],
    /// so [KEEP] farmers stayed on the farm until storage was full (seen on the large map).
    /// </summary>
    private static bool IsFoodSurplus(int food) => food > FoodSurplusAt && OpenResourceGoal("Food") == null;

    private const int FoodSurplusAt = 40; // 4 Houses at 10 food each

    /// <summary>
    /// Farmer hint for the seed line. Capped by the free fields: once every field is planted, a second
    /// Farmer only waits for the same crops.
    /// </summary>
    private string SufficientSeedsTag(int seeds)
    {
        int fieldCap = VillageState.Instance?.FieldCapacity ?? 0;
        int freeFields = fieldCap - CountAllCrops();
        if (fieldCap > 0 && freeFields <= 0)
            return " [SUFFICIENT - all fields planted, 1 Farmer is enough]";
        int farmers = Mathf.Max(1, seeds / 20);
        if (fieldCap > 0) farmers = Mathf.Min(farmers, freeFields);
        return $" [SUFFICIENT - assign {farmers} Farmer(s) to use these seeds!]";
    }

    private string FormatDelta(string label, int current, int last, string suffix)
    {
        if (last < 0) return $"{label}: {current}{suffix}";
        int diff = current - last;
        string change = diff == 0 ? "no change" : diff > 0 ? $"+{diff}" : $"{diff}";
        return $"{label}: {current} (was {last}, {change}){suffix}";
    }

    private void UpdateStateSnapshot()
    {
        if (VillageState.Instance == null) return;
        _lastWood = VillageState.Instance.Wood;
        _lastStone = VillageState.Instance.Stone;
        _lastSeeds = VillageState.Instance.Seeds;
        _lastFood = VillageState.Instance.Food;
    }

    #endregion

    #region Batch Decision Making

    /// <summary>
    /// JSON schema for the batch answer, passed as Ollama's "format". Every field of an assignment is
    /// required (0 / "" mean "none"), names are restricted to the live villagers, jobs and building types,
    /// and there is exactly one assignment per villager. Matches RawBatchDecision.
    /// </summary>
    private static Dictionary<string, object> BuildBatchResponseSchema(IReadOnlyList<Villager> villagers, List<string> availableJobs)
    {
        var names = villagers.Where(v => v != null).Select(v => v.villagerName).ToList();
        // KEEP = leave the villager as it is (models echo the prompt tag; handled as no change)
        var jobs = availableJobs.Concat(new[] { "IDLE", "KEEP" }).Distinct().ToList();
        var buildings = Resources.LoadAll<BuildingData>("").Select(b => b.buildingType.ToString())
            .Distinct().Append("").ToList();

        var assignment = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["villager"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = names },
                ["job"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = jobs },
                ["buildingType"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = buildings },
                ["targetX"] = new Dictionary<string, object> { ["type"] = "integer", ["minimum"] = 0 },
                ["targetY"] = new Dictionary<string, object> { ["type"] = "integer", ["minimum"] = 0 },
                ["gatherAmount"] = new Dictionary<string, object> { ["type"] = "integer", ["minimum"] = 0 },
                ["restUntilEnergy"] = new Dictionary<string, object> { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 100 },
                // Hard cap: answers with paragraph-long reasons (12k chars for 4 villagers) took 30-45 s
                ["reason"] = new Dictionary<string, object> { ["type"] = "string", ["maxLength"] = 200 }
            },
            ["required"] = new[] { "villager", "job", "buildingType", "targetX", "targetY", "gatherAmount", "restUntilEnergy", "reason" }
        };

        var goal = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["type"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = new[] { "GatherResource", "ReachPopulation" } },
                ["resource"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = new[] { "Wood", "Stone", "Seed", "Food", "" } },
                ["amount"] = new Dictionary<string, object> { ["type"] = "integer" },
                ["priority"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = new[] { "Low", "Normal", "High", "Critical" } },
                ["description"] = new Dictionary<string, object> { ["type"] = "string" }
            },
            ["required"] = new[] { "type", "resource", "amount", "priority", "description" }
        };

        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["assignments"] = new Dictionary<string, object>
                {
                    ["type"] = "array",
                    ["items"] = assignment,
                    ["minItems"] = names.Count,
                    ["maxItems"] = names.Count
                },
                ["goals"] = new Dictionary<string, object> { ["type"] = "array", ["items"] = goal }
            },
            // goals stays optional: an omitted or empty list leaves the village goals unchanged
            ["required"] = new[] { "assignments" }
        };
    }

    public async Task<Dictionary<string, JobDecision>> RequestBatchJobDecisions(
        IReadOnlyList<Villager> villagers,
        List<string> availableJobs)
    {
        var results = new Dictionary<string, JobDecision>();
        string rawResponseText = null;

        if (!IsReady || villagers.Count == 0)
            return results;

        bool fullSnapshot = ShouldUseFullSnapshot();
        string systemPrompt = BuildBatchSystemPrompt(availableJobs, villagers.Count);
        LogEvent(logFullPrompts ? $"Batch System Prompt:\n{systemPrompt}" : $"Batch System Prompt called");
        string context = fullSnapshot
            ? BuildBatchContext(villagers, availableJobs)
            : BuildDeltaContext(villagers, availableJobs);

        string promptLabel = fullSnapshot ? "FULL SNAPSHOT" : "DELTA";

        if (useConversationMemory)
            _conversation.SetSystemMessage(systemPrompt);

        string fullPrompt = useConversationMemory
            ? $"{context}\nAssign jobs and locations to ALL villagers:"
            : $"{systemPrompt}\n\n{context}\nAssign jobs and locations to ALL villagers:";

        if (logFullPrompts) LogEvent($"Batch Prompt [{promptLabel}] for {villagers.Count} villagers:\n{fullPrompt} ");
        else LogEvent($"Batch Prompt [{promptLabel}] for {villagers.Count} villagers ({fullPrompt.Length} chars)");

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
        // Logged next to the end tick: equal values prove the game was paused for the whole call
        long requestStartTick = SimTickTracker.CurrentTick;

        try
        {
            // Use the extension method to get full metadata
            object thinkParam = thinkMode switch
            {
                ThinkMode.Off => false,
                ThinkMode.On => true,
                ThinkMode.Low => "low",
                ThinkMode.Medium => "medium",
                ThinkMode.High => "high",
                _ => null // ModelDefault — let the model decide
            };

            // A JSON schema instead of plain "json": Ollama then constrains decoding, so every assignment
            // has all fields and only valid villager / job / building names (same for every model)
            object formatParam = forceJsonFormat ? BuildBatchResponseSchema(villagers, availableJobs) : null;

            // Build runtime options (num_predict, etc.)
            Dictionary<string, object> runtimeOptions = null;
            if (maxOutputTokens > 0)
                runtimeOptions = new Dictionary<string, object> { { "num_predict", maxOutputTokens } };

            var chatResponse = useConversationMemory
                ? await OllamaExtensions.ChatWithMetadataExt(defaultModel, fullPrompt, _conversation, keepAliveSeconds, contextSize, null, thinkParam, formatParam, runtimeOptions)
                : await OllamaExtensions.ChatWithMetadataExt(defaultModel, fullPrompt, keepAliveSeconds, contextSize, null, thinkParam, formatParam, runtimeOptions);
            
            metrics.responseTime = (DateTime.Now - startTime).TotalSeconds;
            if (chatResponse.isError)
                throw new Exception(chatResponse.errorMessage);

            metrics.actualModel = chatResponse.model;
            metrics.thinking = chatResponse.thinking;
            metrics.doneReason = chatResponse.doneReason;
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
            
            rawResponseText = chatResponse.content;

            if (logFullPrompts) LogEvent($"Batch Response:\n{chatResponse.content}");
            else LogEvent($"Batch Response ({chatResponse.content.Length} chars)");

            results = ParseBatchDecisions(chatResponse.content, villagers);

            // Update state for next delta
            UpdateStateSnapshot();
            foreach (var kv in results)
                _lastAssignedJob[kv.Key] = kv.Value.jobName;
            if (_decisionCount == 0)
                MainMenu.GoalsMenu.Instance?.LockGoals();
            _decisionCount++;

            metrics.success = true;
            metrics.decisionsCount = results.Count;
            ConsecutiveFailures = 0;
        }
        catch (Exception e)
        {
            metrics.success = false;
            metrics.errorMessage = e.Message;
            metrics.responseTime = (DateTime.Now - startTime).TotalSeconds;
            
            ConsecutiveFailures++;
            LogError($"Batch Error ({ConsecutiveFailures} in a row): {e.Message}");

            OnError?.Invoke(e.Message);

            foreach (var v in villagers)
            {
                if (v != null)
                    results[v.villagerName] = JobDecision.Idle($"Error: {e.Message}");
            }
        }

        // Record metrics
        RecordMetrics(metrics);

        // Fire benchmark logging event with lightweight state snapshot
        if (OnBatchDecisionLogged != null)
        {
            var vs = VillageState.Instance;
            var inputState = new InputStateSnapshot
            {
                wood = vs != null ? vs.Wood : 0,
                stone = vs != null ? vs.Stone : 0,
                seeds = vs != null ? vs.Seeds : 0,
                food = vs != null ? vs.Food : 0,
                capacity = vs != null ? vs.InventoryCapacity : 0,
                villagerCount = villagers.Count,
                buildingCount = CountFinishedBuildings()
            };

            // Collect idle villager names
            foreach (var v in villagers)
            {
                if (v == null) continue;
                var jh = v.GetComponent<JobHandler>();
                if (jh == null || jh.currentJob == null || jh.ActiveJobLogic == null)
                    inputState.idleVillagers.Add(v.villagerName);
            }

            // Collect active global goal descriptions
            if (GlobalGoals.Instance != null)
            {
                foreach (var g in GlobalGoals.Instance.Goals)
                    inputState.activeGoals.Add(g.Description + (g.isCompleted ? " [DONE]" : ""));
            }

            // Collect building details
            var allBuildings = UnityEngine.Object.FindObjectsByType<Buildings.Building>(FindObjectsSortMode.None);
            foreach (var b in allBuildings)
            {
                if (b == null || b.buildingData == null) continue;
                var tile = b.GetComponentInParent<Tiles.Tile>();
                string pos = tile != null ? $"({tile.GridPos.x},{tile.GridPos.y})" : "";
                if (b.IsFinished())
                    inputState.completedBuildings.Add($"{b.buildingData.buildingType} {pos}");
                else
                    inputState.unfinishedBuildings.Add($"{b.buildingData.buildingType} {pos} ({b.GetProgressPercent()}%)");
            }

            // Collect free build sites for diagnostics
            if (vs?.TileGrid != null)
            {
                var core = vs.GetVillageCore();
                var freeTiles = vs.TileGrid.FindTilesInRadius(core, 5, t =>
                    t.Archetype != null
                    && t.Archetype.Style == Tiles.TileStyle.Grass
                    && !t.HasBuilding
                    && !t.HasResource);
                freeTiles.Sort((a, b2) =>
                {
                    float da = Vector2Int.Distance(a.GridPos, core);
                    float db = Vector2Int.Distance(b2.GridPos, core);
                    return da.CompareTo(db);
                });
                int siteCount = Mathf.Min(freeTiles.Count, 6);
                for (int i = 0; i < siteCount; i++)
                    inputState.freeBuildSites.Add($"({freeTiles[i].GridPos.x},{freeTiles[i].GridPos.y})");
            }

            OnBatchDecisionLogged.Invoke(new BatchDecisionLog
            {
                simTick = SimTickTracker.CurrentTick,
                requestStartTick = requestStartTick,
                triggerReason = _currentTriggerReason,
                contextType = fullSnapshot ? "full" : "delta",
                inputState = inputState,
                rawResponse = rawResponseText,
                systemPrompt = systemPrompt,
                userPrompt = context,
                parsedDecisions = results,
                metrics = metrics
            });
        }

        return results;
    }

    private Dictionary<string, JobDecision> ParseBatchDecisions(string response, IReadOnlyList<Villager> villagers)
    {
        var results = new Dictionary<string, JobDecision>();

        try
        {
            response = Regex.Replace(response, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase).Trim();
            // Strip markdown code fences (```json ... ```) that some models wrap around JSON
            response = Regex.Replace(response, @"```\w*\n?", "").Trim();

            var match = Regex.Match(response, @"\{[\s\S]*\}");
            if (!match.Success)
            {
                LogWarning("No JSON found in batch response");
                _sessionStats.parseFailures++;
                foreach (var v in villagers)
                    if (v != null) results[v.villagerName] = JobDecision.Idle("No JSON");
                return results;
            }

            // JsonUtility can't handle null — remove null-valued fields entirely so defaults apply.
            // Removing a null LAST member ("goals": null before "}") leaves the comma before it ("...],}"), and the
            // whole (valid) answer then failed to parse — so drop trailing commas afterwards.
            string jsonText = Regex.Replace(match.Value, @"""[^""]+""\s*:\s*null\s*,?\s*", "");
            jsonText = Regex.Replace(jsonText, @",(\s*[}\]])", "$1");

            var raw = JsonUtility.FromJson<RawBatchDecision>(jsonText);

            if (raw.assignments != null && raw.assignments.Count > 0)
            {
                var validJobs = GetAvailableJobNames();
                var villagerNames = new HashSet<string>(villagers.Where(v => v != null).Select(v => v.villagerName));
                foreach (var assignment in raw.assignments)
                {
                    if (assignment.villager == null || !villagerNames.Contains(assignment.villager))
                        _sessionStats.unknownVillagers++;
                    if (assignment.villager != null && results.ContainsKey(assignment.villager))
                        _sessionStats.duplicateAssignments++;
                    if (assignment.job != null && assignment.job.Contains("["))
                        _sessionStats.bracketedJobs++;

                    // Cloud models ignore the schema enum at times and echo the prompt tag, e.g. "[KEEP]"
                    string job = assignment.job?.Trim().Trim('[', ']').Trim();

                    // An unknown job name (e.g. "Keeper") would make the villager drop its job. Count it as
                    // a format error for the benchmark, but leave the villager as it is.
                    if (!string.IsNullOrEmpty(job) && !IsKnownJobName(job, validJobs))
                    {
                        _sessionStats.invalidJobs++;
                        LogWarning($"Unknown job '{job}' for {assignment.villager} — treated as KEEP");
                        AddRecentEvent($"{assignment.villager}: job '{job}' does not exist — kept current job");
                        results[assignment.villager] = JobDecision.Keep($"invalid job '{job}' treated as KEEP: {assignment.reason}");
                        continue;
                    }

                    bool validTarget = HasValidTarget(jsonText, assignment.villager, job, assignment.targetX, assignment.targetY);
                    // The schema makes every field required, so models sometimes fill fields that do not apply
                    // (e.g. buildingType "House" on a Lumberjack). Drop those before they reach the job system.
                    bool isBuilder = string.Equals(job, "Builder", StringComparison.OrdinalIgnoreCase);
                    bool isIdle = string.Equals(job, "IDLE", StringComparison.OrdinalIgnoreCase);
                    if (!isBuilder && !string.IsNullOrEmpty(assignment.buildingType)) _sessionStats.strippedBuildingTypes++;
                    if (!isIdle && assignment.restUntilEnergy > 0) _sessionStats.strippedRestTargets++;
                    if ((isBuilder || isIdle) && assignment.gatherAmount > 0) _sessionStats.strippedGatherAmounts++;
                    var decision = new JobDecision
                    {
                        jobName = job ?? "IDLE",
                        buildingType = isBuilder ? assignment.buildingType ?? "" : "",
                        reason = assignment.reason ?? "",
                        success = true,
                        hasTargetArea = validTarget,
                        targetX = assignment.targetX,
                        targetY = assignment.targetY,
                        gatherAmount = isBuilder || isIdle ? 0 : assignment.gatherAmount,
                        restUntilEnergy = isIdle ? assignment.restUntilEnergy : 0
                    };

                    results[assignment.villager] = decision;
                    LogInfo($"Parsed: {assignment.villager} -> {decision.jobName} at ({decision.targetX},{decision.targetY})");
                }
            }
            else
            {
                // Fallback: small models sometimes return a flat dict { "VillagerName": { "job": "X", "location": "(x,y)" } }
                _sessionStats.flatDictFallbacks++;
                TryParseFlatDictFormat(match.Value, villagers, results);
            }

            foreach (var v in villagers)
            {
                if (v != null && !results.ContainsKey(v.villagerName))
                {
                    // Keep the current job: an omitted [KEEP] villager would otherwise be stopped
                    results[v.villagerName] = JobDecision.Keep("Not in response");
                    _sessionStats.missingVillagers++;
                    LogWarning($"Villager {v.villagerName} not in batch response");
                }
            }

            if (raw.goals != null && raw.goals.Count > 0 && VillageGoals.Instance != null)
            {
                var parsedGoals = new List<VillageGoal>();
                foreach (var g in raw.goals)
                {
                    if (!Enum.TryParse<GoalType>(g.type, true, out var goalType)) continue;

                    var goal = new VillageGoal
                    {
                        type = goalType,
                        targetAmount = g.amount,
                        description = !string.IsNullOrEmpty(g.description) ? g.description : g.type,
                        priority = Enum.TryParse<GoalPriority>(g.priority, true, out var prio) ? prio : GoalPriority.Normal
                    };

                    if (goalType == GoalType.GatherResource)
                    {
                        // Normalize common LLM variants (e.g. "Seeds" → "Seed")
                        var resourceStr = g.resource?.TrimEnd('s') is "Seed" or "Wood" or "Stone" or "Food" or "Iron"
                            ? g.resource.TrimEnd('s')
                            : g.resource;

                        if (!Enum.TryParse<ResourceType>(resourceStr, true, out var rt) || rt == ResourceType.None)
                        {
                            LogWarning($"Could not parse resource type '{g.resource}' for goal '{g.description}' — skipping");
                            continue;
                        }
                        goal.targetResource = rt;
                    }

                    LogEvent($"Goal parsed: {goal.description} | type={goal.type} resource={goal.targetResource} amount={goal.targetAmount} priority={goal.priority}");
                    parsedGoals.Add(goal);
                }

                VillageGoals.Instance.SetGoalsFromLLM(parsedGoals);
                _sessionStats.llmGoalSets++;
                _sessionStats.llmGoalsParsed += parsedGoals.Count;
            }

        }
        catch (Exception e)
        {
            LogWarning($"Batch parse error: {e.Message}");
            _sessionStats.parseFailures++;
            foreach (var v in villagers)
                if (v != null) results[v.villagerName] = JobDecision.Idle($"Parse error: {e.Message}");
        }

        return results;
    }

    private static bool IsKnownJobName(string job, List<string> validJobs)
    {
        if (job.Equals("IDLE", StringComparison.OrdinalIgnoreCase)
            || new JobDecision { jobName = job }.IsNoChange) return true;
        return validJobs.Any(j => j.Equals(job, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// JsonUtility turns a missing coordinate into 0, so "targetX": 9 without targetY would send the villager
    /// to (9,0) at the map edge. A target only counts if the model wrote both coordinates and they hit a tile.
    /// Rejected targets are reported as a recent event, so the model sees what went wrong.
    /// </summary>
    private bool HasValidTarget(string json, string villager, string job, int x, int y)
    {
        if (x == 0 && y == 0) return false;
        // Idle / no-change answers ignore the target anyway; no need to report their coordinates
        if (string.IsNullOrEmpty(job) || job.Equals("IDLE", StringComparison.OrdinalIgnoreCase)
            || new JobDecision { jobName = job }.IsNoChange) return false;

        var obj = Regex.Match(json, "\\{[^{}]*\"villager\"\\s*:\\s*\"" + Regex.Escape(villager ?? "") + "\"[^{}]*\\}");
        bool bothWritten = !obj.Success
            || (Regex.IsMatch(obj.Value, "\"targetX\"\\s*:\\s*-?\\d") && Regex.IsMatch(obj.Value, "\"targetY\"\\s*:\\s*-?\\d"));
        var grid = VillageState.Instance?.TileGrid;
        bool onMap = grid == null || grid.TryGet(new Vector2Int(x, y), out _);
        if (bothWritten && onMap) return true;

        _sessionStats.invalidTargets++;
        string why = bothWritten ? "not a tile on the map" : "targetX and targetY must both be given";
        LogWarning($"Ignoring target ({x},{y}) for {villager}: {why}");
        AddRecentEvent($"{villager}: target ({x},{y}) ignored — {why}");
        return false;
    }

    private void TryParseFlatDictFormat(string json, IReadOnlyList<Villager> villagers, Dictionary<string, JobDecision> results)
    {
        // Handles: { "VillagerName": { "job": "X", "location": "(x,y)" }, ... }
        foreach (var v in villagers)
        {
            if (v == null) continue;

            var entryMatch = Regex.Match(json,
                $@"""{Regex.Escape(v.villagerName)}""\s*:\s*\{{([^}}]*)\}}");
            if (!entryMatch.Success) continue;

            string block = entryMatch.Groups[1].Value;

            var jobMatch = Regex.Match(block, @"""job""\s*:\s*""([^""]+)""");
            string jobName = jobMatch.Success ? jobMatch.Groups[1].Value : "IDLE";

            int x = 0, y = 0;
            var locMatch = Regex.Match(block, @"""location""\s*:\s*""\(?\s*(-?\d+)\s*,\s*(-?\d+)\s*\)?""");
            if (locMatch.Success)
            {
                int.TryParse(locMatch.Groups[1].Value, out x);
                int.TryParse(locMatch.Groups[2].Value, out y);
            }

            var reasonMatch = Regex.Match(block, @"""reason""\s*:\s*""([^""]*)""");

            results[v.villagerName] = new JobDecision
            {
                jobName = jobName,
                reason = reasonMatch.Success ? reasonMatch.Groups[1].Value : "",
                success = true,
                hasTargetArea = x != 0 || y != 0,
                targetX = x,
                targetY = y
            };

            LogInfo($"Parsed (flat): {v.villagerName} -> {jobName} at ({x},{y})");
        }
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

            RequestImmediateBatchDecision("individual_request");

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

        if (logFullPrompts) LogVerbose($"Single Prompt:\n{fullPrompt}");
        else LogVerbose($"Single Prompt ({fullPrompt.Length} chars)");

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
            var chatResponse = await OllamaExtensions.ChatWithMetadataExt(defaultModel, fullPrompt, keepAliveSeconds, contextSize);

            metrics.responseTime = (DateTime.Now - startTime).TotalSeconds;
            metrics.responseLength = chatResponse.content.Length;

            metrics.promptEvalCount = chatResponse.promptEvalCount;
            metrics.evalCount = chatResponse.evalCount;
            metrics.totalTokens = chatResponse.TotalTokens;

            metrics.promptEvalDuration = chatResponse.PromptEvalSeconds;
            metrics.evalDuration = chatResponse.EvalSeconds;
            metrics.totalDuration = chatResponse.TotalSeconds;
            metrics.loadDuration = chatResponse.LoadSeconds;

            if (logFullPrompts) LogVerbose($"Response:\n{chatResponse.content}");
            else LogVerbose($"Response ({chatResponse.content.Length} chars)");

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
            
            LogError($"Error: {e.Message}");

            return JobDecision.Idle($"Error: {e.Message}");
        }
    }

    private string BuildSingleSystemPrompt(List<string> availableJobs)
    {
        var style = GlobalSettings.Instance != null ? GlobalSettings.Instance.PromptStyle : PromptStyle.Normal;
        return style switch
        {
            PromptStyle.Caveman    => LLMPromptCaveman.BuildSingleSystemPrompt(availableJobs),
            PromptStyle.Lean       => LLMPromptLean.BuildSingleSystemPrompt(availableJobs),
            PromptStyle.CavemanOld => LLMPromptCavemanOld.BuildSingleSystemPrompt(availableJobs),
            PromptStyle.NormalOld  => LLMPromptNormalOld.BuildSingleSystemPrompt(availableJobs),
            _                      => LLMPromptNormal.BuildSingleSystemPrompt(availableJobs),
        };
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
        if (resources.seedLocations.Count > 0)
            sb.AppendLine($"Seeds: {FormatLocationsSimple(resources.seedLocations)}");
        if (resources.cropLocations.Count > 0)
            sb.AppendLine($"Mature Crops: {FormatLocationsSimple(resources.cropLocations)}");

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
        _sessionStats.totalThinkingChars += metrics.thinking?.Length ?? 0;
        _sessionStats.totalResponseTime += metrics.responseTime;
        _sessionStats.totalDecisions += metrics.decisionsCount;

        _sessionStats.maxPromptTokens = Mathf.Max(_sessionStats.maxPromptTokens, metrics.promptEvalCount);
        if (contextSize > 0 && metrics.promptEvalCount >= contextSize * 0.9f)
            _sessionStats.callsNearContextLimit++;
        if (metrics.doneReason == "length")
            _sessionStats.truncatedResponses++;

        if (metrics.responseTime > _sessionStats.maxResponseTime)
            _sessionStats.maxResponseTime = metrics.responseTime;

        if (metrics.responseTime < _sessionStats.minResponseTime || _sessionStats.minResponseTime == 0)
            _sessionStats.minResponseTime = metrics.responseTime;

        _sessionStats.elapsedRealtime = Time.realtimeSinceStartup - _sessionStats.sessionStartRealtime;
        _sessionStats.elapsedGameTime = Time.time - _sessionStats.sessionStartGameTime;
        _sessionStats.avgGameSpeed = _sessionStats.elapsedRealtime > 0f
            ? _sessionStats.elapsedGameTime / _sessionStats.elapsedRealtime
            : 0f;

        OnMetricsRecorded?.Invoke(metrics);

        LogEvent($"Metrics: Type={metrics.requestType}, " +
                $"Tokens={metrics.totalTokens} (prompt={metrics.promptEvalCount}, response={metrics.evalCount}), " +
                 $"Time={metrics.responseTime:F2}s (eval={metrics.evalDuration:F2}s), Success={metrics.success}");

        if (contextSize > 0 && metrics.promptEvalCount >= contextSize * 0.9f)
            LogWarning($"Context near limit: {metrics.promptEvalCount}/{contextSize} tokens used ({metrics.promptEvalCount * 100f / contextSize:F0}%)");

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

            LogInfo($"Metrics exported to: {path}");
        }
        catch (Exception e)
        {
            LogError($"Failed to export metrics: {e.Message}");
        }
    }

    public void ClearMetricsHistory()
    {
        _metricsHistory.Clear();
        _sessionStats = new LLMSessionStats
        {
            sessionStartRealtime = Time.realtimeSinceStartup,
            sessionStartGameTime = Time.time
        };
        LogInfo("Metrics history cleared");
    }

    public string GetMetricsSummary()
    {
        if (_sessionStats.totalRequests == 0)
            return "No metrics recorded yet.";

        double avgResponseTime = _sessionStats.totalResponseTime / _sessionStats.totalRequests;
        double successRate = (_sessionStats.successfulRequests / (double)_sessionStats.totalRequests) * 100.0;
        int avgTokensPerRequest = _sessionStats.totalRequests > 0 ? _sessionStats.totalTokens / _sessionStats.totalRequests : 0;

        float realElapsed = _sessionStats.elapsedRealtime;
        float gameElapsed = _sessionStats.elapsedGameTime;
        float gameSpeedRatio = _sessionStats.avgGameSpeed;

        return $@"=== LLM Session Metrics ===
Total Requests: {_sessionStats.totalRequests}
Success Rate: {successRate:F1}%
Total Decisions Made: {_sessionStats.totalDecisions}

Session Time:
  Real time:  {realElapsed:F1}s
  Game time:  {gameElapsed:F1}s
  Avg speed:  {gameSpeedRatio:F1}x

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

    private int CountFinishedBuildings()
    {
        int count = 0;
        var buildings = UnityEngine.Object.FindObjectsByType<Buildings.Building>(FindObjectsSortMode.None);
        foreach (var b in buildings)
            if (b != null && b.IsFinished()) count++;
        return count;
    }

    private int CountAllCrops()
    {
        int count = 0;
        var nodes = UnityEngine.Object.FindObjectsByType<ResourceNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var n in nodes)
            if (n != null && n.resourceType == ResourceNode.ResourceType.Crop) count++;
        return count;
    }

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
                    if (node.IsMature)
                        result.treeLocations.Add(pos);
                    break;
                case ResourceNode.ResourceType.Stone:
                    if (node.isMineShaft)
                        result.mineLocations.Add(pos);
                    else
                        result.stoneLocations.Add(pos);
                    break;
                case ResourceNode.ResourceType.Seed:
                    result.seedLocations.Add(pos);
                    break;
                case ResourceNode.ResourceType.Crop:
                    if (node.IsMature)
                        result.cropLocations.Add(pos);
                    break;
            }
        }

        var buildings = UnityEngine.Object.FindObjectsByType<Buildings.Building>(FindObjectsSortMode.None);
        foreach (var b in buildings)
        {
            if (b == null) continue;

            var pos = WorldToGrid(b.transform.position);

            if (!b.IsFinished())
            {
                if (!b.IsReserved)
                    result.buildingLocations.Add(pos);
            }
            else if (b.buildingData != null)
            {
                var type = b.buildingData.buildingType;
                result.completedBuildingCounts.TryGetValue(type, out int current);
                result.completedBuildingCounts[type] = current + 1;

                if (type == Buildings.BuildingType.Farm)
                    result.farmLocations.Add(pos);
                else if (type == Buildings.BuildingType.House)
                    result.houseLocations.Add(pos);
                else if (type == Buildings.BuildingType.Stockpile)
                    result.stockpileLocations.Add(pos);
            }
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
        _conversation?.Clear();
        _lastWood = _lastStone = _lastSeeds = _lastFood = -1;
        _lastAssignedJob.Clear();
        _decisionCount = 0;
        LogInfo("Chat reset");
    }

    private void OnValidate()
    {
        if (_conversation != null) _conversation.MaxPairs = memoryPairs;
    }

    public int MemoryPairs
    {
        get => memoryPairs;
        set
        {
            memoryPairs = Mathf.Max(0, value);
            if (_conversation != null) _conversation.MaxPairs = memoryPairs;
        }
    }

    public int CurrentRetainedPairs => _conversation?.PairCount ?? 0;

    /// <summary>Switches to the given model. Returns false if Ollama does not list it.</summary>
    public bool SetModel(string modelName)
    {
        if (!_availableModels.Contains(modelName)) return false;
        defaultModel = modelName;
        ResetChat();
        OnModelLoaded?.Invoke(modelName);
        return true;
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
            RequestImmediateBatchDecision("editor_force_batch");
    }

    [ContextMenu("Log Resource Locations")]
    private void LogResources()
    {
        var resources = GetAllResourceLocations();
        LogInfo($"Trees: {resources.treeLocations.Count}, Stone: {resources.stoneLocations.Count}, Crops: {resources.cropLocations.Count}");
    }

    [ContextMenu("Show Metrics Summary")]
    private void EditorShowMetrics()
    {
        if (Application.isPlaying)
            LogInfo(GetMetricsSummary());
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

public enum ThinkMode
{
    ModelDefault,
    Off,
    Low,
    Medium,
    High,
    On // think:true — for models with on/off thinking only (e.g. qwen). Appended to keep serialized indices.
}

#region Data Classes

[Serializable]
public class RawBatchDecision
{
    public List<RawSingleAssignment> assignments;
    public List<RawGoalDecision> goals;
}

[Serializable]
public class RawSingleAssignment
{
    public string villager;
    public string job;
    public string buildingType;
    public int targetX;
    public int targetY;
    public int gatherAmount;
    public int restUntilEnergy;
    public string reason;
}

[Serializable]
public class RawGoalDecision
{
    public string type;
    public string resource;
    public int amount;
    public string priority;
    public string description;
}

[Serializable]
public class JobDecision
{
    public string jobName;
    public string buildingType;
    public string reason;
    public bool success;
    public bool hasTargetArea;
    public int targetX;
    public int targetY;
    public int gatherAmount;

    public int restUntilEnergy;

    public bool IsIdle => string.IsNullOrEmpty(jobName) ||
                          jobName.Equals("IDLE", StringComparison.OrdinalIgnoreCase);

    /// <summary>Models echo the prompt tags as job names ("KEEP", "BUSY", "RESTING") — meaning: leave the villager as is.</summary>
    public bool IsNoChange => jobName != null &&
                              (jobName.Equals("KEEP", StringComparison.OrdinalIgnoreCase)
                               || jobName.Equals("BUSY", StringComparison.OrdinalIgnoreCase)
                               || jobName.Equals("RESTING", StringComparison.OrdinalIgnoreCase));

    public static JobDecision Keep(string reason) => new JobDecision
    {
        jobName = "KEEP",
        reason = reason,
        success = true,
        hasTargetArea = false
    };

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

    public string actualModel;   // model name reported back by Ollama
    public string thinking;      // separate reasoning trace (null if none)
    public string doneReason;    // "stop", or "length" if truncated by num_predict
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
    public int totalThinkingChars; // thinking tokens are missing from eval_count for local models with format=json

    public double totalResponseTime;
    public double minResponseTime;
    public double maxResponseTime;

    public int totalDecisions;
    public int skippedFallbacks; // fallback intervals that found nothing new and made no call
    public int skippedTriggers;  // event triggers dropped because nobody could act or the batch already covered them
    public int invalidJobs;      // assignments with a job name that does not exist (treated as KEEP)

    // Format / instruction-following errors, all fixed up by the parser (benchmark: how clean is the model's output)
    public int invalidTargets;         // coordinates dropped: only one of targetX/targetY, or not a tile on the map
    public int strippedBuildingTypes;  // buildingType on a non-Builder assignment
    public int strippedRestTargets;    // restUntilEnergy on a non-IDLE assignment
    public int strippedGatherAmounts;  // gatherAmount on a Builder / IDLE assignment
    public int bracketedJobs;          // job echoed with brackets from the prompt tags, e.g. "[KEEP]"
    public int missingVillagers;       // villager left out of the answer (treated as KEEP)
    public int unknownVillagers;       // assignment for a name that is not a villager
    public int duplicateAssignments;   // second assignment for the same villager in one answer (later one wins)
    public int parseFailures;          // no JSON or a JSON error: every villager fell back to IDLE
    public int flatDictFallbacks;      // answer had no "assignments" list and was read as { "Name": {...} }
    public int llmGoalSets;            // answers that (re)set the village goals
    public int llmGoalsParsed;         // village goals accepted from those answers

    // Context usage: Ollama silently truncates prompts above num_ctx
    public int maxPromptTokens;
    public int callsNearContextLimit;  // prompt >= 90% of contextSize
    public int truncatedResponses;     // doneReason "length" (hit num_predict)

    // Session timing
    public float sessionStartRealtime;
    public float sessionStartGameTime;
    public float elapsedRealtime;
    public float elapsedGameTime;
    public float avgGameSpeed;
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
    public List<Vector2Int> mineLocations = new List<Vector2Int>();
    public List<Vector2Int> seedLocations = new List<Vector2Int>();
    public List<Vector2Int> buildingLocations = new List<Vector2Int>();
    public List<Vector2Int> farmLocations = new List<Vector2Int>();
    public List<Vector2Int> houseLocations = new List<Vector2Int>();
    public List<Vector2Int> stockpileLocations = new List<Vector2Int>();
    public List<Vector2Int> cropLocations = new List<Vector2Int>();
    public Dictionary<Buildings.BuildingType, int> completedBuildingCounts = new Dictionary<Buildings.BuildingType, int>();
}

#endregion