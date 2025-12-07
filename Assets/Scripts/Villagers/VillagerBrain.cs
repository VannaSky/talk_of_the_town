using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bridges LLM decisions to JobHandler.
/// Event-driven: requests new decision when job completes, not just on timer.
/// </summary>
[RequireComponent(typeof(Villager))]
[RequireComponent(typeof(JobHandler))]
public class VillagerBrain : MonoBehaviour
{
    [Header("Decision Timing")]
    [Tooltip("Max seconds between decisions (fallback if no job completion)")]
    [SerializeField] private float maxDecisionInterval = 30f;
    
    [Tooltip("Min seconds between decisions (prevent spam)")]
    [SerializeField] private float minDecisionInterval = 2f;
    
    [Tooltip("Seconds to wait when idle before requesting new decision")]
    [SerializeField] private float idleTimeout = 3f;
    
    [Header("Available Jobs")]
    [Tooltip("JobType assets the LLM can choose from")]
    [SerializeField] private List<JobType> availableJobTypes = new List<JobType>();
    
    [Header("Debug")]
    [SerializeField] private bool logDecisions = true;
    [SerializeField] private JobDecision lastDecision;
    [SerializeField] private string currentState = "Initializing";
    
    private Villager _villager;
    private JobHandler _jobHandler;
    private bool _isProcessing;
    private float _lastDecisionTime;
    private float _idleTime;
    private JobType _lastAssignedJob;
    
    void Awake()
    {
        _villager = GetComponent<Villager>();
        _jobHandler = GetComponent<JobHandler>();
    }
    
    void Start()
    {
        // Auto-load available jobs from Resources if not assigned
        if (availableJobTypes.Count == 0)
        {
            availableJobTypes.AddRange(Resources.LoadAll<JobType>(""));
            if (logDecisions)
                Debug.Log($"[VillagerBrain] Loaded {availableJobTypes.Count} job types from Resources");
        }
        
        StartCoroutine(DecisionLoop());
    }
    
    private IEnumerator DecisionLoop()
    {
        // Wait for systems to initialize
        while (LLMController.Instance == null || !LLMController.Instance.IsReady)
        {
            currentState = "Waiting for LLM...";
            yield return new WaitForSeconds(0.5f);
        }
        
        if (logDecisions)
            Debug.Log($"[VillagerBrain] {_villager.villagerName} starting decision loop");
        
        // Initial decision
        yield return RequestAndApplyDecision();
        
        while (true)
        {
            yield return new WaitForSeconds(0.5f);  // Check every 0.5s
            
            if (_isProcessing) continue;
            
            // Check if we should request a new decision
            DecisionTrigger trigger = ShouldRequestDecision();
            
            if (trigger != DecisionTrigger.None)
            {
                if (logDecisions)
                    Debug.Log($"[VillagerBrain] {_villager.villagerName} decision triggered by: {trigger}");
                
                yield return RequestAndApplyDecision();
            }
        }
    }
    
    private enum DecisionTrigger
    {
        None,
        JobCompleted,
        IdleTimeout,
        MaxIntervalReached,
        NoJob
    }
    
    private DecisionTrigger ShouldRequestDecision()
    {
        float timeSinceLastDecision = Time.time - _lastDecisionTime;
        
        // Respect minimum interval
        if (timeSinceLastDecision < minDecisionInterval)
            return DecisionTrigger.None;
        
        // No job assigned?
        if (_jobHandler.currentJob == null)
        {
            _idleTime += 0.5f;
            currentState = $"Idle ({_idleTime:F1}s)";
            
            if (_idleTime >= idleTimeout)
            {
                _idleTime = 0f;
                return DecisionTrigger.IdleTimeout;
            }
            return DecisionTrigger.None;
        }
        
        // Check job status
        string status = _jobHandler.currentJob.JobLogic?.GetCurrentStatus() ?? "Idle";
        currentState = status;
        
        // Job is looking for work = job cycle completed, ready for new decision
        if (status.Contains("Waiting") || status.Contains("No ") || status.Contains("found"))
        {
            return DecisionTrigger.JobCompleted;
        }
        
        // Max interval reached (fallback)
        if (timeSinceLastDecision >= maxDecisionInterval)
        {
            return DecisionTrigger.MaxIntervalReached;
        }
        
        // Actively working, don't interrupt
        _idleTime = 0f;
        return DecisionTrigger.None;
    }
    
    private IEnumerator RequestAndApplyDecision()
    {
        _isProcessing = true;
        currentState = "Thinking...";
        
        // Build list of job names
        var jobNames = new List<string>();
        foreach (var jt in availableJobTypes)
        {
            if (jt != null)
                jobNames.Add(jt.JobName);
        }
        
        // Request decision
        var task = LLMController.Instance.RequestJobDecision(_villager, jobNames);
        
        while (!task.IsCompleted)
            yield return null;
        
        lastDecision = task.Result;
        _lastDecisionTime = Time.time;
        
        if (!lastDecision.success)
        {
            if (logDecisions)
                Debug.LogWarning($"[VillagerBrain] {_villager.villagerName} decision failed: {lastDecision.reason}");
            _isProcessing = false;
            yield break;
        }
        
        ApplyDecision(lastDecision);
        _isProcessing = false;
    }
    
    private void ApplyDecision(JobDecision decision)
    {
        if (logDecisions)
            Debug.Log($"[VillagerBrain] {_villager.villagerName} -> {decision.jobName}: {decision.reason}");
        
        if (decision.IsIdle)
        {
            _jobHandler.AssignJob(null);
            _lastAssignedJob = null;
            currentState = "Idle (by choice)";
            return;
        }
        
        // Find matching JobType
        JobType matchedJob = null;
        foreach (var jt in availableJobTypes)
        {
            if (jt != null && jt.JobName.Equals(decision.jobName, System.StringComparison.OrdinalIgnoreCase))
            {
                matchedJob = jt;
                break;
            }
        }
        
        if (matchedJob != null)
        {
            // Only reassign if different job
            if (_jobHandler.currentJob != matchedJob)
            {
                _jobHandler.AssignJob(matchedJob);
                _lastAssignedJob = matchedJob;
                
                if (logDecisions)
                    Debug.Log($"[VillagerBrain] {_villager.villagerName} assigned job: {matchedJob.JobName}");
            }
            else if (logDecisions)
            {
                Debug.Log($"[VillagerBrain] {_villager.villagerName} continuing job: {matchedJob.JobName}");
            }
            
            currentState = $"{matchedJob.JobName} (assigned)";
        }
        else
        {
            Debug.LogWarning($"[VillagerBrain] Unknown job: {decision.jobName}");
            _jobHandler.AssignJob(null);
            currentState = "Unknown job";
        }
    }
    
    /// <summary>
    /// Force an immediate decision (useful for testing or events)
    /// </summary>
    public void ForceDecision()
    {
        if (!_isProcessing)
        {
            _idleTime = idleTimeout;  // Trigger immediately
            StartCoroutine(RequestAndApplyDecision());
        }
    }
    
    /// <summary>
    /// Notify that a significant event happened (goal changed, etc.)
    /// </summary>
    public void OnSignificantEvent()
    {
        // Reset timer to allow quicker re-evaluation
        _lastDecisionTime = Time.time - maxDecisionInterval + minDecisionInterval;
    }

#if UNITY_EDITOR
    [ContextMenu("Force Decision Now")]
    private void EditorForceDecision()
    {
        if (Application.isPlaying)
            ForceDecision();
        else
            Debug.LogWarning("Only works in Play mode");
    }
#endif
}