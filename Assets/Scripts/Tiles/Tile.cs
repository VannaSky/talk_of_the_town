using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tiles
{
    public sealed class Tile : MonoBehaviour
    {
        private const string LogCategory = "Tile";
        void LogError(string msg)   => GameLog.LogError(LogCategory, msg, this);
        void LogWarning(string msg) => GameLog.LogWarning(LogCategory, msg, this);
        void LogEvent(string msg)   => GameLog.LogEvent(LogCategory, msg, this);
        void LogInfo(string msg)    => GameLog.LogInfo(LogCategory, msg, this);
        void LogVerbose(string msg) => GameLog.LogVerbose(LogCategory, msg, this);

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
        
        public GameObject ResourceVisual { get; private set; }

        // Replace single ResourceVisual with proper tracking
        private Dictionary<ResourceType, int> _resourceCounts = new ();
        
        public void SetLibrary(TileArchetypeLibrary lib) => library = lib;

        public ResourceInstance Resource { get; private set; }
        public ConstructionInstance Construction { get; private set; }
        public Person Occupant { get; private set; }
        public Buildings.Building PlacedBuilding { get; private set; }

        public bool HasResource => Resource != null;
        public bool HasBuilding => Construction != null;
        public bool IsOccupied  => Occupant != null;
        
        public void SetResourceVisual(GameObject go)
        {
            ResourceVisual = go;
        }
        
        
        /// <summary>Add resources to this tile</summary>
        public void AddResource(ResourceType type, int count = 1)
        {
            if (type == ResourceType.None) return;
        
            if (!_resourceCounts.ContainsKey(type))
                _resourceCounts[type] = 0;
            _resourceCounts[type] += count;
        
            // Update the ResourceInstance if needed
            if (Resource == null || Resource.Type != type)
            {
                TrySetResource(new ResourceInstance(type, _resourceCounts[type]));
            }
        }
    
        /// <summary>Remove resources from this tile</summary>
        public bool RemoveResource(ResourceType type, int count = 1)
        {
            if (!_resourceCounts.ContainsKey(type)) return false;
        
            _resourceCounts[type] = Mathf.Max(0, _resourceCounts[type] - count);
        
            if (_resourceCounts[type] == 0)
            {
                _resourceCounts.Remove(type);
                TrySetResource(null);
                return true;
            }
        
            return false;
        }
    
        /// <summary>Get resource count for a specific type</summary>
        public int GetResourceCount(ResourceType type)
        {
            return _resourceCounts.ContainsKey(type) ? _resourceCounts[type] : 0;
        }
    
        /// <summary>Get all resource counts on this tile</summary>
        public Dictionary<ResourceType, int> GetAllResourceCounts()
        {
            return new Dictionary<ResourceType, int>(_resourceCounts);
        }
    
        /// <summary>Clear all resources</summary>
        public void ClearResources()
        {
            _resourceCounts.Clear();
            TrySetResource(null);
        }

        public bool IsWalkable =>
            archetype != null && archetype.Walkable &&
            (Construction == null || !Construction.BlocksWalk) &&
            (Resource == null || Resource.AllowsWalkThrough) &&
            !IsOccupied;

        public float MoveCost =>
            (archetype?.BaseMoveCost ?? 0f) +
            (Resource?.AddedMoveCost ?? 0f) +
            (Construction?.AddedMoveCost ?? 0f);

        public event Action<Tile> OnChanged;
        public event Action<Person> OnOccupantChanged;
        public event Action<ResourceInstance> OnResourceChanged;
        public event Action<ConstructionInstance> OnBuildingChanged;

#if UNITY_EDITOR
        [ContextMenu("Log Instance Info")]
        private void LogInstanceInfo()
        {
            LogInfo($"{name} instanceID={GetInstanceID()} gridPos={GridPos} " +
                    $"archetype={Archetype?.name}/{Archetype?.Style} " +
                    $"path={gameObject.transform.GetHierarchyPath()}");
        }
#endif
        
        
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
                LogWarning($"{name} at {gridPos} has no TileArchetypeLibrary; cannot set style {style}.");
                return;
            }

            var next = library.Get(style);
            if (next == null)
            {
                LogWarning($"{name} missing archetype mapping for {style}.");
                return;
            }

            if (ReferenceEquals(next, archetype))
            {
                // Debug.Log($"[Tile] SetStyle SKIP {name} id={GetInstanceID()} pos={gridPos} already {style}");
                OnChanged?.Invoke(this);
                return;
            }

            var prev = archetype?.Style.ToString() ?? "NULL";
            var caller = new System.Diagnostics.StackTrace(1, false).GetFrame(0)?.GetMethod();
            var callerStr = caller != null
                ? $"{caller.DeclaringType?.Name}.{caller.Name}"
                : "<unknown>";

           

            archetype = next;
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

        public bool TrySetBuilding(ConstructionInstance bld)
        {
            if (bld != null && !AllowsBuilding(bld.Type)) return false;
            Construction = bld;
            OnBuildingChanged?.Invoke(bld);
            OnChanged?.Invoke(this);
            return true;
        }

        public void SetPlacedBuilding(Buildings.Building building) => PlacedBuilding = building;

        public bool AllowsResource(ResourceType type) =>
            archetype != null && (type == ResourceType.None || archetype.AllowedResources.Contains(type));

        public bool AllowsBuilding(ConstructionType type) =>
            archetype != null && (type == ConstructionType.None || archetype.AllowedConstructions.Contains(type));

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
