using UnityEngine;

[CreateAssetMenu(fileName = "HealthRegenBoostFavour", menuName = "Favour Effects/Health Regen Boost")]
public class HealthRegenBoostFavour : FavourEffect
{
    [Header("Health Regen Boost Settings")]
    [Tooltip("Extra health regeneration per second when the boost is active.")]
    public float HealthRegen = 10f;

    [Tooltip("Time in seconds without taking real damage before the boost activates.")]
    public float RegenTimer = 10f;

    private PlayerStats playerStats;
    private int stacks = 0;
    private float lastRealDamageTime;
    private bool boostActive = false;
    private float currentAppliedBonus = 0f;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        playerStats = player.GetComponent<PlayerStats>();
        if (playerStats == null)
        {
            Debug.LogWarning($"<color=yellow>HealthRegenBoostFavour could not find PlayerStats on {player.name}.</color>");
            return;
        }

        stacks = 1;
        lastRealDamageTime = Time.time;
        boostActive = false;
        currentAppliedBonus = 0f;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (playerStats == null && player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        if (playerStats == null)
        {
            return;
        }

        stacks++;
        if (boostActive)
        {
            ApplyBoost();
        }
    }

    public override void OnPlayerHit(GameObject player, GameObject attacker, ref float damage, FavourEffectManager manager)
    {
        if (damage < 1f)
        {
            return;
        }

        lastRealDamageTime = Time.time;

        if (boostActive)
        {
            RemoveBoost();
        }
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (playerStats == null)
        {
            return;
        }

        if (!boostActive && Time.time - lastRealDamageTime >= RegenTimer)
        {
            ApplyBoost();
        }
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (boostActive)
        {
            RemoveBoost();
        }
    }

    private void ApplyBoost()
    {
        if (playerStats == null)
        {
            return;
        }

        float targetBonus = stacks * HealthRegen;
        float delta = targetBonus - currentAppliedBonus;
        if (Mathf.Approximately(delta, 0f))
        {
            return;
        }

        playerStats.healthRegenPerSecond += delta;
        currentAppliedBonus = targetBonus;
        boostActive = true;
    }

    private void RemoveBoost()
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
        boostActive = false;
    }
}
