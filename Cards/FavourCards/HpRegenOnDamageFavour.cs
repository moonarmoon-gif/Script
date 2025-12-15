using UnityEngine;

[CreateAssetMenu(fileName = "HpRegenOnDamageFavour", menuName = "Favour Effects/Hp Regen On Damage")]
public class HpRegenOnDamageFavour : FavourEffect
{
    [Header("HP Regen On Damage Settings")]
    [Tooltip("Health regeneration per second granted while the buff is active (e.g. 1 = +1 HP/s).")]
    public float HpRegen = 1f;

    [Tooltip("Duration in seconds that the regen buff stays active after taking REAL HP damage (not blocked by shields).")]
    public float Duration = 3f;

    private PlayerStats playerStats;
    private PlayerHealth playerHealth;

    // How many times this favour has been taken (1 on first apply, +1 per upgrade).
    private int stacks = 0;

    // Tracks the currently applied bonus to PlayerStats.healthRegenPerSecond so
    // we can adjust / remove it cleanly.
    private float currentAppliedBonus = 0f;

    // Time (in Time.time) when the current buff should expire.
    private float buffEndTime = 0f;

    // Last observed health value from PlayerHealth.OnHealthChanged so we can
    // detect REAL HP loss (after shields and armour).
    private float lastHealthValue = -1f;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        playerStats = player.GetComponent<PlayerStats>();
        playerHealth = player.GetComponent<PlayerHealth>();

        if (playerStats == null || playerHealth == null)
        {
            Debug.LogWarning($"<color=yellow>HpRegenOnDamageFavour could not find PlayerStats/PlayerHealth on {player.name}.</color>");
            return;
        }

        stacks = 1;
        currentAppliedBonus = 0f;
        buffEndTime = 0f;
        lastHealthValue = playerHealth.CurrentHealth;

        // Listen for REAL HP changes so we only trigger on actual health loss,
        // not on shield hits.
        playerHealth.OnHealthChanged += OnPlayerHealthChanged;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (playerStats == null && player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        if (playerHealth == null && player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }

        if (playerStats == null || playerHealth == null)
        {
            return;
        }

        // "Enhanced" behaviour: increase the effective HpRegen by the same
        // base amount each time this favour is upgraded.
        stacks++;

        // If a buff is currently active, immediately update the applied bonus
        // so the new stack is reflected without waiting for the next hit.
        if (currentAppliedBonus > 0f && Time.time < buffEndTime)
        {
            ApplyRegenBonus();
        }
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (playerStats == null)
        {
            return;
        }

        if (currentAppliedBonus > 0f && Time.time >= buffEndTime)
        {
            RemoveRegenBonus();
        }
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= OnPlayerHealthChanged;
        }

        RemoveRegenBonus();
    }

    private void OnPlayerHealthChanged(float current, float max)
    {
        if (playerHealth == null || playerStats == null)
        {
            return;
        }

        if (lastHealthValue < 0f)
        {
            lastHealthValue = current;
            return;
        }

        // Detect REAL HP damage: current health decreased. When damage is
        // fully absorbed by shields, PlayerHealth does not change its
        // currentHealth value, so this will not trigger.
        if (current < lastHealthValue && current > 0f)
        {
            TriggerBuff();
        }

        lastHealthValue = current;
    }

    private void TriggerBuff()
    {
        if (playerStats == null)
        {
            return;
        }

        ApplyRegenBonus();
        buffEndTime = Time.time + Mathf.Max(0f, Duration);
    }

    private void ApplyRegenBonus()
    {
        if (playerStats == null)
        {
            return;
        }

        if (stacks <= 0 || HpRegen <= 0f)
        {
            return;
        }

        float targetBonus = stacks * HpRegen;
        float delta = targetBonus - currentAppliedBonus;
        if (Mathf.Approximately(delta, 0f))
        {
            return;
        }

        playerStats.healthRegenPerSecond += delta;
        currentAppliedBonus = targetBonus;
    }

    private void RemoveRegenBonus()
    {
        if (playerStats == null)
        {
            return;
        }

        if (!Mathf.Approximately(currentAppliedBonus, 0f))
        {
            playerStats.healthRegenPerSecond -= currentAppliedBonus;
        }

        currentAppliedBonus = 0f;
    }
}
