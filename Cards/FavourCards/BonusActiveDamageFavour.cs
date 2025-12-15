using UnityEngine;

[CreateAssetMenu(fileName = "BonusActiveDamageFavour", menuName = "Favour Effects/Bonus Active Damage")]
public class BonusActiveDamageFavour : FavourEffect
{
    [Header("Bonus Active Damage Settings")]
    [Tooltip("Base bonus damage for ACTIVE projectiles (0.05 = +5% damage).")]
    public float BonusDamage = 0.05f;

    // Internal multiplier applied to qualifying damage. Starts at 1 and
    // increases by BonusDamage on apply and each subsequent upgrade.
    private float currentBonusMultiplier = 1f;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        // First pick: 1 + BonusDamage (e.g. 1.05 for +5%).
        currentBonusMultiplier = 1f + Mathf.Max(0f, BonusDamage);
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        // "Enhanced" behaviour: add the same BonusDamage again each time this
        // favour is upgraded, effectively stacking +BonusDamage repeatedly.
        currentBonusMultiplier += Mathf.Max(0f, BonusDamage);
    }

    public override void OnBeforeDealDamage(GameObject player, GameObject enemy, ref float damage, FavourEffectManager manager)
    {
        damage = ApplyBonus(player, damage, manager);
    }

    public override float PreviewBeforeDealDamage(GameObject player, GameObject enemy, float damage, FavourEffectManager manager)
    {
        return ApplyBonus(player, damage, manager);
    }

    private float ApplyBonus(GameObject player, float damage, FavourEffectManager manager)
    {
        if (damage <= 0f || manager == null)
        {
            return damage;
        }

        ProjectileCards currentCard = manager.CurrentProjectileCard;
        if (currentCard == null)
        {
            return damage;
        }

        // Only apply to ACTIVE projectile systems as requested.
        if (currentCard.projectileSystem != ProjectileCards.ProjectileSystemType.Active)
        {
            return damage;
        }

        if (currentBonusMultiplier <= 0f || Mathf.Approximately(currentBonusMultiplier, 1f))
        {
            return damage;
        }

        damage *= currentBonusMultiplier;
        return damage;
    }
}
