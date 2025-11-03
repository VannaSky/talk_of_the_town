using UnityEngine;

public class ResourceNode : MonoBehaviour
{
    public enum ResourceType
    {
        Tree,
        Rock,
        Water,
    }

    public ResourceType resourceType = ResourceType.Tree;
    public int resourceAmount = 5;

    public bool isReserved = false;

    public void Reserve() => isReserved = true;
    public void Unreserve() => isReserved = false;

    public void Harvest()
    {
        Destroy(gameObject);
    }
}