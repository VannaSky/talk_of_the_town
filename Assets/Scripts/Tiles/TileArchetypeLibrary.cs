using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tiles
{
    [CreateAssetMenu(menuName = "Game/Tile Archetype Library")]
    public sealed class TileArchetypeLibrary : ScriptableObject
    {
        [Serializable] public struct Entry { public TileStyle style; public TileArchetype archetype; }
        [SerializeField] List<Entry> entries;

        public TileArchetype Get(TileStyle s)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].style == s) return entries[i].archetype;
            GameLog.LogError("TileArchetypeLibrary", $"No archetype for {s}", this);
            return null;
        }
    }
}