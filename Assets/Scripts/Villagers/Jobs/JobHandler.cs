using UnityEngine;
using AnimationState = Villagers.Jobs.AnimationState;

public class JobHandler : MonoBehaviour
{
    [Header("Current Job")]
    public JobType currentJob;
    
    [Header("Target Area (from LLM)")]
    [SerializeField] private bool hasTargetArea;
    [SerializeField] private Vector2Int targetArea;

    [Header("References")]
    public Animator animator;
    public VillagerMover villagerMover;
    public VillagerEquipment equipment;

    [Header("Debug")]
    [SerializeField] private int currentJobLevel = 1;
    [SerializeField] private float currentJobXP = 0f;

    public Vector2Int? PreferredTargetArea => hasTargetArea ? targetArea : null;

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
        AssignJobInternal(newJob, false, Vector2Int.zero);
    }

    public void AssignJobWithTarget(JobType newJob, Vector2Int target)
    {
        AssignJobInternal(newJob, true, target);
    }

    private void AssignJobInternal(JobType newJob, bool withTarget, Vector2Int target)
    {
        if (currentJob != null && currentJob.JobLogic != null)
        {
            currentJob.JobLogic.OnJobEnd(this);
            currentJob.JobLogic.ResetState();
        }

        currentJob = newJob;
        hasTargetArea = withTarget;
        targetArea = target;

        if (currentJob != null && currentJob.JobLogic != null)
        {
            currentJob.JobLogic.OnJobStart(this);
            Debug.Log($"[JobHandler] {gameObject.name} started job: {currentJob.JobName}" + 
                      (hasTargetArea ? $" targeting ({targetArea.x},{targetArea.y})" : ""));
        }
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