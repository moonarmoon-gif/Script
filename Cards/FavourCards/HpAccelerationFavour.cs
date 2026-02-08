using UnityEngine;

[CreateAssetMenu(fileName = "AccelerationFavour", menuName = "Favour Effects 2/Acceleration")]
public class AccelerationFavour : FavourEffect
{
    [Header("Acceleration Settings")]
    public int AccelerationGain = 1;

    [Header("Enhanced")]
    public int BonusAccelerationGain = 1;

    [Header("Pick Limit")]
    public int MaxPickLimit = 0;

    private StatusController statusController;

    private int sourceKey;
    private int stacksGranted;

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

        statusController = player.GetComponent<StatusController>();
        if (statusController == null)
        {
            return;
        }

        sourceKey = Mathf.Abs(GetInstanceID());
        if (sourceKey == 0)
        {
            sourceKey = 1;
        }

        stacksGranted = 0;
        ApplyDesiredStacks();
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        AccelerationGain += Mathf.Max(0, BonusAccelerationGain);
        ApplyDesiredStacks();
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (statusController != null && stacksGranted > 0)
        {
            statusController.ConsumeStacks(StatusId.Acceleration, stacksGranted, sourceKey);
        }

        statusController = null;
        stacksGranted = 0;
    }

    private void ApplyDesiredStacks()
    {
        if (statusController == null)
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

        int delta = desired - stacksGranted;
        if (delta > 0)
        {
            statusController.AddStatus(StatusId.Acceleration, delta, -9999f, 0f, null, sourceKey);
            stacksGranted += delta;
        }
        else if (delta < 0)
        {
            statusController.ConsumeStacks(StatusId.Acceleration, -delta, sourceKey);
            stacksGranted += delta;
        }
    }
}

public class HpAccelerationFavour : AccelerationFavour
{
}
