using System;
using System.Collections.Generic;
using Tiles;
using UnityEngine;
using AnimationState = Villagers.Jobs.AnimationState;

[Serializable]
public class FarmerLogic : JobLogic
{
    [Header("Farmer Settings")]
    public float workSpeed = 1f;
    public float stoppingDistance = 1.5f;
    public float plantTime = 3f;
    public float harvestTime = 5f;
    public int seedCost = 2;
    public int foodProduced = 5;
    public int searchRadius = 15;

    [Header("Crop Prefab")]
    public GameObject cropPrefab;

    private enum FarmerPhase { Planting, Harvesting }

    [NonSerialized] private FarmerPhase _phase;
    [NonSerialized] private Tile _targetTile;
    [NonSerialized] private ResourceNode _targetCrop;

    protected override void OnInitialize(JobHandler handler)
    {
        _targetTile = null;
        _targetCrop = null;
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

            case AnimationState.Planting:
                return ExecutePlanting(handler);

            case AnimationState.Farming:
                return ExecuteHarvesting(handler);

            case AnimationState.Idle:
                ExecuteIdle(handler);
                break;
        }
        return false;
    }

    private void ExecuteFindingTarget(JobHandler handler)
    {
        // Phase 1: Look for mature crops to harvest
        _targetCrop = FindMatureCrop(handler);
        if (_targetCrop != null)
        {
            _phase = FarmerPhase.Harvesting;
            _targetCrop.Reserve();
            handler.villagerMover.StopMoving();
            Debug.Log($"[Farmer] Found mature crop at {_targetCrop.transform.position}");
            ChangeState(AnimationState.MovingToTarget, handler);
            return;
        }

        // Phase 2: If no mature crops, try planting (needs seeds)
        if (HasRequiredSeeds())
        {
            _targetTile = FindEmptyGrassTile(handler);
            if (_targetTile != null)
            {
                _phase = FarmerPhase.Planting;
                handler.villagerMover.StopMoving();
                Debug.Log($"[Farmer] Found tile to plant at {_targetTile.GridPos}");
                ChangeState(AnimationState.MovingToTarget, handler);
                return;
            }
        }

        currentStatus = _targetCrop == null && !HasRequiredSeeds()
            ? $"Need {seedCost} seeds! Have: {VillageState.Instance?.Seeds ?? 0}. Waiting..."
            : "No crops or planting spots. Waiting...";
        handler.villagerMover.StopMoving();
        ChangeState(AnimationState.Idle, handler);
    }

    private void ExecuteMovingToTarget(JobHandler handler)
    {
        Vector3 destination;

        if (_phase == FarmerPhase.Harvesting)
        {
            if (_targetCrop == null)
            {
                ChangeState(AnimationState.FindingTarget, handler);
                return;
            }
            destination = _targetCrop.transform.position;
            currentStatus = $"Moving to crop at {destination}";
        }
        else
        {
            if (_targetTile == null)
            {
                ChangeState(AnimationState.FindingTarget, handler);
                return;
            }
            destination = _targetTile.transform.position;
            currentStatus = $"Moving to plant at {_targetTile.GridPos}";
        }

        handler.villagerMover.MoveTo(destination);

        if (handler.villagerMover.IsNearDestination(stoppingDistance))
        {
            handler.villagerMover.StopMoving();

            if (_phase == FarmerPhase.Planting)
                ChangeState(AnimationState.Planting, handler);
            else
                ChangeState(AnimationState.Farming, handler);
        }
    }

    private bool ExecutePlanting(JobHandler handler)
    {
        if (_targetTile == null)
        {
            ChangeState(AnimationState.FindingTarget, handler);
            return false;
        }

        timeSinceLastAction += Time.deltaTime;
        currentStatus = $"Planting ({timeSinceLastAction:F1}/{plantTime:F1})...";

        if (timeSinceLastAction >= plantTime)
        {
            if (!TryConsumeSeeds())
            {
                currentStatus = "Out of seeds!";
                _targetTile = null;
                ChangeState(AnimationState.FindingTarget, handler);
                return false;
            }

            // Instantiate crop on the tile
            if (cropPrefab != null)
            {
                var cropGO = UnityEngine.Object.Instantiate(
                    cropPrefab,
                    _targetTile.transform.position,
                    Quaternion.identity,
                    _targetTile.transform
                );
                cropGO.name = $"Crop_{_targetTile.GridPos.x}_{_targetTile.GridPos.y}";

                var node = cropGO.GetComponent<ResourceNode>();
                if (node != null)
                {
                    node.resourceType = ResourceNode.ResourceType.Crop;
                    node.canRegrow = true;
                    node.growthStage = ResourceNode.GrowthStage.Seedling;
                    node.currentGrowthTimer = 0f;
                }
            }

            currentStatus = "Planted a crop!";
            Debug.Log($"[Farmer] Planted crop at {_targetTile.GridPos}");
            _targetTile = null;
            ChangeState(AnimationState.FindingTarget, handler);
            return true;
        }
        return false;
    }

    private bool ExecuteHarvesting(JobHandler handler)
    {
        if (_targetCrop == null)
        {
            ChangeState(AnimationState.FindingTarget, handler);
            return false;
        }

        timeSinceLastAction += Time.deltaTime;
        currentStatus = $"Harvesting ({timeSinceLastAction:F1}/{harvestTime:F1})...";

        if (timeSinceLastAction >= harvestTime)
        {
            _targetCrop.Harvest(); // resets to Seedling via regrowth system

            if (VillageState.Instance != null)
            {
                VillageState.Instance.AddResource(ResourceType.Food, foodProduced);
                currentStatus = $"Harvested {foodProduced} food!";
                Debug.Log($"[Farmer] Harvested {foodProduced} food!");
            }

            _targetCrop = null;
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

    private ResourceNode FindMatureCrop(JobHandler handler)
    {
        var allNodes = UnityEngine.Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        ResourceNode best = null;
        float bestDist = float.MaxValue;
        Vector3 origin = handler.transform.position;

        Vector3? targetAreaWorld = null;
        if (handler.PreferredTargetArea.HasValue)
        {
            var target = handler.PreferredTargetArea.Value;
            targetAreaWorld = GridToWorld(target);
        }

        foreach (var node in allNodes)
        {
            if (node == null) continue;
            if (node.resourceType != ResourceNode.ResourceType.Crop) continue;
            if (!node.IsMature) continue;
            if (node.isReserved) continue;
            if (!node.gameObject.activeInHierarchy) continue;

            float dist = Vector3.Distance(origin, node.transform.position);

            if (targetAreaWorld.HasValue)
            {
                float distToTarget = Vector3.Distance(targetAreaWorld.Value, node.transform.position);
                dist += distToTarget * 2f;
            }

            if (dist < bestDist)
            {
                bestDist = dist;
                best = node;
            }
        }
        return best;
    }

    private Tile FindEmptyGrassTile(JobHandler handler)
    {
        if (VillageState.Instance == null || VillageState.Instance.TileGrid == null)
            return null;

        Vector3 origin = handler.transform.position;
        Vector2Int centerGrid = WorldToGrid(origin);

        Vector2Int? preferredCenter = handler.PreferredTargetArea;
        if (preferredCenter.HasValue)
            centerGrid = preferredCenter.Value;

        var candidates = VillageState.Instance.TileGrid.FindTilesInRadius(
            centerGrid, searchRadius,
            tile => tile.Archetype != null
                    && (tile.Archetype.Style == TileStyle.Grass || tile.Archetype.Style == TileStyle.Field)
                    && !tile.HasBuilding
                    && !HasCropOnTile(tile)
        );

        if (candidates.Count == 0) return null;

        // Pick the closest one
        Tile best = null;
        float bestDist = float.MaxValue;
        foreach (var tile in candidates)
        {
            float d = Vector3.Distance(origin, tile.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = tile;
            }
        }
        return best;
    }

    private bool HasCropOnTile(Tile tile)
    {
        // Check if any ResourceNode (Crop) already exists as a child of the tile
        var nodes = tile.GetComponentsInChildren<ResourceNode>();
        foreach (var node in nodes)
        {
            if (node.resourceType == ResourceNode.ResourceType.Crop)
                return true;
        }
        return false;
    }

    private bool HasRequiredSeeds()
    {
        if (VillageState.Instance == null) return true;
        return VillageState.Instance.HasResource(ResourceType.Seed, seedCost);
    }

    private bool TryConsumeSeeds()
    {
        if (VillageState.Instance == null) return true;
        return VillageState.Instance.TrySpendResource(ResourceType.Seed, seedCost);
    }

    private Vector3 GridToWorld(Vector2Int gridPos, float cellSize = 2f)
    {
        float x = gridPos.x * cellSize + cellSize / 2f;
        float z = gridPos.y * cellSize + cellSize / 2f;
        return new Vector3(x, 0f, z);
    }

    private Vector2Int WorldToGrid(Vector3 worldPos, float cellSize = 2f)
    {
        int x = Mathf.FloorToInt(worldPos.x / cellSize);
        int z = Mathf.FloorToInt(worldPos.z / cellSize);
        return new Vector2Int(x, z);
    }

    public override void ResetState()
    {
        base.ResetState();
        _targetTile = null;
        _targetCrop = null;
    }
}
