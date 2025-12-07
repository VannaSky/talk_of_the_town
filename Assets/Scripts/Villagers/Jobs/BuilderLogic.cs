using System;
using Buildings;
using Tiles;
using UnityEngine;

[Serializable]
public class BuilderLogic : JobLogic
{
    [Header("Builder Settings")]
    public float buildSpeed = 1f;
    public float stoppingDistance = 1.5f;
    
    [Header("Resource Costs (per build cycle)")]
    public int woodCostPerCycle = 2;
    public int stoneCostPerCycle = 1;

    private enum State { Idle, FindingTarget, CheckingResources, MovingToTarget, Building }
    private State currentState = State.Idle;
    private Building currentTarget = null;

    public override void OnJobStart(JobHandler handler)
    {
        currentState = State.FindingTarget;
        timeSinceLastAction = 0f;
        currentTarget = null;
    }

    public override bool Execute(JobHandler handler)
    {
        switch (currentState)
        {
            case State.FindingTarget:
                currentTarget = FindNextBuilding(handler);
                if (currentTarget != null)
                {
                    currentState = State.CheckingResources;
                }
                else
                {
                    currentStatus = "No building tasks available. Waiting...";
                    handler.villagerMover.StopMoving();
                }
                break;

            case State.CheckingResources:
                // Check if village has resources to build
                if (VillageState.Instance != null)
                {
                    bool hasWood = VillageState.Instance.HasResource(ResourceType.Wood, woodCostPerCycle);
                    bool hasStone = VillageState.Instance.HasResource(ResourceType.Stone, stoneCostPerCycle);
                    
                    if (!hasWood || !hasStone)
                    {
                        currentStatus = $"Need resources! Wood: {VillageState.Instance.Wood}/{woodCostPerCycle}, Stone: {VillageState.Instance.Stone}/{stoneCostPerCycle}. Waiting...";
                        handler.villagerMover.StopMoving();
                        // Stay in this state, recheck next frame
                        return false;
                    }
                }
                
                // Resources available, proceed
                currentTarget.Reserve();
                currentState = State.MovingToTarget;
                handler.villagerMover.StopMoving();
                break;

            case State.MovingToTarget:
                if (currentTarget == null)
                {
                    currentState = State.FindingTarget;
                    break;
                }

                var bt = currentTarget.buildingData != null ? currentTarget.buildingData.buildingType.ToString() : "Building";
                currentStatus = $"Moving to build {bt}";
                handler.villagerMover.MoveTo(currentTarget.transform.position);

                if (handler.villagerMover.IsNearDestination(stoppingDistance))
                {
                    handler.villagerMover.StopMoving();
                    currentState = State.Building;
                    timeSinceLastAction = 0f;
                }
                break;

            case State.Building:
                if (currentTarget == null)
                {
                    currentState = State.FindingTarget;
                    break;
                }

                timeSinceLastAction += Time.deltaTime;
                
                // Consume resources periodically while building
                if (timeSinceLastAction >= 1f)
                {
                    timeSinceLastAction = 0f;
                    
                    // Try to consume resources
                    if (VillageState.Instance != null)
                    {
                        bool consumed = VillageState.Instance.TrySpendResource(ResourceType.Wood, woodCostPerCycle);
                        if (consumed)
                            VillageState.Instance.TrySpendResource(ResourceType.Stone, stoneCostPerCycle);
                        
                        if (!consumed)
                        {
                            currentStatus = "Out of resources! Waiting...";
                            currentTarget.Unreserve();
                            currentState = State.FindingTarget;
                            break;
                        }
                    }
                }
                
                float workApplied = buildSpeed * Time.deltaTime;
                bool levelCompleted = currentTarget.AddWork(workApplied);

                var bt2 = currentTarget.buildingData != null ? currentTarget.buildingData.buildingType.ToString() : "Building";
                currentStatus = $"Building {bt2} ({currentTarget.GetProgressPercent()}%)";

                if (levelCompleted)
                {
                    int finishedLevelIndex = Mathf.Max(0, currentTarget.currentLevel - 1);
                    currentTarget.ShowFinalForLevel(finishedLevelIndex);

                    currentTarget.Unreserve();
                    currentState = State.FindingTarget;
                    handler.villagerMover.StopMoving();
                    
                    if (currentTarget.IsFinished())
                    {
                        Debug.Log($"[Builder] Completed building: {bt2}");
                    }
                    
                    return true;  // Job cycle complete, gain XP
                }
                break;
        }

        return false;
    }

    private Building FindNextBuilding(JobHandler handler)
    {
        var all = UnityEngine.Object.FindObjectsByType<Building>(FindObjectsSortMode.None);
        Building nearest = null;
        float bestDist = float.MaxValue;
        Vector3 origin = handler.transform.position;
        
        foreach (var b in all)
        {
            if (b == null) continue;
            if (b.IsReserved) continue;
            if (b.IsFinished()) continue;
            
            float d = Vector3.Distance(origin, b.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                nearest = b;
            }
        }
        return nearest;
    }
}