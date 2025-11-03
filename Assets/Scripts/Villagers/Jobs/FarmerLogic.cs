using System;
using UnityEngine;

[Serializable]
public class FarmerLogic : JobLogic
{
    [Header("Farmer Settings")]
    public float timeToWater = 3f;
    public float timeToHarvest = 5f;

    public override bool Execute(JobHandler jobHandler)
    {
        timeSinceLastAction += Time.deltaTime;

        if (timeSinceLastAction < timeToWater)
        {
            currentStatus = "Watering crops...";
        }
        else if (timeSinceLastAction < timeToWater + timeToHarvest)
        {
            currentStatus = "Harvesting crops...";
        }
        else
        {
            currentStatus = "Finished tending crops.";
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