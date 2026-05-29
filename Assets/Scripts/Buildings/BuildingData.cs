using System.Collections.Generic;
using Buildings;
using Tiles;
using UnityEngine;

public enum BuildingBonusType { NewVillager, InventoryCapacity, FieldCapacity }

[System.Serializable]
public class BuildingBonus
{
    public BuildingBonusType type;
    public int value = 1;
}

[CreateAssetMenu(fileName = "BuildingData", menuName = "Game/Building Data", order = 100)]
public class BuildingData : ScriptableObject
{
    public BuildingType buildingType = BuildingType.House;

    [Header("Construction")]
    public GameObject foundationPrefab;
    public ConstructionType constructionType = ConstructionType.Hut;

    [Header("Placement")]
    [Tooltip("Offset from tile origin when placing this building. Tweak at runtime to align the prefab.")]
    public Vector3 placementOffset = new Vector3(1f, 0.5f, 1f);

    [System.Serializable]
    public class LevelData
    {
        public List<GameObject> stagePrefabs = new List<GameObject>();
        public GameObject finalPrefab;
        public int workRequired = 10;

        [Header("Resource Cost for this Level")]
        public int woodCost = 5;
        public int stoneCost = 2;
        public int foodCost = 0;

        [Header("Bonuses on Completion")]
        public List<BuildingBonus> bonuses = new List<BuildingBonus>();
    }

    public List<LevelData> levels = new List<LevelData>();

    [Header("Villager Spawning (House only)")]
    [Tooltip("Seconds after completion before a villager appears")]
    public float villagerSpawnDelay = 5f;

    [Header("Farm Fields (Farm only)")]
    [Tooltip("World-unit radius around this farm in which farmers are allowed to plant fields")]
    public float fieldRadius = 8f;
}
