using UnityEngine;
using System.Collections;

public class NPCSpawnPoint : MonoBehaviour
{
    public GameObject npcPrefab;
    public Transform homePoint;
    private GameObject activeNPC;

    private void Start()
    {
        TrySpawnNPC();
    }

    private void Awake()
    {
        if (homePoint == null)
            homePoint = this.transform;
    }

    public void OnNPCReturnedHome(GameObject npc)
    {
        if (npc == activeNPC)
        {
            activeNPC = null;
            StartCoroutine(RespawnAfterDelay());
        }
    }

    private IEnumerator RespawnAfterDelay()
    {
        float delay = Random.Range(30f, 120f);
        yield return new WaitForSeconds(delay);
        TrySpawnNPC();
    }

    private void TrySpawnNPC()
    {
        if (npcPrefab != null && homePoint != null && activeNPC == null)
        {
            activeNPC = Instantiate(npcPrefab, homePoint.position, Quaternion.identity);
            var npcBehavior = activeNPC.GetComponent<NPCBehaviour>();
            if (npcBehavior != null)
            {
                npcBehavior.SetHome(homePoint.position);
                npcBehavior.AssignSpawner(this);

                StartCoroutine(SendHomeLater(npcBehavior));
            }
        }
    }

    private IEnumerator SendHomeLater(NPCBehaviour npc)
    {
        yield return new WaitForSeconds(Random.Range(20f, 60f));
        if (npc != null)
        {
            npc.GoHome();
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.3f);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, 1.5f);
    }
}