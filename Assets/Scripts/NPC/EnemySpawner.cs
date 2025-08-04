using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    [Header("Bandit Spawning")]
    public GameObject banditPrefab;
    public int initialBanditCount = 3;
    public float spawnRadius = 20f;
    public float spawnCheckInterval = 60f;
    public float respawnDelayIfEmpty = 300f; 

    [Header("Boss Spawning")]
    public GameObject bossPrefab;
    public float bossSpawnCooldown = 180f;
    private bool bossSpawned = false;

    private List<GameObject> activeBandits = new();
    private float lastBanditCheckTime;
    private float banditsGoneSince;

    [Header("Debug")]
    public bool showGizmos = true;
    public int gizmoSamplePoints = 25;
    private List<Vector3> lastSampledPoints = new();

    private void Start()
    {
        SpawnBandits(initialBanditCount);
        lastBanditCheckTime = Time.time;
        banditsGoneSince = Time.time;
    }

    private void Update()
    {
        activeBandits.RemoveAll(b => b == null);

        float now = Time.time;

        if (now - lastBanditCheckTime >= spawnCheckInterval)
        {
            lastBanditCheckTime = now;

            if (activeBandits.Count == 0 && now - banditsGoneSince >= respawnDelayIfEmpty)
            {
                SpawnBandits(initialBanditCount);
                banditsGoneSince = now;
            }
            else if (activeBandits.Count > 0)
            {
                SpawnBandits(1);
            }

            if (activeBandits.Count == 0)
                banditsGoneSince = now;
        }


        if (!bossSpawned && activeBandits.Count == 0 && now - banditsGoneSince >= bossSpawnCooldown)
        {
            SpawnBoss();
            bossSpawned = true;
        }
    }

    private void SpawnBandits(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = GetRandomNavmeshPosition();
            GameObject bandit = Instantiate(banditPrefab, pos, Quaternion.identity);
            activeBandits.Add(bandit);
        }
    }

    private void SpawnBoss()
    {
        Vector3 pos = GetRandomNavmeshPosition();
        Instantiate(bossPrefab, pos, Quaternion.identity);
    }

    private Vector3 GetRandomNavmeshPosition()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector2 rand = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = transform.position + new Vector3(rand.x, 0, rand.y);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
#if UNITY_EDITOR
                if (showGizmos) lastSampledPoints.Add(hit.position);
#endif
                return hit.position;
            }
        }

        return transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);

#if UNITY_EDITOR
        Gizmos.color = Color.green;
        int drawCount = Mathf.Min(gizmoSamplePoints, lastSampledPoints.Count);
        for (int i = 0; i < drawCount; i++)
        {
            Gizmos.DrawSphere(lastSampledPoints[i], 0.3f);
        }
#endif
    }
}