using System;
using Environment.Resources;
using Tiles;
using UnityEngine;

namespace Villagers.Jobs
{
    [Serializable]
    public abstract class ResourceGatheringJobLogic : JobLogic
    {
        [Header("Gathering Settings")]
        public float timeToWork = 5f;
        public float timeToCarry = 2f;
        public float stoppingDistance = 1.5f;
        public int resourcePerNode = 3;

        [Header("Target Area")]
        [Tooltip("How strongly to prefer the LLM's target area (higher = stricter)")]
        public float targetAreaWeight = 2f;

        [Tooltip("Max distance from target area to search")]
        public float targetAreaRadius = 15f;

        [Header("Debug")]
        public bool debugResourceSearch = false;

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
                case AnimationState.Gathering:
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
            if (VillageState.Instance != null &&
                VillageState.Instance.GetResource(DepositResourceType) >= VillageState.Instance.InventoryCapacity)
            {
                currentStatus = $"Storage full ({ResourceName})";
                ChangeState(AnimationState.Idle, handler);
                return;
            }

            _currentTarget = FindBestResource(handler);
            if (_currentTarget != null)
            {
                handler.villagerMover.StopMoving();
                LogInfo($"Found {ResourceName} at {_currentTarget.transform.position}");
                ChangeState(AnimationState.MovingToTarget, handler);
            }
            else
            {
                currentStatus = $"No {ResourceName} found! Waiting...";
                if (debugResourceSearch)
                {
                    LogResourceSearchDebug(handler);
                }
                ChangeState(AnimationState.Idle, handler);
            }
        }

        private void LogResourceSearchDebug(JobHandler handler)
        {
            ResourceNode[] allNodes = GameObject.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            LogVerbose($"DEBUG: Looking for {TargetResourceType}, found {allNodes.Length} total ResourceNodes");

            int matchingType = 0;
            int reserved = 0;
            int available = 0;

            foreach (var node in allNodes)
            {
                if (node == null) continue;

                bool isCorrectType = node.resourceType == TargetResourceType;
                bool isReserved = node.isReserved;
                bool isActive = node.gameObject.activeInHierarchy;

                if (isCorrectType) matchingType++;
                if (isCorrectType && isReserved) reserved++;
                if (isCorrectType && !isReserved && isActive) available++;

                if (debugResourceSearch)
                {
                    LogVerbose($"  - {node.name}: type={node.resourceType}, reserved={isReserved}, active={isActive}, match={isCorrectType}");
                }
            }

            LogVerbose($"DEBUG: {matchingType} matching type, {reserved} reserved, {available} available");
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

        private ResourceNode FindBestResource(JobHandler handler)
        {
            // Find ALL ResourceNodes, including inactive parents with active children
            ResourceNode[] allNodes = GameObject.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
        
            if (debugResourceSearch)
            {
                LogVerbose($"Searching for {TargetResourceType} among {allNodes.Length} nodes");
            }

            ResourceNode best = null;
            float bestScore = float.MaxValue;

            Vector3 villagerPos = handler.transform.position;
            Vector3? targetAreaWorld = null;

            if (handler.PreferredTargetArea.HasValue)
            {
                var target = handler.PreferredTargetArea.Value;
                targetAreaWorld = GridToWorld(target);
            }

            foreach (var node in allNodes)
            {
                if (node == null) continue;
            
                // Check if this node matches our target type
                if (node.resourceType != TargetResourceType)
                {
                    if (debugResourceSearch)
                        LogVerbose($"  Skip {node.name}: wrong type ({node.resourceType} != {TargetResourceType})");
                    continue;
                }

                // Check if reserved
                if (node.isReserved)
                {
                    if (debugResourceSearch)
                        LogVerbose($"  Skip {node.name}: reserved");
                    continue;
                }

                // Check if mature (skip regrowing nodes)
                if (!node.IsMature)
                {
                    if (debugResourceSearch)
                        LogVerbose($"  Skip {node.name}: not mature ({node.growthStage})");
                    continue;
                }

                // Check if active
                if (!node.gameObject.activeInHierarchy)
                {
                    if (debugResourceSearch)
                        LogVerbose($"  Skip {node.name}: inactive");
                    continue;
                }

                float distanceToVillager = Vector3.Distance(villagerPos, node.transform.position);
                float score = distanceToVillager;

                if (targetAreaWorld.HasValue)
                {
                    float distanceToTarget = Vector3.Distance(targetAreaWorld.Value, node.transform.position);

                    if (distanceToTarget > targetAreaRadius)
                    {
                        if (debugResourceSearch)
                            LogVerbose($"  Skip {node.name}: too far from target ({distanceToTarget:F1} > {targetAreaRadius})");
                        continue;
                    }

                    score = distanceToVillager + (distanceToTarget * targetAreaWeight);
                }

                if (debugResourceSearch)
                    LogVerbose($"  Consider {node.name}: score={score:F1}");

                if (score < bestScore)
                {
                    bestScore = score;
                    best = node;
                }
            }

            if (best == null && targetAreaWorld.HasValue)
            {
                LogInfo($"No {ResourceName} near target area, falling back to nearest anywhere");
                return FindNearestResourceAnywhere(handler);
            }

            best?.Reserve();
            return best;
        }

        private ResourceNode FindNearestResourceAnywhere(JobHandler handler)
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
                if (!node.IsMature) continue;
                if (!node.gameObject.activeInHierarchy) continue;

                float d = Vector3.Distance(origin, node.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    nearest = node;
                }
            }
            nearest?.Reserve();
            return nearest;
        }

        private Vector3 GridToWorld(Vector2Int gridPos, float cellSize = 2f)
        {
            float x = gridPos.x * cellSize + cellSize / 2f;
            float z = gridPos.y * cellSize + cellSize / 2f;
            return new Vector3(x, 0f, z);
        }

        public override void ResetState()
        {
            _currentTarget?.Unreserve();
            base.ResetState();
            _currentTarget = null;
        }
    }
}