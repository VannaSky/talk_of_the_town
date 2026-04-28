using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class VillagerMover : MonoBehaviour
{
    private NavMeshAgent agent;

    [Header("Movement Settings")]
    [Tooltip("The speed at which the villager moves.")]
    public float moveSpeed = 3.5f;

    [Tooltip("The distance threshold to consider the villager 'near' their destination.")]
    public float proximityThreshold = 2.0f;

    [Tooltip("Rotation speed in degrees per second when facing a target.")]
    public float rotationSpeed = 360f;

    private Vector3? _faceTarget;

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
    }

    public void MoveTo(Vector3 targetPosition)
    {
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(targetPosition);
        }
        else
        {
            Debug.LogError($"{gameObject.name} cannot move! NavMeshAgent is not active or not placed on the NavMesh.");
        }
    }

    public void StopMoving()
    {
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
}