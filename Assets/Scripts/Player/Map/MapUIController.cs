using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MapUIController : MonoBehaviour
{
    [Header("Refs")]
    public MapProjector projector;
    public RectTransform iconsRoot;
    public RectTransform mapRect;
    public RawImage mapImage;

    [Header("Icon Prefab")]
    public GameObject iconPrefab;

    [Header("Default Sprites")]
    public Sprite sprLandmark;
    public Sprite sprTrader;
    public Sprite sprQuestGiver;
    public Sprite sprQuestTarget;
    public Sprite sprPlayer;
    public Sprite sprCustom;

    [Header("Filters")]
    public bool showLandmarks = true;
    public bool showTraders = true;
    public bool showQuestGivers = true;
    public bool showQuestTargets = true;
    public bool showPlayer = true;
    public bool showCustom = true;

    [Header("Zoom/Pan")]
    public float minZoom = 0.5f;
    public float maxZoom = 2.0f;
    public float zoom = 1.0f;
    public bool allowPan = true;
    public Vector2 pan;

    readonly Dictionary<string, RectTransform> _icons = new();

    void OnEnable()
    {
        MapService.OnChanged += Rebuild;
        Rebuild();
    }

    void OnDisable()
    {
        MapService.OnChanged -= Rebuild;
        ClearIcons();
    }

    void Update()
    {
        HandleZoomPan();
        UpdateIconPositions();
    }

    void HandleZoomPan()
    {
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.001f)
        {
            zoom = Mathf.Clamp(zoom + wheel * 0.1f, minZoom, maxZoom);
            mapRect.localScale = Vector3.one * zoom;
        }

        if (allowPan && Input.GetMouseButton(2))
        {
            pan += (Vector2)Input.mousePosition * 0.0f;
        }

        iconsRoot.anchoredPosition = pan;
    }

    void Rebuild()
    {
        ClearIcons();

        foreach (var kv in MapService.All)
        {
            var m = kv.Value;
            if (!m.isActive) continue;
            if (!PassesFilter(m)) continue;

            var iconGO = Instantiate(iconPrefab, iconsRoot);
            var rt = iconGO.GetComponent<RectTransform>();
            var img = iconGO.GetComponentInChildren<Image>();

            img.sprite = m.overrideIcon ? m.overrideIcon : SpriteFor(m.type);
            img.color = m.color;

            _icons[m.id] = rt;
        }
        UpdateIconPositions();
    }

    void UpdateIconPositions()
    {
        if (!projector || !mapRect) return;

        foreach (var kv in MapService.All)
        {
            if (!_icons.TryGetValue(kv.Key, out var rt)) continue;
            var m = kv.Value;
            if (!m.isActive) { rt.gameObject.SetActive(false); continue; }

            bool inside = projector.IsInsideWorldBounds(m.worldPos);
            rt.gameObject.SetActive(inside);
            if (!inside) continue;

            rt.anchoredPosition = projector.WorldToMapAnchored(m.worldPos);
            if (m.type == MapIconType.Player)
            {
                float zRot = projector.WorldYawToMapZ(m.headingY);
                rt.localEulerAngles = new Vector3(0f, 0f, -zRot + 180);
            }
            else
            {
                rt.localEulerAngles = Vector3.zero;
            }
        }
    }

    void ClearIcons()
    {
        foreach (var rt in _icons.Values)
            if (rt) Destroy(rt.gameObject);
        _icons.Clear();
    }

    bool PassesFilter(MapMarkerData m)
    {
        return m.type switch
        {
            MapIconType.Landmark => showLandmarks,
            MapIconType.Trader => showTraders,
            MapIconType.QuestGiver => showQuestGivers,
            MapIconType.QuestTarget => showQuestTargets,
            MapIconType.Player => showPlayer,
            MapIconType.Custom => showCustom,
            _ => true
        };
    }

    Sprite SpriteFor(MapIconType t) => t switch
    {
        MapIconType.Landmark => sprLandmark,
        MapIconType.Trader => sprTrader,
        MapIconType.QuestGiver => sprQuestGiver,
        MapIconType.QuestTarget => sprQuestTarget,
        MapIconType.Player => sprPlayer,
        MapIconType.Custom => sprCustom,
        _ => sprCustom
    };
}