using System;
using UnityEngine;

[Serializable]
public class BuilderLogic : JobLogic
{
    [Header("Builder Settings")]
    public float timeToBuild = 6f;
    public float timeToGatherMaterials = 3f;

    public override bool Execute(JobHandler jobHandler)
    {
        timeSinceLastAction += Time.deltaTime;

        if (timeSinceLastAction < timeToGatherMaterials)
        {
            currentStatus = "Gathering materials...";
        }
        else if (timeSinceLastAction < timeToGatherMaterials + timeToBuild)
        {
            currentStatus = "Building structure...";
        }
        else
        {
            currentStatus = "Finished building.";
            timeSinceLastAction = 0f;
            return true;
        }

        return false;
    }
}