using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class VillagerMover : MonoBehaviour
{
    private const string LogCategory = "VillagerMover";
    void LogError(string msg)   => GameLog.LogError(LogCategory, msg, this);
    void LogWarning(string msg) => GameLog.LogWarning(LogCategory, msg, this);
    void LogEvent(string msg)   => GameLog.LogEvent(LogCategory, msg, this);
    void LogInfo(string msg)    => GameLog.LogInfo(LogCategory, msg, this);
    void LogVerbose(string msg) => GameLog.LogVerbose(LogCategory, msg, this);

    private NavMeshAgent agent;

    [Header("Movement Settings")]
    [Tooltip("The speed at which the villager moves.")]
    public float moveSpeed = 3.5f;

    [Tooltip("The distance threshold to consider the villager 'near' their destination.")]
    public float proximityThreshold = 2.0f;

    [Tooltip("Rotation speed in degrees per second when facing a target.")]
    public float rotationSpeed = 360f;

    [Header("Unstuck")]
    [Tooltip("Game seconds without progress before the villager counts as stuck.")]
    public float stuckTimeout = 3f;

    [Tooltip("Distance the villager has to cover within stuckTimeout to count as moving.")]
    public float stuckProgressDistance = 0.3f;

    [Tooltip("Radius searched for a NavMesh point from which the destination is reachable.")]
    public float unstuckSearchRadius = 8f;

    [Tooltip("A path that ends this close to the destination counts as reaching it (targets inside obstacles).")]
    public float reachTolerance = 2.5f;

    /// <summary>Raised after a stuck villager was warped: (mover, from, to, usedFallback).</summary>
    public static event System.Action<VillagerMover, Vector3, Vector3, bool> OnUnstuck;

    private Vector3? _faceTarget;

    private bool _moveRequested;
    private Vector3 _destination;
    private Vector3 _progressAnchor;
    private float _noProgressTime;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        
        agent.speed = moveSpeed;
        agent.stoppingDistance = 0.1f;
        agent.isStopped = true;
    }
    
    void Update()
    {
        if (agent.speed != moveSpeed)
        {
            agent.speed = moveSpeed;
        }

        if (_faceTarget.HasValue)
        {
            Vector3 dir = _faceTarget.Value - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRot, rotationSpeed * Time.deltaTime);

                if (Quaternion.Angle(transform.rotation, targetRot) < 1f)
                    _faceTarget = null;
            }
            else
            {
                _faceTarget = null;
            }
        }

        CheckStuck();
    }

    public void MoveTo(Vector3 targetPosition)
    {
        // Jobs call MoveTo every frame; only a new destination restarts the stuck timer
        if (!_moveRequested || FlatDistance(_destination, targetPosition) > 0.5f)
            ResetStuckTimer();
        _moveRequested = true;
        _destination = targetPosition;

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(targetPosition);
        }
        else
        {
            LogError($"{gameObject.name} cannot move! NavMeshAgent is not active or not placed on the NavMesh.");
        }
    }

    public void StopMoving()
    {
        _moveRequested = false;
        if (agent.enabled && agent.isOnNavMesh && !agent.isStopped)
        {
            FaceTarget(agent.destination);
            agent.isStopped = true;
        }
    }

    public bool IsNearDestination(float threshold = -1f)
    {
        float checkThreshold = (threshold > 0) ? threshold : proximityThreshold;
        
        if (agent.pathPending || agent.remainingDistance == float.PositiveInfinity || agent.remainingDistance < 0)
        {
            return false;
        }
        
        return agent.remainingDistance <= checkThreshold && !agent.isStopped;
    }

    public bool IsMoving()
    {
        return !agent.isStopped && agent.velocity.sqrMagnitude > 0.01f;
    }

    public void FaceTarget(Vector3 target)
    {
        _faceTarget = target;
    }

    public bool IsFacingTarget => !_faceTarget.HasValue;

    // ── Unstuck ─────────────────────────────────────────────────────────
    // Carving NavMeshObstacles of neighbouring buildings can close the gap a villager stands in, or a
    // foundation is placed around it. The agent then "walks" on the spot forever. If it makes no progress
    // and its destination is unreachable from where it stands, warp it to the nearest point that has a way.

    private void ResetStuckTimer()
    {
        _noProgressTime = 0f;
        _progressAnchor = transform.position;
    }

    private void CheckStuck()
    {
        if (!agent.enabled || !_moveRequested) return;
        if (agent.isOnNavMesh && (agent.isStopped || agent.pathPending)) return;

        if (FlatDistance(transform.position, _progressAnchor) > stuckProgressDistance)
        {
            ResetStuckTimer();
            return;
        }

        _noProgressTime += Time.deltaTime;
        if (_noProgressTime < stuckTimeout) return;
        ResetStuckTimer();

        // Standing still is fine if the destination is reachable: arrived, or briefly blocked by others
        if (!NavMesh.SamplePosition(_destination, out var destHit, reachTolerance, agent.areaMask)) return;
        if (CanReach(transform.position, destHit.position, 0.3f)) return;

        TryUnstick(destHit.position);
    }

    private void TryUnstick(Vector3 target)
    {
        Vector3 from = transform.position;
        const int directions = 16;

        // Rings of increasing radius, so the first hit is the shortest warp
        for (float r = 1f; r <= unstuckSearchRadius; r += 1f)
        {
            for (int i = 0; i < directions; i++)
            {
                float angle = i * Mathf.PI * 2f / directions;
                Vector3 candidate = from + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r;
                if (!NavMesh.SamplePosition(candidate, out var hit, 0.75f, agent.areaMask)) continue;
                if (!CanReach(hit.position, target, 0.1f)) continue;

                WarpAndResume(from, hit.position, usedFallback: false);
                return;
            }
        }

        // Nothing nearby has a way out: last resort, put it next to its destination
        WarpAndResume(from, target, usedFallback: true);
    }

    private void WarpAndResume(Vector3 from, Vector3 to, bool usedFallback)
    {
        if (!agent.Warp(to)) return;
        agent.isStopped = false;
        agent.SetDestination(_destination);

        string msg = $"{gameObject.name} was stuck at {from} — warped to {to}{(usedFallback ? " (fallback: next to destination)" : "")}";
        if (usedFallback) LogWarning(msg); else LogEvent(msg);
        OnUnstuck?.Invoke(this, from, to, usedFallback);
    }

    /// <summary>A path counts if it is complete or ends within reachTolerance of the target (targets inside obstacles).</summary>
    private bool CanReach(Vector3 from, Vector3 target, float sampleRadius)
    {
        if (!NavMesh.SamplePosition(from, out var start, sampleRadius, agent.areaMask)) return false;
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(start.position, target, agent.areaMask, path)) return false;
        if (path.status == NavMeshPathStatus.PathComplete) return true;
        return path.corners.Length > 0 && FlatDistance(path.corners[path.corners.Length - 1], target) <= reachTolerance;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}