using UnityEngine;

[CreateAssetMenu(fileName = "HpAccelerationFavour", menuName = "Favour Effects 2/Hp Acceleration")]
public class HpAccelerationFavour : FavourEffect
{
    [Header("Hp Acceleration Settings")]
    public int AccelerationGain = 1;
    public float HealthThreshold = 100f;

    [Header("Enhanced")]
    public int BonusAccelerationGain = 1;

    [Header("Pick Limit")]
    public int MaxPickLimit = 0;

    private PlayerHealth playerHealth;
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

        playerHealth = player.GetComponent<PlayerHealth>();
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
        if (deltaTime <= 0f)
        {
            return;
        }

        rescanTimer -= deltaTime;
        if (rescanTimer > 0f)
        {
            return;
        }

        rescanTimer = 0.1f;
        UpdateAccelerationStacks(false);
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (statusController != null && stacksGranted > 0)
        {
            statusController.ConsumeStacks(StatusId.Acceleration, stacksGranted, sourceKey);
        }

        playerHealth = null;
        statusController = null;
        stacksGranted = 0;
        rescanTimer = 0f;
    }

    private void UpdateAccelerationStacks(bool force)
    {
        if (playerHealth == null || statusController == null)
        {
            return;
        }

        if (!playerHealth.IsAlive)
        {
            if (stacksGranted > 0)
            {
                statusController.ConsumeStacks(StatusId.Acceleration, stacksGranted, sourceKey);
                stacksGranted = 0;
            }
            return;
        }

        int desired = Mathf.Max(0, AccelerationGain);
        if (desired <= 0)
        {
            if (stacksGranted > 0)
            {
                statusController.ConsumeStacks(StatusId.Acceleration, stacksGranted, sourceKey);
                stacksGranted = 0;
            }
            return;
        }

        float thresholdPercent = Mathf.Clamp(HealthThreshold, 0f, 100f);
        bool meetsThreshold = false;

        if (playerHealth.MaxHealth > 0f)
        {
            if (thresholdPercent >= 99.99f)
            {
                meetsThreshold = Mathf.Abs(playerHealth.CurrentHealth - playerHealth.MaxHealth) <= 0.01f;
            }
            else
            {
                float fraction = playerHealth.CurrentHealth / playerHealth.MaxHealth;
                meetsThreshold = fraction >= thresholdPercent / 100f;
            }
        }

        if (!meetsThreshold)
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
