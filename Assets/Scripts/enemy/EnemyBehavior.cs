using UnityEngine;
using UnityEngine.AI;
using StarterAssets;
using System.Collections;
using System.Collections.Generic;
using LootEntry = InventoryManager.LootEntry;

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

    [Header("Hit React Control")]
    [SerializeField] private bool ignoreHitDuringAttack = true;
    [SerializeField] private bool ignoreHitDuringAttackWindup = true;
    [SerializeField, Range(0f, 1f)] private float attackUninterruptibleWindow = 0.6f;
    [SerializeField] private float hitReactCooldown = 0.35f;

    private float _nextHitReactTime = 0f;

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
    [SerializeField, Range(1, 10)] private int rolls = 1;
    [SerializeField] private bool allowDuplicates = true;

    [SerializeField] private List<LootEntry> loot = new List<LootEntry>();
    [SerializeField, Range(0, 1000)] private int bonusGold = 0;

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

        bool canPlayHit = Time.time >= _nextHitReactTime;

        var stateNow = animator.GetCurrentAnimatorStateInfo(0);
        bool inAttack = stateNow.IsTag(attackStateTag);

        bool enteringAttack = animator.IsInTransition(0) &&
                              animator.GetNextAnimatorStateInfo(0).IsTag(attackStateTag);

        bool withinUninterruptible =
            inAttack && ((stateNow.normalizedTime % 1f) < attackUninterruptibleWindow);

        bool blockHit =
            (ignoreHitDuringAttack && inAttack) ||
            (ignoreHitDuringAttackWindup && enteringAttack) ||
            withinUninterruptible;

        if (canPlayHit && !blockHit)
        {
            animator.ResetTrigger("hit");
            animator.SetTrigger("hit");
            _nextHitReactTime = Time.time + hitReactCooldown;
        }
        if (hurtSFX) audioSource.PlayOneShot(hurtSFX);

        if (health <= 0)
        {
            Die();
            return;
        }
    }

    private void Die()
    {
        agent.isStopped = true;
        animator.SetTrigger("isDead");
        audioSource.PlayOneShot(deathSFX);
        IsDead = true;
        PlayerStatsTracker.Instance?.RegisterEnemyKill();
        QuestService.ReportKill(gameObject);
        QuestService.ReportKeyEnemyKilled(gameObject);

        TryDropLoot();
        Destroy(gameObject, 5f);
    }

    private void TryDropLoot()
    {
        var pool = new List<LootEntry>(loot);

        var gained = new Dictionary<ItemData, int>();
        int goldGained = 0;

        for (int r = 0; r < rolls; r++)
        {
            var candidates = new List<LootEntry>();
            foreach (var e in pool)
            {
                if (e.item == null) continue;
                if (UnityEngine.Random.value <= Mathf.Clamp01(e.chance))
                    candidates.Add(e);
            }
            if (candidates.Count == 0) continue;

            float totalW = 0f;
            foreach (var c in candidates) totalW += Mathf.Max(0.0001f, c.weight);

            float pick = UnityEngine.Random.value * totalW;
            LootEntry chosen = candidates[0];
            float acc = 0f;
            foreach (var c in candidates)
            {
                acc += Mathf.Max(0.0001f, c.weight);
                if (pick <= acc) { chosen = c; break; }
            }

            int amount = UnityEngine.Random.Range(
                Mathf.Max(1, chosen.min),
                Mathf.Max(chosen.min, chosen.max) + 1
            );
            amount = Mathf.Max(1, amount);

            InventoryManager.Instance.AddItem(chosen.item, amount);

            if (!gained.ContainsKey(chosen.item)) gained[chosen.item] = 0;
            gained[chosen.item] += amount;

            if (!allowDuplicates) pool.Remove(chosen);
        }

        if (bonusGold > 0)
        {
            InventoryManager.Instance.AddGold(bonusGold);
            goldGained = bonusGold;
        }

        List<string> parts = new List<string>();
        foreach (var kv in gained)
        {
            if (kv.Key != null)
                parts.Add($"{kv.Value}x {kv.Key.itemName}");
        }
        if (goldGained > 0) parts.Add($"{goldGained} gold");

        string summary = parts.Count > 0 ? string.Join(", ", parts) : "nothing";
        var niceName = DialogueService.CleanName(gameObject.name);
        DialogueService.BeginOneLiner(niceName, $"dropped {summary}.", null, 3f, true);
    }
}
