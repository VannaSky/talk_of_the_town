using System;
using System.Collections.Generic;
using UnityEngine;
using AnimationState = Villagers.Jobs.AnimationState;

public class VillagerEquipment : MonoBehaviour
{
    [Serializable]
    public class JobToolMapping
    {
        public string jobName;
        public GameObject workToolPrefab;
        public GameObject carryItemPrefab;
        public AnimationState[] workStates;
        public AnimationState[] carryStates;
    }

    [Header("Setup")]
    public string handBoneName = "Hand_R";
    public JobToolMapping[] toolMappings;

    private Transform _handBone;
    private Dictionary<string, (GameObject workTool, GameObject carryItem)> _instances = new();
    private string _activeJob;
    private GameObject _activeObject;

    void Awake()
    {
        _handBone = FindBoneRecursive(transform, handBoneName);
        if (_handBone == null)
            Debug.LogWarning($"[VillagerEquipment] Could not find bone '{handBoneName}' on {name}");
    }

    void Start()
    {
        if (_handBone == null || toolMappings == null) return;

        foreach (var mapping in toolMappings)
        {
            GameObject workTool = null;
            GameObject carryItem = null;

            if (mapping.workToolPrefab != null)
            {
                workTool = Instantiate(mapping.workToolPrefab, _handBone);
                workTool.SetActive(false);
            }

            if (mapping.carryItemPrefab != null)
            {
                carryItem = Instantiate(mapping.carryItemPrefab, _handBone);
                carryItem.SetActive(false);
            }

            _instances[mapping.jobName] = (workTool, carryItem);
        }
    }

    public void UpdateVisuals(string jobName, AnimationState state)
    {
        // Hide previous active object
        if (_activeObject != null)
        {
            _activeObject.SetActive(false);
            _activeObject = null;
        }

        if (string.IsNullOrEmpty(jobName)) return;

        // Find the mapping for this job
        JobToolMapping mapping = null;
        foreach (var m in toolMappings)
        {
            if (m.jobName == jobName)
            {
                mapping = m;
                break;
            }
        }

        if (mapping == null) return;
        if (!_instances.TryGetValue(jobName, out var instances)) return;

        // Check work states
        if (instances.workTool != null && mapping.workStates != null)
        {
            foreach (var ws in mapping.workStates)
            {
                if (ws == state)
                {
                    instances.workTool.SetActive(true);
                    _activeObject = instances.workTool;
                    return;
                }
            }
        }

        // Check carry states
        if (instances.carryItem != null && mapping.carryStates != null)
        {
            foreach (var cs in mapping.carryStates)
            {
                if (cs == state)
                {
                    instances.carryItem.SetActive(true);
                    _activeObject = instances.carryItem;
                    return;
                }
            }
        }
    }

    public void HideAll()
    {
        if (_activeObject != null)
        {
            _activeObject.SetActive(false);
            _activeObject = null;
        }
    }

    private static Transform FindBoneRecursive(Transform parent, string boneName)
    {
        if (parent.name == boneName) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var result = FindBoneRecursive(parent.GetChild(i), boneName);
            if (result != null) return result;
        }
        return null;
    }
}
