using UnityEngine;

[CreateAssetMenu(fileName = "FullHealthMoreDamageFavour", menuName = "Favour Effects/Full Health More Damage")]
public class FullHealthMoreDamageFavour : FavourEffect
{
    [Header("Full Health More Damage Settings")]
    [Tooltip("Extra damage percent when the character is at full health (e.g., 15 = +15%).")]
    public float ExtraDamage = 15f;

    [Header("Enhanced")]
    [Tooltip("Additional extra damage percent when enhanced (e.g., 15 = +15%).")]
    public float BonusDamage = 15f;

    private float currentBonusMultiplier = 1f;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        float basePercent = Mathf.Max(0f, ExtraDamage);
        currentBonusMultiplier = 1f + basePercent / 100f;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        float bonusPercent = Mathf.Max(0f, BonusDamage);
        currentBonusMultiplier += bonusPercent / 100f;
    }

    public override void OnBeforeDealDamage(GameObject player, GameObject enemy, ref float damage, FavourEffectManager manager)
    {
        damage = ApplyBonus(player, damage);
    }

    public override float PreviewBeforeDealDamage(GameObject player, GameObject enemy, float damage, FavourEffectManager manager)
    {
        return ApplyBonus(player, damage);
    }

    private float ApplyBonus(GameObject player, float damage)
    {
        if (player == null || damage <= 0f || currentBonusMultiplier <= 1f)
        {
            return damage;
        }

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth == null || !playerHealth.IsAlive)
        {
            return damage;
        }

        // Full health check
        if (Mathf.Approximately(playerHealth.CurrentHealth, playerHealth.MaxHealth) && playerHealth.MaxHealth > 0f)
        {
            damage *= currentBonusMultiplier;
        }

        return damage;
    }
}
