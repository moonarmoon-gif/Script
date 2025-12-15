using UnityEngine;

[CreateAssetMenu(fileName = "BurnDurationFavour", menuName = "Favour Effects/Burn Duration")] 
public class BurnDurationFavour : FavourEffect
{
    [Header("Burn Duration Settings")]
    [Tooltip("Bonus burn duration per card in seconds.")]
    public float BonusBurnDuration = 1f;

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
        playerStats.burnDurationBonus += BonusBurnDuration;
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
        playerStats.burnDurationBonus += BonusBurnDuration;
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (playerStats == null || stacks <= 0)
        {
            return;
        }

        float total = BonusBurnDuration * stacks;
        playerStats.burnDurationBonus = Mathf.Max(0f, playerStats.burnDurationBonus - total);
    }
}
