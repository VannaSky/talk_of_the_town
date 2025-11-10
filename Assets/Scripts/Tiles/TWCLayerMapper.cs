using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Tiles
{
    /// <summary>
    /// Maps dynamic TWC layer names to fixed TileStyle archetypes
    /// </summary>
    [CreateAssetMenu(menuName = "Tiles/TWC Layer Mapper")]
    public class TWCLayerMapper : ScriptableObject
    {
        [System.Serializable]
        public class LayerMapping
        {
            public string twcLayerName;
            public TileStyle tileStyle;
            public int priority = 0;  // Higher = applied later (wins conflicts)
        }

        public List<LayerMapping> mappings = new List<LayerMapping>();

        private Dictionary<string, LayerMapping> _cache;

        public void RebuildCache()
        {
            _cache = new Dictionary<string, LayerMapping>();
            foreach (var m in mappings)
                if (!string.IsNullOrEmpty(m.twcLayerName))
                    _cache[m.twcLayerName] = m;
        }

        public bool TryGetMapping(string layerName, out LayerMapping mapping)
        {
            if (_cache == null) RebuildCache();
            return _cache.TryGetValue(layerName, out mapping);
        }

        public IEnumerable<LayerMapping> GetAllMappingsSortedByPriority()
        {
            if (_cache == null) RebuildCache();
            return mappings.OrderBy(m => m.priority);
        }
    }
}