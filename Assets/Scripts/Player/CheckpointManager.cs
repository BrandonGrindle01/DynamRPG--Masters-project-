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
        _currentSpawn = point;

        if (saveToPlayerPrefs)
        {
            var key = PPX + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            PlayerPrefs.SetString(key, point.name);
            PlayerPrefs.Save();
        }
        // Debug.Log($"Checkpoint set: {point.name}");
    }
}