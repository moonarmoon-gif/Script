using UnityEngine;

[CreateAssetMenu(fileName = "BurnDamageFavour", menuName = "Favour Effects/Burn Damage")] 
public class BurnDamageFavour : FavourEffect
{
    [Header("Burn Damage Settings")]
    [Tooltip("Bonus total burn damage per card as a percent (25 = +25% total burn damage).")]
    public float BonusTotalDamage = 25f;

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
        float delta = Mathf.Max(0f, BonusTotalDamage) / 100f;
        playerStats.burnTotalDamageMultiplier += delta;
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
        float delta = Mathf.Max(0f, BonusTotalDamage) / 100f;
        playerStats.burnTotalDamageMultiplier += delta;
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (playerStats == null || stacks <= 0)
        {
            return;
        }

        float delta = Mathf.Max(0f, BonusTotalDamage) / 100f;
        float total = delta * stacks;
        playerStats.burnTotalDamageMultiplier = Mathf.Max(0f, playerStats.burnTotalDamageMultiplier - total);
    }
}
