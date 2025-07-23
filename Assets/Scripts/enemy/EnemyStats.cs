using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyStats", menuName = "RPG/Enemy Stats")]
public class EnemyStats : ScriptableObject
{
    public string enemyName;
    public int maxHealth = 10;
    public int damage = 1;
    public int defense = 1;
    public float sightRange = 10f;
    public float attackRange = 2f;
    public float speed = 3.5f;
    public ItemData lootDrop;
    public EnemyType enemyType;
}

public enum EnemyType
{
    Bandit,
    Spider,
    Goblin
}