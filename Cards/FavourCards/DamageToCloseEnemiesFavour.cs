using UnityEngine;

[CreateAssetMenu(fileName = "DamageToCloseEnemiesFavour", menuName = "Favour Effects/Damage To Close Enemies")]
public class DamageToCloseEnemiesFavour : FavourEffect
{
    [Header("Close Enemy Damage Settings")]
    [Tooltip("Base bonus damage to close enemies (0.25 = +25%).")]
    public float IncreasedDamage = 0.25f;

    [Tooltip("Additional bonus damage per extra card (0.1 = +10%).")]
    public float AdditionalDamage = 0.1f;

    [Tooltip("Radius within which enemies are considered close.")]
    public float Radius = 6f;

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
        damage = ApplyBonus(player, enemy, damage);
    }

    public override float PreviewBeforeDealDamage(GameObject player, GameObject enemy, float damage, FavourEffectManager manager)
    {
        return ApplyBonus(player, enemy, damage);
    }

    private float ApplyBonus(GameObject player, GameObject enemy, float damage)
    {
        if (player == null || enemy == null || damage <= 0f)
        {
            return damage;
        }

        Vector3 playerPos = player.transform.position;
        Vector3 enemyPos = enemy.transform.position;
        float radiusSq = Radius * Radius;
        float distSq = (playerPos - enemyPos).sqrMagnitude;

        if (distSq <= radiusSq && currentBonusMultiplier > 0f)
        {
            damage *= currentBonusMultiplier;
        }

        return damage;
    }
}
