using UnityEngine;

[CreateAssetMenu(fileName = "SufferElementBonusDmgFavour", menuName = "Favour Effects/Suffer Element Bonus Damage")]
public class SufferElementBonusDmgFavour : FavourEffect
{
    [Header("Suffer Element Bonus Damage Settings")]
    [Tooltip("Bonus damage against enemies currently affected by a status effect (0.15 = +15%).")]
    public float BonusDamage = 0.15f;

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

    private bool HasAnyStatusEffect(GameObject enemy)
    {
        if (enemy == null)
        {
            return false;
        }

        // Burn
        var burn = enemy.GetComponent<BurnEffect.BurnStatus>() ?? enemy.GetComponentInChildren<BurnEffect.BurnStatus>();
        if (burn != null)
        {
            return true;
        }

        // Slow
        var slow = enemy.GetComponent<SlowStatus>() ?? enemy.GetComponentInChildren<SlowStatus>();
        if (slow != null)
        {
            return true;
        }

        // Static
        var stat = enemy.GetComponent<StaticStatus>() ?? enemy.GetComponentInChildren<StaticStatus>();
        if (stat != null)
        {
            return true;
        }

        return false;
    }

    private float ApplyBonus(GameObject enemy, float damage)
    {
        if (damage <= 0f)
        {
            return damage;
        }

        if (!HasAnyStatusEffect(enemy))
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
