using UnityEngine;

[CreateAssetMenu(fileName = "BlessingLowHealthFavour", menuName = "Favour Effects/Blessing On Low Health")] 
public class BlessingLowHealthFavour : FavourEffect
{
    [Header("Blessing On Low Health Settings")]
    [Tooltip("Duration in seconds for the BLESSING status when health falls below the threshold.")]
    public float BlessingDuration = 10f;

    public float HealthRegen = 1f;

    [Tooltip("Health threshold fraction 0-1 at which Blessing triggers (0.5 = 50% health).")]
    public float HealthThreshold = 0.5f;

    [Tooltip("Cooldown in seconds between Blessing activations.")]
    public float Cooldown = 60f;

    [Header("Enhanced")]
    [Tooltip("Additional health regeneration per second (added on top of the base +1) when this favour is enhanced.")]
    public float BonusHealthRegen = 1f;

    private PlayerHealth playerHealth;
    private PlayerStats playerStats;
    private StatusController statusController;
    private float currentAppliedRegenBonus;
    private float regenBuffEndTime;
    private float lastTriggerTime = -999f;

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

        if (playerStats == null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        if (statusController == null)
        {
            statusController = player.GetComponent<StatusController>();
        }

        if (playerHealth == null || playerStats == null || statusController == null)
        {
            return;
        }
        currentAppliedRegenBonus = 0f;
        regenBuffEndTime = 0f;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (playerHealth == null || playerStats == null || statusController == null)
        {
            OnApply(player, manager, sourceCard);
        }

        HealthRegen += Mathf.Max(0f, BonusHealthRegen);

        if (currentAppliedRegenBonus > 0f && GameStateManager.PauseSafeTime < regenBuffEndTime)
        {
            ApplyRegenBonus();
        }
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (playerHealth == null || playerStats == null || statusController == null || !playerHealth.IsAlive)
        {
            return;
        }

        if (currentAppliedRegenBonus > 0f && GameStateManager.PauseSafeTime >= regenBuffEndTime)
        {
            RemoveRegenBonus();
        }

        float threshold = Mathf.Clamp01(HealthThreshold);
        float healthFraction = playerHealth.MaxHealth > 0f
            ? playerHealth.CurrentHealth / playerHealth.MaxHealth
            : 0f;

        if (healthFraction <= threshold && GameStateManager.PauseSafeTime >= lastTriggerTime + Cooldown)
        {
            ActivateBlessing();
        }
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        RemoveRegenBonus();
        playerHealth = null;
        playerStats = null;
        statusController = null;
    }

    private void ActivateBlessing()
    {
        if (statusController == null || playerStats == null)
        {
            return;
        }

        float duration = Mathf.Max(0f, BlessingDuration);
        if (duration <= 0f)
        {
            return;
        }

        // Apply exactly one stack of BLESSING for the resolved duration. If the
        // player already has Blessing, this will refresh/extend it according to
        // StatusController's AddStatus stacking rules.
        statusController.AddStatus(StatusId.Blessing, 1, duration);

        ApplyRegenBonus();
        regenBuffEndTime = GameStateManager.PauseSafeTime + duration;
        lastTriggerTime = GameStateManager.PauseSafeTime;
    }

    private void ApplyRegenBonus()
    {
        if (playerStats == null)
        {
            return;
        }

        float targetBonus = Mathf.Max(0f, HealthRegen);
        float delta = targetBonus - currentAppliedRegenBonus;
        if (Mathf.Approximately(delta, 0f))
        {
            return;
        }

        playerStats.healthRegenPerSecond += delta;
        currentAppliedRegenBonus = targetBonus;
    }

    private void RemoveRegenBonus()
    {
        if (playerStats == null)
        {
            return;
        }

        if (!Mathf.Approximately(currentAppliedRegenBonus, 0f))
        {
            playerStats.healthRegenPerSecond -= currentAppliedRegenBonus;
        }

        currentAppliedRegenBonus = 0f;
    }
}
