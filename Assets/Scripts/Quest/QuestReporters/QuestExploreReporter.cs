using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class QuestExploreTrigger : MonoBehaviour
{
    public float radius = 6f;

    private void Reset()
    {
        var c = GetComponent<SphereCollider>();
        c.isTrigger = true; c.radius = radius;
    }

    private void Awake()
    {
        var c = GetComponent<SphereCollider>();
        c.isTrigger = true; c.radius = radius;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<StarterAssets.FirstPersonController>())
            QuestService.ReportExplore(transform);
    }
}
