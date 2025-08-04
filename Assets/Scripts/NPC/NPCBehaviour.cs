using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarterAssets;
using UnityEngine.AI;
using UnityEngine.InputSystem.XR;
using System.Text.RegularExpressions;

public class NPCBehaviour : MonoBehaviour
{
    [Header("General NPC Stats")]
    public ItemData item;
    public Collider MainCol;
    public Animator animator;

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
}
