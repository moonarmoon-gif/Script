using UnityEngine;

[CreateAssetMenu(fileName = "ProjectileCooldownReductionFavour", menuName = "Favour Effects/Projectile Cooldown Reduction")]
public class ProjectileCooldownReductionFavour : FavourEffect
{
    [Header("Cooldown Reduction Settings")]
    [Tooltip("Global projectile cooldown reduction per card (percent).")]
    public float CooldownReduction = 5f;

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
        float delta = Mathf.Max(0f, CooldownReduction) / 100f;
        playerStats.projectileCooldownReduction += delta;
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
        float delta = Mathf.Max(0f, CooldownReduction) / 100f;
        playerStats.projectileCooldownReduction += delta;
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (playerStats == null || stacks <= 0)
        {
            return;
        }

        float delta = Mathf.Max(0f, CooldownReduction) / 100f;
        float total = delta * stacks;
        playerStats.projectileCooldownReduction = Mathf.Max(0f, playerStats.projectileCooldownReduction - total);
    }
}
