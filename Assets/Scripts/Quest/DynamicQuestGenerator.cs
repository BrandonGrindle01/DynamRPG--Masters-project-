using UnityEngine;
using UnityEngine.AI;
using System.Linq;
using System.Collections.Generic;
using System;

public class DynamicQuestGenerator : MonoBehaviour
{
    public static DynamicQuestGenerator Instance { get; private set; }

    [Header("DEV Overrides")]
    [SerializeField] public bool devForceCollectOnly = false;
    [SerializeField] public bool devForceExploreOnly = false;

    private void D(string msg)
    {
#if UNITY_EDITOR
        Debug.Log($"[DQG] {msg}");
#else
    Debug.Log($"[DQG] {msg}");
#endif
    }

    [Header("Bias & Defaults")]
    public int minBridgeQuests = 1;
    public int maxBridgeQuests = 3;

    [Header("Rewards Tuning")]
    public int baseGold = 30;
    public int goldPerDifficulty = 10;

    [Header("Grounding")]
    [Tooltip("What counts as ground to snap spawned content onto (include Terrain).")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField, Range(0.2f, 3f)] private float spawnClearance = 1.25f;
    [SerializeField] private bool avoidTerrainTrees = true;

    [Header("Explore Setup")]
    [SerializeField] private GameObject secretChestPrefab;
    [SerializeField] private GameObject banditPrefab;
    [SerializeField] private float searchRadius = 10f;
    [SerializeField, Range(0.05f, 1f)] private float zoneAlpha = 0.15f;
    [SerializeField] private int minZoneEnemies = 0;
    [SerializeField] private int maxZoneEnemies = 2;
    [SerializeField] private Material zoneMaterialTemplate;

    [Header("Deliver Setup")]
    [SerializeField] private ItemData parcelItem;
    [SerializeField] private float deliverRadius = 2f;

    [Header("Weight Balancing")]
    [Range(0f, 1f)] public float statBiasStrength = 0.25f;
    [Min(3)] public int exposureWindow = 12;
    [Range(0f, 2f)] public float exposureCorrection = 0.5f;
    [Range(0f, 1f)] public float recencyPenalty = 0.35f;
    [Range(0f, 1f)] public float repeatPenalty = 0.20f;
    [Range(0f, 1f)] public float minWeight = 0.02f;
    [Range(0f, 3f)] public float maxWeight = 2.0f;

    [Header("Target Mix (should sum ? 1)")]
    [Range(0f, 1f)] public float targetExplore = 0.22f;
    [Range(0f, 1f)] public float targetKill = 0.22f;
    [Range(0f, 1f)] public float targetCollect = 0.20f;
    [Range(0f, 1f)] public float targetDeliver = 0.18f;
    [Range(0f, 1f)] public float targetSteal = 0.18f;

    [Header("Steal Tuning")]
    [Range(0f, 0.5f)] public float stealCriminalBonus = 0.12f; 
    [Range(1f, 2f)] public float stealCrimeAffMul = 1.25f;

    private readonly Queue<QuestType> _history = new Queue<QuestType>();

    public enum AnchorType { AnyRemote, SecretOnly, TownOnly, BanditCampOnly }

    [Serializable]
    public class CollectSpec
    {
        public ItemData item;
        [Min(1)] public int minNodes = 3;
        [Min(1)] public int maxNodes = 5;
        [Min(1f)] public float areaRadius = 8f;
        public AnchorType anchor = AnchorType.AnyRemote;
        [Tooltip("Optional SceneRef id for a specific anchor (e.g., 'cave').")]
        public string anchorRefId;
        [Tooltip("Relative chance when multiple specs are valid.")]
        public float weight = 1f;
    }

    [Header("Collect Options")]
    [SerializeField] private List<CollectSpec> collectOptions = new List<CollectSpec>();

    [Header("Variety / Personality")]
    [SerializeField] private int recentWindow = 3;
    private readonly Queue<QuestType> _recent = new Queue<QuestType>();

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    static int GetQuestAreaMask()
    {
        int idx = NavMesh.GetAreaFromName("QuestNPC");
        if (idx < 0) { Debug.LogWarning("NavMesh area 'QuestNPC' not found."); return NavMesh.AllAreas; }
        return 1 << idx;
    }

    private static bool IsBanditGiver(GameObject go) => go && go.CompareTag("Bandit");
    private static bool IsTownGiver(GameObject go) => go && go.CompareTag("Town");

    private static bool CriminalMode(GameObject forcedGiver)
    {
        return (WorldTags.Instance && WorldTags.Instance.IsPlayerCriminal()) || IsBanditGiver(forcedGiver);
    }

    private QuestType RePickNonCriminalType(QuestType original)
    {
        if (original != QuestType.Steal) return original;
        var pool = new[] { QuestType.Explore, QuestType.Kill, QuestType.Collect, QuestType.Deliver };
        return pool[UnityEngine.Random.Range(0, pool.Length)];
    }

    // --------------------------------------------------------------------
    // MAIN ENTRY
    // --------------------------------------------------------------------
    public DynamicQuest GenerateNextDynamicQuest(string contextTag, bool assign = true, GameObject forcedGiver = null)
    {
        Debug.Log("Generating Dynamic Quest");

        var player = PlayerStatsTracker.Instance;
        Vector3 playerPos = player ? player.transform.position : Vector3.zero;
        bool playerCriminal = WorldTags.Instance && WorldTags.Instance.IsPlayerCriminal();

        if (forcedGiver && !playerCriminal && IsBanditGiver(forcedGiver))
            forcedGiver = WorldTags.Instance ? WorldTags.Instance.GetQuestGiver(false) : null;

        if (!assign)
        {
            var pend = QuestService.GetPending();
            if (pend != null)
            {
                if (forcedGiver && pend.questGiver != forcedGiver)
                {
                    pend.questGiver = forcedGiver;
                    QuestService.SetPendingOffer(pend);
                }
                return pend;
            }
        }

        if (forcedGiver && playerCriminal && IsTownGiver(forcedGiver))
            forcedGiver = WorldTags.Instance ? WorldTags.Instance.GetQuestGiver(true) : null;

        QuestType type = PickTypeWeighted();
        var persona = ScorePersonality(player);
        if (persona == Personality.Explorer && type == QuestType.Kill) type = QuestType.Explore;
        if (persona == Personality.Aggressive && type == QuestType.Explore) type = QuestType.Kill;

        if (type == QuestType.Steal && forcedGiver && !IsBanditGiver(forcedGiver))
            type = RePickNonCriminalType(type);

        if (devForceCollectOnly) type = QuestType.Collect;

        if (devForceExploreOnly) type = QuestType.Explore;

        var w = WorldTags.Instance;
        GameObject chosenGiver = forcedGiver;
        if (!chosenGiver)
        {
            bool needBandit = playerCriminal || type == QuestType.Steal;
            chosenGiver = w ? w.GetQuestGiver(needBandit) : null;

            if (playerCriminal && (!chosenGiver || IsTownGiver(chosenGiver))) return null;

            if (!playerCriminal && chosenGiver && IsBanditGiver(chosenGiver))
            {
                var townGiver = w ? w.GetQuestGiver(false) : null;
                if (townGiver) chosenGiver = townGiver;
            }
        }

        if (type == QuestType.Steal)
        {
            if (!playerCriminal)
            {
                type = RePickNonCriminalType(type);
            }
            else if (!IsBanditGiver(chosenGiver))
            {
                var banditGiver = w ? w.GetQuestGiver(true) : null;
                if (banditGiver) chosenGiver = banditGiver;
                else type = RePickNonCriminalType(type);
            }
        }

        bool criminalCtx = CriminalMode(chosenGiver);

        DynamicQuest q = MakeQuestByTypeCriminalAware(type, playerPos, contextTag, criminalCtx);
        q.questGiver = chosenGiver;

        q.goldReward = baseGold + goldPerDifficulty * (persona == Personality.Aggressive ? 2 : 1);

        if (assign) QuestService.AssignDynamic(q);
        return q;
    }

    public DynamicQuest GetCurrentQuest() => QuestService.GetCurrent();

    // --------------------------------------------------------------------
    // TYPE PICKER + HELPERS
    // --------------------------------------------------------------------
    private QuestType PickTypeWeighted()
    {
        var weights = new Dictionary<QuestType, float>
    {
        { QuestType.Explore, targetExplore },
        { QuestType.Kill,    targetKill    },
        { QuestType.Collect, targetCollect },
        { QuestType.Deliver, targetDeliver },
        { QuestType.Steal,   targetSteal   },
    };

        var pm = PlayerStatsTracker.Instance;
        var wt = WorldTags.Instance;
        bool isCriminalNow = wt && wt.IsPlayerCriminal();

        float ExploreAff()
        {
            float d = pm ? pm.distanceTraveled : 0f;
            float t = pm ? pm.timeSinceLastQuest : 0f;
            float x = (d / 900f) + (t / 150f);
            return 1f - Mathf.Exp(-x);
        }

        float CrimeAff()
        {
            float c = pm ? pm.crimesCommitted : 0f;
            return 1f - Mathf.Exp(-c / 4f);
        }

        float Pacifism()
        {
            float kills = pm ? pm.enemiesKilled : 0f;
            float avoided = pm ? pm.fightsAvoided : 0f;
            float denom = kills + avoided;
            float raw = denom > 0f ? (avoided / denom) : 0.5f;
            float confidence = Mathf.Clamp01(denom / 6f);
            return Mathf.Lerp(0.5f, raw, confidence);
        }

        float exploreAff = ExploreAff();
        float crimeAff = CrimeAff();
        float pacifism = Pacifism();
        float s = statBiasStrength;

        weights[QuestType.Explore] *= Mathf.Lerp(1f - s, 1f + s, exploreAff);
        weights[QuestType.Collect] *= Mathf.Lerp(1f - s * 0.6f, 1f + s * 0.6f, Mathf.Clamp01((pm ? pm.secretsFound : 0) / 10f));
        weights[QuestType.Kill] *= Mathf.Lerp(1f + s * 0.2f, 1f - s * 0.2f, pacifism);
        weights[QuestType.Deliver] *= Mathf.Lerp(1f + s * 0.15f, 1f - s * 0.15f, crimeAff);
        weights[QuestType.Steal] *= Mathf.Lerp(1f - s * 0.3f, 1f + s * 0.9f, crimeAff);

        if (!wt || wt.remoteLocations.Count == 0) weights[QuestType.Explore] *= 0.2f;
        if (!wt || wt.banditCamps.Count == 0) weights[QuestType.Kill] *= 0.6f;

        if (!isCriminalNow) weights[QuestType.Steal] = 0f;

        if (_recent.Count > 0)
        {
            var last = _recent.Last();
            int repeats = _recent.Count(t => t == last);
            weights[last] *= (1f - recencyPenalty);
            if (repeats >= 2) weights[last] *= (1f - repeatPenalty);
        }

        if (_history.Count > 0)
        {
            float histCount = _history.Count;
            float eps = 1e-3f;

            float Target(QuestType t) => t switch
            {
                QuestType.Explore => targetExplore,
                QuestType.Kill => targetKill,
                QuestType.Collect => targetCollect,
                QuestType.Deliver => targetDeliver,
                QuestType.Steal => targetSteal,
                _ => 0.2f
            };

            foreach (var t in weights.Keys.ToList())
            {
                float seen = _history.Count(x => x == t);
                float freq = seen / histCount;
                float corr = Mathf.Pow((Target(t) + eps) / (freq + eps), exposureCorrection);
                weights[t] *= corr;
            }
        }

        foreach (var k in weights.Keys.ToList())
        {
            if (weights[k] > 0f)
                weights[k] = Mathf.Clamp(weights[k], minWeight, maxWeight);
        }

        if (isCriminalNow)
        {
            weights[QuestType.Steal] += stealCriminalBonus;
            weights[QuestType.Steal] *= stealCrimeAffMul;
        }

        float sum = weights.Values.Sum();
        if (sum <= 0f)
        {
            weights[QuestType.Explore] = 1f; sum = 1f;
        }
        else
        {
            foreach (var k in weights.Keys.ToList())
                weights[k] /= sum;
        }

#if UNITY_EDITOR
        Debug.Log("[DQG Weights] " + string.Join(", ", weights.Select(kv => $"{kv.Key}:{kv.Value:F2}")));
#endif
        RuntimeDevConsole.SetWeights(weights.ToDictionary(kv => kv.Key, kv => kv.Value));

        float r = UnityEngine.Random.value;
        float acc = 0f;
        foreach (var kv in weights)
        {
            acc += kv.Value;
            if (r <= acc)
            {
                _recent.Enqueue(kv.Key);
                while (_recent.Count > recentWindow) _recent.Dequeue();

                _history.Enqueue(kv.Key);
                while (_history.Count > exposureWindow) _history.Dequeue();

                return kv.Key;
            }
        }
        return QuestType.Explore;
    }

    private DynamicQuest MakeQuestByTypeCriminalAware(QuestType type, Vector3 playerPos, string ctx, bool criminalCtx)
    {
        switch (type)
        {
            case QuestType.Kill: return MakeKillQuest(playerPos, ctx, criminalCtx);
            case QuestType.Steal: return MakeStealQuest(playerPos, criminalCtx);
            case QuestType.Collect: return MakeCollectQuest(playerPos, criminalCtx);
            case QuestType.Deliver: return MakeDeliverQuest(playerPos, criminalCtx);
            case QuestType.Explore:
            default: return MakeExploreQuest(playerPos, criminalCtx);
        }
    }

    // --------------------------------------------------------------------
    // QUEST MAKERS
    // --------------------------------------------------------------------
    private Personality ScorePersonality(PlayerStatsTracker s)
    {
        if (s == null) return Personality.Balanced;
        if (s.crimesCommitted > s.enemiesKilled && s.crimesCommitted >= 2) return Personality.Criminal;
        if (s.enemiesKilled >= 4 && s.fightsAvoided < s.enemiesKilled / 2) return Personality.Aggressive;
        if (s.distanceTraveled > 800f && s.timeSinceLastQuest > 60f) return Personality.Explorer;
        if (s.fightsAvoided > s.enemiesKilled + 2) return Personality.Pacifist;
        return Personality.Balanced;
    }

    private DynamicQuest MakeKillQuest(Vector3 playerPos, string ctx, bool criminalCtx)
    {
        var wt = WorldTags.Instance;
        Vector3 center;

        if (!criminalCtx)
        {
            Transform camp = wt && wt.banditCamps.Count > 0
                ? wt.banditCamps.OrderBy(c => Vector3.Distance(playerPos, c.position)).FirstOrDefault()
                : null;

            center = camp ? camp.position :
                playerPos + new Vector3(UnityEngine.Random.Range(25f, 45f), 0f, UnityEngine.Random.Range(25f, 45f));
        }
        else
        {
            Transform town = wt && wt.townLocations.Count > 0
                ? wt.townLocations.OrderBy(t => Vector3.Distance(playerPos, t.position)).FirstOrDefault()
                : null;

            Vector3 basePos = town ? town.position : playerPos;
            Vector2 r = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(18f, 32f);
            center = basePos + new Vector3(r.x, 0f, r.y);
        }

        center = SnapToGround(center);
        int need = UnityEngine.Random.Range(3, 6);
        string tag = criminalCtx ? "Guard" : "Bandit";

        var q = new DynamicQuest
        {
            type = QuestType.Kill,
            questName = (criminalCtx ? $"Punish the patrol ({need})" : $"Thin the camp ({need})"),
            targetPosition = center,
            areaRadius = 25f,
            requiredCount = need,
            killRequiredTag = tag,
            contextTag = string.IsNullOrEmpty(ctx) ? (criminalCtx ? "crime" : "bandit") : ctx,
            introText = (criminalCtx ? $"Take out {need} {tag.ToLower()}s near the marked area."
                                     : $"Defeat {need} bandits at the marked camp."),
            successText = (criminalCtx ? "That should deliver a msg." : "Thank you, that should hold the bandits at bay.")
        };

        if (!criminalCtx) SetupKillRuntime(q);
        return q;
    }

    private DynamicQuest MakeStealQuest(Vector3 playerPos, bool criminalCtx)
    {
        var items = UnityEngine.Object.FindObjectsByType<ItemCollection>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var target = items.Where(i => i.isOwned)
                          .OrderBy(i => Vector3.Distance(playerPos, i.transform.position))
                          .FirstOrDefault();

        string itemName = target && target.itemData ? target.itemData.itemName : "valuables";

        return new DynamicQuest
        {
            type = QuestType.Steal,
            questName = criminalCtx ? $"Acquire {itemName}" : $"Lift {itemName}",
            targetObject = target ? target.gameObject : null,
            targetItemName = target && target.itemData ? target.itemData.itemName : null,
            requiredCount = 1,
            requireStealth = true,
            introText = criminalCtx ? "Keep it quiet. Don’t get seen." : "Keep it quiet.",
            successText = criminalCtx ? "nicely done rook." : "Clean job."
        };
    }

    private DynamicQuest MakeExploreQuest(Vector3 playerPos, bool criminalCtx)
    {
        var giver = KeyQuestManager.Instance ? KeyQuestManager.Instance.Current?.questGiver : null;
        Vector3 center = PickExploreCenter(giver); 

        if (criminalCtx && WorldTags.Instance && WorldTags.Instance.townLocations.Count > 0)
        {
            var town = WorldTags.Instance.townLocations
                        .OrderBy(t => Vector3.Distance(center, t.position)).First();
            if (Vector3.Distance(center, town.position) < 40f)
                center = STGFromPoint(center + (center - town.position).normalized * 30f);
        }

        return new DynamicQuest
        {
            type = QuestType.Explore,
            questName = criminalCtx ? "Case the area" : "Search the area",
            targetPosition = center,
            requiredCount = 1,
            areaRadius = searchRadius,
            introText = criminalCtx ? "Scout the marked zone and look for an opportunity."
                                : "Investigate the marked zone and look for anything out of place.",
            successText = criminalCtx ? "Recon complete." : "Findings secured."
        };
    }

    private DynamicQuest MakeCollectQuest(Vector3 playerPos, bool criminalCtx)
    {
        var spec = PickCollectSpecFiltered(criminalCtx);
        if (spec == null || spec.item == null)
            return MakeExploreQuest(playerPos, criminalCtx);

        Vector3 anchorPos = PickCollectAnchorPosition(spec, playerPos);
        int need = UnityEngine.Random.Range(spec.minNodes, spec.maxNodes + 1);
        need = Mathf.Max(1, need);

        var q = new DynamicQuest
        {
            type = QuestType.Collect,
            questName = criminalCtx ? $"Lift {need} {spec.item.itemName}" : $"Gather {need} {spec.item.itemName}",
            targetPosition = anchorPos,
            areaRadius = Mathf.Max(2f, spec.areaRadius),
            requiredCount = need,
            targetItemName = spec.item.itemName,
            collectItem = spec.item,
            introText = criminalCtx ? $"Acquire {need} {spec.item.itemName} from the marked area."
                                    : $"Gather {need} {spec.item.itemName} from the marked area.",
            successText = criminalCtx ? "perfectly done." : "thats just what i needed! thank you"
        };

        SetupCollectRuntime(q, spec);
        return q;
    }

    private DynamicQuest MakeDeliverQuest(Vector3 playerPos, bool criminalCtx)
    {
        Vector3 dropPos = playerPos + new Vector3(UnityEngine.Random.Range(18f, 32f), 0f, UnityEngine.Random.Range(18f, 32f));
        var wt = WorldTags.Instance;

        if (!criminalCtx)
        {
            var town = wt ? wt.GetTownLocation() : null;
            if (town) dropPos = town.position;
        }
        else
        {
            var remote = wt ? (wt.secretLocations.Count > 0 ? wt.secretLocations : wt.remoteLocations) : null;
            if (remote != null && remote.Count > 0)
                dropPos = remote[UnityEngine.Random.Range(0, remote.Count)].position;
        }

        dropPos = STGFromPoint(dropPos);

        return new DynamicQuest
        {
            type = QuestType.Deliver,
            questName = criminalCtx ? "Drop the package" : "Deliver the parcel",
            requiredCount = 1,
            introText = criminalCtx ? "Bring the package to the drop site." : "Take the parcel to the marked spot.",
            successText = "the parcel has been retrieved, thanks",
            targetPosition = dropPos,
            areaRadius = deliverRadius,
            deliverItem = parcelItem
        };
    }

    // --------------------------------------------------------------------
    // EXPLORE HELPERS (zone + hidden token / chest)
    // --------------------------------------------------------------------
    private Vector3 PickExploreCenter(GameObject forcedGiver)
    {
        var wt = WorldTags.Instance;
        Vector3 basePos = forcedGiver ? forcedGiver.transform.position :
                          (PlayerStatsTracker.Instance ? PlayerStatsTracker.Instance.transform.position : Vector3.zero);

        var anchors = new List<Transform>();
        if (wt != null)
        {
            anchors.AddRange(wt.remoteLocations);
            anchors.AddRange(wt.secretLocations);
        }

        anchors = anchors.Where(a =>
            a != null &&
            (forcedGiver == null || Vector3.Distance(a.position, basePos) > 40f) &&
            (wt == null || wt.banditCamps.TrueForAll(c => Vector3.Distance(a.position, c.position) > 25f))
        ).ToList();

        Transform pick = (anchors.Count > 0) ? anchors[UnityEngine.Random.Range(0, anchors.Count)] : null;
        Vector3 center = pick ? pick.position : basePos + new Vector3(UnityEngine.Random.Range(25f, 45f), 0f, UnityEngine.Random.Range(25f, 45f));

        bool isSecretAnchor = pick && wt != null && wt.secretLocations.Contains(pick);
        if (!isSecretAnchor)
        {
            Vector2 r = UnityEngine.Random.insideUnitCircle * 12f;
            center = new Vector3(center.x + r.x, center.y, center.z + r.y);
        }

        return STGFromPoint(center);
    }

    public void SetupExploreSearchRuntime(DynamicQuest q)
    {
        if (q == null) return;

        float r = (q.areaRadius > 0f) ? q.areaRadius : searchRadius; 
        float a = (q.zoneAlphaOverride >= 0f) ? q.zoneAlphaOverride : zoneAlpha;
        float spread = (q.scatterFactor > 0f) ? q.scatterFactor : 0.8f;

        var zone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        zone.name = "QuestSearchZone";
        var col = zone.GetComponent<Collider>(); if (col) Destroy(col);
        var pos = STGFromPoint(q.targetPosition);

        zone.transform.position = pos;
        zone.transform.localScale = new Vector3(r * 2f, 5f, r * 2f);

        var mr = zone.GetComponent<MeshRenderer>();
        if (mr && zoneMaterialTemplate)
        {
            var mat = Instantiate(zoneMaterialTemplate);
            var color = new Color(0.2f, 0.7f, 1f, a);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
        QuestService.RegisterRuntimeObject(q.questId, zone);

        Vector3 tokenPos;
        if (!TryFindClearGroundPoint(pos, r * spread * 0.95f, out tokenPos))
            tokenPos = STGFromPoint(pos);
        tokenPos.y += 0.05f;
        GameObject tokenGO = null;
        if (secretChestPrefab != null)
        {
            tokenGO = Instantiate(secretChestPrefab, tokenPos, Quaternion.identity);
            tokenGO.name = "QuestSecretChest";
            var chest = tokenGO.GetComponent<SecretChest>();
            if (chest != null) { chest.isQuestChest = true; chest.questTokenId = $"q_{q.questId}_token"; }
            else { var hook = MakeInvisibleToken(tokenPos, $"q_{q.questId}_token"); tokenGO = hook.gameObject; }
        }
        else
        {
            var hook = MakeInvisibleToken(tokenPos, $"q_{q.questId}_token");
            tokenGO = hook.gameObject;
        }
        QuestService.RegisterRuntimeObject(q.questId, tokenGO);

        var spawned = new List<EnemyBehavior>();
        int toSpawn = banditPrefab ? UnityEngine.Random.Range(minZoneEnemies, maxZoneEnemies + 1) : 0;
        for (int i = 0; i < toSpawn; i++)
        {
            Vector2 rv = UnityEngine.Random.insideUnitCircle * (r * spread);
            var p = STGFromPoint(pos + new Vector3(rv.x, 0f, rv.y));
            var eGO = SafeSpawnOnNavmesh(banditPrefab, p, 3f);
            if (eGO)
            {
                QuestService.RegisterRuntimeObject(q.questId, eGO);
                var eb = eGO.GetComponent<EnemyBehavior>();
                if (eb) spawned.Add(eb);
            }
        }

        var avoidHook = tokenGO.AddComponent<QuestChestAvoidanceHook>();
        avoidHook.enemies = spawned.Where(x => x != null).ToList();
    }


    // --------------------------------------------------------------------
    // DELIVER HELPERS (drop zone + trigger)
    // --------------------------------------------------------------------
    public void SetupDeliverRuntime(DynamicQuest q)
    {
        if (q == null) return;

        float r = (q.deliverRadiusOverride >= 0f) ? q.deliverRadiusOverride :
                  (q.areaRadius > 0f ? q.areaRadius : deliverRadius);
        float a = (q.zoneAlphaOverride >= 0f) ? q.zoneAlphaOverride : 0.18f;

        var pos = STGFromPoint(q.targetPosition);

        var zone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        zone.name = "DeliverDropZone";
        var col = zone.GetComponent<Collider>(); if (col) Destroy(col);
        zone.transform.position = pos;
        zone.transform.localScale = new Vector3(r * 2f, 5f, r * 2f);
        var mr = zone.GetComponent<MeshRenderer>();
        if (mr && zoneMaterialTemplate)
        {
            var mat = Instantiate(zoneMaterialTemplate);

            var color = new Color(0.6f, 1f, 0.4f, a);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        QuestService.RegisterRuntimeObject(q.questId, zone);
        var trigger = new GameObject("DeliverTrigger");
        var sc = trigger.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = Mathf.Max(1f, r * 0.75f);
        trigger.transform.position = pos;

        var drop = trigger.AddComponent<DeliverySpot>();
        drop.questId = q.questId;
        drop.requiredItem = q.deliverItem;
        QuestService.RegisterRuntimeObject(q.questId, trigger);
    }


    // --------------------------------------------------------------------
    // COLLECT HELPERS (spawn actual item prefabs when available)
    // --------------------------------------------------------------------
    public void SetupCollectRuntime(DynamicQuest q)
    {
        CollectSpec spec = null;
        if (collectOptions != null && q != null && q.collectItem != null)
        {
            spec = collectOptions.FirstOrDefault(s => s != null && s.item == q.collectItem);
        }

        if (spec == null && q != null)
        {
            spec = new CollectSpec
            {
                item = q.collectItem,
                minNodes = Mathf.Max(1, q.requiredCount),
                maxNodes = Mathf.Max(1, q.requiredCount),
                areaRadius = Mathf.Max(6f, q.areaRadius),
                anchor = AnchorType.AnyRemote,
                weight = 1f
            };
        }

        SetupCollectRuntime(q, spec);
    }

    public void SetupCollectRuntime(DynamicQuest q, CollectSpec spec)
    {
        if (q == null || spec == null) return;
        float r = Mathf.Max(2f, q.areaRadius);
        float spread = (q.scatterFactor > 0f) ? q.scatterFactor : 0.8f;

        int nodes = Mathf.Max(1, q.requiredCount);
        for (int i = 0; i < nodes; i++)
        {
            Vector3 p;
            if (!TryFindClearGroundPoint(q.targetPosition, r * spread, out p))
            {
                Debug.Log(q.targetPosition);
                p = STGFromPoint(q.targetPosition);
            }
            GameObject node = null;

            if (spec.item && spec.item.worldPrefab)
            {
                node = Instantiate(spec.item.worldPrefab, p, Quaternion.identity);
                node.name = $"Collect_{spec.item.itemName}";
            }
            else
            {
                node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                node.name = "CollectNode";
                node.transform.position = p;
                node.transform.localScale = Vector3.one * 0.5f;

                var sc = node.GetComponent<SphereCollider>(); if (sc) sc.isTrigger = true;
                var mr = node.GetComponent<MeshRenderer>();
                if (mr)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    var mat = new Material(shader);
                    var c = new Color(0.3f, 1f, 0.6f, 0.8f);
                    if (shader.name.Contains("Standard")) mat.SetColor("_Color", c); else mat.SetColor("_BaseColor", c);
                    mr.material = mat;
                }

                var cn = node.AddComponent<CollectNode>();
                cn.item = spec.item;
            }

            QuestService.RegisterRuntimeObject(q.questId, node);
        }
    }

    public void SetupKillRuntime(DynamicQuest q)
    {
        if (q == null) return;

        if (!string.IsNullOrEmpty(q.killRequiredTag) && q.killRequiredTag != "Bandit")
            return;
        float spread = (q.scatterFactor > 0f) ? q.scatterFactor : 0.7f;
        int present = 0;
        var enemies = UnityEngine.Object.FindObjectsByType<EnemyBehavior>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var e in enemies)
        {
            if (!e) continue;
            if (!e.gameObject.CompareTag(q.killRequiredTag ?? "Bandit")) continue;
            if (q.areaRadius > 0f && Vector3.Distance(e.transform.position, q.targetPosition) > q.areaRadius) continue;
            present++;
        }

        if (banditPrefab && present < q.requiredCount)
        {
            int toSpawn = q.requiredCount - present;
            for (int i = 0; i < toSpawn; i++)
            {
                Vector2 r = UnityEngine.Random.insideUnitCircle * (Mathf.Max(8f, q.areaRadius * spread));
                var p = SnapToGround(q.targetPosition + new Vector3(r.x, 0f, r.y));
                var e = SafeSpawnOnNavmesh(banditPrefab, p, 3f);
                if (e) QuestService.RegisterRuntimeObject(q.questId, e);
            }
        }
    }

    // --------------------------------------------------------------------
    // COLLECT PICKING (spec + anchor)
    // --------------------------------------------------------------------
    private CollectSpec PickCollectSpec()
    {
        if (collectOptions == null || collectOptions.Count == 0) return null;
        var wt = WorldTags.Instance;

        List<CollectSpec> valid = new List<CollectSpec>();
        foreach (var spec in collectOptions)
        {
            if (spec == null || spec.item == null) continue;

            var anchors = GetAnchorsFor(spec.anchor);
            if (anchors == null || anchors.Count == 0) continue;

            if (!string.IsNullOrWhiteSpace(spec.anchorRefId))
            {
                bool found = anchors.Any(a =>
                {
                    var sr = a ? a.GetComponent<SceneRef>() : null;
                    return sr && sr.id == spec.anchorRefId;
                });
                if (!found) continue;
            }

            valid.Add(spec);
        }

        if (valid.Count == 0) return null;

        float sum = valid.Sum(v => Mathf.Max(0.0001f, v.weight));
        float r = UnityEngine.Random.value * sum;
        foreach (var v in valid)
        {
            float w = Mathf.Max(0.0001f, v.weight);
            if (r <= w) return v;
            r -= w;
        }
        return valid[0];
    }

    private CollectSpec PickCollectSpecFiltered(bool criminalCtx)
    {
        if (collectOptions == null || collectOptions.Count == 0) return null;

        var candidates = collectOptions.Where(s => s != null && s.item != null).ToList();
        if (criminalCtx)
            candidates = candidates.Where(s => s.anchor != AnchorType.TownOnly).ToList();

        if (candidates.Count == 0) return null;

        float sum = candidates.Sum(v => Mathf.Max(0.0001f, v.weight));
        float r = UnityEngine.Random.value * sum;
        foreach (var v in candidates)
        {
            float w = Mathf.Max(0.0001f, v.weight);
            if (r <= w) return v;
            r -= w;
        }
        return candidates[0];
    }

    private Vector3 PickCollectAnchorPosition(CollectSpec spec, Vector3 playerPos)
    {
        var anchors = GetAnchorsFor(spec.anchor);
        if (anchors == null || anchors.Count == 0)
            return STGFromPoint(playerPos);

        Transform pick = null;

        if (!string.IsNullOrWhiteSpace(spec.anchorRefId))
        {
            pick = anchors.FirstOrDefault(a =>
            {
                var sr = a ? a.GetComponent<SceneRef>() : null;
                return sr && sr.id == spec.anchorRefId;
            });
        }

        if (!pick)
            pick = anchors.OrderBy(a => Vector3.Distance(playerPos, a.position)).FirstOrDefault();
        return STGFromPoint(pick ? pick.position : playerPos);
    }

    private List<Transform> GetAnchorsFor(AnchorType t)
    {
        var wt = WorldTags.Instance;
        if (wt == null) return null;
        switch (t)
        {
            case AnchorType.SecretOnly: return wt.secretLocations;
            case AnchorType.TownOnly: return wt.townLocations;
            case AnchorType.BanditCampOnly: return wt.banditCamps;
            case AnchorType.AnyRemote:
            default: return wt.remoteLocations;
        }
    }

    // --------------------------------------------------------------------
    // Small runtime helper classes (embedded)
    // --------------------------------------------------------------------
    private HiddenQuestToken MakeInvisibleToken(Vector3 at, string tokenId)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var sc = go.GetComponent<SphereCollider>(); if (sc) sc.isTrigger = true;
        var mr = go.GetComponent<MeshRenderer>(); if (mr) mr.enabled = false;
        go.transform.position = at;
        go.transform.localScale = Vector3.one * 0.35f;
        var hook = go.AddComponent<HiddenQuestToken>();
        hook.questTokenId = tokenId;
        return hook;
    }

    public class HiddenQuestToken : MonoBehaviour
    {
        public string questTokenId;
        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (string.IsNullOrEmpty(questTokenId)) return;
            QuestService.ReportEnteredLocation(questTokenId);
            Destroy(gameObject);
        }
    }

    public class DeliverySpot : MonoBehaviour
    {
        public string questId;
        public ItemData requiredItem;

        private bool HasItem()
        {
            if (!requiredItem || InventoryManager.Instance == null) return true;
            foreach (var slot in InventoryManager.Instance.inventory)
                if (slot.item == requiredItem && slot.quantity > 0)
                    return true;
            return false;
        }

        private void ConsumeOne()
        {
            if (!requiredItem || InventoryManager.Instance == null) return;
            InventoryManager.Instance.RemoveItem(requiredItem, 1);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (!HasItem())
            {
                DialogueService.BeginOneLiner("", "You need the parcel to deliver here.", null, 2f, true);
                return;
            }

            ConsumeOne();
            QuestService.ReportDelivered(questId);
            DialogueService.BeginOneLiner("", "Delivered.", null, 2f, true);
            Destroy(gameObject);
        }
    }

    public class CollectNode : MonoBehaviour
    {
        public ItemData item;
        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (item != null) InventoryManager.Instance?.AddItem(item, 1);
            Destroy(gameObject);
        }
    }

    // --------------------------------------------------------------------
    // Utilities
    // --------------------------------------------------------------------
    private Vector3 SnapToGround(Vector3 pos)
    {
        var start = pos + Vector3.up * 50f;
        if (Physics.Raycast(start, Vector3.down, out var hit, 200f, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point;
        return pos;
    }

    private Vector3 STGFromPoint(Vector3 pos, float verticalWindow = 6f)
    {
        var start = pos + Vector3.up * verticalWindow;
        Debug.Log(pos);
        if (Physics.Raycast(start, Vector3.down, out var hit, verticalWindow * 2f, groundMask, QueryTriggerInteraction.Ignore))
        {
            Ray ray = new Ray(start, Vector3.down);
            Debug.DrawRay(ray.origin, ray.direction,Color.red, 10000f);

            return hit.point;
        }
            

        return SnapToGround(pos);
    }

    private GameObject SafeSpawnOnNavmesh(GameObject prefab, Vector3 near, float maxDistance, bool questAgent = false)
    {
        if (!prefab) return null;

        int mask = questAgent ? GetQuestAreaMask() : NavMesh.AllAreas;

        Vector3 spawn = near;
        if (NavMesh.SamplePosition(near, out var hit, maxDistance, mask))
            spawn = hit.position;

        var go = Instantiate(prefab, spawn, Quaternion.identity);

        var agent = go.GetComponent<NavMeshAgent>();
        if (agent)
        {
            agent.areaMask = mask;

            if (!agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(go.transform.position, out var snap, 5f, mask))
                    agent.Warp(snap.position);
                else
                    agent.enabled = false;
            }
        }

        return go;
    }

    private bool TryFindClearGroundPoint(Vector3 center, float radius, out Vector3 outPos)
    {
        for (int i = 0; i < 24; i++)
        {
            Vector2 r = UnityEngine.Random.insideUnitCircle * Mathf.Max(0.5f, radius);
            var candidate = STGFromPoint(center + new Vector3(r.x, .5f, r.y));

            bool blocked = Physics.CheckSphere(candidate, spawnClearance, obstacleMask, QueryTriggerInteraction.Ignore);
            if (blocked) continue;

            if (avoidTerrainTrees && IsNearTerrainTree(candidate, spawnClearance)) continue;

            outPos = candidate;
            return true;
        }

        outPos = STGFromPoint(center);
        return false;
    }

    private bool IsNearTerrainTree(Vector3 worldPos, float minDist)
    {
        if (!avoidTerrainTrees) return false;

        var terrain = Terrain.activeTerrain;
        if (!terrain) return false;

        var data = terrain.terrainData;
        var tPos = terrain.transform.position;
        var size = data.size;
        float nx = (worldPos.x - tPos.x) / size.x;
        float nz = (worldPos.z - tPos.z) / size.z;
        if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) return false;

        float minSqr = minDist * minDist;
        var trees = data.treeInstances;
        for (int i = 0; i < trees.Length; i++)
        {
            var ti = trees[i];
            float tx = tPos.x + ti.position.x * size.x;
            float tz = tPos.z + ti.position.z * size.z;
            float dx = worldPos.x - tx;
            float dz = worldPos.z - tz;
            if ((dx * dx + dz * dz) < minSqr) return true;
        }
        return false;
    }

    public class QuestChestAvoidanceHook : MonoBehaviour
    {
        public List<EnemyBehavior> enemies = new List<EnemyBehavior>();
        private bool fired;

        private void Awake()
        {
            // Prefer an explicit chest event if available
            var chest = GetComponent<SecretChest>();
            if (chest != null)
            {
                chest.OnOpened += HandleOpened; // see tiny patch below
            }
        }

        private void OnDestroy()
        {
            var chest = GetComponent<SecretChest>();
            if (chest != null) chest.OnOpened -= HandleOpened;
        }

        private void HandleOpened()
        {
            TryMarkAvoided();
        }

        // Fallback for the invisible token variant (trigger == “opened”)
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player")) TryMarkAvoided();
        }

        private void TryMarkAvoided()
        {
            if (fired) return;
            fired = true;

            bool anyAlive = enemies.Any(e => e != null && e.gameObject.activeInHierarchy);
            if (anyAlive)
            {
                // Use your real tracker/field names here
                var ps = PlayerStatsTracker.Instance;
                if (ps != null) ps.fightsAvoided++;   // or PlayerActivity.FightsAvioded++;
            }
        }
    }
}
