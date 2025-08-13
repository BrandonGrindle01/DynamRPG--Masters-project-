using UnityEngine;

public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance;

    [Header("Defaults")]
    public Transform defaultSpawn;

    [Header("Persistence")]
    public bool saveToPlayerPrefs = true;
    private const string PPX = "CP_";

    private Transform _currentSpawn;
    public Transform CurrentSpawn => _currentSpawn != null ? _currentSpawn : defaultSpawn;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (saveToPlayerPrefs && defaultSpawn != null)
        {
            var key = PPX + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            var saved = PlayerPrefs.GetString(key, "");
            if (!string.IsNullOrEmpty(saved))
            {
                var t = GameObject.Find(saved)?.transform;
                if (t) _currentSpawn = t;
            }
        }
    }

    public void SetCheckpoint(Transform point)
    {
        var cp = point.GetComponent<Checkpoint>();
        if (cp && cp.isTown && WorldTags.Instance && WorldTags.Instance.IsPlayerCriminal())
        {
            Debug.Log("[Checkpoint] Player is criminal; ignoring town checkpoint: " + point.name);
            return;
        }

        _currentSpawn = point;

        if (saveToPlayerPrefs)
        {
            var key = PPX + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            PlayerPrefs.SetString(key, point.name);
            PlayerPrefs.Save();
        }
    }

    public Checkpoint GetClosestCheckpoint(Vector3 from, bool avoidTown = false)
    {
        Checkpoint best = null;
        float bestSq = float.PositiveInfinity;

        for (int i = 0; i < Checkpoint.All.Count; i++)
        {
            var c = Checkpoint.All[i];
            if (avoidTown && c.isTown) continue;

            float dSq = (c.SpawnTransform.position - from).sqrMagnitude;
            if (dSq < bestSq)
            {
                best = c;
                bestSq = dSq;
            }
        }

        if (best == null)
        {
            for (int i = 0; i < Checkpoint.All.Count; i++)
            {
                var c = Checkpoint.All[i];
                float dSq = (c.SpawnTransform.position - from).sqrMagnitude;
                if (dSq < bestSq) { best = c; bestSq = dSq; }
            }
        }
        return best;
    }

    public Transform GetClosestAllowedSpawn(Vector3 from)
    {
        bool avoidTown = WorldTags.Instance && WorldTags.Instance.IsPlayerCriminal();
        var cp = GetClosestCheckpoint(from, avoidTown);
        return cp ? cp.SpawnTransform : (CurrentSpawn ? CurrentSpawn : defaultSpawn);
    }
}