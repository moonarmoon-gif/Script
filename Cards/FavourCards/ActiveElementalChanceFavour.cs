using UnityEngine;

[CreateAssetMenu(fileName = "ActiveElementalChanceFavour", menuName = "Favour Effects/Active Elemental Chance")]
public class ActiveElementalChanceFavour : FavourEffect
{
    [Header("Active Elemental Chance Settings")]
    [Tooltip("Bonus status/elemental effect chance for ACTIVE projectiles only (0.05 = +5% chance).")]
    public float IncreasedChance = 0.05f;

    private PlayerStats playerStats;
    private int stacks = 0;
    private float appliedBonusPercent = 0f;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        playerStats = player.GetComponent<PlayerStats>();
        if (playerStats == null)
        {
            Debug.LogWarning($"<color=yellow>ActiveElementalChanceFavour could not find PlayerStats on {player.name}.</color>");
            return;
        }

        stacks = 1;
        ApplyBonus();
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

        // Enhanced: increase chance by the same amount again.
        stacks++;
        ApplyBonus();
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (playerStats == null || stacks <= 0)
        {
            return;
        }

        float delta = Mathf.Max(0f, appliedBonusPercent);
        playerStats.activeProjectileStatusEffectChanceBonus = Mathf.Max(0f, playerStats.activeProjectileStatusEffectChanceBonus - delta);
        appliedBonusPercent = 0f;
        stacks = 0;
    }

    private void ApplyBonus()
    {
        if (playerStats == null || stacks <= 0)
        {
            return;
        }

        // activeProjectileStatusEffectChanceBonus in PlayerStats is 0-100.
        // We treat IncreasedChance as a raw fraction (0.05 = +5%) and apply
        // only the DELTA from the last applied bonus so stacking/upgrade math
        // stays correct.
        float newTotalBonusPercent = Mathf.Max(0f, IncreasedChance) * stacks * 100f;
        float delta = newTotalBonusPercent - appliedBonusPercent;
        if (Mathf.Approximately(delta, 0f))
        {
            return;
        }

        playerStats.hasProjectileStatusEffect = true;
        playerStats.activeProjectileStatusEffectChanceBonus += delta;
        appliedBonusPercent = newTotalBonusPercent;
    }
}