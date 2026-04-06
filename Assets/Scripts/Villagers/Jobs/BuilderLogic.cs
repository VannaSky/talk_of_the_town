using System;
using Buildings;
using Tiles;
using UnityEngine;
using AnimationState = Villagers.Jobs.AnimationState;

[Serializable]
public class BuilderLogic : JobLogic
{
    [Header("Builder Settings")]
    public float buildSpeed = 1f;
    public float stoppingDistance = 1.5f;

    [Header("Resource Costs (per build cycle)")]
    public int woodCostPerCycle = 2;
    public int stoneCostPerCycle = 1;

    [NonSerialized] private Building _currentTarget = null;
    [NonSerialized] private float _resourceCheckTimer = 0f;

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

            case AnimationState.Idle:
                ExecuteWaitingForResources(handler);
                break;

            case AnimationState.MovingToTarget:
                ExecuteMovingToTarget(handler);
                break;

            case AnimationState.Building:
                return ExecuteBuilding(handler);
        }
        return false;
    }

    private void ExecuteFindingTarget(JobHandler handler)
    {
        _currentTarget = FindNextBuilding(handler);
        if (_currentTarget != null)
        {
            if (HasRequiredResources())
            {
                _currentTarget.Reserve();
                handler.villagerMover.StopMoving();
                ChangeState(AnimationState.MovingToTarget, handler);
            }
            else
            {
                currentStatus = $"Need resources! Wood: {VillageState.Instance.Wood}/{woodCostPerCycle}, Stone: {VillageState.Instance.Stone}/{stoneCostPerCycle}";
                ChangeState(AnimationState.Idle, handler);
            }
        }
        else
        {
            currentStatus = "No building tasks available. Waiting...";
            handler.villagerMover.StopMoving();
            ChangeState(AnimationState.Idle, handler);
        }
    }

    private void ExecuteWaitingForResources(JobHandler handler)
    {
        timeSinceLastAction += Time.deltaTime;
        if (timeSinceLastAction >= 1f)
        {
            ChangeState(AnimationState.FindingTarget, handler);
        }
    }

    private void ExecuteMovingToTarget(JobHandler handler)
    {
        if (_currentTarget == null)
        {
            ChangeState(AnimationState.FindingTarget, handler);
            return;
        }

        var buildingName = GetBuildingName();
        currentStatus = $"Moving to build {buildingName}";
        handler.villagerMover.MoveTo(_currentTarget.transform.position);

        if (handler.villagerMover.IsNearDestination(stoppingDistance))
        {
            handler.villagerMover.StopMoving();
            _resourceCheckTimer = 0f;
            ChangeState(AnimationState.Building, handler);
        }
    }

    private bool ExecuteBuilding(JobHandler handler)
    {
        if (_currentTarget == null)
        {
            ChangeState(AnimationState.FindingTarget, handler);
            return false;
        }

        _resourceCheckTimer += Time.deltaTime;

        if (_resourceCheckTimer >= 1f)
        {
            _resourceCheckTimer = 0f;

            if (!TryConsumeResources())
            {
                currentStatus = "Out of resources! Waiting...";
                _currentTarget.Unreserve();
                _currentTarget = null;
                ChangeState(AnimationState.FindingTarget, handler);
                return false;
            }
        }

        float workApplied = buildSpeed * Time.deltaTime;
        bool levelCompleted = _currentTarget.AddWork(workApplied);

        var buildingName = GetBuildingName();
        currentStatus = $"Building {buildingName} ({_currentTarget.GetProgressPercent()}%)";

        if (levelCompleted)
        {
            int finishedLevelIndex = Mathf.Max(0, _currentTarget.currentLevel - 1);
            _currentTarget.ShowFinalForLevel(finishedLevelIndex);
            _currentTarget.Unreserve();

            bool finished = _currentTarget.IsFinished();
            if (finished)
            {
                Debug.Log($"[Builder] Completed building: {buildingName}");
            }

            _currentTarget = null;
            ChangeState(AnimationState.FindingTarget, handler);
            return true;
        }

        return false;
    }

    private bool HasRequiredResources()
    {
        if (VillageState.Instance == null) return true;
        return VillageState.Instance.HasResource(ResourceType.Wood, woodCostPerCycle)
            && VillageState.Instance.HasResource(ResourceType.Stone, stoneCostPerCycle);
    }

    private bool TryConsumeResources()
    {
        if (VillageState.Instance == null) return true;

        bool consumed = VillageState.Instance.TrySpendResource(ResourceType.Wood, woodCostPerCycle);
        if (consumed)
        {
            VillageState.Instance.TrySpendResource(ResourceType.Stone, stoneCostPerCycle);
        }
        return consumed;
    }

    private string GetBuildingName()
    {
        return _currentTarget?.buildingData != null
            ? _currentTarget.buildingData.buildingType.ToString()
            : "Building";
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

    public override void ResetState()
    {
        base.ResetState();
        _currentTarget = null;
        _resourceCheckTimer = 0f;
    }
}