using System;
using Tiles;
using Villagers.Jobs;
using AnimationState = Villagers.Jobs.AnimationState;

[Serializable]
public class MinerLogic : ResourceGatheringJobLogic
{
    protected override ResourceNode.ResourceType TargetResourceType => ResourceNode.ResourceType.Stone;
    protected override ResourceType DepositResourceType => ResourceType.Stone;
    protected override AnimationState WorkingAnimationState => AnimationState.Mining;
    protected override string WorkingVerb => "Mining stone";
    protected override string ResourceName => "stone";
}