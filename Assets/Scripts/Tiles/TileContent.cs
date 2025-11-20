using Tiles;


public sealed class ResourceInstance
{
    public ResourceType Type { get; }
    public int Amount { get; private set; }
    public float AddedMoveCost { get; }
    public bool AllowsWalkThrough { get; }

    public ResourceInstance(ResourceType type, int amount = 1, float addedCost = 0f, bool walkThrough = true)
    { Type = type; Amount = amount; AddedMoveCost = addedCost; AllowsWalkThrough = walkThrough; }
}

public sealed class ConstructionInstance
{
    public ConstructionType Type { get; }
    public float AddedMoveCost { get; }
    public bool BlocksWalk { get; }

    public ConstructionInstance(ConstructionType type, float addedCost = 0f, bool blocks = true)
    { Type = type; AddedMoveCost = addedCost; BlocksWalk = blocks; }
}