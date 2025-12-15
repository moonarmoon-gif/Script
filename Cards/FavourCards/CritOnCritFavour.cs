using UnityEngine;

[CreateAssetMenu(fileName = "CritOnCritFavour", menuName = "Favour Effects/Crit On Crit")]
public class CritOnCritFavour : FavourEffect
{
    [Header("Crit On Crit Settings")]
    [Tooltip("Bonus crit chance (in percent) granted per stack when killing an enemy with a critical strike (e.g. 2 = +2% per stack).")]
    public float BonusCritChance = 2f;

    [Tooltip("Maximum crit chance bonus (in percent) this favour can grant at once (e.g. 10 = +10% max).")]
    public float MaximumCritChance = 10f;

    [Tooltip("Duration in seconds for each individual crit-on-crit stack (per kill). Example: 5 = each stack lasts 5s.")]
    public float CritDuration = 5f;

    private PlayerStats playerStats;

    // Current number of active stacks and total applied bonus (percent)
    private int activeStacks = 0;
    private float currentBonusPercent = 0f;

    // Time remaining per stack; independent timers as requested
    private System.Collections.Generic.List<float> stackExpiryTimes = new System.Collections.Generic.List<float>();

    // Cached reference to the owning player for convenience
    private GameObject owner;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        owner = player;

        if (player == null)
        {
            return;
        }

        playerStats = player.GetComponent<PlayerStats>();
        if (playerStats == null)
        {
            Debug.LogWarning($"<color=yellow>CritOnCritFavour could not find PlayerStats on {player.name}.</color>");
            return;
        }

        activeStacks = 0;
        currentBonusPercent = 0f;
        stackExpiryTimes.Clear();

        // Mark this favour card as one-time-per-run so it cannot appear again
        // after being chosen once.
        if (sourceCard != null && CardSelectionManager.Instance != null)
        {
            CardSelectionManager.Instance.RegisterOneTimeFavourUsed(sourceCard);
        }
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        // Enhanced: increase the maximum crit bonus cap by the same base amount.
        MaximumCritChance += Mathf.Max(0f, MaximumCritChance);
    }

    public override void OnEnemyKilled(GameObject player, GameObject enemy, FavourEffectManager manager)
    {
        if (playerStats == null)
        {
            return;
        }

        // Only trigger when the LAST hit was a critical strike
        if (!playerStats.lastHitWasCrit)
        {
            return;
        }

        float perStack = Mathf.Max(0f, BonusCritChance);
        if (perStack <= 0f)
        {
            return;
        }

        // Add a new independent stack lasting CritDuration seconds
        float now = Time.time;
        float duration = Mathf.Max(0f, CritDuration);
        float expiry = now + duration;

        stackExpiryTimes.Add(expiry);
        activeStacks++;

        RecalculateBonus();
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (playerStats == null || stackExpiryTimes.Count == 0)
        {
            return;
        }

        float now = Time.time;
        bool changed = false;

        for (int i = stackExpiryTimes.Count - 1; i >= 0; i--)
        {
            if (now >= stackExpiryTimes[i])
            {
                stackExpiryTimes.RemoveAt(i);
                activeStacks--;
                if (activeStacks < 0) activeStacks = 0;
                changed = true;
            }
        }

        if (changed)
        {
            RecalculateBonus();
        }
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (playerStats == null)
        {
            return;
        }

        if (!Mathf.Approximately(currentBonusPercent, 0f))
        {
            playerStats.critChance = Mathf.Max(0f, playerStats.critChance - currentBonusPercent);
        }

        currentBonusPercent = 0f;
        activeStacks = 0;
        stackExpiryTimes.Clear();
    }

    private void RecalculateBonus()
    {
        if (playerStats == null)
        {
            return;
        }

        float perStack = Mathf.Max(0f, BonusCritChance);
        float maxCap = Mathf.Max(0f, MaximumCritChance);

        float desiredBonus = Mathf.Min(activeStacks * perStack, maxCap);
        float delta = desiredBonus - currentBonusPercent;

        if (Mathf.Approximately(delta, 0f))
        {
            return;
        }

        playerStats.critChance += delta;
        currentBonusPercent = desiredBonus;
    }
}
