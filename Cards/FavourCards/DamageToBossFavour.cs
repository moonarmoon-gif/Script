using UnityEngine;

[CreateAssetMenu(fileName = "DamageToBossFavour", menuName = "Favour Effects/Damage To Boss")]
public class DamageToBossFavour : FavourEffect
{
    [Header("Boss Damage Settings")]
    [Tooltip("Base bonus damage to boss enemies (0.25 = +25%).")]
    public float IncreasedDamage = 0.25f;

    [Tooltip("Additional bonus damage per extra card (0.1 = +10%).")]
    public float AdditionalDamage = 0.1f;

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

        EnemyCardTag tag = enemy.GetComponent<EnemyCardTag>() ?? enemy.GetComponentInParent<EnemyCardTag>();
        if (tag == null)
        {
            return damage;
        }

        if (tag.rarity == CardRarity.Boss && currentBonusMultiplier > 0f)
        {
            damage *= currentBonusMultiplier;
        }

        return damage;
    }
}
