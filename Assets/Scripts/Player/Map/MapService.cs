using System;
using System.Collections.Generic;
using UnityEngine;

public enum MapIconType { Landmark, Trader, QuestGiver, QuestTarget, Player, Custom }

[Serializable]
public class MapMarkerData
{
    public string id;
    public Vector3 worldPos;
    public MapIconType type;
    public bool isActive = true;
    public Sprite overrideIcon;
    public Color color = Color.white;
    public object context;

    public float headingY;
}

public static class MapService
{
    public static event Action OnChanged;

    static readonly Dictionary<string, MapMarkerData> _markers = new();

    public static void AddOrUpdate(MapMarkerData m)
    {
        if (m == null || string.IsNullOrEmpty(m.id)) return;

        if (_markers.TryGetValue(m.id, out var old))
        {
            bool structuralChanged =
                old.type != m.type ||
                old.overrideIcon != m.overrideIcon ||
                old.color != m.color ||
                old.isActive != m.isActive;

            old.worldPos = m.worldPos;
            old.headingY = m.headingY;
            old.context = m.context;

            if (structuralChanged)
            {
                old.type = m.type;
                old.overrideIcon = m.overrideIcon;
                old.color = m.color;
                old.isActive = m.isActive;
                OnChanged?.Invoke();
            }
        }
        else
        {
            _markers[m.id] = m;
            OnChanged?.Invoke();
        }
    }

    public static void SetActive(string id, bool active)
    {
        if (_markers.TryGetValue(id, out var m))
        {
            if (m.isActive != active)
            {
                m.isActive = active;
                OnChanged?.Invoke();
            }
        }
    }
    public static void Remove(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (_markers.Remove(id)) OnChanged?.Invoke();
    }

    public static IReadOnlyDictionary<string, MapMarkerData> All => _markers;

    public static void AddSimple(string id, Vector3 worldPos, MapIconType type, Sprite icon = null, Color? color = null, object ctx = null)
    {
        AddOrUpdate(new MapMarkerData
        {
            id = id,
            worldPos = worldPos,
            type = type,
            overrideIcon = icon,
            color = color ?? Color.white,
            context = ctx,
            isActive = true
        });
    }
}