using System.Collections.Generic;
using Buildings;
using UnityEngine;

[CreateAssetMenu(fileName = "BuildingData", menuName = "Game/Building Data", order = 100)]
public class BuildingData : ScriptableObject
{
    public BuildingType buildingType = BuildingType.House;

    [System.Serializable]
    public class LevelData
    {
        public List<GameObject> stagePrefabs = new List<GameObject>();
        public GameObject finalPrefab;
        public int workRequired = 10;
    }

    public List<LevelData> levels = new List<LevelData>();
}
