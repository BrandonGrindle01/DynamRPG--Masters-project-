using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WorldTags : MonoBehaviour
{
    public static WorldTags Instance;

    [Header("World State Flags")]
    public bool isPlayerWanted = false;
    public bool hasAttackedGuards = false;
    public bool hasHelpedVillagers = false;
    //public bool hasJoinedBandits;

    [Header("World Locations")]
    public List<Transform> townLocations = new();
    public List<Transform> remoteLocations = new();
    public List<Transform> secretLocations = new();
    public List<Transform> banditCamps = new();
    public List<GameObject> potentialQuestGivers = new();

    public List<Transform> guardSpawnPoints;

    [Header("World Prefabs")]
    [SerializeField] private GameObject guardPrefab;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SetPlayerWanted(bool value)
    {
        isPlayerWanted = value;
    }

    public void SetAttackedGuards(bool value)
    {
        hasAttackedGuards = value;
    }

    public void SetHelpedVillagers(bool value)
    {
        hasHelpedVillagers = value;
    }

    public bool IsPlayerCriminal()
    {
        return isPlayerWanted || hasAttackedGuards;
    }

    public bool IsPlayerGood()
    {
        return hasHelpedVillagers && !isPlayerWanted;
    }

    //public bool IsPlayerBanditFriendly()
    //{
    //    return hasJoinedBandits;
    //}

    public Transform GetTownLocation()
    {
        if (townLocations.Count > 0)
            return townLocations[Random.Range(0, townLocations.Count)];
        return null;
    }

    public Transform GetRemoteLocation()
    {
        if (remoteLocations.Count > 0)
            return remoteLocations[Random.Range(0, remoteLocations.Count)];
        return null;
    }

    public Transform GetSecretLocation()
    {
        if (secretLocations.Count > 0)
            return secretLocations[Random.Range(0, secretLocations.Count)];
        return null;
    }

    public Transform GetBanditCamp()
    {
        if (banditCamps.Count > 0)
            return banditCamps[Random.Range(0, banditCamps.Count)];
        return null;
    }

    public GameObject GetQuestGiver(bool criminal = false)
    {
        List<GameObject> filtered = potentialQuestGivers.FindAll(giver =>
        {
            if (criminal)
                return giver.CompareTag("Bandit");
            else
                return giver.CompareTag("Town");
        });

        if (filtered.Count > 0)
            return filtered[Random.Range(0, filtered.Count)];
        return null;
    }

    public void SetFlag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return;
        _flags.Add(tag);
    }

    public void ClearFlag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return;
        _flags.Remove(tag);
    }

    public bool HasFlag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return false;
        return _flags.Contains(tag);
    }
}

