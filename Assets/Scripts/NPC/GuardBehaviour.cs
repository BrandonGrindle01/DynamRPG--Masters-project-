using StarterAssets;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public enum GuardState { Patrol, Chase, Attack }

[RequireComponent(typeof(NavMeshAgent))]
public class GuardBehaviour : MonoBehaviour
{
    [Header("State Manager")]
    [SerializeField] private GuardState currentState = GuardState.Patrol;

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
    [SerializeField] private float timeBetweenAttacks = 1f;
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

    //misc spawner info
    private GuardZone spawner;
    private BoxCollider zone;

    [Header("Death Drops")]
    [SerializeField] private int minGoldDrop = 5;
    [SerializeField] private int maxGoldDrop = 20;

    private IEnumerator WaitAtPoint()
    {
        isWaiting = true;
        agent.ResetPath();
        animator.SetFloat("MoveSpeed", 0f);

        float waitTime = Random.Range(minWaitTime, maxWaitTime);
        yield return new WaitForSeconds(waitTime);

        isWaiting = false;
    }

    public void Initialize(Transform p, GuardZone s, BoxCollider z)
    {
        player = p;
        spawner = s;
        zone = z;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        if (player == null)
        {
            GameObject pl = GameObject.FindGameObjectWithTag("Player");
            if (pl) player = pl.transform;
        }

        agent.stoppingDistance = attackRange * 0.9f;
        agent.autoBraking = true;
        agent.updateRotation = false;
    }

    private void Update()
    {
        if (IsDead || player == null) return;

        bool canSeePlayer =  PlayerInSight();

        bool mayEngage = (WorldTags.Instance != null && WorldTags.Instance.IsPlayerCriminal());
        bool inAttack = canSeePlayer && PlayerInAttackRange();
        
        if (mayEngage && inAttack) currentState = GuardState.Attack;
        else if (mayEngage && canSeePlayer) currentState = GuardState.Chase;
        else currentState = GuardState.Patrol;

        switch (currentState)
        {
            case GuardState.Patrol: Patrol(); break;
            case GuardState.Chase: ChasePlayer(); break;
            case GuardState.Attack: AttackPlayer(); break;
        }

        animator.SetFloat("MoveSpeed", agent.velocity.magnitude);
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
        Bounds b = zone ? zone.bounds : new Bounds(transform.position, Vector3.one * (walkRange * 2f));

        for (int i = 0; i < 20; i++)
        {
            Vector3 random = new Vector3(
                Random.Range(b.min.x, b.max.x),
                b.center.y,
                Random.Range(b.min.z, b.max.z)
            );

            if (Physics.Raycast(random + Vector3.up * 20f, Vector3.down, out RaycastHit groundHit, 50f, groundLayer))
                random = groundHit.point;

            if (NavMesh.SamplePosition(random, out NavMeshHit nm, 2f, NavMesh.AllAreas))
            {
                var path = new NavMeshPath();
                if (agent.CalculatePath(nm.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    walkPoint = nm.position;
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
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= attackRange + 0.4f)
        {
            player.GetComponent<FirstPersonController>()?.playerDamaged(AttkDMG);
        }
    }


    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        health -= amount;
        Debug.Log($"{gameObject.name} took {amount} damage. Remaining: {health}");
        animator.SetTrigger("Hit");
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

        PlayerStatsTracker.Instance?.RegisterCrime();

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
