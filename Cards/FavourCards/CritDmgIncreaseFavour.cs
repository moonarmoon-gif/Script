using UnityEngine;

[CreateAssetMenu(fileName = "CritDmgIncreaseFavour", menuName = "Favour Effects/Crit Damage Increase")]
public class CritDmgIncreaseFavour : FavourEffect
{
    [Header("Crit Damage Increase Settings")]
    [Tooltip("Increase to crit damage (percent) per favour stack.")]
    public float CritDamageIncreasePercent = 10f;

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
        ApplyBonus(1);
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
        ApplyBonus(1);
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (playerStats == null || stacks <= 0)
        {
            return;
        }

        ApplyBonus(-stacks);
        stacks = 0;
    }

    private void ApplyBonus(int direction)
    {
        float delta = CritDamageIncreasePercent * direction;

        playerStats.critDamage += delta;
        playerStats.projectileCritDamage += delta;
    }
}
