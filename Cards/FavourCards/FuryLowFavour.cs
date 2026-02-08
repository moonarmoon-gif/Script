using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "RageLowFavour", menuName = "Favour Effects/Rage Low")]
public class RageLowFavour : FavourEffect
{
    [Header("Fury Low Settings")]
    [Tooltip("Base number of Fury stacks granted when this favour is first picked.")]
    [FormerlySerializedAs("FuryAmount")]
    public int RageAmount = 2;

    [Header("Enhanced")]
    [Tooltip("Additional Fury stacks granted each time this favour is picked again (enhanced).")]
    [FormerlySerializedAs("BonusFury")]
    public int BonusRage = 2;

    private int currentRageAmount = 0;
    private PlayerHealth playerHealth;
    private StatusController statusController;
    private int rageStacksGranted;
    private int sourceKey;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        if (playerHealth == null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }

        if (statusController == null)
        {
            statusController = player.GetComponent<StatusController>();
        }

        if (playerHealth == null || statusController == null)
        {
            return;
        }

        sourceKey = Mathf.Abs(GetInstanceID());
        if (sourceKey == 0)
        {
            sourceKey = 1;
        }

        currentRageAmount = Mathf.Max(0, RageAmount);
        rageStacksGranted = 0;

        UpdateRageStacks();
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (playerHealth == null || statusController == null)
        {
            OnApply(player, manager, sourceCard);
        }

        int bonus = Mathf.Max(0, BonusRage);
        currentRageAmount += bonus;

        UpdateRageStacks();
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (playerHealth == null || statusController == null || !playerHealth.IsAlive)
        {
            return;
        }

        UpdateRageStacks();
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (statusController != null && rageStacksGranted > 0)
        {
            statusController.ConsumeStacks(StatusId.Rage, rageStacksGranted, sourceKey);
        }

        rageStacksGranted = 0;
        playerHealth = null;
        statusController = null;
    }

    private void UpdateRageStacks()
    {
        if (playerHealth == null || statusController == null || currentRageAmount <= 0)
        {
            return;
        }

        rageStacksGranted = Mathf.Max(0, statusController.GetStacks(StatusId.Rage, sourceKey));

        float max = playerHealth.MaxHealth;
        if (max <= 0f)
        {
            return;
        }

        float current = playerHealth.CurrentHealth;
        float fraction = current / max;

        float thresholdFraction = 0.5f;
        if (StatusControllerManager.Instance != null)
        {
            thresholdFraction = StatusControllerManager.Instance.RageLowHealthThresholdPercent / 100f;
        }

        if (fraction <= thresholdFraction)
        {
            int desiredStacks = currentRageAmount;
            int delta = desiredStacks - rageStacksGranted;
            if (delta > 0)
            {
                statusController.AddStatus(StatusId.Rage, delta, -1f, 0f, null, sourceKey);
                rageStacksGranted += delta;
            }
            else if (delta < 0)
            {
                statusController.ConsumeStacks(StatusId.Rage, -delta, sourceKey);
                rageStacksGranted += delta;
            }
        }
        else
        {
            if (rageStacksGranted > 0)
            {
                statusController.ConsumeStacks(StatusId.Rage, rageStacksGranted, sourceKey);
                rageStacksGranted = 0;
            }
        }
    }
}

public class FuryLowFavour : RageLowFavour
{
}
