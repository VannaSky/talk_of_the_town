using System.Collections.Generic;
using UnityEngine;

namespace Environment.Resources
{
    /// <summary>
    /// Scene singleton that manages stump prefabs for tree regrowth visuals.
    /// Assign stump prefabs in the Inspector. ResourceNodes of type Tree with
    /// canRegrow=true will automatically pick a random stump when cut down.
    /// </summary>
    public class ResourceRegrowthManager : MonoBehaviour
    {
        private static ResourceRegrowthManager _instance;
        public static ResourceRegrowthManager Instance => _instance;

        [Header("Tree Stump Prefabs")]
        [Tooltip("Random stump is chosen when a tree is cut down. Appears while the tree regrows.")]
        [SerializeField] List<GameObject> stumpPrefabs;

        public bool HasStumps => stumpPrefabs != null && stumpPrefabs.Count > 0;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// Instantiates a random stump prefab at the given position/rotation under the given parent.
        /// Returns null if no stump prefabs are configured.
        /// </summary>
        public GameObject SpawnStump(Vector3 position, Quaternion rotation, Transform parent)
        {
            if (!HasStumps) return null;
            var prefab = stumpPrefabs[Random.Range(0, stumpPrefabs.Count)];
            return Instantiate(prefab, position, rotation, parent);
        }
    }
}
