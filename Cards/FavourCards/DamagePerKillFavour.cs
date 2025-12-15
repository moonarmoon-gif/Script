using UnityEngine;

/// <summary>
/// Favour effect: damage increases per enemy killed, with stacks that reset
/// if you don't get a kill within a given time window.
/// </summary>
[CreateAssetMenu(fileName = "DamagePerKillFavour", menuName = "Favour Effects/Damage Per Kill")]
public class DamagePerKillFavour : FavourEffect
{
    [Header("Damage Per Kill Settings")]
    [Tooltip("Damage increase per stack, expressed as a fraction (0.01 = +1%).")]
    public float damageIncreasePerStack = 0.01f;

    [Tooltip("Maximum number of stacks that can be accumulated.")]
    public int maxStacks = 25;

    [Tooltip("Time window in seconds to gain a new stack before all stacks reset.")]
    public float stackResetTimer = 2f;

    private int currentStacks = 0;
    private float lastStackTime = 0f;
    private PlayerStats playerStats;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        playerStats = player.GetComponent<PlayerStats>();
        if (playerStats == null)
        {
            Debug.LogWarning($"<color=yellow>DamagePerKillFavour could not find PlayerStats on {player.name}.</color>");
            return;
        }

        currentStacks = 0;
        lastStackTime = Time.time;
        ApplyBonus();
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (playerStats == null)
        {
            return;
        }

        currentStacks = 0;
        ApplyBonus();
    }

    public override void OnEnemyKilled(GameObject player, GameObject enemy, FavourEffectManager manager)
    {
        if (playerStats == null)
        {
            return;
        }

        float now = Time.time;

        if (now - lastStackTime > stackResetTimer)
        {
            currentStacks = 0;
        }

        lastStackTime = now;

        if (currentStacks < maxStacks)
        {
            currentStacks++;
        }

        ApplyBonus();
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (playerStats == null || currentStacks <= 0)
        {
            return;
        }

        if (Time.time - lastStackTime > stackResetTimer)
        {
            currentStacks = 0;
            ApplyBonus();
        }
    }

    private void ApplyBonus()
    {
        if (playerStats == null)
        {
            return;
        }

        float multiplier = 1f + currentStacks * damageIncreasePerStack;
        if (multiplier < 0f)
        {
            multiplier = 0f;
        }

        playerStats.favourDamageMultiplier = multiplier;
    }
}
