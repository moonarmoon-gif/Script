using UnityEngine;

[CreateAssetMenu(fileName = "ReflectLowFavour", menuName = "Favour Effects 2/Reflect Low")]
public class ReflectLowFavour : FavourEffect
{
    [Header("Reflect Low Settings")]
    [Tooltip("Base number of Reflect stacks granted each recharge.")]
    public int ReflectGain = 1;

    [Tooltip("Number of melee-like enemy hits required to grant Reflect stacks.")]
    public int NumberOfHits = 3;

    [Header("Enhanced")]
    [Tooltip("Additional Reflect stacks gained each recharge when enhanced.")]
    public int BonusReflectGain = 1;

    private const float MeleeRangeThreshold = 4f;
    private int currentGainPerTick = 0;
    private int hitCount = 0;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentGainPerTick = Mathf.Max(0, ReflectGain);
        hitCount = 0;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentGainPerTick += Mathf.Max(0, BonusReflectGain);
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
        if (distance > MeleeRangeThreshold)
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

        if (currentGainPerTick <= 0)
        {
            return;
        }

        StatusController status = player.GetComponent<StatusController>();
        if (status == null)
        {
            return;
        }

        status.AddStatus(StatusId.Reflect, currentGainPerTick, -1f);
    }
}
