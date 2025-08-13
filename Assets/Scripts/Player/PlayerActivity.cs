using TMPro;
using UnityEngine;

public class PlayerStatsTracker : MonoBehaviour
{
    public static PlayerStatsTracker Instance;

    [Header("Exploration")]
    public float distanceTraveled;
    private Vector3 _lastPos;
    private CharacterController _cc;

    [Header("Combat & Behavior")]
    public int enemiesKilled;
    public int crimesCommitted;
    public int fightsAvoided;
    public int secretsFound;

    [Header("Narrative")]
    public float timeSinceLastQuest;
    public float timeInIdle;

    private float _idleTimer;

    private const float MinMoveSpeed = 0.15f;
    private const float TeleportThreshold = 15f;

    [Header("Wanted UI")]
    [SerializeField] private GameObject WantedUi;
    private TextMeshProUGUI crime;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        _cc = GetComponent<CharacterController>();
        _lastPos = transform.position;
        crime = WantedUi.transform.Find("CrimeTitle/crimes")?.GetComponent<TextMeshProUGUI>();
    }

    private void Update()
    {
        timeSinceLastQuest += Time.deltaTime;
        TrackCriminalBehaviour();
    }

    private void LateUpdate()
    {
        TrackDistanceAndIdle();
    }

    private void TrackDistanceAndIdle()
    {
        float jump = Vector3.Distance(transform.position, _lastPos);
        if (jump > TeleportThreshold)
        {
            _lastPos = transform.position;
            _idleTimer = 0f;
            timeInIdle = 0f;
            return;
        }

        float speed;

        if (_cc != null)
        {
            Vector3 v = _cc.velocity; v.y = 0f;
            speed = v.magnitude;
            distanceTraveled += speed * Time.deltaTime;
        }
        else
        {
            distanceTraveled += jump;
            speed = jump / Mathf.Max(Time.deltaTime, 0.0001f);
        }

        if (speed > MinMoveSpeed)
            _idleTimer = 0f;
        else
            _idleTimer += Time.deltaTime;

        timeInIdle = _idleTimer;
        _lastPos = transform.position;
    }

    private void TrackCriminalBehaviour()
    {
        if (crimesCommitted > 0 && WorldTags.Instance != null)
        {
            WorldTags.Instance.isPlayerWanted = true;
            WantedUi.SetActive(true);
            if (crime != null)
            {
                crime.text = crimesCommitted.ToString();
            }
        }
    }

    public void NotifyTeleported(Vector3 newPosition)
    {
        _lastPos = newPosition;
        _idleTimer = 0f;
        timeInIdle = 0f;
    }

    public void RegisterEnemyKill() => enemiesKilled++;
    public void RegisterCrime() => crimesCommitted++;
    public void RegisterFightAvoided() => fightsAvoided++;
    public void RegisterSecretFound() => secretsFound++;
    public void ResetQuestTimer() => timeSinceLastQuest = 0f;
}

