using UnityEngine;

[CreateAssetMenu(fileName = "CriticalAccelerationFavour", menuName = "Favour Effects/Critical Acceleration")] 
public class CriticalAccelerationFavour : FavourEffect
{
    [Header("Critical Acceleration Settings")]
    [Tooltip("Number of critical hits required within the time window to trigger Acceleration.")]
    public int CritRequired = 5;

    [Tooltip("Time window (seconds) in which the required number of crits must occur.")]
    public float TimeLimit = 1f;

    [Header("Stacks")]
    [Tooltip("Number of ACCELERATION stacks granted each time this favour triggers.")]
    public int AccelerationStack = 1;

    [Tooltip("Maximum ACCELERATION stacks this favour can grant in total.")]
    public int MaxStack = 1;

    [Header("Enhanced")]
    [Tooltip("Additional MaxStack granted when this favour is enhanced.")]
    public int BonusMaxStack = 1;

    [Header("Pick Limit")]
    public int MaxPickLimit = 0;

    private PlayerStats playerStats;
    private StatusController statusController;

    private int critCount;
    private float windowStartTime;

    private bool initialized;
    private int currentMaxStack;

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

        if (statusController == null)
        {
            statusController = player.GetComponent<StatusController>();
        }

        if (playerStats == null || statusController == null)
        {
            return;
        }

        critCount = 0;
        windowStartTime = Time.time;
        initialized = true;

        currentMaxStack = Mathf.Max(1, MaxStack);

        // One-time favour: prevent this card from appearing again this run.
        if (sourceCard != null && CardSelectionManager.Instance != null)
        {
            CardSelectionManager.Instance.RegisterOneTimeFavourUsed(sourceCard);
        }
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        // Upgrades increase the maximum stacks this favour can grant.
        if (!initialized)
        {
            OnApply(player, manager, sourceCard);
        }
        else
        {
            currentMaxStack += Mathf.Max(0, BonusMaxStack);
        }
    }

    public override void OnBeforeDealDamage(GameObject player, GameObject enemy, ref float damage, FavourEffectManager manager)
    {
        if (!initialized)
        {
            OnApply(player, manager, null);
        }

        if (playerStats == null || statusController == null)
        {
            return;
        }

        if (!playerStats.lastHitWasCrit)
        {
            return;
        }

        int required = Mathf.Max(1, CritRequired);
        float limit = Mathf.Max(0f, TimeLimit);

        float now = Time.time;

        // Reset window if time limit has passed since first crit in the window.
        if (now - windowStartTime > limit)
        {
            windowStartTime = now;
            critCount = 0;
        }

        critCount++;

        if (critCount >= required)
        {
            TryGrantAccelerationStack();

            // Start a new window from now, but keep the same requirement for future triggers.
            windowStartTime = now;
            critCount = 0;
        }
    }

    private void TryGrantAccelerationStack()
    {
        if (statusController == null)
        {
            return;
        }

        int maxStacks = Mathf.Max(1, currentMaxStack > 0 ? currentMaxStack : MaxStack);
        int currentStacks = statusController.GetStacks(StatusId.Acceleration);

        if (currentStacks >= maxStacks)
        {
            return;
        }

        int stacksToAdd = Mathf.Max(1, AccelerationStack);
        if (currentStacks + stacksToAdd > maxStacks)
        {
            stacksToAdd = maxStacks - currentStacks;
        }

        if (stacksToAdd <= 0)
        {
            return;
        }

        statusController.AddStatus(StatusId.Acceleration, stacksToAdd, -1f);
    }
}
