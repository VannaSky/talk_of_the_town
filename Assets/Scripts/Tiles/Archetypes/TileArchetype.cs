using System.Collections.Generic;
using UnityEngine;

namespace Tiles
{
    [CreateAssetMenu(menuName="Game/Tile Archetype")]
    public sealed class TileArchetype : ScriptableObject
    {
        [SerializeField] TileStyle style;
        [SerializeField] bool walkable = true;
        [SerializeField] float baseMoveCost = 1f;
        [SerializeField] List<ResourceType> allowedResources;
        [SerializeField] List<BuildingType> allowedBuildings;
      

        public TileStyle Style => style;
        public bool Walkable => walkable;
        public float BaseMoveCost => baseMoveCost;
        public IReadOnlyList<ResourceType> AllowedResources => allowedResources;
        public IReadOnlyList<BuildingType> AllowedBuildings => allowedBuildings;
    }
}