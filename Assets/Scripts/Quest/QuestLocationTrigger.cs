using UnityEngine;

public class QuestLocationTrigger : MonoBehaviour
{
    [SerializeField] private string markerId;

    public void SetMarkerId(string id) { markerId = id; }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var id = string.IsNullOrEmpty(markerId) ? gameObject.name : markerId;
        QuestService.ReportEnteredLocation(id);
    }
}