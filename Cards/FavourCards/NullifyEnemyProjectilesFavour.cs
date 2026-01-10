using UnityEngine;

[CreateAssetMenu(fileName = "NullifyEnemyProjectilesFavour", menuName = "Favour Effects 2/Nullify Enemy Projectiles")]
public class NullifyEnemyProjectilesFavour : FavourEffect
{
    [Header("Nullify Projectile Settings")]
    [Tooltip("Nullify stacks granted when the condition is met.")]
    public int NullifyGain = 1;

    [Tooltip("Number of ranged-like enemy hits required to grant Nullify stacks.")]
    public int NumberOfHits = 3;

    [Header("Enhanced")]
    [Tooltip("Additional Nullify stacks granted per enhancement.")]
    public int BonusNullifyGain = 1;

    private const float MeleeRangeThreshold = 4f;
    private int currentGainPerTrigger = 0;
    private int hitCount = 0;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentGainPerTrigger = Mathf.Max(0, NullifyGain);
        hitCount = 0;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentGainPerTrigger += Mathf.Max(0, BonusNullifyGain);
    }

    public override void OnPlayerHit(GameObject player, GameObject attacker, ref float damage, FavourEffectManager manager)
    {
        return;
    }

    public override void OnPlayerDamageFinalized(GameObject player, GameObject attacker, float finalDamage, bool isStatusTick, bool isAoeDamage, FavourEffectManager manager)
    {
        if (player == null || attacker == null)
        {
            return;
        }

        if (isStatusTick || isAoeDamage)
        {
            return;
        }

        if (finalDamage < 1f)
        {
            return;
        }

        float distance = Vector3.Distance(attacker.transform.position, player.transform.position);
        if (distance <= MeleeRangeThreshold)
        {
            return;
        }

        int required = Mathf.Max(1, NumberOfHits);
        hitCount++;
        if (hitCount < required)
        {
            return;
        }

        hitCount = 0;

        if (currentGainPerTrigger <= 0)
        {
            return;
        }

        StatusController status = player.GetComponent<StatusController>();
        if (status == null)
        {
            return;
        }

        status.AddStatus(StatusId.Nullify, currentGainPerTrigger, -1f);
    }
}
