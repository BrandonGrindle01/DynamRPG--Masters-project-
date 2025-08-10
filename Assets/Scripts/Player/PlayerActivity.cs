using Unity.VisualScripting;
using UnityEngine;

public class PlayerStatsTracker : MonoBehaviour
{
    public static PlayerStatsTracker Instance;

    [Header("Exploration")]
    public float distanceTraveled;
    private Vector3 lastPosition;

    [Header("Combat & Behavior")]
    public int enemiesKilled;
    public int crimesCommitted;
    public int fightsAvoided;
    public int secretsFound;

    [Header("Narrative")]
    public float timeSinceLastQuest;
    public float timeInIdle;

    private float idleTimer;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        lastPosition = transform.position;
    }

    private void Update()
    {
        TrackDistance();
        TrackIdleTime();
        TrackCriminalBehaviour();
        timeSinceLastQuest += Time.deltaTime;
    }

    private void TrackDistance()
    {
        float moved = Vector3.Distance(transform.position, lastPosition);
        if (moved > 0.1f)
        {
            distanceTraveled += moved;
            idleTimer = 0f;
        }
        else
        {
            idleTimer += Time.deltaTime;
        }
        lastPosition = transform.position;
    }

    private void TrackIdleTime()
    {
        timeInIdle = idleTimer;
    }

    private void TrackCriminalBehaviour()
    {
        if(crimesCommitted > 0)
        {
            WorldTags.Instance.isPlayerWanted = true;
        }
    }
    public void RegisterEnemyKill() => enemiesKilled++;
    public void RegisterCrime() => crimesCommitted++;
    public void RegisterFightAvoided() => fightsAvoided++;
    public void RegisterSecretFound() => secretsFound++;
    public void ResetQuestTimer() => timeSinceLastQuest = 0f;
}

