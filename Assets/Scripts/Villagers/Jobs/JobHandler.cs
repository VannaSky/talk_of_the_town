using System;
using UnityEngine;
using AnimationState = Villagers.Jobs.AnimationState;

public class JobHandler : MonoBehaviour
{
    private const string LogCategory = "JobHandler";
    void LogError(string msg)   => GameLog.LogError(LogCategory, msg, this);
    void LogWarning(string msg) => GameLog.LogWarning(LogCategory, msg, this);
    void LogInfo(string msg)    => GameLog.LogInfo(LogCategory, msg, this);
    void LogVerbose(string msg) => GameLog.LogVerbose(LogCategory, msg, this);

    [Header("Current Job")]
    public JobType currentJob;
    private JobLogic _jobLogicInstance;
    public JobLogic ActiveJobLogic => _jobLogicInstance;

    [Header("Target Area (from LLM)")]
    [SerializeField] private bool hasTargetArea;
    [SerializeField] private Vector2Int targetArea;
    [SerializeField] private string preferredBuildingType;

    [Header("References")]
    public Animator animator;
    public VillagerMover villagerMover;
    public VillagerEquipment equipment;

    [Header("Debug")]
    [SerializeField] private int currentJobLevel = 1;
    [SerializeField] private float currentJobXP = 0f;

    /// <summary>Fired when this villager can't find work. Throttled to once per <see cref="IdleNotifyCooldown"/> seconds.</summary>
    public event Action<JobHandler> OnBecameIdle;

    /// <summary>Fired when the current job's animation state changes.</summary>
    public event Action<AnimationState, AnimationState> OnAnimationStateChanged;

    /// <summary>Fired when a new job is assigned.</summary>
    public event Action<JobType> OnJobAssigned;

    private const float IdleNotifyCooldown = 10f;
    private float _lastIdleNotifyTime = -999f;

    public Vector2Int? PreferredTargetArea => hasTargetArea ? targetArea : null;
    public string PreferredBuildingType => preferredBuildingType;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (villagerMover == null)
            villagerMover = GetComponent<VillagerMover>();
        if (equipment == null)
            equipment = GetComponent<VillagerEquipment>();
    }

    void Update()
    {
        if (currentJob != null && _jobLogicInstance != null)
        {
            bool completed = _jobLogicInstance.Execute(this);
            if (completed)
            {
                AddJobXP(10f);
            }
        }
    }

    public void AssignJob(JobType newJob)
    {
        AssignJobInternal(newJob, false, Vector2Int.zero, "");
    }

    public void AssignJobWithTarget(JobType newJob, Vector2Int target)
    {
        AssignJobInternal(newJob, true, target, "");
    }

    public void AssignJobWithBuilding(JobType newJob, Vector2Int target, string buildingType)
    {
        AssignJobInternal(newJob, true, target, buildingType);
    }

    private void AssignJobInternal(JobType newJob, bool withTarget, Vector2Int target, string buildingType)
    {
        if (_jobLogicInstance != null)
        {
            _jobLogicInstance.OnJobEnd(this);
            _jobLogicInstance.ResetState();
        }

        currentJob = newJob;
        hasTargetArea = withTarget;
        targetArea = target;
        preferredBuildingType = buildingType;

        if (currentJob != null && currentJob.JobLogic != null)
        {
            _jobLogicInstance = currentJob.JobLogic.Clone();
            _jobLogicInstance.ResetState();
            _jobLogicInstance.OnJobStart(this);
            LogInfo($"{gameObject.name} started job: {currentJob.JobName}" +
                      (hasTargetArea ? $" targeting ({targetArea.x},{targetArea.y})" : ""));
        }
        else
        {
            _jobLogicInstance = null;
        }

        OnJobAssigned?.Invoke(currentJob);
    }

    internal void NotifyIdle()
    {
        if (Time.time - _lastIdleNotifyTime < IdleNotifyCooldown) return;
        _lastIdleNotifyTime = Time.time;
        OnBecameIdle?.Invoke(this);
    }

    internal void NotifyStateChanged(AnimationState oldState, AnimationState newState)
    {
        OnAnimationStateChanged?.Invoke(oldState, newState);
    }

    public bool HasDifferentTargetArea(Vector2Int newTarget)
    {
        if (!hasTargetArea) return true;
        return targetArea != newTarget;
    }

    public int GetCurrentJobLevel() => currentJobLevel;

    private void AddJobXP(float amount)
    {
        currentJobXP += amount;
        if (currentJobXP >= 100f)
        {
            currentJobXP -= 100f;
            currentJobLevel++;
            LogInfo($"{gameObject.name} leveled up to {currentJobLevel}!");
        }
    }
}