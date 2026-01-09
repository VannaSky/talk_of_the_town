using UnityEngine;
using System;
using Tiles;
using AnimationState = Villagers.Jobs.AnimationState;

[Serializable]
public abstract class ResourceGatheringJobLogic : JobLogic
{
    [Header("Gathering Settings")]
    public float timeToWork = 5f;
    public float timeToCarry = 2f;
    public float stoppingDistance = 1.5f;
    public int resourcePerNode = 3;

    [NonSerialized] protected ResourceNode _currentTarget = null;

    protected abstract ResourceNode.ResourceType TargetResourceType { get; }
    protected abstract ResourceType DepositResourceType { get; }
    protected abstract AnimationState WorkingAnimationState { get; }
    protected abstract string WorkingVerb { get; }
    protected abstract string ResourceName { get; }

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

            case AnimationState.Chopping:
            case AnimationState.Mining:
                ExecuteWorking(handler);
                break;

            case AnimationState.Carrying:
                return ExecuteCarrying(handler);

            case AnimationState.Idle:
                ExecuteIdle(handler);
                break;
        }
        return false;
    }

    private void ExecuteFindingTarget(JobHandler handler)
    {
        _currentTarget = FindNearestResource(handler);
        if (_currentTarget != null)
        {
            _currentTarget.Reserve();
            handler.villagerMover.StopMoving();
            ChangeState(AnimationState.MovingToTarget, handler);
        }
        else
        {
            currentStatus = $"No {ResourceName} found! Waiting...";
            ChangeState(AnimationState.Idle, handler);
        }
    }

    private void ExecuteMovingToTarget(JobHandler handler)
    {
        if (_currentTarget == null)
        {
            ChangeState(AnimationState.FindingTarget, handler);
            return;
        }

        currentStatus = $"Moving to {ResourceName} at {_currentTarget.transform.position}";
        handler.villagerMover.MoveTo(_currentTarget.transform.position);

        if (handler.villagerMover.IsNearDestination(stoppingDistance))
        {
            handler.villagerMover.StopMoving();
            ChangeState(WorkingAnimationState, handler);
        }
    }

    private void ExecuteWorking(JobHandler handler)
    {
        if (_currentTarget == null)
        {
            ChangeState(AnimationState.FindingTarget, handler);
            return;
        }

        timeSinceLastAction += Time.deltaTime;
        currentStatus = $"{WorkingVerb} ({timeSinceLastAction:F1}/{timeToWork:F1})...";

        if (timeSinceLastAction >= timeToWork)
        {
            _currentTarget.Harvest();
            ChangeState(AnimationState.Carrying, handler);
        }
    }

    private bool ExecuteCarrying(JobHandler handler)
    {
        timeSinceLastAction += Time.deltaTime;
        currentStatus = $"Carrying {ResourceName} ({timeSinceLastAction:F1}/{timeToCarry:F1})...";

        if (timeSinceLastAction >= timeToCarry)
        {
            if (VillageState.Instance != null)
            {
                VillageState.Instance.AddResource(DepositResourceType, resourcePerNode);
                currentStatus = $"Deposited {resourcePerNode} {ResourceName}!";
            }

            if (_currentTarget != null)
                _currentTarget.Unreserve();

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

    private ResourceNode FindNearestResource(JobHandler handler)
    {
        ResourceNode[] allNodes = GameObject.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        ResourceNode nearest = null;
        float bestDist = float.MaxValue;
        Vector3 origin = handler.transform.position;

        foreach (var node in allNodes)
        {
            if (node == null) continue;
            if (node.resourceType != TargetResourceType) continue;
            if (node.isReserved) continue;

            float d = Vector3.Distance(origin, node.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                nearest = node;
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