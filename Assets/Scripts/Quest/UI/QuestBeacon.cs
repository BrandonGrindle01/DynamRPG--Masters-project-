using UnityEngine;

public struct QuestBeaconParams
{
    public float yStart;
    public float height;
    public float radius;
    public float alpha;
}

public static class QuestBeacon
{
    public static GameObject Create(Transform target, QuestBeaconParams p)
    {
        var root = new GameObject("QuestBeacon");
        root.transform.position = target ? target.position : Vector3.zero;
        var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(cyl.GetComponent<Collider>());
        cyl.transform.SetParent(root.transform, false);

        float halfHeight = Mathf.Max(0.01f, p.height * 0.5f);
        cyl.transform.localScale = new Vector3(Mathf.Max(0.01f, p.radius), halfHeight, Mathf.Max(0.01f, p.radius));

        cyl.transform.localPosition = new Vector3(0f, p.yStart + halfHeight, 0f);


        Shader shader = Shader.Find("Unlit/Transparent");
        if (!shader) shader = Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        var c = new Color(1f, 0.92f, 0.3f, Mathf.Clamp01(p.alpha));
        mat.color = c;

        var mr = cyl.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.sharedMaterial.renderQueue = 3000;

        var lightGO = new GameObject("BeamLight");
        lightGO.transform.SetParent(root.transform, false);
        lightGO.transform.localPosition = new Vector3(0, p.yStart + p.height + 0.5f, 0);
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Spot;
        light.range = Mathf.Max(20f, p.height + 4f);
        light.spotAngle = 55f;
        light.intensity = 3f;

        var pulse = root.AddComponent<QuestBeaconPulse>(); pulse.lightRef = light; pulse.mesh = cyl.transform;
        var follow = root.AddComponent<FollowTransform>(); follow.target = target;

        return root;
    }
}

public class QuestBeaconPulse : MonoBehaviour
{
    public Light lightRef;
    public Transform mesh;
    public float speed = 2f, scaleAmp = 0.15f, lightAmp = 1.5f;
    Vector3 baseScale; float baseIntensity;

    void Start()
    {
        baseScale = mesh ? mesh.localScale : Vector3.one;
        baseIntensity = lightRef ? lightRef.intensity : 2f;
    }

    void Update()
    {
        float t = Time.time * speed;
        float s = 1f + Mathf.Sin(t) * scaleAmp;
        if (mesh) mesh.localScale = new Vector3(baseScale.x * s, baseScale.y, baseScale.z * s);
        if (lightRef) lightRef.intensity = baseIntensity + Mathf.Abs(Mathf.Sin(t)) * lightAmp;
    }
}

public class FollowTransform : MonoBehaviour
{
    public Transform target; public Vector3 offset = Vector3.zero;
    void LateUpdate()
    {
        if (!target) { Destroy(gameObject); return; }
        transform.position = target.position + offset;
    }
}