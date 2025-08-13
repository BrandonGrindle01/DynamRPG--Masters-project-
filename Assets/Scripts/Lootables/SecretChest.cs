using UnityEngine;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class SecretChest : MonoBehaviour
{
    [Header("Animation & FX")]
    [SerializeField] private Animator animator;
    [SerializeField] private string openTrigger = "OpenChest";
    [SerializeField] private AudioSource sfx;
    [SerializeField] private AudioClip openClip;

    [Header("Loot (self-contained)")]
    [SerializeField, Range(1, 10)] private int rolls = 1;
    [SerializeField] private bool allowDuplicates = true;

    [Serializable]
    public class LootEntry
    {
        public ItemData item;
        public int min = 1;
        public int max = 1;
        [Range(0f, 1f)] public float chance = 1f;
        public float weight = 1f;
    }
    [SerializeField] private List<LootEntry> loot = new List<LootEntry>();
    [SerializeField, Range(0, 1000)] private int bonusGold = 0;

    [Header("Persistence")]
    [SerializeField] private string chestId = "";
    private const string PPX = "CHEST_OPEN_";

    [Header("Stats / Secrets")]
    [SerializeField] private bool countAsSecret = true;

    [Header("Quest Hook (optional)")]
    [SerializeField] private bool isQuestChest = false;
    [SerializeField] private string questId = "";

    private bool opened;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (string.IsNullOrEmpty(chestId))
            chestId = string.IsNullOrEmpty(questId) ? Guid.NewGuid().ToString("N") : ("QCHEST_" + questId);

        opened = PlayerPrefs.GetInt(PPX + chestId, 0) == 1;
    }

    public bool CanInteract() => !opened;

    public void Interact()
    {
        if (opened) return;
        opened = true;
        PlayerPrefs.SetInt(PPX + chestId, 1);
        PlayerPrefs.Save();

        if (animator && !string.IsNullOrEmpty(openTrigger))
            animator.SetTrigger(openTrigger);
        if (sfx && openClip)
            sfx.PlayOneShot(openClip);

        GiveLoot();

        if (countAsSecret) PlayerStatsTracker.Instance?.RegisterSecretFound();

        //if (isQuestChest && !string.IsNullOrEmpty(questId))
            //QuestManager.Instance?.NotifyQuestChestOpened(questId, this);
    }

    private void GiveLoot()
    {
        var pool = new List<LootEntry>(loot);

        var gained = new Dictionary<ItemData, int>();
        int goldGained = 0;

        for (int r = 0; r < rolls; r++)
        {
            var candidates = new List<LootEntry>();
            foreach (var e in pool)
            {
                if (e.item == null) continue;
                if (UnityEngine.Random.value <= Mathf.Clamp01(e.chance))
                    candidates.Add(e);
            }
            if (candidates.Count == 0) continue;

            float totalW = 0f;
            foreach (var c in candidates) totalW += Mathf.Max(0.0001f, c.weight);

            float pick = UnityEngine.Random.value * totalW;
            LootEntry chosen = candidates[0];
            float acc = 0f;
            foreach (var c in candidates)
            {
                acc += Mathf.Max(0.0001f, c.weight);
                if (pick <= acc) { chosen = c; break; }
            }

            int amount = UnityEngine.Random.Range(
                Mathf.Max(1, chosen.min),
                Mathf.Max(chosen.min, chosen.max) + 1
            );
            amount = Mathf.Max(1, amount);

            InventoryManager.Instance.AddItem(chosen.item, amount);

            if (!gained.ContainsKey(chosen.item)) gained[chosen.item] = 0;
            gained[chosen.item] += amount;

            if (!allowDuplicates) pool.Remove(chosen);
        }

        if (bonusGold > 0)
        {
            InventoryManager.Instance.AddGold(bonusGold);
            goldGained = bonusGold;
        }

        List<string> parts = new List<string>();
        foreach (var kv in gained)
        {
            if (kv.Key != null)
                parts.Add($"{kv.Value}x {kv.Key.itemName}");
        }
        if (goldGained > 0) parts.Add($"{goldGained} gold");

        string summary = parts.Count > 0 ? string.Join(", ", parts) : "nothing";

        DialogueService.BeginOneLiner("Chest", $"You found {summary}.");
    }

    public void MarkAsQuestChest(string id)
    {
        isQuestChest = true;
        questId = id;

        if (!string.IsNullOrEmpty(questId))
        {
            chestId = "QCHEST_" + questId;
            opened = PlayerPrefs.GetInt(PPX + chestId, 0) == 1;
        }
    }
}