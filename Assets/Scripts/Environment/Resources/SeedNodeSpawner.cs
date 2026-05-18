using System.Collections;
using System.Collections.Generic;
using Tiles;
using UnityEngine;

namespace Environment.Resources
{
    /// <summary>
    /// Periodically spawns new seed resource nodes on empty grass tiles,
    /// simulating flowers and wild plants growing naturally on the map.
    /// </summary>
    public class SeedNodeSpawner : MonoBehaviour
    {
        [Header("Spawning")]
        [Tooltip("Prefabs with a ResourceNode of type Seed. A random one is picked each spawn.")]
        [SerializeField] private List<GameObject> seedNodePrefabs = new List<GameObject>();

        [Tooltip("Seconds between spawn attempts.")]
        [SerializeField] private float spawnInterval = 60f;

        [Tooltip("Maximum number of seed nodes allowed on the map at once. 0 = no limit.")]
        [SerializeField] private int maxSeedNodes = 20;

        [Tooltip("How many nodes to try spawning per interval.")]
        [SerializeField] private int spawnCountPerInterval = 2;

        [Header("References")]
        [SerializeField] private TileGrid tileGrid;

        private void Start()
        {
            if (tileGrid == null)
                tileGrid = FindFirstObjectByType<TileGrid>();

            if (seedNodePrefabs == null || seedNodePrefabs.Count == 0)
            {
                Debug.LogWarning("[SeedNodeSpawner] No seed node prefabs assigned — spawner disabled.");
                return;
            }

            StartCoroutine(SpawnLoop());
        }

        private IEnumerator SpawnLoop()
        {
            // Stagger first spawn so it doesn't fire immediately on scene load
            yield return new WaitForSeconds(spawnInterval);

            while (true)
            {
                TrySpawnSeeds();
                yield return new WaitForSeconds(spawnInterval);
            }
        }

        private void TrySpawnSeeds()
        {
            int currentCount = CountSeedNodes();
            if (maxSeedNodes > 0 && currentCount >= maxSeedNodes)
                return;

            var candidates = GetCandidateTiles();
            if (candidates.Count == 0)
                return;

            int toSpawn = maxSeedNodes > 0
                ? Mathf.Min(spawnCountPerInterval, maxSeedNodes - currentCount)
                : spawnCountPerInterval;

            for (int i = 0; i < toSpawn && candidates.Count > 0; i++)
            {
                int idx = Random.Range(0, candidates.Count);
                Tile tile = candidates[idx];
                candidates.RemoveAt(idx);

                var prefab = seedNodePrefabs[Random.Range(0, seedNodePrefabs.Count)];
                Vector3 pos = tile.transform.position;
                Instantiate(prefab, pos, Quaternion.Euler(0, Random.Range(0f, 360f), 0), transform);
            }
        }

        private int CountSeedNodes()
        {
            int count = 0;
            var nodes = FindObjectsByType<ResourceNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var n in nodes)
                if (n != null && n.resourceType == ResourceNode.ResourceType.Seed)
                    count++;
            return count;
        }

        private List<Tile> GetCandidateTiles()
        {
            return tileGrid.FindAllTiles(tile =>
                tile.Archetype != null
                && tile.Archetype.Style == TileStyle.Grass
                && !tile.HasBuilding
                && !tile.HasResource);
        }
    }
}
