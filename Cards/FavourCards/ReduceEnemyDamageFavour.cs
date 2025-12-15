using UnityEngine;

[CreateAssetMenu(fileName = "ReduceEnemyDamageFavour", menuName = "Favour Effects/Reduce Enemy Damage")]
public class ReduceEnemyDamageFavour : FavourEffect
{
    [Header("Enemy Damage Reduction Settings")]
    [Tooltip("Percent reduction applied to non-boss enemy damage (5 = 5%).")]
    public float DamageReductionPercent = 5f;

    private PlayerStats playerStats;
    private float currentMultiplier = 1f;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        if (playerStats == null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        if (playerStats == null)
        {
            return;
        }

        float stepPercent = Mathf.Max(0f, DamageReductionPercent);
        float stepMultiplier = 1f - (stepPercent / 100f);
        stepMultiplier = Mathf.Clamp(stepMultiplier, 0f, 1f);

        if (stepMultiplier <= 0f)
        {
            return;
        }

        if (playerStats.nonBossIncomingDamageMultiplier <= 0f)
        {
            playerStats.nonBossIncomingDamageMultiplier = 1f;
        }

        currentMultiplier = stepMultiplier;
        playerStats.nonBossIncomingDamageMultiplier *= currentMultiplier;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        if (playerStats == null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        if (playerStats == null)
        {
            return;
        }

        float stepPercent = Mathf.Max(0f, DamageReductionPercent);
        float stepMultiplier = 1f - (stepPercent / 100f);
        stepMultiplier = Mathf.Clamp(stepMultiplier, 0f, 1f);

        if (stepMultiplier <= 0f)
        {
            return;
        }

        playerStats.nonBossIncomingDamageMultiplier *= stepMultiplier;
        currentMultiplier *= stepMultiplier;
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (playerStats == null)
        {
            return;
        }

        if (currentMultiplier <= 0f)
        {
            return;
        }

        if (playerStats.nonBossIncomingDamageMultiplier > 0f)
        {
            playerStats.nonBossIncomingDamageMultiplier /= currentMultiplier;
        }

        currentMultiplier = 1f;
    }
}
