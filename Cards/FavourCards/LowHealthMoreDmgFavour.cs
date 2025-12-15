using UnityEngine;

[CreateAssetMenu(fileName = "LowHealthMoreDmgFavour", menuName = "Favour Effects/Low Health More Damage")]
public class LowHealthMoreDmgFavour : FavourEffect
{
    [Header("Low Health More Damage Settings")]
    [Tooltip("Bonus damage against enemies at or below the health threshold (0.15 = +15%).")]
    public float BonusDamage = 0.15f;

    [Tooltip("Health threshold as a FRACTION (0-1). 0.5 = 50% or lower health.")]
    public float HealthThreshold = 0.5f;

    private float currentBonusMultiplier = 1f;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentBonusMultiplier = 1f + Mathf.Max(0f, BonusDamage);
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        // Enhanced: increase BonusDamage by the same base amount each time.
        currentBonusMultiplier += Mathf.Max(0f, BonusDamage);
    }

    public override void OnBeforeDealDamage(GameObject player, GameObject enemy, ref float damage, FavourEffectManager manager)
    {
        damage = ApplyBonus(enemy, damage);
    }

    public override float PreviewBeforeDealDamage(GameObject player, GameObject enemy, float damage, FavourEffectManager manager)
    {
        return ApplyBonus(enemy, damage);
    }

    private float ApplyBonus(GameObject enemy, float damage)
    {
        if (enemy == null || damage <= 0f)
        {
            return damage;
        }

        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>() ?? enemy.GetComponentInParent<EnemyHealth>();
        if (enemyHealth == null)
        {
            return damage;
        }

        float maxHealth = enemyHealth.MaxHealth;
        if (maxHealth <= 0f)
        {
            return damage;
        }

        float normalizedHealth = enemyHealth.CurrentHealth / maxHealth;
        if (normalizedHealth <= HealthThreshold && currentBonusMultiplier > 0f)
        {
            damage *= currentBonusMultiplier;
        }

        return damage;
    }
}
