using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(BoxCollider))]
public class GuardZone : MonoBehaviour
{
    [Header("Spawning")]
    [SerializeField] private GameObject guardPrefab;
    [SerializeField] private int baseGuards = 2;
    [SerializeField] private int maxGuards = 10;
    [SerializeField] private float spawnInterval = 6f;
    [SerializeField] private float minDistanceFromPlayer = 8f;
    [SerializeField] private float sampleRadius = 2.0f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Behavior")]
    [SerializeField] private float calmDespawnDelay = 10f;

    private readonly List<GameObject> activeGuards = new();
    private Coroutine spawnLoop;
    private Transform player;
    private BoxCollider box;
    private bool playerInside;
    private bool spawningEnabled;

    private void Awake()
    {
        box = GetComponent<BoxCollider>();
        if (!box.isTrigger) box.isTrigger = true;
    }

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        spawningEnabled = true;
    }

    private void OnEnable()
    {
        TryStartSpawning();
    }

    private void OnDisable()
    {
        if (spawnLoop != null) StopCoroutine(spawnLoop);
        spawnLoop = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInside = true;
        TryStartSpawning();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInside = false;
        StopSpawnLoop();
        StartCoroutine(DespawnIfCalmAfterDelay());
    }

    private void TryStartSpawning()
    {
        if (!spawningEnabled || spawnLoop != null) return;
        spawnLoop = StartCoroutine(SpawnLoop());
    }

    private void StopSpawnLoop()
    {
        if (spawnLoop != null) StopCoroutine(spawnLoop);
        spawnLoop = null;
    }

    private IEnumerator SpawnLoop()
    {
        var wait = new WaitForSeconds(spawnInterval);

        while (true)
        {
            int desired = CalcDesiredGuardCount();

            activeGuards.RemoveAll(g => g == null);

            int toSpawn = Mathf.Clamp(desired - activeGuards.Count, 0, maxGuards - activeGuards.Count);

            for (int i = 0; i < toSpawn; i++)
            {
                if (TryGetSpawnPoint(out Vector3 pos) &&
                    (player == null || Vector3.Distance(pos, player.position) >= minDistanceFromPlayer))
                {
                    var guard = Instantiate(guardPrefab, pos, Quaternion.identity);
                    activeGuards.Add(guard);

                    var ai = guard.GetComponent<GuardBehaviour>();
                    if (ai != null)
                    {
                        ai.Initialize(player, this, box);
                    }
                }
            }
            if (!playerInside && !WorldTags.Instance.IsPlayerCriminal())
            {
                StopSpawnLoop();
                StartCoroutine(DespawnIfCalmAfterDelay());
                yield break;
            }

            yield return wait;
        }
    }

    private int CalcDesiredGuardCount()
    {
        int crimes = PlayerStatsTracker.Instance ? PlayerStatsTracker.Instance.crimesCommitted : 0;
        bool attackedGuards = WorldTags.Instance && WorldTags.Instance.hasAttackedGuards;
        bool isWanted = WorldTags.Instance && WorldTags.Instance.isPlayerWanted;

        int extra = Mathf.FloorToInt(crimes * 0.5f);
        if (attackedGuards) extra += 2;
        if (isWanted) extra += 1;

        int desired = baseGuards + extra;
        return Mathf.Clamp(desired, 0, maxGuards);
    }

    private bool TryGetSpawnPoint(out Vector3 pos)
    {
        Bounds b = box.bounds;
        for (int i = 0; i < 12; i++)
        {
            Vector3 random = new Vector3(
                Random.Range(b.min.x, b.max.x),
                b.center.y,
                Random.Range(b.min.z, b.max.z)
            );

            if (Physics.Raycast(random + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 50f, groundMask))
            {
                random = hit.point;
            }

            if (NavMesh.SamplePosition(random, out NavMeshHit hitNM, sampleRadius, NavMesh.AllAreas))
            {
                pos = hitNM.position;
                return true;
            }
        }

        pos = b.center;
        return false;
    }

    private IEnumerator DespawnIfCalmAfterDelay()
    {
        yield return new WaitForSeconds(calmDespawnDelay);

        if (playerInside) yield break;
        if (WorldTags.Instance != null && WorldTags.Instance.IsPlayerCriminal()) yield break;

        for (int i = activeGuards.Count - 1; i >= 0; i--)
        {
            if (activeGuards[i] != null) Destroy(activeGuards[i]);
        }
        activeGuards.Clear();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var box = GetComponent<BoxCollider>();
        if (!box) return;

        Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.25f);
        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(1f, 0.35f, 0.1f, 1f);
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = prev;
    }
#endif
}
