using System;
using Buildings;
using Tiles;
using UnityEngine;

[Serializable]
public class FarmerLogic : JobLogic
{
    [Header("Farmer Settings")]
    public float workSpeed = 1f;
    public float stoppingDistance = 1.5f;
    public float harvestTime = 5f;
    public int foodPerHarvest = 10;
    public int seedCostPerHarvest = 1;

    private enum State { Idle, FindingTarget, MovingToTarget, Working }
    private State currentState = State.Idle;
    private Building currentTarget = null;
    private float workProgress = 0f;

    public override void OnJobStart(JobHandler handler)
    {
        currentState = State.FindingTarget;
        timeSinceLastAction = 0f;
        workProgress = 0f;
        currentTarget = null;
    }

    public override bool Execute(JobHandler handler)
    {
        switch (currentState)
        {
            case State.FindingTarget:
                currentTarget = FindNextFarm(handler);
                if (currentTarget != null)
                {
                    // Check if we have seeds to farm
                    if (VillageState.Instance != null && !VillageState.Instance.HasResource(ResourceType.Seed, seedCostPerHarvest))
                    {
                        currentStatus = $"Need seeds to farm! Have: {VillageState.Instance.Seeds}. Waiting...";
                        handler.villagerMover.StopMoving();
                        return false;
                    }
                    
                    currentTarget.Reserve();
                    currentState = State.MovingToTarget;
                    handler.villagerMover.StopMoving();
                }
                else
                {
                    currentStatus = "No farm available. Waiting...";
                    handler.villagerMover.StopMoving();
                }
                break;

            case State.MovingToTarget:
                if (currentTarget == null)
                {
                    currentState = State.FindingTarget;
                    break;
                }

                currentStatus = $"Moving to farm";
                handler.villagerMover.MoveTo(currentTarget.transform.position);

                if (handler.villagerMover.IsNearDestination(stoppingDistance))
                {
                    handler.villagerMover.StopMoving();
                    currentState = State.Working;
                    workProgress = 0f;
                    
                    // Consume seeds when starting to work
                    if (VillageState.Instance != null)
                    {
                        VillageState.Instance.TrySpendResource(ResourceType.Seed, seedCostPerHarvest);
                    }
                }
                break;

            case State.Working:
                if (currentTarget == null)
                {
                    currentState = State.FindingTarget;
                    break;
                }

                workProgress += workSpeed * Time.deltaTime;
                currentStatus = $"Farming ({workProgress:F1}/{harvestTime:F1})...";

                if (workProgress >= harvestTime)
                {
                    // Harvest complete - produce food!
                    if (VillageState.Instance != null)
                    {
                        VillageState.Instance.AddResource(ResourceType.None, 0); // Food isn't in ResourceType yet
                        // For now, let's add it as a special case
                        Debug.Log($"[Farmer] Harvested {foodPerHarvest} food!");
                        
                        // TODO: Add Food to ResourceType enum and VillageState
                        // VillageState.Instance.AddFood(foodPerHarvest);
                    }
                    
                    currentTarget.Unreserve();
                    currentState = State.FindingTarget;
                    handler.villagerMover.StopMoving();
                    workProgress = 0f;
                    return true;  // Job cycle complete, gain XP
                }
                break;
        }

        return false;
    }

    private Building FindNextFarm(JobHandler handler)
    {
        var all = UnityEngine.Object.FindObjectsByType<Building>(FindObjectsSortMode.None);
        Building nearest = null;
        float bestDist = float.MaxValue;
        Vector3 origin = handler != null ? handler.transform.position : Vector3.zero;

        foreach (var b in all)
        {
            if (b == null) continue;
            if (b.IsReserved) continue;
            if (!b.IsFinished()) continue;  // Farm must be finished
            if (b.buildingData == null) continue;
            if (b.buildingData.buildingType != BuildingType.Farm) continue;

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