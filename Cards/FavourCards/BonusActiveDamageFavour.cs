using UnityEngine;

[CreateAssetMenu(fileName = "BonusActiveDamageFavour", menuName = "Favour Effects/Bonus Active Damage")]
public class BonusActiveDamageFavour : FavourEffect
{
    [Header("Bonus Active Damage Settings")]
    [Tooltip("Flat bonus damage added to ACTIVE projectiles.")]
    public float BonusDamage = 0.05f;

    private float currentFlatBonusDamage = 0f;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentFlatBonusDamage = Mathf.Max(0f, BonusDamage);
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentFlatBonusDamage += Mathf.Max(0f, BonusDamage);
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

        if (currentFlatBonusDamage <= 0f)
        {
            return damage;
        }

        damage += currentFlatBonusDamage;
        return damage;
    }
}
