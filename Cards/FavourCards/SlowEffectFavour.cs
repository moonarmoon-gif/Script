using UnityEngine;

[CreateAssetMenu(fileName = "SlowEffectFavour", menuName = "Favour Effects/Slow Effect")] 
public class SlowEffectFavour : FavourEffect
{
    [Header("Slow Strength Settings")]
    [Tooltip("Bonus slow strength per card as a percent (10 = +10% slow strength).")]
    public float BonusSlowStrength = 10f;

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
        float delta = Mathf.Max(0f, BonusSlowStrength) / 100f;
        playerStats.slowStrengthBonus += delta;
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
        float delta = Mathf.Max(0f, BonusSlowStrength) / 100f;
        playerStats.slowStrengthBonus += delta;
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (playerStats == null || stacks <= 0)
        {
            return;
        }

        float delta = Mathf.Max(0f, BonusSlowStrength) / 100f;
        float total = delta * stacks;
        playerStats.slowStrengthBonus = Mathf.Max(0f, playerStats.slowStrengthBonus - total);
    }
}
