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
    
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        
        agent.speed = moveSpeed;
        agent.isStopped = true;
    }
    
    void Update()
    {
        if (agent.speed != moveSpeed)
        {
            agent.speed = moveSpeed;
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
}