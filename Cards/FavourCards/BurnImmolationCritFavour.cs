using UnityEngine;

[CreateAssetMenu(fileName = "BurnImmolationCritFavour", menuName = "Favour Effects/Burn Immolation Crit")]
public class BurnImmolationCritFavour : FavourEffect
{
    [Header("Pick Limit")]
    public int MaxPickLimit = 0;

    private PlayerStats playerStats;
    private int stacks;

    protected override int GetMaxPickLimit()
    {
        return MaxPickLimit;
    }

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
        playerStats.burnImmolationCanCrit = true;
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
        playerStats.burnImmolationCanCrit = true;
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (playerStats == null || stacks <= 0)
        {
            return;
        }

        stacks = Mathf.Max(0, stacks - 1);
        if (stacks <= 0)
        {
            playerStats.burnImmolationCanCrit = false;
        }
    }
}
