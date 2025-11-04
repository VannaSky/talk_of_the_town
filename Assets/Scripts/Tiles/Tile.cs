using System;
using System.Linq;
using UnityEngine;

namespace Tiles
{
    public sealed class Tile : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] Vector2Int gridPos;
        [SerializeField] TileArchetype archetype;

        [Header("Person Slot (optional)")]
        [SerializeField] Transform personAnchor; // Empty child where the person stands

        public Vector2Int GridPos => gridPos;
        public TileArchetype Archetype => archetype;

        public ResourceInstance Resource { get; private set; }
        public BuildingInstance Building { get; private set; }
        public Person Occupant { get; private set; }

        public bool HasResource => Resource != null;
        public bool HasBuilding => Building != null;
        public bool IsOccupied => Occupant != null;

        public bool IsWalkable =>
            archetype.Walkable &&
            (Building == null || !Building.BlocksWalk) &&
            (Resource == null || Resource.AllowsWalkThrough) &&
            !IsOccupied;

        public float MoveCost =>
            archetype.BaseMoveCost +
            (Resource?.AddedMoveCost ?? 0f) +
            (Building?.AddedMoveCost ?? 0f);

        public event Action<Tile> OnChanged;
        public event Action<Person> OnOccupantChanged;
        public event Action<ResourceInstance> OnResourceChanged;
        public event Action<BuildingInstance> OnBuildingChanged;

        public bool TryEnter(Person p)
        {
            if (!IsWalkable || p == null) return false;
            Occupant = p;
            if (personAnchor != null) p.WarpTo(personAnchor);
            else p.transform.position = transform.position;
            OnOccupantChanged?.Invoke(p);
            OnChanged?.Invoke(this);
            return true;
        }

        public void Leave(Person p)
        {
            if (p != Occupant) return;
            Occupant = null;
            OnOccupantChanged?.Invoke(null);
            OnChanged?.Invoke(this);
        }

        public bool TrySetResource(ResourceInstance res)
        {
            if (res != null && !AllowsResource(res.Type)) return false;
            Resource = res;
            OnResourceChanged?.Invoke(res);
            OnChanged?.Invoke(this);
            return true;
        }

        public bool TrySetBuilding(BuildingInstance bld)
        {
            if (bld != null && !AllowsBuilding(bld.Type)) return false;
            Building = bld;
            OnBuildingChanged?.Invoke(bld);
            OnChanged?.Invoke(this);
            return true;
        }

        public bool AllowsResource(ResourceType type) =>
            type == ResourceType.None || Archetype.AllowedResources.Contains(type);

        public bool AllowsBuilding(BuildingType type) =>
            type == BuildingType.None || Archetype.AllowedBuildings.Contains(type);

#if UNITY_EDITOR
        void OnValidate()
        {
            // If you use XZ world for 3D, map to grid here:
            gridPos = new Vector2Int(Mathf.RoundToInt(transform.position.x),
                Mathf.RoundToInt(transform.position.z));
        }
#endif
    }
}
