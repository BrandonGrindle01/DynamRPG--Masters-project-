using UnityEngine;

public static class KeyQuestRuntimeBinder
{
    public static void BindAll(KeyQuestSO[] keys)
    {
        if (keys == null) return;
        foreach (var k in keys)
        {
            if (k == null) continue;

            if (k.questGiver == null && !string.IsNullOrWhiteSpace(k.questGiverRefId))
            {
                var sr = SceneRef.Find(k.questGiverRefId);
                k.questGiver = sr ? sr.gameObject : null;
            }

            if (k.targetEnemy == null && !string.IsNullOrWhiteSpace(k.targetEnemyRefId))
            {
                var sr = SceneRef.Find(k.targetEnemyRefId);
                k.targetEnemy = sr ? sr.gameObject : null;
            }

            if (k.targetLocation == null && !string.IsNullOrWhiteSpace(k.targetLocationRefId))
            {
                var sr = SceneRef.Find(k.targetLocationRefId);
                k.targetLocation = sr ? sr.transform : null;
            }
        }
    }
}