using UnityEngine;
using UnityEngine.AI;
using StarterAssets;
using System.Collections;

public enum EnemyState { Patrol, Chase, Attack }
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyBehavior : MonoBehaviour
{
    [Header("State Manager")]
    [SerializeField] private EnemyState currentState = EnemyState.Patrol;

    [Header("Actor Manager")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;

    [Header("Attack Manager")]
    [SerializeField] private float sightRange = 10f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private Transform player;
    [SerializeField] private int AttkDMG = 10;

    [Header("Attack Timing")]
    [SerializeField] private float timeBetweenAttacks = 2f;
    private float nextAttackTime = 0f;
    private bool isAttacking = false;
    [SerializeField] private float hitPercent = 0.5f;
    [SerializeField] private string attackStateTag = "Attack";

    private bool _attackInProgress = false;
    private bool _damageThisSwing = false;
    private bool _wasInAttackTag = false;
    private float _nextAttackReadyTime = 0f;
    private bool alreadyAttacked;
    [SerializeField] private float health = 50f;

    private bool IsDead = false;

    [Header("Agent Navigation Manager")]
    private Vector3 walkPoint;
    private bool walkPointSet;
    [SerializeField] private float walkRange = 10f;
    [SerializeField] private LayerMask groundLayer;

    [SerializeField] private float minWaitTime = 1f;
    [SerializeField] private float maxWaitTime = 3f;

    private bool isWaiting = false;
    private Quaternion targetRotation;
    private float turnSpeed = 5f;

    [Header("Death Drops")]
    [SerializeField] private int minGoldDrop = 5;
    [SerializeField] private int maxGoldDrop = 20;

    private IEnumerator WaitAtPoint()
    {
        isWaiting = true;
        agent.ResetPath();
        animator.SetFloat("Moving", 0f);

        float waitTime = Random.Range(minWaitTime, maxWaitTime);
        yield return new WaitForSeconds(waitTime);

        isWaiting = false;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        agent.stoppingDistance = attackRange * 0.9f;
        agent.autoBraking = true;
        agent.updateRotation = false;
    }

    private void Update()
    {
        if (IsDead) return;

        bool playerInSight = PlayerInSight();

        if (playerInSight && PlayerInAttackRange())
            currentState = EnemyState.Attack;
        else if (playerInSight)
            currentState = EnemyState.Chase;
        else
            currentState = EnemyState.Patrol;

        switch (currentState)
        {
            case EnemyState.Patrol: Patrol(); break;
            case EnemyState.Chase: ChasePlayer(); break;
            case EnemyState.Attack: AttackPlayer(); break;
        }

        animator.SetFloat("Moving", agent.velocity.magnitude);
    }

    private bool PlayerInSight()
    {
        if (player == null) return false;

        Vector3 direction = player.position - transform.position;
        float distance = direction.magnitude;

        if (distance > sightRange)
            return false;

        Ray ray = new Ray(transform.position + Vector3.up, direction.normalized);
        if (Physics.Raycast(ray, out RaycastHit hit, sightRange))
        {
            return hit.collider.CompareTag("Player");
        }

        return false;
    }

    private bool PlayerInAttackRange()
    {
        if (player == null) return false;
        return Vector3.Distance(transform.position, player.position) <= attackRange;
    }

    private void Patrol()
    {
        if (isWaiting) return;

        if (!walkPointSet)
        {
            SearchWalkPoint();
            if (walkPointSet)
            {
                Vector3 direction = (walkPoint - transform.position).normalized;
                direction.y = 0;
                if (direction != Vector3.zero)
                    targetRotation = Quaternion.LookRotation(direction);
            }
        }

        if (walkPointSet)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);
            float angleDiff = Quaternion.Angle(transform.rotation, targetRotation);
            if (angleDiff < 10f)
            {
                agent.SetDestination(walkPoint);
            }

            if (Vector3.Distance(transform.position, walkPoint) < 1f)
            {
                walkPointSet = false;
                StartCoroutine(WaitAtPoint());
            }
        }
    }

    private void SearchWalkPoint()
    {
        for (int i = 0; i < 20; i++) 
        {
            Vector3 randomPoint = transform.position + new Vector3(
                Random.Range(-walkRange, walkRange),
                0,
                Random.Range(-walkRange, walkRange)
            );

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 1.0f, NavMesh.AllAreas))
            {
                NavMeshPath path = new NavMeshPath();
                if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    walkPoint = hit.position;
                    walkPointSet = true;
                    return;
                }
            }
        }

        walkPointSet = false;
    }

    private void ChasePlayer()
    {
        if (player == null) return;
        agent.SetDestination(player.position);
        Vector3 look = player.position - transform.position;
        look.y = 0f;
        if (agent.desiredVelocity.sqrMagnitude > 0.01f) look = agent.desiredVelocity;
        if (look.sqrMagnitude > 0.0001f)
        {
            Quaternion target = Quaternion.LookRotation(look.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * 12f);
        }
    }

    //DOUBLE CHECK LOGIC
    private void AttackPlayer()
    {
        if (player == null) return;

        Vector3 flat = player.position - transform.position; flat.y = 0f;
        if (flat.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), Time.deltaTime * 12f);

        bool inRange = PlayerInAttackRange();

        if (!_attackInProgress && Time.time >= _nextAttackReadyTime && inRange)
        {
            _attackInProgress = true;
            _damageThisSwing = false;
            animator.ResetTrigger("Attack");
            animator.SetTrigger("Attack");
        }

        bool coolingDown = Time.time < _nextAttackReadyTime;
        if (_attackInProgress || (coolingDown && inRange))
            agent.ResetPath();
        else
            agent.SetDestination(player.position);

        var state = animator.GetCurrentAnimatorStateInfo(0);
        bool inAttackTag = state.IsTag(attackStateTag);

        if (inAttackTag)
        {
            float cycle = state.normalizedTime % 1f;

            if (!_damageThisSwing && cycle >= hitPercent)
            {
                TryApplyMeleeHit();
                _damageThisSwing = true;
            }
        }

        if (_wasInAttackTag && !inAttackTag)
        {
            _attackInProgress = false;
            _nextAttackReadyTime = Time.time + timeBetweenAttacks;
        }

        _wasInAttackTag = inAttackTag;
    }

    private void TryApplyMeleeHit()
    {
        if (player == null) return;
        Debug.Log("trying to Attacking player");
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= attackRange + 0.4f) 
        {
            Debug.Log("Attacking player");
            player.GetComponent<FirstPersonController>()?.playerDamaged(AttkDMG);
        }
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        health -= amount;
        Debug.Log($"{gameObject.name} took {amount} damage. Remaining: {health}");
        animator.SetTrigger("hit");
        if (health <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} died.");
        agent.isStopped = true;
        animator.SetBool("IsDead", true);
        IsDead = true;
        DropGold();
        Destroy(gameObject, 5f);
    }

    private void ResetAttack()
    {
        alreadyAttacked = false;
    }

    private void DropGold()
    {
        int goldAmount = Random.Range(minGoldDrop, maxGoldDrop + 1);
        Debug.Log($"{gameObject.name} dropped {goldAmount} gold.");
        InventoryManager.Instance?.AddGold(goldAmount);
    }
}
