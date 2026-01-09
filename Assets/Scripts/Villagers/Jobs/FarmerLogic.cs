using System;
using Buildings;
using Tiles;
using UnityEngine;
using AnimationState = Villagers.Jobs.AnimationState;

[Serializable]
public class FarmerLogic : JobLogic
{
    [Header("Farmer Settings")]
    public float workSpeed = 1f;
    public float stoppingDistance = 1.5f;
    public float processTime = 5f;
    public int seedCost = 2;
    public int foodProduced = 5;

    [NonSerialized] private Building _currentTarget = null;

    protected override void OnInitialize(JobHandler handler)
    {
        _currentTarget = null;
        ChangeState(AnimationState.FindingTarget, handler);
    }

    protected override bool ExecuteState(JobHandler handler)
    {
        switch (_currentState)
        {
            case AnimationState.FindingTarget:
                ExecuteFindingTarget(handler);
                break;

            case AnimationState.MovingToTarget:
                ExecuteMovingToTarget(handler);
                break;

            case AnimationState.Farming:
                return ExecuteProcessing(handler);

            case AnimationState.Idle:
                ExecuteIdle(handler);
                break;
        }
        return false;
    }

    private void ExecuteFindingTarget(JobHandler handler)
    {
        _currentTarget = FindNextFarm(handler);
        if (_currentTarget == null)
        {
            currentStatus = "No farm available. Waiting...";
            handler.villagerMover.StopMoving();
            ChangeState(AnimationState.Idle, handler);
            return;
        }

        if (!HasRequiredResources())
        {
            currentStatus = $"Need {seedCost} seeds! Have: {VillageState.Instance?.Seeds ?? 0}. Waiting...";
            handler.villagerMover.StopMoving();
            ChangeState(AnimationState.Idle, handler);
            return;
        }

        _currentTarget.Reserve();
        handler.villagerMover.StopMoving();
        ChangeState(AnimationState.MovingToTarget, handler);
    }

    private void ExecuteMovingToTarget(JobHandler handler)
    {
        if (_currentTarget == null)
        {
            ChangeState(AnimationState.FindingTarget, handler);
            return;
        }

        currentStatus = "Moving to farm";
        handler.villagerMover.MoveTo(_currentTarget.transform.position);

        if (handler.villagerMover.IsNearDestination(stoppingDistance))
        {
            handler.villagerMover.StopMoving();

            if (!TryConsumeResources())
            {
                currentStatus = "Out of seeds! Waiting...";
                _currentTarget.Unreserve();
                _currentTarget = null;
                ChangeState(AnimationState.FindingTarget, handler);
                return;
            }

            ChangeState(AnimationState.Farming, handler);
        }
    }

    private bool ExecuteProcessing(JobHandler handler)
    {
        if (_currentTarget == null)
        {
            ChangeState(AnimationState.FindingTarget, handler);
            return false;
        }

        timeSinceLastAction += Time.deltaTime;
        currentStatus = $"Processing seeds ({timeSinceLastAction:F1}/{processTime:F1})...";

        if (timeSinceLastAction >= processTime)
        {
            if (VillageState.Instance != null)
            {
                VillageState.Instance.AddResource(ResourceType.Food, foodProduced);
                currentStatus = $"Produced {foodProduced} food!";
                Debug.Log($"[Farmer] Produced {foodProduced} food from {seedCost} seeds!");
            }

            _currentTarget.Unreserve();
            _currentTarget = null;
            ChangeState(AnimationState.FindingTarget, handler);
            return true;
        }
        return false;
    }

    private void ExecuteIdle(JobHandler handler)
    {
        timeSinceLastAction += Time.deltaTime;
        if (timeSinceLastAction >= 1f)
        {
            ChangeState(AnimationState.FindingTarget, handler);
        }
    }

    private bool HasRequiredResources()
    {
        if (VillageState.Instance == null) return true;
        return VillageState.Instance.HasResource(ResourceType.Seed, seedCost);
    }

    private bool TryConsumeResources()
    {
        if (VillageState.Instance == null) return true;
        return VillageState.Instance.TrySpendResource(ResourceType.Seed, seedCost);
    }

    private Building FindNextFarm(JobHandler handler)
    {
        var all = UnityEngine.Object.FindObjectsByType<Building>(FindObjectsSortMode.None);
        Building nearest = null;
        float bestDist = float.MaxValue;
        Vector3 origin = handler.transform.position;

        foreach (var b in all)
        {
            if (b == null) continue;
            if (b.IsReserved) continue;
            if (!b.IsFinished()) continue;
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

    public override void ResetState()
    {
        base.ResetState();
        _currentTarget = null;
    }
}