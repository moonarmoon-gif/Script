using UnityEngine;

[CreateAssetMenu(fileName = "DamageOnHighHealthFavour", menuName = "Favour Effects/Damage On High Health")]
public class DamageOnHighHealthFavour : FavourEffect
{
    [Header("High Health Damage Settings")]
    [Tooltip("Base bonus damage against high-health enemies (0.5 = +50%).")]
    public float IncreasedDamage = 0.5f;

    [Tooltip("Additional bonus damage per extra card (0.25 = +25%).")]
    public float AdditionalDamage = 0.25f;

    [Tooltip("Health threshold as fraction 0-1 (0.9 = 90% health or above).")]
    public float HealthThreshold = 0.9f;

    private float currentBonusMultiplier = 1f;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentBonusMultiplier = 1f + IncreasedDamage;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentBonusMultiplier += AdditionalDamage;
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
        if (normalizedHealth >= HealthThreshold && currentBonusMultiplier > 0f)
        {
            damage *= currentBonusMultiplier;
        }

        return damage;
    }
}
