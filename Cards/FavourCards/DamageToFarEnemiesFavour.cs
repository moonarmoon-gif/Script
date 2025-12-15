using UnityEngine;

[CreateAssetMenu(fileName = "DamageToFarEnemiesFavour", menuName = "Favour Effects/Damage To Far Enemies")]
public class DamageToFarEnemiesFavour : FavourEffect
{
    [Header("Far Enemy Damage Settings")]
    [Tooltip("Base bonus damage to far enemies (0.25 = +25%).")]
    public float IncreasedDamage = 0.25f;

    [Tooltip("Additional bonus damage per extra card (0.1 = +10%).")]
    public float AdditionalDamage = 0.1f;

    [Tooltip("Minimum distance at which enemies are considered far.")]
    public float Range = 10f;

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
        float rangeSq = Range * Range;
        float distSq = (playerPos - enemyPos).sqrMagnitude;

        if (distSq >= rangeSq && currentBonusMultiplier > 0f)
        {
            damage *= currentBonusMultiplier;
        }

        return damage;
    }
}
