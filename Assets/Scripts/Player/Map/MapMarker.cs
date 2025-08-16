using UnityEngine;

[DisallowMultipleComponent]
public class MapMarker : MonoBehaviour
{
    public string markerId;
    public MapIconType type = MapIconType.Landmark;
    public Sprite iconOverride;
    public Color color = Color.white;
    public bool updateEveryFrame = true;

    public bool trackRotation = true;

    void OnEnable()
    {
        if (string.IsNullOrEmpty(markerId)) markerId = gameObject.name;
        Push();
    }

    void Update()
    {
        if (updateEveryFrame) Push();
    }

    void OnDisable()
    {
        MapService.Remove(markerId);
    }

    void Push()
    {
        MapService.AddOrUpdate(new MapMarkerData
        {
            id = markerId,
            worldPos = transform.position,
            type = type,
            overrideIcon = iconOverride,
            color = color,
            isActive = true,
            context = this,

            headingY = trackRotation ? transform.eulerAngles.y : 0f
        });
    }
}
