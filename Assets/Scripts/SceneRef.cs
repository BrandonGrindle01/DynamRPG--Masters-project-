using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SceneRef : MonoBehaviour
{
    [Tooltip("Unique ID for this object within the loaded scene(s).")]
    public string id;

    private static readonly Dictionary<string, SceneRef> map = new();

    private void OnEnable()
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        map[id] = this;
    }

    private void OnDisable()
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        if (map.TryGetValue(id, out var cur) && cur == this) map.Remove(id);
    }

    public static SceneRef Find(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        map.TryGetValue(key, out var sr);
        return sr;
    }

    public static T FindComponent<T>(string key) where T : Component
    {
        var sr = Find(key);
        return sr ? sr.GetComponent<T>() : null;
    }
}