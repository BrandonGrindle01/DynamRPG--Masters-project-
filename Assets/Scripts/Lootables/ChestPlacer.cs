using System.Collections.Generic;
using UnityEngine;

public class ChestPlacer : MonoBehaviour
{
    [Header("Area")]
    public BoxCollider area;

    [Header("Spawn")]
    public GameObject chestPrefab;
    public int count = 3;

    [Header("Placement")]
    public LayerMask groundLayer;
    public float raycastHeight = 30f;
    public float minSeparation = 6f;
    public int maxTriesPerChest = 50;

    [Header("Rotation")]
    public bool alignToGroundNormal = true;
    public bool randomYaw = true;

    private readonly List<Vector3> _placed = new List<Vector3>();

    private void Start()
    {
        if (!area || !chestPrefab) return;

        for (int i = 0; i < count; i++)
        {
            if (TryPickPoint(area, out var hitInfo))
            {
                var rot = ComputeRotation(hitInfo);
                Instantiate(chestPrefab, hitInfo.point, rot);
                _placed.Add(hitInfo.point);
            }
        }
    }

    private bool TryPickPoint(BoxCollider a, out RaycastHit hit)
    {
        var b = a.bounds;

        for (int attempt = 0; attempt < maxTriesPerChest; attempt++)
        {
            Vector3 top = new Vector3(
                Random.Range(b.min.x, b.max.x),
                b.max.y + raycastHeight,
                Random.Range(b.min.z, b.max.z)
            );

            if (Physics.Raycast(top, Vector3.down, out hit, raycastHeight * 2f, groundLayer))
            {
                if (IsFarFromOthers(hit.point))
                    return true;
            }
        }

        hit = default;
        return false;
    }

    private bool IsFarFromOthers(Vector3 point)
    {
        for (int i = 0; i < _placed.Count; i++)
        {
            if (Vector3.SqrMagnitude(point - _placed[i]) < minSeparation * minSeparation)
                return false;
        }
        return true;
    }

    private Quaternion ComputeRotation(RaycastHit hit)
    {
        Vector3 up = alignToGroundNormal ? hit.normal : Vector3.up;

        Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.ProjectOnPlane(Vector3.right, up).normalized;

        Quaternion baseRot = Quaternion.LookRotation(forward, up);
        if (randomYaw)
        {
            float yaw = Random.Range(0f, 360f);
            baseRot = Quaternion.AngleAxis(yaw, up) * baseRot;
        }

        return baseRot;
    }
}