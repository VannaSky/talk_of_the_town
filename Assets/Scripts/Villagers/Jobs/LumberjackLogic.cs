using System;
using UnityEngine;

[Serializable]
public class LumberjackLogic : JobLogic
{
    [Header("Lumberjack Settings")]
    public float timeToChop = 4f;
    public float timeToCarry = 2f;

    public override bool Execute(JobHandler jobHandler)
    {
        timeSinceLastAction += Time.deltaTime;

        if (timeSinceLastAction < timeToChop)
        {
            currentStatus = "Chopping wood...";
        }
        else if (timeSinceLastAction < timeToChop + timeToCarry)
        {
            currentStatus = "Carrying wood...";
        }
        else
        {
            currentStatus = "Finished gathering wood.";
            timeSinceLastAction = 0f;
            return true;
        }

        return false;
    }

    public override string GetCurrentStatus()
    {
        return currentStatus;
    }
}