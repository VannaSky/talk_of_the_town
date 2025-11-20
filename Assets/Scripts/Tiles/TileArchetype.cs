using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Tiles
{
    [CreateAssetMenu(menuName="Game/Tile Archetype")]
    public sealed class TileArchetype : ScriptableObject
    {
        [SerializeField] TileStyle style;
        [SerializeField] bool walkable = true;
        [SerializeField] float baseMoveCost = 1f;
        [SerializeField] List<ResourceType> allowedResources;
        [FormerlySerializedAs("allowedBuildings")] [SerializeField] List<ConstructionType> allowedConstructions;
      

        public TileStyle Style => style;
        public bool Walkable => walkable;
        public float BaseMoveCost => baseMoveCost;
        public IReadOnlyList<ResourceType> AllowedResources => allowedResources;
        public IReadOnlyList<ConstructionType> AllowedConstructions => allowedConstructions;
    }
}