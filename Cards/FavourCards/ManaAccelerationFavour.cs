using UnityEngine;

[CreateAssetMenu(fileName = "ManaAccelerationFavour", menuName = "Favour Effects/Mana Acceleration")]
public class ManaAccelerationFavour : FavourEffect
{
    [Header("Mana Acceleration Settings")]
    public int AccelerationGain = 1;
    public float ManaThreshold = 90f;
    public float ManaCheck = 1f;

    [Header("Enhanced")]
    public int BonusAccelerationGain = 1;

    [Header("Pick Limit")]
    public int MaxPickLimit = 0;

    private PlayerMana playerMana;
    private StatusController statusController;

    private int sourceKey;
    private int stacksGranted;
    private float rescanTimer;

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

        playerMana = player.GetComponent<PlayerMana>();
        statusController = player.GetComponent<StatusController>();

        sourceKey = Mathf.Abs(GetInstanceID());
        if (sourceKey == 0)
        {
            sourceKey = 1;
        }

        stacksGranted = 0;
        rescanTimer = 0f;

        UpdateAccelerationStacks(true);
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        AccelerationGain += Mathf.Max(0, BonusAccelerationGain);
        UpdateAccelerationStacks(true);
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (playerMana == null || statusController == null)
        {
            return;
        }

        rescanTimer -= deltaTime;
        if (rescanTimer > 0f)
        {
            return;
        }

        rescanTimer = Mathf.Max(0.01f, ManaCheck);
        UpdateAccelerationStacks(false);
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (statusController != null && stacksGranted > 0)
        {
            statusController.ConsumeStacks(StatusId.Acceleration, stacksGranted, sourceKey);
        }

        stacksGranted = 0;
        playerMana = null;
        statusController = null;
    }

    private void UpdateAccelerationStacks(bool force)
    {
        if (playerMana == null || statusController == null)
        {
            return;
        }

        int desired = Mathf.Max(0, AccelerationGain);
        stacksGranted = Mathf.Max(0, statusController.GetStacks(StatusId.Acceleration, sourceKey));
        if (desired <= 0)
        {
            if (stacksGranted > 0)
            {
                statusController.ConsumeStacks(StatusId.Acceleration, stacksGranted, sourceKey);
                stacksGranted = 0;
            }
            return;
        }

        float thresholdPercent = Mathf.Clamp(ManaThreshold, 0f, 100f);
        float fraction = 0f;
        if (playerMana.MaxManaExact > 0f)
        {
            fraction = playerMana.CurrentManaExact / playerMana.MaxManaExact;
        }

        bool shouldHaveStacks = fraction > thresholdPercent / 100f;

        if (!shouldHaveStacks)
        {
            if (stacksGranted > 0)
            {
                statusController.ConsumeStacks(StatusId.Acceleration, stacksGranted, sourceKey);
                stacksGranted = 0;
            }
            return;
        }

        int delta = desired - stacksGranted;
        if (delta > 0)
        {
            statusController.AddStatus(StatusId.Acceleration, delta, -1f, 0f, null, sourceKey);
            stacksGranted += delta;
        }
        else if (delta < 0)
        {
            statusController.ConsumeStacks(StatusId.Acceleration, -delta, sourceKey);
            stacksGranted += delta;
        }
    }
}
