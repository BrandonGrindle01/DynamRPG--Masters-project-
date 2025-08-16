using UnityEngine;

[ExecuteAlways]
public class QuestLocation : MonoBehaviour
{
    public string displayName = "Quest Area";
    public Color gizmoColor = new Color(0f, 0.8f, 1f, 0.35f);

    public SphereCollider sphere;
    public BoxCollider box;

    public Vector3 Center => box ? (box.transform.TransformPoint(box.center))
                                 : (sphere ? (sphere.transform.TransformPoint(sphere.center)) : transform.position);

    public bool Contains(Vector3 worldPos)
    {
        if (sphere)
        {
            float r = sphere.radius * Mathf.Max(
                sphere.transform.lossyScale.x, sphere.transform.lossyScale.y, sphere.transform.lossyScale.z
            );
            return Vector3.Distance(Center, worldPos) <= r;
        }
        if (box) return box.bounds.Contains(worldPos);
        return Vector3.Distance(Center, worldPos) <= 2f;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;

        if (sphere)
        {
            float r = sphere.radius * Mathf.Max(
                sphere.transform.lossyScale.x, sphere.transform.lossyScale.y, sphere.transform.lossyScale.z
            );
            Gizmos.DrawSphere(Center, r);
        }
        else if (box)
        {
            Gizmos.matrix = Matrix4x4.TRS(box.transform.position, box.transform.rotation, box.transform.lossyScale);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else
        {
            Gizmos.DrawSphere(transform.position, 2f);
        }
    }
}