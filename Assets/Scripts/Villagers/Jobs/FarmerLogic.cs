using System;
using Buildings;
using UnityEngine;

[Serializable]
public class FarmerLogic : JobLogic
{
    [Header("Farmer Settings")]
    public float workSpeed = 1f;
    public float stoppingDistance = 1.5f;

    private enum State { Idle, FindingTarget, MovingToTarget, Working }
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
                currentTarget = FindNextFarm(handler);
                if (currentTarget != null)
                {
                    currentTarget.Reserve();
                    currentState = State.MovingToTarget;
                    handler.villagerMover.StopMoving();
                }
                else
                {
                    currentStatus = "No farm tasks available. Waiting...";
                    handler.villagerMover.StopMoving();
                }
                break;

            case State.MovingToTarget:
                if (currentTarget == null)
                {
                    currentState = State.FindingTarget;
                    break;
                }

                currentStatus = $"Moving to farm at {currentTarget.transform.position}";
                handler.villagerMover.MoveTo(currentTarget.transform.position);

                if (handler.villagerMover.IsNearDestination(stoppingDistance))
                {
                    handler.villagerMover.StopMoving();
                    currentState = State.Working;
                    timeSinceLastAction = 0f;
                }
                break;

            case State.Working:
                if (currentTarget == null)
                {
                    currentState = State.FindingTarget;
                    break;
                }

                timeSinceLastAction += Time.deltaTime;
                float workApplied = workSpeed * Time.deltaTime;
                bool levelCompleted = currentTarget.AddWork(workApplied);

                var bt = currentTarget.buildingData != null ? currentTarget.buildingData.buildingType.ToString() : "Farm";
                currentStatus = $"Working on {bt} ({currentTarget.GetProgressPercent()}%)";

                if (levelCompleted)
                {
                    int finishedLevelIndex = Mathf.Max(0, currentTarget.currentLevel - 1);
                    currentTarget.Unreserve();
                    currentState = State.FindingTarget;
                    handler.villagerMover.StopMoving();
                    return true;
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
}