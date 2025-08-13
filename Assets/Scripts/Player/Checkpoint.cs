using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [Tooltip("If null, this object's transform is used as the spawn point.")]
    public Transform spawnPoint;

    public static readonly List<Checkpoint> All = new();
    public Transform SpawnTransform => spawnPoint != null ? spawnPoint : transform;

    public bool isTown = false; 


    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnEnable() { All.Add(this); }
    private void OnDisable() { All.Remove(this); }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var t = SpawnTransform;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(t.position, 0.35f);
        Gizmos.DrawLine(t.position, t.position + t.forward * 1.0f);
    }
#endif
    public static Checkpoint GetClosest(Vector3 from)
    {
        Checkpoint best = null;
        float bestSq = float.PositiveInfinity;

        for (int i = 0; i < All.Count; i++)
        {
            float dSq = (All[i].SpawnTransform.position - from).sqrMagnitude;
            if (dSq < bestSq)
            {
                best = All[i];
                bestSq = dSq;
            }
        }
        return best;
    }
}