using UnityEngine;
using UnityEngine.AI;
using StarterAssets;
using System.Collections;
using System.Collections.Generic;

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

    [SerializeField] private float hitPercent = 0.5f;
    [SerializeField] private string attackStateTag = "Attack";

    private bool _attackInProgress = false;
    private bool _damageThisSwing = false;
    private bool _wasInAttackTag = false;
    private float _nextAttackReadyTime = 0f;

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
    [SerializeField] private List<LootDrop> possibleDrops = new();

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [SerializeField] private AudioClip aggroSFX;
    [SerializeField] private AudioClip footstepSFX;
    [SerializeField] private float stepRate = 1.4f;
    [SerializeField] private AudioClip attackWhooshSFX;
    [SerializeField] private AudioClip hurtSFX;
    [SerializeField] private AudioClip deathSFX;

    private float stepTimer = 0f;

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

        if (!audioSource) audioSource = GetComponent<AudioSource>();
        if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;

        agent.autoBraking = true;
        agent.updateRotation = false;
    }

    private void Update()
    {
        if (IsDead) return;

        bool playerInSight = PlayerInSight();
        EnemyState prev = currentState;
        if (playerInSight && PlayerInAttackRange())
            currentState = EnemyState.Attack;
        else if (playerInSight)
            currentState = EnemyState.Chase;
        else
            currentState = EnemyState.Patrol;

        if (prev != currentState)
        {
            if (currentState == EnemyState.Chase) 
                audioSource.PlayOneShot(footstepSFX);

        }

        switch (currentState)
        {
            case EnemyState.Patrol: Patrol(); break;
            case EnemyState.Chase: ChasePlayer(); break;
            case EnemyState.Attack: AttackPlayer(); break;
        }

        animator.SetFloat("Moving", agent.velocity.magnitude);
        FootstepSFX();
    }

    private void FootstepSFX()
    {
        if (!footstepSFX || IsDead) return;

        bool isMoving = agent.enabled && !isWaiting && agent.velocity.magnitude > 0.2f;

        if (isMoving)
        {
            stepTimer += Time.deltaTime;
            float interval = 1f / Mathf.Max(0.01f, stepRate);
            if (stepTimer >= interval)
            {
                stepTimer = 0f;
                if (audioSource) audioSource.PlayOneShot(footstepSFX);
            }
        }
        else
        {
            stepTimer = 0f;
        }
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

        agent.stoppingDistance = 0.1f;

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
        agent.stoppingDistance = attackRange * 0.9f;
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
        agent.stoppingDistance = attackRange * 0.9f;
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

            audioSource.PlayOneShot(attackWhooshSFX);
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
        audioSource.PlayOneShot(hurtSFX);
        if (health <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} died.");
        agent.isStopped = true;
        animator.SetTrigger("isDead");
        audioSource.PlayOneShot(deathSFX);
        IsDead = true;
        DropGold();
        TryDropLoot();
        Destroy(gameObject, 5f);
    }

    private void DropGold()
    {
        int goldAmount = Random.Range(minGoldDrop, maxGoldDrop + 1);
        InventoryManager.Instance?.AddGold(goldAmount);
        var niceName = DialogueService.CleanName(gameObject.name);
        DialogueService.BeginOneLiner(niceName, $"Dropped {goldAmount} gold", null, 3f, true);
    }

    private void TryDropLoot()
    {
        var validDrops = possibleDrops.FindAll(d => d.item != null && Random.value <= d.dropChance);

        if (validDrops.Count > 0)
        {
            var selected = validDrops[Random.Range(0, validDrops.Count)];
            InventoryManager.Instance?.AddItem(selected.item, 1);
            Debug.Log($"{gameObject.name} dropped: {selected.item.name}");
        }
    }
}
