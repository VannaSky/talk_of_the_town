using System;
using Environment.Resources;
using Tiles;
using Villagers.Jobs;
using AnimationState = Villagers.Jobs.AnimationState;

[Serializable]
public class LumberjackLogic : ResourceGatheringJobLogic
{
    protected override ResourceNode.ResourceType TargetResourceType => ResourceNode.ResourceType.Tree;
    protected override ResourceType DepositResourceType => ResourceType.Wood;
    protected override AnimationState WorkingAnimationState => AnimationState.Chopping;
    protected override string WorkingVerb => "Chopping wood";
    protected override string ResourceName => "wood";
}