using UnityEngine;

public class ResourceNode : MonoBehaviour
{
    public enum ResourceType
    {
        Tree,
        Stone,
        Seed,
        Water,
        Crop,
    }

    public enum GrowthStage { Seedling, Growing, Mature }

    public ResourceType resourceType = ResourceType.Tree;
    public int resourceAmount = 5;

    public bool isReserved = false;

    [Header("Growth")]
    public GrowthStage growthStage = GrowthStage.Mature;
    public bool canRegrow = false;
    public float growthTime = 30f;
    public float currentGrowthTimer = 0f;
    [Tooltip("Resource amount to restore on regrow to Mature")]
    public int defaultResourceAmount = 5;

    [Header("Stage Visuals (optional)")]
    public GameObject seedlingVisual;
    public GameObject growingVisual;
    public GameObject matureVisual;

    public bool IsMature => growthStage == GrowthStage.Mature;

    void Start()
    {
        if (defaultResourceAmount <= 0)
            defaultResourceAmount = resourceAmount;
        UpdateVisuals();
    }

    void Update()
    {
        if (growthStage == GrowthStage.Mature) return;

        currentGrowthTimer += Time.deltaTime;
        if (currentGrowthTimer >= growthTime)
        {
            currentGrowthTimer = 0f;
            if (growthStage == GrowthStage.Seedling)
                growthStage = GrowthStage.Growing;
            else if (growthStage == GrowthStage.Growing)
                growthStage = GrowthStage.Mature;

            UpdateVisuals();
        }
    }

    public void Reserve() => isReserved = true;
    public void Unreserve() => isReserved = false;

    public void Harvest()
    {
        if (canRegrow)
        {
            growthStage = GrowthStage.Seedling;
            currentGrowthTimer = 0f;
            resourceAmount = defaultResourceAmount;
            isReserved = false;
            UpdateVisuals();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void UpdateVisuals()
    {
        if (seedlingVisual == null && growingVisual == null && matureVisual == null)
            return;

        if (seedlingVisual != null) seedlingVisual.SetActive(growthStage == GrowthStage.Seedling);
        if (growingVisual != null) growingVisual.SetActive(growthStage == GrowthStage.Growing);
        if (matureVisual != null) matureVisual.SetActive(growthStage == GrowthStage.Mature);
    }
}
