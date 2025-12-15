using UnityEngine;

[CreateAssetMenu(fileName = "RarityScalingDamageFavour", menuName = "Favour Effects/Rarity Scaling Damage")]
public class RarityScalingDamageFavour : FavourEffect
{
    [Header("Damage Bonus Per Rarity (percent)")]
    public float CommonBonusDamage = 1f;
    public float UncommonBonusDamage = 2f;
    public float RareBonusDamage = 3f;
    public float EpicBonusDamage = 5f;
    public float LegendaryBonusDamage = 7f;
    public float MythicBonusDamage = 10f;

    private int stacks = 0;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        stacks = 1;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        stacks++;
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
        if (stacks <= 0)
        {
            return damage;
        }

        if (enemy == null || damage <= 0f)
        {
            return damage;
        }

        EnemyCardTag tag = enemy.GetComponentInParent<EnemyCardTag>();
        if (tag == null)
        {
            return damage;
        }

        float bonusPerStack = GetBonusForRarity(tag.rarity);
        if (bonusPerStack <= 0f)
        {
            return damage;
        }

        float totalBonusPercent = bonusPerStack * stacks;
        float multiplier = 1f + (totalBonusPercent / 100f);
        damage *= multiplier;

        return damage;
    }

    private float GetBonusForRarity(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Common:
                return CommonBonusDamage;
            case CardRarity.Uncommon:
                return UncommonBonusDamage;
            case CardRarity.Rare:
                return RareBonusDamage;
            case CardRarity.Epic:
                return EpicBonusDamage;
            case CardRarity.Legendary:
                return LegendaryBonusDamage;
            case CardRarity.Mythic:
            case CardRarity.Boss:
                return MythicBonusDamage;
            default:
                return 0f;
        }
    }
}
