using UnityEngine;

[CreateAssetMenu(fileName = "CritOnCritFavour", menuName = "Favour Effects 2/Crit On Crit")]
public class CritOnCritFavour : FavourEffect
{
    [Header("Crit On Crit Settings")]
    [Tooltip("Bonus crit chance (in percent) granted per stack when killing an enemy with a critical strike (e.g. 2 = +2% per stack).")]
    public float FrenzyGain = 2f;

    [Tooltip("Maximum crit chance bonus (in percent) this favour can grant at once (e.g. 10 = +10% max).")]
    public float MaximumFrenzyStacks = 10f;

    [Tooltip("Duration in seconds for each individual crit-on-crit stack (per kill). Example: 5 = each stack lasts 5s.")]
    public float FrenzyDuration = 5f;

    private PlayerStats playerStats;

    private StatusController playerStatus;
    private int sourceKey;

    private readonly System.Collections.Generic.List<float> stackExpiryTimes = new System.Collections.Generic.List<float>();

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        playerStats = player.GetComponent<PlayerStats>();
        playerStatus = player.GetComponent<StatusController>();

        sourceKey = Mathf.Abs(GetInstanceID());
        if (sourceKey == 0)
        {
            sourceKey = 1;
        }

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
        OnApply(player, manager, sourceCard);
    }

    public override void OnEnemyKilled(GameObject player, GameObject enemy, FavourEffectManager manager)
    {
        if (playerStats == null || playerStatus == null)
        {
            return;
        }

        // Only trigger when the LAST hit was a critical strike
        if (!playerStats.lastHitWasCrit)
        {
            return;
        }

        float duration = Mathf.Max(0f, FrenzyDuration);
        if (duration <= 0f)
        {
            return;
        }

        int stacksPerKill = Mathf.RoundToInt(Mathf.Max(0f, FrenzyGain));
        int maxStacks = Mathf.RoundToInt(Mathf.Max(0f, MaximumFrenzyStacks));
        if (stacksPerKill <= 0 || maxStacks <= 0)
        {
            return;
        }

        float now = Time.time;
        for (int i = stackExpiryTimes.Count - 1; i >= 0; i--)
        {
            if (now >= stackExpiryTimes[i])
            {
                stackExpiryTimes.RemoveAt(i);
            }
        }

        int room = maxStacks - stackExpiryTimes.Count;
        if (room <= 0)
        {
            return;
        }

        int toAdd = Mathf.Min(room, stacksPerKill);
        for (int i = 0; i < toAdd; i++)
        {
            stackExpiryTimes.Add(now + duration);
            playerStatus.AddStatus(StatusId.Frenzy, 1, duration, 0f, null, sourceKey);
        }
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (stackExpiryTimes.Count == 0)
        {
            return;
        }

        float now = Time.time;

        for (int i = stackExpiryTimes.Count - 1; i >= 0; i--)
        {
            if (now >= stackExpiryTimes[i])
            {
                stackExpiryTimes.RemoveAt(i);
            }
        }
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (playerStatus == null && player != null)
        {
            playerStatus = player.GetComponent<StatusController>();
        }

        if (playerStatus != null)
        {
            while (playerStatus.ConsumeStacks(StatusId.Frenzy, 1, sourceKey)) { }
        }
        stackExpiryTimes.Clear();
    }
}
