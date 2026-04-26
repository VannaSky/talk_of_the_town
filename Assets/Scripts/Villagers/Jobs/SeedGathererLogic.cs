using System;
using Environment.Resources;
using Tiles;
using Villagers.Jobs;
using AnimationState = Villagers.Jobs.AnimationState;

[Serializable]
public class SeedGathererLogic : ResourceGatheringJobLogic
{
    protected override ResourceNode.ResourceType TargetResourceType => ResourceNode.ResourceType.Seed;
    protected override ResourceType DepositResourceType => ResourceType.Seed;
    protected override AnimationState WorkingAnimationState => AnimationState.Gathering;
    protected override string WorkingVerb => "Gathering seeds";
    protected override string ResourceName => "seeds";
}