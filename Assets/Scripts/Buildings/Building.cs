using System.Collections.Generic;
using UnityEngine;

public enum BuildingType
{
    House,
    Stockpile,
    Farm
}

[System.Serializable]
public class BuildingLevelVisual
{
    public List<GameObject> buildStages = new List<GameObject>();
    public GameObject finalObject;
    public int workRequired = 10;
}

public class Building : MonoBehaviour
{
    public BuildingData buildingData;

    [HideInInspector]
    public List<BuildingLevelVisual> levels = new List<BuildingLevelVisual>();

    public int currentLevel = 0;
    public float currentWork = 0f;

    private bool isReserved = false;
    public bool IsReserved => isReserved;
    public void Reserve() => isReserved = true;
    public void Unreserve() => isReserved = false;

    private class RuntimeLevel
    {
        public List<GameObject> stageInstances = new List<GameObject>();
        public GameObject finalInstance;
        public int workRequired;
    }

    private List<RuntimeLevel> runtimeLevels = new List<RuntimeLevel>();

    public bool IsFinished()
    {
        return currentLevel >= runtimeLevels.Count;
    }

    public bool AddWork(float amount)
    {
        if (IsFinished()) return false;
        var level = runtimeLevels[currentLevel];
        currentWork += amount;
        bool levelCompleted = false;
        if (currentWork >= level.workRequired)
        {
            currentWork -= level.workRequired;
            int finishedLevel = currentLevel;
            currentLevel++;
            levelCompleted = true;
            ShowFinalForLevel(finishedLevel);
        }
        UpdateVisuals();
        return levelCompleted;
    }

    public float GetProgressFraction()
    {
        if (IsFinished()) return 1f;
        var level = runtimeLevels[currentLevel];
        if (level.workRequired <= 0) return 0f;
        return Mathf.Clamp01(currentWork / level.workRequired);
    }

    public int GetProgressPercent() => Mathf.RoundToInt(GetProgressFraction() * 100f);

    private void Awake()
    {
        BuildRuntimeLevels();
    }

    private void BuildRuntimeLevels()
    {
        runtimeLevels.Clear();
        if (buildingData != null && buildingData.levels != null && buildingData.levels.Count > 0)
        {
            for (int i = 0; i < buildingData.levels.Count; i++)
            {
                var ld = buildingData.levels[i];
                var rl = new RuntimeLevel();
                rl.workRequired = ld.workRequired;
                var levelContainer = new GameObject($"Level_{i}");
                levelContainer.transform.SetParent(transform, false);
                levelContainer.hideFlags = HideFlags.DontSaveInBuild;
                if (ld.stagePrefabs != null)
                {
                    for (int s = 0; s < ld.stagePrefabs.Count; s++)
                    {
                        var prefab = ld.stagePrefabs[s];
                        if (prefab == null) continue;
                        var inst = Instantiate(prefab, levelContainer.transform);
                        inst.SetActive(false);
                        rl.stageInstances.Add(inst);
                    }
                }
                if (ld.finalPrefab != null)
                {
                    var fin = Instantiate(ld.finalPrefab, levelContainer.transform);
                    fin.SetActive(false);
                    rl.finalInstance = fin;
                }
                runtimeLevels.Add(rl);
            }
            return;
        }

        for (int i = 0; i < levels.Count; i++)
        {
            var lv = levels[i];
            var rl = new RuntimeLevel();
            rl.workRequired = lv.workRequired;
            if (lv.buildStages != null)
            {
                for (int s = 0; s < lv.buildStages.Count; s++)
                {
                    var go = lv.buildStages[s];
                    if (go == null) continue;
                    rl.stageInstances.Add(go);
                }
            }
            rl.finalInstance = lv.finalObject;
            runtimeLevels.Add(rl);
        }
    }

    public void UpdateVisuals()
    {
        for (int i = 0; i < runtimeLevels.Count; i++)
        {
            var rl = runtimeLevels[i];
            if (rl == null) continue;
            if (rl.stageInstances != null)
            {
                foreach (var go in rl.stageInstances) if (go != null) go.SetActive(false);
            }
            if (rl.finalInstance != null) rl.finalInstance.SetActive(false);
        }
        if (IsFinished()) return;
        var cur = runtimeLevels[currentLevel];
        if (cur.stageInstances != null && cur.stageInstances.Count > 0)
        {
            float frac = GetProgressFraction();
            int stageIndex = Mathf.Clamp(Mathf.FloorToInt(frac * cur.stageInstances.Count), 0, cur.stageInstances.Count - 1);
            if (cur.stageInstances[stageIndex] != null) cur.stageInstances[stageIndex].SetActive(true);
        }
    }

    public void ShowFinalForLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= runtimeLevels.Count) return;
        var rl = runtimeLevels[levelIndex];
        if (rl == null) return;
        if (rl.stageInstances != null)
        {
            foreach (var go in rl.stageInstances) if (go != null) go.SetActive(false);
        }
        if (rl.finalInstance != null) rl.finalInstance.SetActive(true);
    }
}
