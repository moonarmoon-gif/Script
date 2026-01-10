using UnityEngine;

[CreateAssetMenu(fileName = "FuryLowFavour", menuName = "Favour Effects/Fury Low")]
public class FuryLowFavour : FavourEffect
{
    [Header("Fury Low Settings")]
    [Tooltip("Base number of Fury stacks granted when this favour is first picked.")]
    public int FuryAmount = 2;

    [Header("Enhanced")]
    [Tooltip("Additional Fury stacks granted each time this favour is picked again (enhanced).")]
    public int BonusFury = 2;

    private int currentFuryAmount = 0;
    private PlayerHealth playerHealth;
    private StatusController statusController;
    private int furyStacksGranted;

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

        currentFuryAmount = Mathf.Max(0, FuryAmount);
        furyStacksGranted = 0;

        UpdateFuryStacks();
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (playerHealth == null || statusController == null)
        {
            OnApply(player, manager, sourceCard);
        }

        int bonus = Mathf.Max(0, BonusFury);
        currentFuryAmount += bonus;

        UpdateFuryStacks();
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (playerHealth == null || statusController == null || !playerHealth.IsAlive)
        {
            return;
        }

        UpdateFuryStacks();
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (statusController != null && furyStacksGranted > 0)
        {
            statusController.ConsumeStacks(StatusId.Rage, furyStacksGranted);
        }

        furyStacksGranted = 0;
        playerHealth = null;
        statusController = null;
    }

    private void UpdateFuryStacks()
    {
        if (playerHealth == null || statusController == null || currentFuryAmount <= 0)
        {
            return;
        }

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
            int desiredStacks = currentFuryAmount;
            int delta = desiredStacks - furyStacksGranted;
            if (delta > 0)
            {
                statusController.AddStatus(StatusId.Rage, delta, -1f);
                furyStacksGranted += delta;
            }
        }
        else
        {
            if (furyStacksGranted > 0)
            {
                statusController.ConsumeStacks(StatusId.Rage, furyStacksGranted);
                furyStacksGranted = 0;
            }
        }
    }
}
