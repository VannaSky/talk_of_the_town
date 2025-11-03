using UnityEngine;
using System; // Make sure this is included for [Serializable]

[Serializable]
public class LumberjackLogic : JobLogic
{
    private enum State
    {
        Idle,
        FindingTarget,
        MovingToTarget,
        Chopping,
        Carrying
    }
    
    [Header("Lumberjack Settings")]
    public float timeToChop = 4f;
    public float timeToCarry = 2f;
    public float stoppingDistance = 1.5f;

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
                currentTarget = FindNextTree();
                if (currentTarget != null)
                {
                    currentTarget.Reserve();
                    currentState = State.MovingToTarget;
                }
                else
                {
                    currentStatus = "No trees found! Waiting...";
                }
                handler.villagerMover.StopMoving();
                break;

            case State.MovingToTarget:
                currentStatus = $"Moving to tree at {currentTarget.transform.position}";
                handler.villagerMover.MoveTo(currentTarget.transform.position);

                if (handler.villagerMover.IsNearDestination(stoppingDistance))
                {
                    handler.villagerMover.StopMoving();
                    currentState = State.Chopping;
                    timeSinceLastAction = 0f; 
                }
                break;
                
            case State.Chopping:
                timeSinceLastAction += Time.deltaTime;
                currentStatus = $"Chopping wood ({timeSinceLastAction:F1}/{timeToChop:F1})...";

                if (timeSinceLastAction >= timeToChop)
                {
                    currentTarget.Harvest();
                    timeSinceLastAction = 0f;
                    currentState = State.Carrying;
                }
                break;

            case State.Carrying:
                timeSinceLastAction += Time.deltaTime;
                currentStatus = $"Carrying wood ({timeSinceLastAction:F1}/{timeToCarry:F1})...";
                
                if (timeSinceLastAction >= timeToCarry)
                {
                    currentTarget.Unreserve();
                    handler.JobFinished();
                    currentState = State.FindingTarget;
                    return true;
                }
                break;
        }
        return false;
    }

    private ResourceNode FindNextTree()
    {
        ResourceNode[] allNodes = GameObject.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        foreach (var node in allNodes)
        {
            if (node.resourceType == ResourceNode.ResourceType.Tree && !node.isReserved)
            {
                return node;
            }
        }
        return null;
    }
}