using UnityEngine;

[CreateAssetMenu(fileName = "BlessingLowHealthFavour", menuName = "Favour Effects/Blessing On Low Health")] 
public class BlessingLowHealthFavour : FavourEffect
{
    [Header("Blessing On Low Health Settings")]
    [Tooltip("Duration in seconds for the BLESSING status when health falls below the threshold.")]
    public float BlessingDuration = 10f;

    [Tooltip("Health threshold fraction 0-1 at which Blessing triggers (0.5 = 50% health).")]
    public float HealthThreshold = 0.5f;

    [Tooltip("Cooldown in seconds between Blessing activations.")]
    public float Cooldown = 60f;

    [Header("Enhanced")]
    [Tooltip("Additional Blessing duration (seconds) when this favour is enhanced.")]
    public float BonusBlessingDuration = 5f;

    private PlayerHealth playerHealth;
    private StatusController statusController;
    private float currentBlessingDuration;
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

        if (statusController == null)
        {
            statusController = player.GetComponent<StatusController>();
        }

        if (playerHealth == null || statusController == null)
        {
            return;
        }

        currentBlessingDuration = Mathf.Max(0f, BlessingDuration);
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (playerHealth == null || statusController == null)
        {
            OnApply(player, manager, sourceCard);
        }
        else
        {
            currentBlessingDuration += Mathf.Max(0f, BonusBlessingDuration);
        }
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (playerHealth == null || statusController == null || !playerHealth.IsAlive)
        {
            return;
        }

        float threshold = Mathf.Clamp01(HealthThreshold);
        float healthFraction = playerHealth.MaxHealth > 0f
            ? playerHealth.CurrentHealth / playerHealth.MaxHealth
            : 0f;

        if (healthFraction <= threshold && Time.time >= lastTriggerTime + Cooldown)
        {
            ActivateBlessing();
        }
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        // Blessing is handled entirely via StatusController duration; no explicit
        // cleanup is required when the favour is removed.
        playerHealth = null;
        statusController = null;
    }

    private void ActivateBlessing()
    {
        if (statusController == null)
        {
            return;
        }

        float duration = Mathf.Max(0f, currentBlessingDuration);
        if (duration <= 0f)
        {
            return;
        }

        // Apply exactly one stack of BLESSING for the resolved duration. If the
        // player already has Blessing, this will refresh/extend it according to
        // StatusController's AddStatus stacking rules.
        statusController.AddStatus(StatusId.Blessing, 1, duration);
        lastTriggerTime = Time.time;
    }
}
