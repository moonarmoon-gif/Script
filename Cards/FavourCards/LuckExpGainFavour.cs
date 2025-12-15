using UnityEngine;

[CreateAssetMenu(fileName = "LuckExpGainFavour", menuName = "Favour Effects/Luck & EXP Gain")]
public class LuckExpGainFavour : FavourEffect
{
    [Header("Luck & Experience Settings")]
    [Tooltip("Luck gained per card.")]
    public float LuckGain = 10f;

    [Tooltip("Experience multiplier bonus per card (0.05 = +5%).")]
    public float ExpGain = 0.05f;

    private PlayerStats playerStats;
    private int stacks = 0;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        playerStats = player.GetComponent<PlayerStats>();
        if (playerStats == null)
        {
            Debug.LogWarning($"<color=yellow>LuckExpGainFavour could not find PlayerStats on {player.name}.</color>");
            return;
        }

        stacks = 1;
        playerStats.luck += LuckGain;
        playerStats.experienceMultiplier += ExpGain;
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
        playerStats.luck += LuckGain;
        playerStats.experienceMultiplier += ExpGain;
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (playerStats == null || stacks <= 0)
        {
            return;
        }

        float totalLuck = stacks * LuckGain;
        float totalExp = stacks * ExpGain;

        playerStats.luck -= totalLuck;
        playerStats.experienceMultiplier -= totalExp;
    }
}
