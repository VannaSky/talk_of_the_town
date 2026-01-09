using UnityEngine;
using System;
using Tiles;
using AnimationState = Villagers.Jobs.AnimationState;

// For ResourceType

[Serializable]
public class LumberjackLogic : JobLogic
{
 
    
    [Header("Lumberjack Settings")]
    public float timeToChop = 4f;
    public float timeToCarry = 2f;
    public float stoppingDistance = 1.5f;
    public int woodPerTree = 5;

    [Header("Animation")]
    [Tooltip("Animator int parameter that mirrors the lumberjack state.")]
    public string animatorStateParameter = "AnimationState";

    private AnimationState currentState = AnimationState.Idle;
    private ResourceNode currentTarget = null;
    
    public override void OnJobStart(JobHandler handler)
    {
        timeSinceLastAction = 0f;
        ChangeState(AnimationState.FindingTarget, handler);
    }

    public override bool Execute(JobHandler handler)
    {
        switch (currentState)
        {
            case AnimationState.FindingTarget:
                currentTarget = FindNextTree(handler);
                if (currentTarget != null)
                {
                    currentTarget.Reserve();
                    handler.villagerMover.StopMoving();
                    ChangeState(AnimationState.MovingToTarget, handler);
                }
                else
                {
                    currentStatus = "No trees found! Waiting...";
                    // Optionally go to Idle animation while waiting
                    ChangeState(AnimationState.Idle, handler);
                }
                break;

            case AnimationState.MovingToTarget:
                if (currentTarget == null)
                {
                    ChangeState(AnimationState.FindingTarget, handler);
                    break;
                }
                
                currentStatus = $"Moving to tree at {currentTarget.transform.position}";
                handler.villagerMover.MoveTo(currentTarget.transform.position);

                if (handler.villagerMover.IsNearDestination(stoppingDistance))
                {
                    handler.villagerMover.StopMoving();
                    ChangeState(AnimationState.Chopping, handler);
                }
                break;
                
            case AnimationState.Chopping:
                if (currentTarget == null)
                {
                    ChangeState(AnimationState.FindingTarget, handler);
                    break;
                }
                
                timeSinceLastAction += Time.deltaTime;
                currentStatus = $"Chopping wood ({timeSinceLastAction:F1}/{timeToChop:F1})...";

                if (timeSinceLastAction >= timeToChop)
                {
                    currentTarget.Harvest();
                    ChangeState(AnimationState.Carrying, handler);
                }
                break;

            case AnimationState.Carrying:
                timeSinceLastAction += Time.deltaTime;
                currentStatus = $"Carrying wood ({timeSinceLastAction:F1}/{timeToCarry:F1})...";
                
                if (timeSinceLastAction >= timeToCarry)
                {
                    if (VillageState.Instance != null)
                    {
                        VillageState.Instance.AddResource(ResourceType.Wood, woodPerTree);
                        currentStatus = $"Deposited {woodPerTree} wood!";
                    }
                    
                    if (currentTarget != null)
                        currentTarget.Unreserve();

                    ChangeState(AnimationState.FindingTarget, handler);
                    return true;  // Job complete, gain XP
                }
                break;
        }
        return false;
    }

    // Central place for state changes + animation sync
    private void ChangeState(AnimationState newState, JobHandler handler)
    {
        if (currentState == newState)
            return;

        currentState = newState;
        timeSinceLastAction = 0f;

        // Update Animator
        if (handler != null && handler.animator != null && !string.IsNullOrEmpty(animatorStateParameter))
        {
            handler.animator.SetInteger(animatorStateParameter, (int)currentState);
        }
    }

    private ResourceNode FindNextTree(JobHandler handler)
    {
        ResourceNode[] allNodes = GameObject.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        ResourceNode nearest = null;
        float bestDist = float.MaxValue;
        Vector3 origin = handler.transform.position;
        
        foreach (var node in allNodes)
        {
            if (node == null) continue;
            if (node.resourceType != ResourceNode.ResourceType.Tree) continue;
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
}
