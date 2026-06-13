using UnityEngine;
using Tiles;

/// <summary>
/// Villager with tile awareness - integrates with JobHandler for task execution
/// </summary>
public class Villager : MonoBehaviour
{
    [Header("Identity")]
    public string villagerName;
    private int _villagerId;
    public int VillagerId => _villagerId;
    public void AssignId(int id) => _villagerId = id;
    
    [Header("References")]
    [SerializeField] private TileGrid tileGrid;
    [SerializeField] private float cellSize = 2f;  // Must match TileGridSpawner
    
    [Header("Energy")]
    [SerializeField] private float energy = 100f;
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float energyDrainRate = 1f;
    [SerializeField] private float energyWalkDrainRate = 0.3f;
    [SerializeField] private float energyRecoveryRate = 2f;

    [Header("State (Read-Only)")]
    [SerializeField] private Vector2Int currentGridPos;
    
    private const string LogCategory = "Villager";
    void LogError(string msg)   => GameLog.LogError(LogCategory, msg, this);
    void LogWarning(string msg) => GameLog.LogWarning(LogCategory, msg, this);
    void LogEvent(string msg)   => GameLog.LogEvent(LogCategory, msg, this);
    void LogInfo(string msg)    => GameLog.LogInfo(LogCategory, msg, this);
    void LogVerbose(string msg) => GameLog.LogVerbose(LogCategory, msg, this);

    // Cached references
    private JobHandler _jobHandler;
    private Tile _currentTile;
    
    // Public accessors
    public Vector2Int GridPosition => currentGridPos;
    public Tile CurrentTile => _currentTile;
    public JobHandler JobHandler => _jobHandler;
    public float Energy => energy;
    public float MaxEnergy => maxEnergy;
    public int EnergyPercent => Mathf.RoundToInt(energy / maxEnergy * 100f);
    public float EnergyDrainRate => energyDrainRate;
    public float EnergyWalkDrainRate => energyWalkDrainRate;
    public float EnergyRecoveryRate => energyRecoveryRate;

    /// <summary>
    /// Work speed multiplier based on energy level.
    /// 1.0 at energy >= 30, scales linearly down to ~0 between 30 and 5, 0 below 5.
    /// </summary>
    public float WorkSpeedMultiplier
    {
        get
        {
            if (energy >= 30f) return 1f;
            if (energy < 5f) return 0f;
            return energy / 30f;
        }
    }
    
    void Awake()
    {
        _jobHandler = GetComponent<JobHandler>();

        if (string.IsNullOrEmpty(villagerName))
            villagerName = gameObject.name;
    }
    
    void Start()
    {
        if (tileGrid == null)
            tileGrid = FindFirstObjectByType<TileGrid>();
            
        UpdateCurrentTile();
    }
    
    void Update()
    {
        UpdateCurrentTile();
        UpdateEnergy();
    }

    private void UpdateEnergy()
    {
        var state = _jobHandler?.ActiveJobLogic?.GetCurrentState();
        bool isActiveWork = state == Villagers.Jobs.AnimationState.Chopping
            || state == Villagers.Jobs.AnimationState.Mining
            || state == Villagers.Jobs.AnimationState.Gathering
            || state == Villagers.Jobs.AnimationState.Building
            || state == Villagers.Jobs.AnimationState.Farming
            || state == Villagers.Jobs.AnimationState.Planting;
        bool isWalking = state == Villagers.Jobs.AnimationState.MovingToTarget
            || state == Villagers.Jobs.AnimationState.Carrying;

        if (isActiveWork)
            energy = Mathf.Max(0f, energy - energyDrainRate * Time.deltaTime);
        else if (isWalking)
            energy = Mathf.Max(0f, energy - energyWalkDrainRate * Time.deltaTime);
        else
            energy = Mathf.Min(maxEnergy, energy + energyRecoveryRate * Time.deltaTime);
    }
    
    /// <summary>
    /// Convert world position to grid position using floor (tiles at corners)
    /// </summary>
    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x / cellSize);
        int z = Mathf.FloorToInt(worldPos.z / cellSize);
        return new Vector2Int(x, z);
    }
    
    /// <summary>
    /// Convert grid position to world position (tile center)
    /// </summary>
    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        float x = gridPos.x * cellSize + cellSize / 2f;
        float z = gridPos.y * cellSize + cellSize / 2f;
        return new Vector3(x, transform.position.y, z);
    }
    
    private void UpdateCurrentTile()
    {
        Vector2Int newPos = WorldToGrid(transform.position);
        
        if (newPos != currentGridPos || _currentTile == null)
        {
            currentGridPos = newPos;
            
            if (tileGrid != null && tileGrid.TryGet(currentGridPos, out Tile tile))
                _currentTile = tile;
            else
                _currentTile = null;
        }
    }
    
    /// <summary>
    /// Get current job name from JobHandler
    /// </summary>
    public string GetCurrentJobName()
    {
        if (_jobHandler == null || _jobHandler.currentJob == null)
            return "Idle";
        return _jobHandler.currentJob.JobName;
    }
    
    /// <summary>
    /// Get current job status from JobLogic
    /// </summary>
    public string GetCurrentJobStatus()
    {
        if (_jobHandler == null || _jobHandler.ActiveJobLogic == null)
            return "Idle";
        return _jobHandler.ActiveJobLogic.GetCurrentStatus();
    }
    
    /// <summary>
    /// Get serializable data for LLM context
    /// </summary>
    public VillagerData GetData()
    {
        return new VillagerData
        {
            id = _villagerId,
            name = villagerName,
            x = currentGridPos.x,
            y = currentGridPos.y,
            currentJob = GetCurrentJobName(),
            jobStatus = GetCurrentJobStatus(),
            jobLevel = _jobHandler?.GetCurrentJobLevel() ?? 0,
            tileType = _currentTile?.Archetype?.Style.ToString() ?? "Unknown",
            energy = EnergyPercent
        };
    }

#if UNITY_EDITOR
    [ContextMenu("Log Villager State")]
    private void LogState()
    {
        var data = GetData();
        LogInfo($"[{data.id}] {data.name} at ({data.x},{data.y}) on {data.tileType}, " +
                $"job={data.currentJob} (lvl {data.jobLevel}), status={data.jobStatus}");
    }
#endif
}

/// <summary>
/// Serializable villager data for LLM consumption
/// </summary>
[System.Serializable]
public class VillagerData
{
    public int id;
    public string name;
    public int x;
    public int y;
    public string currentJob;
    public string jobStatus;
    public int jobLevel;
    public string tileType;
    public int energy;
}