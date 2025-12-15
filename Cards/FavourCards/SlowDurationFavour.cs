using UnityEngine;

[CreateAssetMenu(fileName = "SlowDurationFavour", menuName = "Favour Effects/Slow Duration")] 
public class SlowDurationFavour : FavourEffect
{
    [Header("Slow Duration Settings")]
    [Tooltip("Bonus slow duration per card in seconds.")]
    public float BonusSlowDuration = 1f;

    private PlayerStats playerStats;
    private int stacks = 0;

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

        stacks = 1;
        playerStats.slowDurationBonus += BonusSlowDuration;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (playerStats == null && player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        if (playerStats == null)
        {
            return;
        }

        stacks++;
        playerStats.slowDurationBonus += BonusSlowDuration;
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (playerStats == null || stacks <= 0)
        {
            return;
        }

        float total = BonusSlowDuration * stacks;
        playerStats.slowDurationBonus = Mathf.Max(0f, playerStats.slowDurationBonus - total);
    }
}
