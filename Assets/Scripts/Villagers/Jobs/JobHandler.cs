using System;
using UnityEngine;
using AnimationState = Villagers.Jobs.AnimationState;

public class JobHandler : MonoBehaviour
{
    [Header("Current Job")]
    public JobType currentJob;

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
        if (currentJob != null && currentJob.JobLogic != null)
        {
            bool completed = currentJob.JobLogic.Execute(this);
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
        if (currentJob != null && currentJob.JobLogic != null)
        {
            currentJob.JobLogic.OnJobEnd(this);
            currentJob.JobLogic.ResetState();
        }

        currentJob = newJob;
        hasTargetArea = withTarget;
        targetArea = target;
        preferredBuildingType = buildingType;

        if (currentJob != null && currentJob.JobLogic != null)
        {
            currentJob.JobLogic.OnJobStart(this);
            Debug.Log($"[JobHandler] {gameObject.name} started job: {currentJob.JobName}" +
                      (hasTargetArea ? $" targeting ({targetArea.x},{targetArea.y})" : ""));
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
            Debug.Log($"[JobHandler] {gameObject.name} leveled up to {currentJobLevel}!");
        }
    }
}