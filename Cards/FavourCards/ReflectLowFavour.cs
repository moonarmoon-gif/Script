using UnityEngine;

[CreateAssetMenu(fileName = "ReflectLowFavour", menuName = "Favour Effects/Reflect Low")]
public class ReflectLowFavour : FavourEffect
{
    [Header("Reflect Low Settings")]
    [Tooltip("Base number of Reflect stacks granted each recharge.")]
    public int ReflectGain = 1;

    [Tooltip("Time in seconds between automatic Reflect stack gains.")]
    public float RechargeTimer = 30f;

    [Tooltip("Maximum Reflect stacks that can be maintained by this favour.")]
    public int MaxStacks = 2;

    [Header("Enhanced")]
    [Tooltip("Additional Reflect stacks gained each recharge when enhanced.")]
    public int BonusReflectGain = 1;

    [Tooltip("Additional maximum Reflect stacks granted when enhanced.")]
    public int BonusMaxStacks = 1;

    private float rechargeTimerRemaining = 0f;
    private int currentMaxStacks = 0;
    private int currentGainPerTick = 0;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentGainPerTick = Mathf.Max(0, ReflectGain);
        currentMaxStacks = Mathf.Max(0, MaxStacks);

        // Start with max stacks the first time this favour is obtained.
        if (player != null && currentMaxStacks > 0)
        {
            StatusController status = player.GetComponent<StatusController>();
            if (status != null)
            {
                int existing = status.GetStacks(StatusId.Reflect);
                int toAdd = Mathf.Max(0, currentMaxStacks - existing);
                if (toAdd > 0)
                {
                    status.AddStatus(StatusId.Reflect, toAdd, -1f);
                }
            }
        }

        rechargeTimerRemaining = RechargeTimer;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentGainPerTick += Mathf.Max(0, BonusReflectGain);
        currentMaxStacks += Mathf.Max(0, BonusMaxStacks);
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (player == null || RechargeTimer <= 0f || currentGainPerTick <= 0 || currentMaxStacks <= 0)
        {
            return;
        }

        rechargeTimerRemaining -= deltaTime;
        if (rechargeTimerRemaining > 0f)
        {
            return;
        }

        rechargeTimerRemaining += RechargeTimer;

        StatusController status = player.GetComponent<StatusController>();
        if (status == null)
        {
            return;
        }

        int existing = status.GetStacks(StatusId.Reflect);
        if (existing >= currentMaxStacks)
        {
            return;
        }

        int room = currentMaxStacks - existing;
        int toAdd = Mathf.Min(room, currentGainPerTick);
        if (toAdd > 0)
        {
            status.AddStatus(StatusId.Reflect, toAdd, -1f);
        }
    }
}
