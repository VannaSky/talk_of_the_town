using System;
using UnityEngine;

[Serializable]
public class BuilderLogic : JobLogic
{
    public float buildSpeed = 1f;
    public float stoppingDistance = 1.5f;

    private enum State { Idle, FindingTarget, MovingToTarget, Building }
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
                    currentTarget.Reserve();
                    currentState = State.MovingToTarget;
                    handler.villagerMover.StopMoving();
                }
                else
                {
                    currentStatus = "No building tasks available. Waiting...";
                    handler.villagerMover.StopMoving();
                }
                break;

            case State.MovingToTarget:
                if (currentTarget == null)
                {
                    currentState = State.FindingTarget;
                    break;
                }

                var bt = currentTarget.buildingData != null ? currentTarget.buildingData.buildingType.ToString() : "Building";
                currentStatus = $"Moving to build {bt} at {currentTarget.transform.position}";
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
                float workApplied = buildSpeed * Time.deltaTime;
                bool levelCompleted = currentTarget.AddWork(workApplied);

                var bt2 = currentTarget.buildingData != null ? currentTarget.buildingData.buildingType.ToString() : "Building";
                currentStatus = $"Building {bt2} ({currentTarget.GetProgressPercent()}%)";

                if (levelCompleted)
                {
                    int finishedLevelIndex = Mathf.Max(0, currentTarget.currentLevel - 1);
                    currentTarget.ShowFinalForLevel(finishedLevelIndex);

                    if (currentTarget.IsFinished())
                    {
                        currentTarget.Unreserve();
                        currentState = State.FindingTarget;
                        handler.villagerMover.StopMoving();
                        return true;
                    }
                    else
                    {
                        currentTarget.Unreserve();
                        currentState = State.FindingTarget;
                        handler.villagerMover.StopMoving();
                        return true;
                    }
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