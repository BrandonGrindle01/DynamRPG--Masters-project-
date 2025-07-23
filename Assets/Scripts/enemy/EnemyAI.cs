using StarterAssets;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public EnemyStats stats;

    private int currentHealth;
    private bool isDead = false;

    private NavMeshAgent agent;
    private Transform player;
    private bool walkPointSet;
    private Vector3 walkPoint;
    private bool alreadyAttacked;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        player = GameObject.FindGameObjectWithTag("Player").transform;
        currentHealth = stats.maxHealth;
        agent.stoppingDistance = stats.attackRange;
    }

    private void Update()
    {
        if (isDead) return;

        bool inSightRange = Vector3.Distance(transform.position, player.position) <= stats.sightRange;
        bool inAttackRange = Vector3.Distance(transform.position, player.position) <= stats.attackRange;

        if (!inSightRange && !inAttackRange) Patrol();
        else if (inSightRange && !inAttackRange) Chase();
        else if (inSightRange && inAttackRange) StartCoroutine(Attack());
    }

    private void Patrol()
    {
        if (!walkPointSet) SearchWalkPoint();
        else agent.SetDestination(walkPoint);

        if (Vector3.Distance(transform.position, walkPoint) < 1f)
            walkPointSet = false;
    }

    private void SearchWalkPoint()
    {
        float randomZ = Random.Range(-10, 10);
        float randomX = Random.Range(-10, 10);
        walkPoint = new Vector3(transform.position.x + randomX, transform.position.y, transform.position.z + randomZ);

        if (NavMesh.SamplePosition(walkPoint, out NavMeshHit hit, 1f, NavMesh.AllAreas))
        {
            walkPoint = hit.position;
            walkPointSet = true;
        }
    }

    private void Chase()
    {
        agent.speed = stats.speed;
        agent.SetDestination(player.position);
    }

    private IEnumerator Attack()
    {
        if (alreadyAttacked) yield break;

        alreadyAttacked = true;
        agent.SetDestination(transform.position);
        transform.LookAt(player);

        FirstPersonController.instance.playerDamaged(stats.damage);

        yield return new WaitForSeconds(2f);
        alreadyAttacked = false;
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= Mathf.Max(1, amount / stats.defense);
        if (currentHealth <= 0 && !isDead)
        {
            isDead = true;
            InventoryManager.Instance.AddItem(stats.lootDrop);
            Destroy(gameObject, 3f);
        }
    }
}