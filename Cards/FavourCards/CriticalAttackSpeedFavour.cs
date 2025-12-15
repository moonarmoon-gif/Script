using UnityEngine;

[CreateAssetMenu(fileName = "CriticalAttackSpeedFavour", menuName = "Favour Effects/Critical Attack Speed")]
public class CriticalAttackSpeedFavour : FavourEffect
{
    [Header("Crit Attack Speed Settings")]
    [Tooltip("Attack speed bonus applied per crit stack (percent). Example: 10 = +10% attack speed.")]
    public float AttackSpeedIncrement = 10f;

    [Tooltip("Duration of the attack speed buff (seconds).")]
    public float Duration = 3f;

    [Tooltip("Maximum stacks of the attack speed buff.")]
    public int MaxStack = 3;

    private PlayerStats playerStats;
    private int currentStacks = 0;
    private float remainingDuration = 0f;

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
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        OnApply(player, manager, sourceCard);
        MaxStack += 1;
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        ClearStacks();
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (playerStats == null && player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        if (playerStats == null)
        {
            return;
        }

        if (currentStacks <= 0)
        {
            return;
        }

        remainingDuration -= deltaTime;
        if (remainingDuration <= 0f)
        {
            ClearStacks();
        }
    }

    public override void OnBeforeDealDamage(GameObject player, GameObject enemy, ref float damage, FavourEffectManager manager)
    {
        if (playerStats == null && player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        if (playerStats == null)
        {
            return;
        }

        if (!playerStats.lastHitWasCrit)
        {
            return;
        }

        // Use AttackSpeedIncrement as a direct percentage bonus to
        // PlayerStats.attackSpeedPercent (e.g. 10 = +10% per stack).
        float perStackAttackSpeed = Mathf.Max(0f, AttackSpeedIncrement);
        if (perStackAttackSpeed <= 0f)
        {
            return;
        }

        int maxStacks = Mathf.Max(1, MaxStack);
        int newStacks = currentStacks < maxStacks ? currentStacks + 1 : currentStacks;

        if (newStacks != currentStacks)
        {
            int deltaStacks = newStacks - currentStacks;
            if (playerStats != null && deltaStacks > 0)
            {
                // Increase attack speed percent for ACTIVE projectiles only;
                // passive projectile cooldowns should not be affected.
                playerStats.attackSpeedPercent += perStackAttackSpeed * deltaStacks;
            }

            currentStacks = newStacks;
        }

        remainingDuration = Mathf.Max(Duration, 0f);
    }

    private void ClearStacks()
    {
        if (currentStacks > 0 && playerStats != null && AttackSpeedIncrement > 0f)
        {
            float perStackAttackSpeed = Mathf.Max(0f, AttackSpeedIncrement);
            float total = perStackAttackSpeed * currentStacks;
            // Remove the temporary attack speed bonus while clamping at 0.
            playerStats.attackSpeedPercent = Mathf.Max(0f, playerStats.attackSpeedPercent - total);
        }

        currentStacks = 0;
        remainingDuration = 0f;
    }
}
