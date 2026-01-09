using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bridges LLM batch decisions to JobHandler.
/// Listens for batch decisions instead of requesting individually.
/// </summary>
[RequireComponent(typeof(Villager))]
[RequireComponent(typeof(JobHandler))]
public class VillagerBrain : MonoBehaviour
{
    [Header("Decision Timing")]
    [SerializeField] private float checkInterval = 1f;
    [SerializeField] private float idleTimeout = 5f;

    [Header("Available Jobs")]
    [SerializeField] private List<JobType> availableJobTypes = new List<JobType>();

    [Header("Debug")]
    [SerializeField] private bool logDecisions = true;
    [SerializeField] private JobDecision lastDecision;
    [SerializeField] private string currentState = "Initializing";

    private Villager _villager;
    private JobHandler _jobHandler;
    private float _idleTime;
    private float _lastAppliedDecisionTime;

    void Awake()
    {
        _villager = GetComponent<Villager>();
        _jobHandler = GetComponent<JobHandler>();
    }

    void Start()
    {
        if (availableJobTypes.Count == 0)
        {
            availableJobTypes.AddRange(Resources.LoadAll<JobType>(""));
            if (logDecisions)
                Debug.Log($"[VillagerBrain] Loaded {availableJobTypes.Count} job types");
        }

        // Subscribe to batch decisions
        if (LLMController.Instance != null)
        {
            LLMController.Instance.OnBatchDecisionMade += OnBatchDecisionReceived;
        }

        StartCoroutine(WaitForLLMAndSubscribe());
        StartCoroutine(MonitorLoop());
    }

    void OnDestroy()
    {
        if (LLMController.Instance != null)
        {
            LLMController.Instance.OnBatchDecisionMade -= OnBatchDecisionReceived;
        }
    }

    private IEnumerator WaitForLLMAndSubscribe()
    {
        while (LLMController.Instance == null)
        {
            currentState = "Waiting for LLM...";
            yield return new WaitForSeconds(0.5f);
        }

        // Subscribe if not already
        LLMController.Instance.OnBatchDecisionMade -= OnBatchDecisionReceived;
        LLMController.Instance.OnBatchDecisionMade += OnBatchDecisionReceived;

        if (logDecisions)
            Debug.Log($"[VillagerBrain] {_villager.villagerName} subscribed to batch decisions");
    }

    private void OnBatchDecisionReceived(Dictionary<string, JobDecision> decisions)
    {
        if (decisions.TryGetValue(_villager.villagerName, out var decision))
        {
            if (logDecisions)
                Debug.Log($"[VillagerBrain] {_villager.villagerName} received batch decision: {decision.jobName}");

            ApplyDecision(decision);
        }
        else
        {
            if (logDecisions)
                Debug.LogWarning($"[VillagerBrain] {_villager.villagerName} not in batch decision");
        }
    }

    private IEnumerator MonitorLoop()
    {
        // Wait for systems
        while (LLMController.Instance == null || !LLMController.Instance.IsReady)
        {
            yield return new WaitForSeconds(0.5f);
        }

        while (true)
        {
            yield return new WaitForSeconds(checkInterval);

            // Check if we need to request a decision
            if (ShouldRequestDecision())
            {
                if (LLMController.Instance.UseBatchDecisions)
                {
                    // Request batch (will affect all villagers)
                    if (!LLMController.Instance.IsBatchProcessing)
                    {
                        if (logDecisions)
                            Debug.Log($"[VillagerBrain] {_villager.villagerName} triggering batch decision");
                        LLMController.Instance.RequestImmediateBatchDecision();
                    }
                }
                else
                {
                    // Fallback to individual decision
                    yield return RequestIndividualDecision();
                }
            }

            UpdateState();
        }
    }

    private bool ShouldRequestDecision()
    {
        // No job assigned
        if (_jobHandler.currentJob == null)
        {
            _idleTime += checkInterval;
            if (_idleTime >= idleTimeout)
            {
                _idleTime = 0f;
                return true;
            }
            return false;
        }

        // Check job status
        string status = _jobHandler.currentJob.JobLogic?.GetCurrentStatus() ?? "Idle";

        // Job is waiting/stuck
        if (status.Contains("Waiting") || status.Contains("No ") || status.Contains("found"))
        {
            _idleTime += checkInterval;
            if (_idleTime >= idleTimeout)
            {
                _idleTime = 0f;
                return true;
            }
        }
        else
        {
            _idleTime = 0f;
        }

        return false;
    }

    private void UpdateState()
    {
        if (_jobHandler.currentJob == null)
        {
            currentState = $"Idle ({_idleTime:F1}s)";
        }
        else
        {
            currentState = _jobHandler.currentJob.JobLogic?.GetCurrentStatus() ?? "Working";
        }
    }

    private IEnumerator RequestIndividualDecision()
    {
        currentState = "Thinking...";

        var jobNames = new List<string>();
        foreach (var jt in availableJobTypes)
        {
            if (jt != null)
                jobNames.Add(jt.JobName);
        }

        var task = LLMController.Instance.RequestJobDecision(_villager, jobNames);

        while (!task.IsCompleted)
            yield return null;

        ApplyDecision(task.Result);
    }

    private void ApplyDecision(JobDecision decision)
    {
        if (decision == null) return;

        lastDecision = decision;
        _lastAppliedDecisionTime = Time.time;

        if (logDecisions)
        {
            string targetInfo = decision.hasTargetArea ? $" at ({decision.targetX},{decision.targetY})" : "";
            Debug.Log($"[VillagerBrain] {_villager.villagerName} -> {decision.jobName}{targetInfo}: {decision.reason}");
        }

        if (decision.IsIdle || !decision.success)
        {
            _jobHandler.AssignJob(null);
            currentState = "Idle";
            return;
        }

        JobType matchedJob = FindJobType(decision.jobName);

        if (matchedJob != null)
        {
            bool jobChanged = _jobHandler.currentJob != matchedJob;
            bool targetChanged = decision.hasTargetArea && _jobHandler.HasDifferentTargetArea(decision.TargetPosition);

            if (jobChanged || targetChanged)
            {
                if (decision.hasTargetArea)
                {
                    _jobHandler.AssignJobWithTarget(matchedJob, decision.TargetPosition);
                }
                else
                {
                    _jobHandler.AssignJob(matchedJob);
                }

                if (logDecisions)
                    Debug.Log($"[VillagerBrain] {_villager.villagerName} assigned {matchedJob.JobName}");
            }

            currentState = $"{matchedJob.JobName}";
        }
        else
        {
            Debug.LogWarning($"[VillagerBrain] Unknown job: {decision.jobName}");
            _jobHandler.AssignJob(null);
            currentState = "Unknown job";
        }
    }

    private JobType FindJobType(string jobName)
    {
        foreach (var jt in availableJobTypes)
        {
            if (jt != null && jt.JobName.Equals(jobName, System.StringComparison.OrdinalIgnoreCase))
                return jt;
        }
        return null;
    }

    public void ForceDecision()
    {
        _idleTime = idleTimeout;
        if (LLMController.Instance != null && LLMController.Instance.UseBatchDecisions)
        {
            LLMController.Instance.RequestImmediateBatchDecision();
        }
        else
        {
            StartCoroutine(RequestIndividualDecision());
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Force Decision Now")]
    private void EditorForceDecision()
    {
        if (Application.isPlaying)
            ForceDecision();
    }
#endif
}