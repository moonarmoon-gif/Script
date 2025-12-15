using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CorruptionFavour", menuName = "Favour Effects/Corruption")]
public class CorruptionFavour : FavourEffect
{
    [Header("Corruption Settings")]
    [Tooltip("Bonus damage per stack for normal enemies (percent).")]
    public float BonusDamagePerStack = 5f;

    [Tooltip("Bonus damage per stack when the enemy is a Boss (percent).")]
    public float BossBonusDamagePerStack = 1f;

    [Tooltip("Maximum corruption stacks per enemy.")]
    public int MaxStacks = 100;

    private int cardStacks = 0;
    private readonly Dictionary<EnemyHealth, int> stacksByEnemy = new Dictionary<EnemyHealth, int>();

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (cardStacks <= 0)
        {
            cardStacks = 1;
        }
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (cardStacks <= 0)
        {
            cardStacks = 1;
        }
        else
        {
            cardStacks++;
        }
    }

    public override void OnBeforeDealDamage(GameObject player, GameObject enemy, ref float damage, FavourEffectManager manager)
    {
        damage = ApplyCorruption(enemy, damage, true);
    }

    public override float PreviewBeforeDealDamage(GameObject player, GameObject enemy, float damage, FavourEffectManager manager)
    {
        return ApplyCorruption(enemy, damage, false);
    }

    private float ApplyCorruption(GameObject enemy, float damage, bool incrementStacks)
    {
        if (damage <= 0f || cardStacks <= 0)
        {
            return damage;
        }

        if (enemy == null)
        {
            return damage;
        }

        EnemyHealth enemyHealth = enemy.GetComponentInParent<EnemyHealth>();
        if (enemyHealth == null || !enemyHealth.IsAlive)
        {
            return damage;
        }

        int stacks;
        if (!stacksByEnemy.TryGetValue(enemyHealth, out stacks))
        {
            stacks = 0;
        }

        int nextStacks = stacks;
        if (nextStacks < MaxStacks)
        {
            nextStacks++;
        }

        if (incrementStacks)
        {
            stacksByEnemy[enemyHealth] = nextStacks;
        }

        EnemyCardTag tag = enemyHealth.GetComponent<EnemyCardTag>() ?? enemy.GetComponentInParent<EnemyCardTag>();
        bool isBoss = tag != null && tag.rarity == CardRarity.Boss;

        float basePerStack = isBoss ? BossBonusDamagePerStack : BonusDamagePerStack;
        if (basePerStack <= 0f)
        {
            return damage;
        }

        float perStack = basePerStack * cardStacks;
        float totalBonusPercent = perStack * nextStacks;
        float multiplier = 1f + (totalBonusPercent / 100f);
        damage *= multiplier;

        return damage;
    }

    public override void OnEnemyKilled(GameObject player, GameObject enemy, FavourEffectManager manager)
    {
        if (enemy == null || stacksByEnemy.Count == 0)
        {
            return;
        }

        EnemyHealth enemyHealth = enemy.GetComponentInParent<EnemyHealth>();
        if (enemyHealth != null)
        {
            stacksByEnemy.Remove(enemyHealth);
        }
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        stacksByEnemy.Clear();
        cardStacks = 0;
    }
}
