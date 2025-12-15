using UnityEngine;

[CreateAssetMenu(fileName = "CriticalAccelerationFavour", menuName = "Favour Effects/Critical Acceleration")] 
public class CriticalAccelerationFavour : FavourEffect
{
    [Header("Critical Acceleration Settings")]
    [Tooltip("Number of critical hits required within the time window to trigger Acceleration.")]
    public int CritRequired = 5;

    [Tooltip("Time window (seconds) in which the required number of crits must occur.")]
    public float TimeLimit = 1f;

    private PlayerStats playerStats;
    private StatusController statusController;

    private int critCount;
    private float windowStartTime;

    private bool initialized;

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

        // One-time favour: prevent this card from appearing again this run.
        if (sourceCard != null && CardSelectionManager.Instance != null)
        {
            CardSelectionManager.Instance.RegisterOneTimeFavourUsed(sourceCard);
        }
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        // No enhanced parameters specified; upgrades just re-run setup.
        OnApply(player, manager, sourceCard);
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
            // Grant one stack of Acceleration with default duration (resolved by StatusControllerManager).
            statusController.AddStatus(StatusId.Acceleration, 1, -1f);

            // Start a new window from now, but keep the same requirement for future triggers.
            windowStartTime = now;
            critCount = 0;
        }
    }
}
