using UnityEngine;

public class DeathBarrier : MonoBehaviour
{
    [SerializeField] private int playerKillDamage = 9999;
    [SerializeField] private float npcKillDamage = 9999f;

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<StarterAssets.FirstPersonController>();
        if (player)
        {
            player.playerDamaged(playerKillDamage);
            return;
        }

        var enemy = other.GetComponentInParent<EnemyBehavior>();
        if (enemy) { enemy.TakeDamage(npcKillDamage); return; }

        var guard = other.GetComponentInParent<GuardBehaviour>();
        if (guard) { guard.TakeDamage(npcKillDamage); return; }

        var npc = other.GetComponentInParent<NPCBehaviour>();
        if (npc) { npc.TakeDamage(npcKillDamage); return; }

        if (other.attachedRigidbody)
        {
            Destroy(other.attachedRigidbody.gameObject);
        }
    }
}