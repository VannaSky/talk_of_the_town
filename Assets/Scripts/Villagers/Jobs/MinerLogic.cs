using UnityEngine;
using System;
using Tiles;

[Serializable]
public class MinerLogic : JobLogic
{
    private enum State
    {
        Idle,
        FindingTarget,
        MovingToTarget,
        Mining,
        Carrying
    }
    
    [Header("Miner Settings")]
    public float timeToMine = 5f;
    public float timeToCarry = 2f;
    public float stoppingDistance = 1.5f;
    public int stonePerDeposit = 3;

    private State currentState = State.Idle;
    private ResourceNode currentTarget = null;
    
    public override void OnJobStart(JobHandler handler)
    {
        currentState = State.FindingTarget;
        timeSinceLastAction = 0f;
    }

    public override bool Execute(JobHandler handler)
    {
        switch (currentState)
        {
            case State.FindingTarget:
                currentTarget = FindNextStone(handler);
                if (currentTarget != null)
                {
                    currentTarget.Reserve();
                    currentState = State.MovingToTarget;
                }
                else
                {
                    currentStatus = "No stone deposits found! Waiting...";
                }
                handler.villagerMover.StopMoving();
                break;

            case State.MovingToTarget:
                if (currentTarget == null)
                {
                    currentState = State.FindingTarget;
                    break;
                }
                
                currentStatus = $"Moving to stone at {currentTarget.transform.position}";
                handler.villagerMover.MoveTo(currentTarget.transform.position);

                if (handler.villagerMover.IsNearDestination(stoppingDistance))
                {
                    handler.villagerMover.StopMoving();
                    currentState = State.Mining;
                    timeSinceLastAction = 0f; 
                }
                break;
                
            case State.Mining:
                if (currentTarget == null)
                {
                    currentState = State.FindingTarget;
                    break;
                }
                
                timeSinceLastAction += Time.deltaTime;
                currentStatus = $"Mining stone ({timeSinceLastAction:F1}/{timeToMine:F1})...";

                if (timeSinceLastAction >= timeToMine)
                {
                    currentTarget.Harvest();
                    timeSinceLastAction = 0f;
                    currentState = State.Carrying;
                }
                break;

            case State.Carrying:
                timeSinceLastAction += Time.deltaTime;
                currentStatus = $"Carrying stone ({timeSinceLastAction:F1}/{timeToCarry:F1})...";
                
                if (timeSinceLastAction >= timeToCarry)
                {
                    // Deposit stone to village inventory
                    if (VillageState.Instance != null)
                    {
                        VillageState.Instance.AddResource(ResourceType.Stone, stonePerDeposit);
                        currentStatus = $"Deposited {stonePerDeposit} stone!";
                    }
                    
                    currentTarget.Unreserve();
                    currentState = State.FindingTarget;
                    return true;  // Job complete, gain XP
                }
                break;
        }
        return false;
    }

    private ResourceNode FindNextStone(JobHandler handler)
    {
        ResourceNode[] allNodes = GameObject.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        ResourceNode nearest = null;
        float bestDist = float.MaxValue;
        Vector3 origin = handler.transform.position;
        
        foreach (var node in allNodes)
        {
            if (node == null) continue;
            if (node.resourceType != ResourceNode.ResourceType.Stone) continue;  // Rock in ResourceNode
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