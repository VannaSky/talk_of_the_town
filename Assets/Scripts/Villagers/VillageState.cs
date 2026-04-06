using System;
using System.Collections.Generic;
using Tiles;
using UnityEngine;

/// <summary>
/// Central hub for all village state - inventory, villagers, and map access.
/// Provides unified data for LLM decision-making.
/// </summary>
public class VillageState : MonoBehaviour
{
    public static VillageState Instance { get; private set; }
    
    [Header("References")]
    [SerializeField] private TileGrid tileGrid;
    
    [Header("Inventory")]
    [SerializeField] private int wood = 0;
    [SerializeField] private int stone = 0;
    [SerializeField] private int seeds = 0;
    [SerializeField] private int iron = 0;
    [SerializeField] private int food = 0;
    
    [Header("Registered Villagers")]
    [SerializeField] private List<Villager> villagers = new List<Villager>();
    
    // Public accessors
    public TileGrid TileGrid => tileGrid;
    public IReadOnlyList<Villager> Villagers => villagers;
    
    // Resource accessors
    public int Wood => wood;
    public int Stone => stone;
    public int Seeds => seeds;
    public int Iron => iron;
    public int Food => food;
    
    // Events
    public event Action<ResourceType, int, int> OnResourceChanged;  // type, oldValue, newValue
    public event Action<Villager> OnVillagerRegistered;
    public event Action<Villager> OnVillagerUnregistered;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    void Start()
    {
        // Auto-find TileGrid if not assigned
        if (tileGrid == null)
        {
            tileGrid = FindFirstObjectByType<TileGrid>();
            if (tileGrid != null)
                Debug.Log($"[VillageState] Found TileGrid: {tileGrid.name}");
            else
                Debug.LogWarning("[VillageState] No TileGrid found!");
        }
        
        // Auto-register existing villagers
        var existingVillagers = FindObjectsByType<Villager>(FindObjectsSortMode.None);
        foreach (var v in existingVillagers)
            RegisterVillager(v);
    }
    
    #region Villager Registry
    
    public void RegisterVillager(Villager villager)
    {
        if (villager != null && !villagers.Contains(villager))
        {
            villagers.Add(villager);
            OnVillagerRegistered?.Invoke(villager);
            Debug.Log($"[VillageState] Registered villager: {villager.villagerName}");
        }
    }
    
    public void UnregisterVillager(Villager villager)
    {
        if (villager != null && villagers.Remove(villager))
        {
            OnVillagerUnregistered?.Invoke(villager);
            Debug.Log($"[VillageState] Unregistered villager: {villager.villagerName}");
        }
    }
    
    #endregion
    
    #region Resource Management
    
    public void AddResource(ResourceType type, int amount)
    {
        if (amount <= 0) return;
        
        int oldValue = GetResource(type);
        
        switch (type)
        {
            case ResourceType.Wood: wood += amount; break;
            case ResourceType.Stone: stone += amount; break;
            case ResourceType.Seed: seeds += amount; break;
            case ResourceType.Iron: iron += amount; break;
            case ResourceType.Food: food += amount; break;
        }
        
        int newValue = GetResource(type);
        OnResourceChanged?.Invoke(type, oldValue, newValue);
        Debug.Log($"[VillageState] +{amount} {type} (now {newValue})");
    }
    
    public bool TrySpendResource(ResourceType type, int amount)
    {
        if (amount <= 0) return true;
        if (GetResource(type) < amount) return false;
        
        int oldValue = GetResource(type);
        
        switch (type)
        {
            case ResourceType.Wood: wood -= amount; break;
            case ResourceType.Stone: stone -= amount; break;
            case ResourceType.Seed: seeds -= amount; break;
            case ResourceType.Iron: iron -= amount; break;
            case ResourceType.Food: food -= amount; break;
        }
        
        int newValue = GetResource(type);
        OnResourceChanged?.Invoke(type, oldValue, newValue);
        Debug.Log($"[VillageState] -{amount} {type} (now {newValue})");
        return true;
    }
    
    public int GetResource(ResourceType type)
    {
        return type switch
        {
            ResourceType.Wood => wood,
            ResourceType.Stone => stone,
            ResourceType.Seed => seeds,
            ResourceType.Iron => iron,
            ResourceType.Food => food,
            _ => 0
        };
    }
    
    public bool HasResource(ResourceType type, int amount)
    {
        return GetResource(type) >= amount;
    }
    
    #endregion
    
    #region LLM Data Export
    
    /// <summary>
    /// Get complete village state for LLM consumption
    /// </summary>
    public VillageSnapshot GetSnapshot()
    {
        var snapshot = new VillageSnapshot
        {
            resources = new ResourceSnapshot
            {
                wood = wood,
                stone = stone,
                seeds = seeds,
                iron = iron,
                food = food
            },
            villagers = new List<VillagerSnapshot>()
        };
        
        foreach (var v in villagers)
        {
            if (v == null) continue;
            
            var data = v.GetData();
            snapshot.villagers.Add(new VillagerSnapshot
            {
                name = data.name,
                x = data.x,
                y = data.y,
                currentJob = data.currentJob,
                jobStatus = data.jobStatus,
                jobLevel = data.jobLevel,
                tileType = data.tileType
            });
        }
        
        return snapshot;
    }
    
    /// <summary>
    /// Get compact JSON string of village state for LLM prompt
    /// </summary>
    public string GetVillageContextJson()
    {
        var snapshot = GetSnapshot();
        return JsonUtility.ToJson(snapshot, false);
    }
    
    /// <summary>
    /// Get human-readable village summary for LLM prompt
    /// </summary>
    public string GetVillageContextReadable()
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine("=== VILLAGE RESOURCES ===");
        sb.AppendLine($"Wood: {wood} | Stone: {stone} | Seeds: {seeds} | Iron: {iron} | Food: {food}");
        
        sb.AppendLine();
        sb.AppendLine("=== VILLAGERS ===");
        foreach (var v in villagers)
        {
            if (v == null) continue;
            var d = v.GetData();
            sb.AppendLine($"- {d.name} at ({d.x},{d.y}) [{d.tileType}]: {d.currentJob} (Lvl {d.jobLevel}) - {d.jobStatus}");
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Get compact map JSON
    /// </summary>
    public string GetMapContextJson()
    {
        if (tileGrid == null) return "{}";
        return tileGrid.ExportToJsonCompact();
    }
    
    /// <summary>
    /// Analyze tiles near a specific position for LLM context
    /// </summary>
    public NearbyTilesInfo GetNearbyTilesInfo(Vector2Int centerPos, int radius = 3)
    {
        var info = new NearbyTilesInfo();
        
        if (tileGrid == null) return info;
        
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                var pos = centerPos + new Vector2Int(dx, dy);
                if (tileGrid.TryGet(pos, out Tile tile))
                {
                    var style = tile.Archetype?.Style ?? TileStyle.Grass;
                    
                    switch (style)
                    {
                        case TileStyle.Forest: info.forestCount++; break;
                        case TileStyle.Mountain: info.mountainCount++; break;
                        case TileStyle.Water: info.waterCount++; break;
                        case TileStyle.Grass: info.grassCount++; break;
                    }
                    
                    // Check for resources
                    var resources = tile.GetAllResourceCounts();
                    foreach (var kvp in resources)
                    {
                        switch (kvp.Key)
                        {
                            case ResourceType.Wood: info.nearbyWood += kvp.Value; break;
                            case ResourceType.Stone: info.nearbyStone += kvp.Value; break;
                        }
                    }
                    
                    // Check for buildings
                    if (tile.HasBuilding)
                        info.buildingCount++;
                }
            }
        }
        
        return info;
    }
    
    #endregion

#if UNITY_EDITOR
    [ContextMenu("Log Village State")]
    private void LogVillageState()
    {
        Debug.Log(GetVillageContextReadable());
    }
    
    [ContextMenu("Add 10 Wood (Test)")]
    private void TestAddWood() => AddResource(ResourceType.Wood, 10);
    
    [ContextMenu("Add 10 Stone (Test)")]
    private void TestAddStone() => AddResource(ResourceType.Stone, 10);
#endif
}

#region Data Classes for LLM

[Serializable]
public class VillageSnapshot
{
    public ResourceSnapshot resources;
    public List<VillagerSnapshot> villagers;
}

[Serializable]
public class ResourceSnapshot
{
    public int wood;
    public int stone;
    public int seeds;
    public int iron;
    public int food;
}

[Serializable]
public class VillagerSnapshot
{
    public string name;
    public int x;
    public int y;
    public string currentJob;
    public string jobStatus;
    public int jobLevel;
    public string tileType;
}

[Serializable]
public class NearbyTilesInfo
{
    public int forestCount;
    public int mountainCount;
    public int grassCount;
    public int waterCount;
    public int buildingCount;
    public int nearbyWood;
    public int nearbyStone;
    
    public override string ToString()
    {
        return $"Nearby: {forestCount} forest, {mountainCount} mountain, {grassCount} grass, " +
               $"{buildingCount} buildings, {nearbyWood} wood, {nearbyStone} stone";
    }
}

#endregion