using UnityEngine;
using UnityEngine.UI;

public class MapProjector : MonoBehaviour
{
    [Header("World reference (X/Z)")]
    public Vector2 worldMin = new Vector2(-500, -500);
    public Vector2 worldMax = new Vector2(500, 500);

    [Header("Map rect (UI)")]
    public RectTransform mapRect;

    [Header("Fix orientation")]
    public bool flipX = false;
    public bool flipY = true;
    public bool swapAxes = false;

    public float northUpOffsetDeg = 0f;

    public Vector2 WorldToMapAnchored(Vector3 worldPos)
    {
        float u = Mathf.InverseLerp(worldMin.x, worldMax.x, worldPos.x);
        float v = Mathf.InverseLerp(worldMin.y, worldMax.y, worldPos.z);

        if (flipX) u = 1f - u;
        if (flipY) v = 1f - v;

        if (swapAxes)
        {
            var tmp = u; u = v; v = tmp;
        }

        Vector2 size = mapRect.rect.size;
        float ax = (u - 0.5f) * size.x;
        float ay = (v - 0.5f) * size.y;
        return new Vector2(ax, ay);
    }

    public float WorldYawToMapZ(float worldYawDeg)
    {
        float a = worldYawDeg + northUpOffsetDeg;

        if (swapAxes) a -= 90f;

        bool invert = (flipX ^ flipY);
        if (invert) a = -a;

        return -a;
    }

    public bool IsInsideWorldBounds(Vector3 worldPos)
    {
        return worldPos.x >= worldMin.x && worldPos.x <= worldMax.x &&
               worldPos.z >= worldMin.y && worldPos.z <= worldMax.y;
    }
}
