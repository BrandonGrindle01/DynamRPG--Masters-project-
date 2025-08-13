using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarterAssets;
using UnityEngine.AI;
using UnityEngine.InputSystem.XR;
using System.Text.RegularExpressions;

[System.Serializable]
public class LootDrop
{
    public ItemData item;
    [Range(0f, 1f)] public float dropChance = 0.2f;
}

public class NPCBehaviour : MonoBehaviour
{
    [Header("General NPC Stats")]
    public Collider MainCol;
    public Animator animator;

    [Header("NPC Health")]
    [SerializeField] private float health = 50f;
    [SerializeField] private GameObject deathEffect;
    private bool isDead = false;

    [Header("Death Drops")]
    [SerializeField] private int minGoldDrop = 5;
    [SerializeField] private int maxGoldDrop = 20;
    [SerializeField] private List<LootDrop> possibleDrops = new();

    [Header("NPC Navigation")]
    [SerializeField] private NavMeshAgent agent;

    [SerializeField] private Vector3 walkPoint;
    bool walkpointSet;
    [SerializeField] private float walkrange;
    [SerializeField] private LayerMask GroundQuery;

    [Header("NPC Spawner logic")]
    private NPCSpawnPoint spawner;
    private bool isReturningHome = false;

    [SerializeField] private Vector3 homePosition;
    [SerializeField] private List<Transform> stallPoints = new();
    [SerializeField] private float loiterDuration = 5f;
    private bool isLoitering = false;
    private float loiterTimer = 0f;

    [Header("NPC Dialogue")]
    public AudioSource source;
    public AudioClip[] genericResponce;

    [SerializeField] private int SpeechIntervalMin, SpeechIntervalMax;
    bool playingAudio = false;

    [Header("Ragdoll")]
    [SerializeField] private Rigidbody hipRigidbody;   
    private Rigidbody[] ragdollBodies;
    private Collider[] ragdollColliders;

    [Header("Flee Behaviour")]
    [SerializeField] private bool enableFleeOnHit = true;
    [SerializeField] private float fleeDuration = 3f;
    [SerializeField] private float fleeDistance = 12f;
    [SerializeField, Range(1f, 3f)] private float fleeSpeedMultiplier = 1.5f;
    [SerializeField] private LayerMask fleeNavMask = ~0;

    private bool inDanger;
    private float fleeTimer;
    private float baseSpeed;
    private Transform player;

    IEnumerator randomVoiceRange()
    {
        yield return new WaitForSeconds(Random.Range(SpeechIntervalMin, SpeechIntervalMax));
        playRandAudio();
        playingAudio = false;
    }
    private void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        ragdollBodies = GetComponentsInChildren<Rigidbody>(includeInactive: true);
        ragdollColliders = GetComponentsInChildren<Collider>(includeInactive: true);

        baseSpeed = agent.speed;
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        SetRagdoll(false);
    }
    private void SetRagdoll(bool enabled)
    {
        if (animator) animator.enabled = !enabled;
        if (MainCol) MainCol.enabled = !enabled;
        if (agent)
        {
            agent.isStopped = enabled;
            agent.updatePosition = !enabled;
            agent.updateRotation = !enabled;
        }

        foreach (var rb in ragdollBodies)
        {
            rb.isKinematic = !enabled;
            rb.detectCollisions = enabled;
        }

        foreach (var col in ragdollColliders)
        {
            if (col == MainCol) continue;
            col.enabled = enabled;
        }
    }

    public void SetHome(Vector3 home)
    {
        homePosition = home;

    }
    public void AssignSpawner(NPCSpawnPoint npcSpawner)
    {
        spawner = npcSpawner;
    }

    public void GoHome()
    {
        isReturningHome = true;
        walkPoint = homePosition;
        walkpointSet = true;
    }

    private void Update()
    {
        patrol();
        if (!playingAudio)
        {
            playingAudio = true;
            StartCoroutine(randomVoiceRange());
        }
        if (inDanger)
        {
            Flee();
            return;
        }
    }

    private void patrol()
    {
        if (isLoitering)
        {
            loiterTimer -= Time.deltaTime;
            if (loiterTimer <= 0f)
            {
                isLoitering = false;
                walkpointSet = false;
            }
            animator.SetFloat("MoveSpeed", 0f);
            return;
        }

        if (!walkpointSet) SearchWalkPoint();

        if (walkpointSet)
        {
            agent.SetDestination(walkPoint);

            Vector3 disttoWP = transform.position - walkPoint;
            if (agent.pathStatus == NavMeshPathStatus.PathInvalid || agent.pathStatus == NavMeshPathStatus.PathPartial)
            {
                walkpointSet = false;
            }
            else if (disttoWP.magnitude < 1f)
            {
                if (isReturningHome)
                {
                    spawner?.OnNPCReturnedHome(this.gameObject);
                    Destroy(this.gameObject);
                    return;
                }

                isLoitering = true;
                loiterTimer = Random.Range(loiterDuration / 2f, loiterDuration * 1.5f);
                transform.LookAt(walkPoint + (walkPoint - transform.position).normalized);
            }

            animator.SetFloat("MoveSpeed", agent.velocity.magnitude);
        }
        else
        {
            animator.SetFloat("MoveSpeed", 0f);
        }
    }

    private void SearchWalkPoint()
    {
        NavMeshHit hit;

        if (homePosition != Vector3.zero && Random.value < 0.3f)
        {
            if (NavMesh.SamplePosition(homePosition, out hit, 2f, NavMesh.AllAreas))
            {
                walkPoint = hit.position;
                walkpointSet = true;
                return;
            }
        }
        //if (stallPoints.Count > 0 && Random.value < 0.5f)
        //{
        //    Transform randomStall = stallPoints[Random.Range(0, stallPoints.Count)];
        //    if (randomStall != null && NavMesh.SamplePosition(randomStall.position, out hit, 2f, NavMesh.AllAreas))
        //    {
        //        walkPoint = hit.position;
        //        walkpointSet = true;
        //        return;
        //    }
        //}
        for (int i = 0; i < 10; i++)
        {
            Vector3 randomPoint = transform.position + new Vector3(Random.Range(-walkrange, walkrange), 0, Random.Range(-walkrange, walkrange));
            if (NavMesh.SamplePosition(randomPoint, out hit, walkrange, NavMesh.AllAreas))
            {
                walkPoint = hit.position;
                walkpointSet = true;
                return;
            }
        }
    }

    private void playRandAudio()
    {
        if (genericResponce.Length > 0)
        {
            int index = Random.Range(0, genericResponce.Length);
            source.clip = genericResponce[index];
            source.volume = .4f;
            source.Play();
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        health -= amount;
        Debug.Log($"{gameObject.name} took {amount} damage. Remaining: {health}");
        animator.SetTrigger("Hit");
        if (enableFleeOnHit && !inDanger)
        {
            StartCoroutine(DelayFleeAfterHit(.4f));
        }
        if (health <= 0)
        {
            Die(); 
        }
    }

    private void SetFleeDestination()
    {
        if (player == null) return;

        Vector3 fleeDir = (transform.position - player.position);
        fleeDir.y = 0f;
        if (fleeDir.sqrMagnitude < 0.01f) fleeDir = transform.forward;
        fleeDir.Normalize();
        Vector3 desired = transform.position + fleeDir * fleeDistance;

        for (int i = 0; i < 6; i++)
        {
            Vector3 TrialLoc = desired + new Vector3(
                Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f)
            );

            if (NavMesh.SamplePosition(TrialLoc, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                Vector3 look = (hit.position - transform.position);
                look.y = 0f;
                if (look.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), Time.deltaTime * 10f);
                return;
            }
        }

        agent.SetDestination(transform.position + fleeDir * (fleeDistance * 0.5f));
    }

    private void Flee()
    {
        if (agent == null) { inDanger = false; return; }

        fleeTimer -= Time.deltaTime;
        animator.SetFloat("MoveSpeed", agent.velocity.magnitude);

        if (!agent.pathPending && agent.remainingDistance < 1.5f)
        {
            SetFleeDestination();
        }

        if (fleeTimer <= 0f)
        {
            inDanger = false;
            agent.speed = baseSpeed;
            walkpointSet = false;
            isLoitering = false;
        }
    }

    private IEnumerator DelayFleeAfterHit(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (!isDead) 
        {
            inDanger = true;
            fleeTimer = fleeDuration;

            agent.speed = baseSpeed * fleeSpeedMultiplier;
            SetFleeDestination();
        }
    }

    private void Die()
    {
        isDead = true;

        if (deathEffect != null)
            Instantiate(deathEffect, transform.position, Quaternion.identity);

        Debug.Log("Player committed a crime — murder!");

        PlayerStatsTracker.Instance.RegisterCrime();
        SetRagdoll(true);

        DropGold();
        TryDropLoot();

        agent.isStopped = true;
        MainCol.enabled = false;

        Destroy(gameObject, 5f);
    }

    private void DropGold()
    {
        int goldAmount = Random.Range(minGoldDrop, maxGoldDrop + 1);
        Debug.Log($"{gameObject.name} dropped {goldAmount} gold.");

        // Replace with your actual gold pickup or economy system:
        InventoryManager.Instance?.AddGold(goldAmount);
    }

    private void TryDropLoot()
    {
        var validDrops = possibleDrops.FindAll(drop => drop.item != null && Random.value <= drop.dropChance);

        if (validDrops.Count > 0)
        {
            var selected = validDrops[Random.Range(0, validDrops.Count)];
            InventoryManager.Instance?.AddItem(selected.item, 1);
            Debug.Log($"{gameObject.name} dropped: {selected.item.name}");
        }
    }
}