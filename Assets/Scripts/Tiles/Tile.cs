using System;
using System.Linq;
using UnityEngine;

namespace Tiles
{
    public sealed class Tile : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] Vector2Int gridPos;

        [Header("Rules")]
        [SerializeField] TileArchetypeLibrary library;   // mapping style → archetype
        [SerializeField] TileArchetype archetype;        // current, runtime-selected

        [Header("Person Slot (optional)")]
        [SerializeField] Transform personAnchor;

        public Vector2Int GridPos => gridPos;
        public TileArchetype Archetype => archetype;
        public TileArchetypeLibrary Library => library;

        public void SetLibrary(TileArchetypeLibrary lib) => library = lib;

        public ResourceInstance Resource { get; private set; }
        public BuildingInstance Building { get; private set; }
        public Person Occupant { get; private set; }

        public bool HasResource => Resource != null;
        public bool HasBuilding => Building != null;
        public bool IsOccupied  => Occupant != null;

        public bool IsWalkable =>
            archetype != null && archetype.Walkable &&
            (Building == null || !Building.BlocksWalk) &&
            (Resource == null || Resource.AllowsWalkThrough) &&
            !IsOccupied;

        public float MoveCost =>
            (archetype?.BaseMoveCost ?? 0f) +
            (Resource?.AddedMoveCost ?? 0f) +
            (Building?.AddedMoveCost ?? 0f);

        public event Action<Tile> OnChanged;
        public event Action<Person> OnOccupantChanged;
        public event Action<ResourceInstance> OnResourceChanged;
        public event Action<BuildingInstance> OnBuildingChanged;

        /// <summary>Runtime init from the spawner.</summary>
        public void Init(Vector2Int pos, TileArchetypeLibrary lib = null)
        {
            gridPos = pos;
            if (lib != null) library = lib;
        }

        /// <summary>Set style via library mapping; assigns the active archetype.</summary>
        public void SetStyle(TileStyle style)
        {
            if (library == null)
            {
                Debug.LogWarning($"[Tile] {name} at {gridPos} has no TileArchetypeLibrary; cannot set style {style}.");
                return;
            }

            var next = library.Get(style);
            if (next == null)
            {
                Debug.LogWarning($"[Tile] {name} missing archetype mapping for {style}.");
                return;
            }

            if (ReferenceEquals(next, archetype)) { OnChanged?.Invoke(this); return; }

            archetype = next;                     // ← actual assignment
            // Debug.Log($"[Tile] {name} style → {archetype?.name}");
            
            OnChanged?.Invoke(this);
        }

        public bool TryEnter(Person p)
        {
            if (!IsWalkable || p == null) return false;
            Occupant = p;
            if (personAnchor != null) p.WarpTo(personAnchor); else p.transform.position = transform.position;
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
            archetype != null && (type == ResourceType.None || archetype.AllowedResources.Contains(type));

        public bool AllowsBuilding(BuildingType type) =>
            archetype != null && (type == BuildingType.None || archetype.AllowedBuildings.Contains(type));

#if UNITY_EDITOR
        void OnValidate()
        {
            gridPos = new Vector2Int(
                Mathf.RoundToInt(transform.position.x),
                Mathf.RoundToInt(transform.position.z)
            );
        }
#endif
    }
}
