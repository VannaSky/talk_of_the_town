using System;
using System.Collections.Generic;
using Tiles;
using UnityEngine;

/// <summary>
/// Manages village-wide goals that influence LLM decision-making.
/// Goals provide strategic direction for villager job assignments.
/// </summary>
public class VillageGoals : MonoBehaviour
{
    public static VillageGoals Instance { get; private set; }
    
    [Header("Active Goals")]
    [SerializeField] private List<VillageGoal> activeGoals = new List<VillageGoal>();
    
    [Header("Auto-Generated Goals")]
    [SerializeField] private bool autoGenerateGoals = true;
    [SerializeField] private int lowResourceThreshold = 20;
    
    public IReadOnlyList<VillageGoal> ActiveGoals => activeGoals;
    
    public event Action<VillageGoal> OnGoalAdded;
    public event Action<VillageGoal> OnGoalCompleted;
    public event Action<VillageGoal> OnGoalFailed;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        // Subscribe to resource changes for auto-goals
        if (VillageState.Instance != null)
        {
            VillageState.Instance.OnResourceChanged += OnResourceChanged;
        }
    }
    
    void Update()
    {
        // Check goal completion
        for (int i = activeGoals.Count - 1; i >= 0; i--)
        {
            var goal = activeGoals[i];
            if (IsGoalComplete(goal))
            {
                Debug.Log($"[VillageGoals] Goal completed: {goal.description}");
                activeGoals.RemoveAt(i);
                OnGoalCompleted?.Invoke(goal);
            }
        }
    }
    
    #region Goal Management
    
    public void AddGoal(VillageGoal goal)
    {
        // Don't add duplicates
        foreach (var existing in activeGoals)
        {
            if (existing.type == goal.type && existing.targetResource == goal.targetResource)
                return;
        }
        
        activeGoals.Add(goal);
        Debug.Log($"[VillageGoals] New goal: {goal.description}");
        OnGoalAdded?.Invoke(goal);
    }
    
    public void RemoveGoal(VillageGoal goal)
    {
        activeGoals.Remove(goal);
    }
    
    public void ClearGoals()
    {
        activeGoals.Clear();
    }
    
    private bool IsGoalComplete(VillageGoal goal)
    {
        if (VillageState.Instance == null) return false;
        
        switch (goal.type)
        {
            case GoalType.GatherResource:
                return VillageState.Instance.GetResource(goal.targetResource) >= goal.targetAmount;
            
            case GoalType.BuildStructure:
                // TODO: Check if building exists
                return false;
            
            case GoalType.ReachPopulation:
                return VillageState.Instance.Villagers.Count >= goal.targetAmount;
            
            default:
                return false;
        }
    }
    
    #endregion
    
    #region Auto-Generated Goals
    
    private void OnResourceChanged(ResourceType type, int oldValue, int newValue)
    {
        if (!autoGenerateGoals) return;
        
        // If resource dropped below threshold, create a gathering goal
        if (newValue < lowResourceThreshold && oldValue >= lowResourceThreshold)
        {
            AddGoal(new VillageGoal
            {
                type = GoalType.GatherResource,
                targetResource = type,
                targetAmount = lowResourceThreshold * 3,  // Target 3x the threshold
                priority = GoalPriority.High,
                description = $"Gather {type} (critically low!)"
            });
        }
    }
    
    #endregion
    
    #region LLM Context
    
    /// <summary>
    /// Get goals formatted for LLM prompt
    /// </summary>
    public string GetGoalsForPrompt()
    {
        if (activeGoals.Count == 0)
            return "No specific goals set. Focus on balanced resource gathering and village growth.";
        
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ACTIVE VILLAGE GOALS (prioritize these!):");
        
        foreach (var goal in activeGoals)
        {
            string progress = GetGoalProgress(goal);
            string priorityStr = goal.priority == GoalPriority.Critical ? "[CRITICAL] " :
                                 goal.priority == GoalPriority.High ? "[HIGH] " : "";
            sb.AppendLine($"- {priorityStr}{goal.description} {progress}");
        }
        
        return sb.ToString();
    }
    
    private string GetGoalProgress(VillageGoal goal)
    {
        if (VillageState.Instance == null) return "";
        
        switch (goal.type)
        {
            case GoalType.GatherResource:
                int current = VillageState.Instance.GetResource(goal.targetResource);
                return $"({current}/{goal.targetAmount})";
            
            case GoalType.ReachPopulation:
                return $"({VillageState.Instance.Villagers.Count}/{goal.targetAmount})";
            
            default:
                return "";
        }
    }
    
    #endregion

#if UNITY_EDITOR
    [ContextMenu("Add Goal: Gather 50 Wood")]
    private void TestAddWoodGoal()
    {
        AddGoal(new VillageGoal
        {
            type = GoalType.GatherResource,
            targetResource = ResourceType.Wood,
            targetAmount = 50,
            priority = GoalPriority.Normal,
            description = "Gather 50 Wood"
        });
    }
    
    [ContextMenu("Add Goal: Gather 30 Stone")]
    private void TestAddStoneGoal()
    {
        AddGoal(new VillageGoal
        {
            type = GoalType.GatherResource,
            targetResource = ResourceType.Stone,
            targetAmount = 30,
            priority = GoalPriority.Normal,
            description = "Gather 30 Stone"
        });
    }
#endif
}

#region Data Types

public enum GoalType
{
    GatherResource,
    BuildStructure,
    ReachPopulation,
    Survive  // General survival goal
}

public enum GoalPriority
{
    Low,
    Normal,
    High,
    Critical
}

[Serializable]
public class VillageGoal
{
    public GoalType type;
    public ResourceType targetResource;
    public ConstructionType targetBuilding;
    public int targetAmount;
    public GoalPriority priority = GoalPriority.Normal;
    public string description;
}

#endregion