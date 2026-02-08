using UnityEngine;

[CreateAssetMenu(fileName = "AbsorptionPerTimeFavour", menuName = "Favour Effects/Absorption Per Time")] 
public class AbsorptionPerTimeFavour : FavourEffect
{
    [Header("Absorption Per Time Settings")]
    [Tooltip("Absorption stacks granted instantly when this favour is first picked.")]
    public int InstantAbsorptionGain = 1;

    [Tooltip("Absorption stacks granted every interval while this favour is active.")]
    public int AbsorptionGainOnInterval = 1;

    [Tooltip("Maximum Absorption stacks this favour can maintain in total (including the initial instant gain).")]
    public int MaxStacks = 2;

    [Tooltip("Interval between periodic Absorption gains (seconds).")]
    public float Interval = 30f;

    [Header("Enhanced")]
    [Tooltip("Additional Absorption stacks added to each interval tick when this favour is enhanced.")]
    public int BonusAbsorption = 1;

    [Tooltip("Additional maximum Absorption stacks allowed when this favour is enhanced.")]
    public int BonusMaxStacks = 2;

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

        currentIntervalGain = Mathf.Max(0, AbsorptionGainOnInterval);
        currentMaxStacks = Mathf.Max(0, MaxStacks);

        // Apply the initial instant Absorption gain, clamped by MaxStacks.
        int instantGain = Mathf.Max(0, InstantAbsorptionGain);
        if (instantGain > 0 && currentMaxStacks > 0)
        {
            int existing = statusController.GetStacks(StatusId.Absorption);
            int remaining = Mathf.Max(0, currentMaxStacks - existing);
            int toAdd = Mathf.Min(instantGain, remaining);
            if (toAdd > 0)
            {
                statusController.AddStatus(StatusId.Absorption, toAdd, -1f);
            }
        }

        // Schedule the first periodic tick.
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

        currentIntervalGain += Mathf.Max(0, BonusAbsorption);
        currentMaxStacks += Mathf.Max(0, BonusMaxStacks);
        currentMaxStacks = Mathf.Max(0, currentMaxStacks);
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

        int existing = statusController.GetStacks(StatusId.Absorption);
        if (existing >= currentMaxStacks)
        {
            // Already at or above cap; just schedule the next check.
            nextTickTime = GameStateManager.PauseSafeTime + interval;
            return;
        }

        int remaining = currentMaxStacks - existing;
        int toAdd = Mathf.Min(currentIntervalGain, remaining);
        if (toAdd > 0)
        {
            statusController.AddStatus(StatusId.Absorption, toAdd, -1f);
        }

        nextTickTime = GameStateManager.PauseSafeTime + interval;
    }
}
