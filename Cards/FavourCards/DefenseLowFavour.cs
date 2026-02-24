using UnityEngine;

[CreateAssetMenu(fileName = "DefenseLowFavour", menuName = "Favour Effects/Defense Low")] 
public class DefenseLowFavour : FavourEffect
{
    [Header("Defense Low Settings")]
    [Tooltip("Defense stacks granted immediately when this favour is picked.")]
    public int DefenseRecieved = 1;

    [Tooltip("Maximum Defense stacks this favour will maintain in total.")]
    public int MaxStacks = 3;

    [Tooltip("Seconds between periodic Defense gains.")]
    public float Interval = 30f;

    [Header("Enhanced")]
    [Tooltip("Additional Defense stacks granted on each interval tick when this favour is enhanced.")]
    public int BonusDefenseRecieved = 1;

    [Tooltip("Additional maximum Defense stacks allowed when this favour is enhanced.")]
    public int BonusMaxStacks = 3;

    private StatusController statusController;

    private int currentIntervalGain;
    private int currentMaxStacks;
    private float nextTickTime;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        if (statusController == null)
        {
            statusController = player.GetComponent<StatusController>();
        }

        if (statusController == null)
        {
            return;
        }

        currentIntervalGain = Mathf.Max(0, DefenseRecieved);
        currentMaxStacks = Mathf.Max(0, MaxStacks);

        // Instant Defense gain on pick, clamped by max stacks.
        int instantGain = Mathf.Max(0, DefenseRecieved);
        if (instantGain > 0 && currentMaxStacks > 0)
        {
            int existing = statusController.GetStacks(StatusId.Defense);
            int remaining = Mathf.Max(0, currentMaxStacks - existing);
            int toAdd = Mathf.Min(instantGain, remaining);
            if (toAdd > 0)
            {
                statusController.AddStatus(StatusId.Defense, toAdd, -1f);
            }
        }

        float interval = Mathf.Max(0f, Interval);
        nextTickTime = interval > 0f ? GameStateManager.PauseSafeTime + interval : float.PositiveInfinity;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (statusController == null && player != null)
        {
            statusController = player.GetComponent<StatusController>();
        }

        if (statusController == null)
        {
            return;
        }

        currentIntervalGain += Mathf.Max(0, BonusDefenseRecieved);
        currentMaxStacks += Mathf.Max(0, BonusMaxStacks);
        currentMaxStacks = Mathf.Max(0, currentMaxStacks);

        int instantGain = Mathf.Max(0, DefenseRecieved);
        if (instantGain > 0 && currentMaxStacks > 0)
        {
            int existing = statusController.GetStacks(StatusId.Defense);
            int remaining = Mathf.Max(0, currentMaxStacks - existing);
            int toAdd = Mathf.Min(instantGain, remaining);
            if (toAdd > 0)
            {
                statusController.AddStatus(StatusId.Defense, toAdd, -1f);
            }
        }
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (statusController == null && player != null)
        {
            statusController = player.GetComponent<StatusController>();
        }

        if (statusController == null)
        {
            return;
        }

        if (currentIntervalGain <= 0 || currentMaxStacks <= 0)
        {
            return;
        }

        float interval = Mathf.Max(0f, Interval);
        if (interval <= 0f)
        {
            return;
        }

        if (GameStateManager.PauseSafeTime < nextTickTime)
        {
            return;
        }

        int existing = statusController.GetStacks(StatusId.Defense);
        if (existing >= currentMaxStacks)
        {
            // Already at or above cap; just schedule next check.
            nextTickTime = GameStateManager.PauseSafeTime + interval;
            return;
        }

        int remaining = currentMaxStacks - existing;
        int toAdd = Mathf.Min(currentIntervalGain, remaining);
        if (toAdd > 0)
        {
            statusController.AddStatus(StatusId.Defense, toAdd, -1f);
        }

        nextTickTime = GameStateManager.PauseSafeTime + interval;
    }
}
