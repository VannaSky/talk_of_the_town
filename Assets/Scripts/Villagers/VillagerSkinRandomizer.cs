using UnityEngine;

public class VillagerSkinRandomizer : MonoBehaviour
{
    [Tooltip("Parent object that contains the skin variant GameObjects (direct children with SkinnedMeshRenderers)")]
    [SerializeField] private GameObject skinRoot;

    private void Awake()
    {
        if (skinRoot == null) return;

        var variants = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in skinRoot.transform)
            if (child.GetComponent<SkinnedMeshRenderer>() != null)
                variants.Add(child.gameObject);

        if (variants.Count == 0) return;

        int chosen = Random.Range(0, variants.Count);
        for (int i = 0; i < variants.Count; i++)
            variants[i].SetActive(i == chosen);
    }
}
