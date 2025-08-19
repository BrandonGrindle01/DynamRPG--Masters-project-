using UnityEngine;

public class QuestGiverIndicator : MonoBehaviour
{
    [Header("Anchor & Offset")]
    public Transform anchor;
    public float heightOffset = 2.2f;
    public bool lookToCamera = true;
    public bool lookYaw = true;


    [Header("Prefabs (assign in Inspector)")]
    public GameObject exclamationPrefab;
    public GameObject questionPrefab;

    [Header("Icon Rotation Offsets (local)")]
    public Vector3 exclamationRotationOffset = Vector3.zero;
    public Vector3 questionRotationOffset = new Vector3(90f, 0f, 0f);

    [Header("Beacon visuals (assign the asset created via Create?Quests?Beacon Params)")]
    public bool spawnBeacon = true;
    public float beamHeight = 20f;
    public float beamRadius = 0.05f;
    [Range(0.05f, 1f)] public float beamAlpha = 0.15f;

    [Header("Beacon Params (SO)")]
    public QuestBeaconParamsAsset beaconParams;

    public void AssignBeaconParams(QuestBeaconParamsAsset p) => beaconParams = p;

    private GameObject _excl;
    private GameObject _ques;
    private GameObject _beacon;

    private void Awake()
    {
        if (!anchor) anchor = transform;
    }

    private void LateUpdate()
    {
        Vector3 basePos = anchor.position + Vector3.up * heightOffset;
        if (_excl) _excl.transform.position = basePos;
        if (_ques) _ques.transform.position = basePos;
        if (_beacon) _beacon.transform.position = anchor.position;

        if (lookToCamera)
        {
            var cam = Camera.main; if (!cam) return;
            LookAtPlayer(_excl, exclamationRotationOffset, cam);
            LookAtPlayer(_ques, questionRotationOffset, cam);
        }
    }

    private void LookAtPlayer(GameObject go, Vector3 offset, Camera cam)
    {
        if (!go) return;

        Vector3 toCam = cam.transform.position - go.transform.position;
        if (lookYaw) toCam.y = 0f;
        if (toCam.sqrMagnitude < 1e-6f) return;

        Quaternion look = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
        go.transform.rotation = look * Quaternion.Euler(offset);
    }


    private void EnsureBeacon()
    {
        if (!spawnBeacon) return;
        if (_beacon) return;

        var matTemplate = beaconParams ? beaconParams.materialTemplate : null;
        float yStart = heightOffset + 0.5f;

        _beacon = QuestBeacon.CreateBeam(
            anchor ? anchor : transform,
            yStart,
            beamHeight,
            beamRadius,
            beamAlpha,
            matTemplate
        );
        if (_beacon) _beacon.SetActive(false);
    }

    private GameObject Ensure(GameObject prefab, ref GameObject instance)
    {
        if (!prefab) return null;
        if (!instance)
        {
            instance = Instantiate(prefab, anchor.position + Vector3.up * heightOffset, Quaternion.identity);
        }
        return instance;
    }

    public void AssignPrefabs(GameObject excl, GameObject ques)
    {
        exclamationPrefab = excl; questionPrefab = ques;
    }

    public void ShowExclamation()
    {
        Ensure(exclamationPrefab, ref _excl);
        EnsureBeacon();

        if (_beacon)
        {
            var c = beaconParams ? beaconParams.normalColor : Color.cyan;
            c.a = beamAlpha;
            QuestBeacon.SetBeaconColor(_beacon, c);
            _beacon.SetActive(true);
        }
        if (_excl) _excl.SetActive(true);
        if (_ques) _ques.SetActive(false);
    }

    public void ShowQuestion()
    {
        Ensure(questionPrefab, ref _ques);
        EnsureBeacon();

        if (_beacon)
        {
            var c = beaconParams ? beaconParams.questionColor : Color.yellow;
            c.a = beamAlpha;
            QuestBeacon.SetBeaconColor(_beacon, c);
            _beacon.SetActive(true);
        }
        if (_ques) _ques.SetActive(true);
        if (_excl) _excl.SetActive(false);
    }

    public void HideAll()
    {
        if (_excl) _excl.SetActive(false);
        if (_ques) _ques.SetActive(false);
        if (_beacon) _beacon.SetActive(false);
    }
}