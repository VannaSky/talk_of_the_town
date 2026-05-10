using UnityEngine;
using Tiles;

/// <summary>
/// Villager with tile awareness - integrates with JobHandler for task execution
/// </summary>
public class Villager : MonoBehaviour
{
    [Header("Identity")]
    public string villagerName;
    
    [Header("References")]
    [SerializeField] private TileGrid tileGrid;
    [SerializeField] private float cellSize = 2f;  // Must match TileGridSpawner
    
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
            name = villagerName,
            x = currentGridPos.x,
            y = currentGridPos.y,
            currentJob = GetCurrentJobName(),
            jobStatus = GetCurrentJobStatus(),
            jobLevel = _jobHandler?.GetCurrentJobLevel() ?? 0,
            tileType = _currentTile?.Archetype?.Style.ToString() ?? "Unknown"
        };
    }

#if UNITY_EDITOR
    [ContextMenu("Log Villager State")]
    private void LogState()
    {
        var data = GetData();
        LogInfo($"{data.name} at ({data.x},{data.y}) on {data.tileType}, " +
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
    public string name;
    public int x;
    public int y;
    public string currentJob;
    public string jobStatus;
    public int jobLevel;
    public string tileType;
}