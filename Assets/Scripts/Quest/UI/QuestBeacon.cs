using UnityEngine;

public static class QuestBeacon
{
    public static GameObject CreateBeam(Transform target, float yStart, float height, float radius, float alpha, Material template)
    {
        if (!target) return null;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "QuestBeacon";
        var col = go.GetComponent<Collider>(); if (col) Object.Destroy(col);

        go.transform.SetParent(null);
        go.transform.position = target.position;
        go.transform.localScale = new Vector3(Mathf.Max(0.01f, radius * 2f), Mathf.Max(0.1f, height / 2f), Mathf.Max(0.01f, radius * 2f));

        var baseY = target.position.y + yStart;
        var halfH = Mathf.Max(0.1f, height / 2f);
        var pos = go.transform.position; pos.y = baseY + halfH; go.transform.position = pos;

        var mr = go.GetComponent<MeshRenderer>();
        var mat = template ? new Material(template) : CreateFallbackMaterial();
        if (mr) mr.material = mat;

        EnforceTransparent(mat);

        var c = new Color(0.2f, 0.95f, 1f, Mathf.Clamp01(alpha));
        SetColor(mat, c);

        var follow = go.AddComponent<_BeaconFollow>();
        follow.target = target;
        follow.yStart = yStart;
        follow.halfHeight = halfH;

        return go;
    }

    public static void SetBeaconColor(GameObject beacon, Color c)
    {
        if (!beacon) return;
        var mr = beacon.GetComponent<MeshRenderer>();
        if (!mr || !mr.material) return;
        SetColor(mr.material, c);
    }

    private static void SetColor(Material m, Color c)
    {
        if (!m) return;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        else if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    private static void EnforceTransparent(Material m)
    {
        if (!m) return;

        if (m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return;
        }

        if (m.shader && m.shader.name == "Standard")
        {
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 3f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }

    private static Material CreateFallbackMaterial()
    {
        string[] tries = {
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Lit",
            "Sprites/Default",
            "UI/Default",
            "Standard"
        };

        Shader sh = null;
        foreach (var name in tries) { sh = Shader.Find(name); if (sh) break; }
        return sh ? new Material(sh) : null;
    }

    private class _BeaconFollow : MonoBehaviour
    {
        public Transform target;
        public float yStart;
        public float halfHeight;

        void LateUpdate()
        {
            if (!target) { Destroy(gameObject); return; }
            var p = target.position;
            p.y = p.y + yStart + halfHeight;
            transform.position = p;
        }
    }
}