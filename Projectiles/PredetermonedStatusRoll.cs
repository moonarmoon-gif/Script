using UnityEngine;

/// <summary>
/// Stores deterministic (pre-rolled) outcomes for on-hit status effects.
/// Rolls once per projectile instance, so on-hit does not re-roll.
/// </summary>
public class PredeterminedStatusRoll : MonoBehaviour
{
    [Header("Burn Pre-Roll")]
    public bool burnRolled;
    public bool burnWillApply;

    [Header("Slow Pre-Roll")]
    public bool slowRolled;
    public bool slowWillApply;

    [Tooltip("Snapshot of SlowEffect.slowStacksPerHit at roll time so stacks are deterministic per projectile instance.")]
    [Range(1, 4)]
    public int slowStacksPerHit = 1;

    // Optional: roll as soon as the projectile instance is initialized (after Awake, before first Update).
    // This helps ensure the snapshot reflects the projectile's configured values for this instance.
    private void Start()
    {
        EnsureRolled();
    }

    public void EnsureRolled()
    {
        PlayerStats stats = Object.FindObjectOfType<PlayerStats>();

        // Burn
        BurnEffect burn = GetComponent<BurnEffect>();
        if (burn != null && !burnRolled)
        {
            burnRolled = true;

            float effectiveChance = burn.burnChance;
            if (stats != null && stats.hasProjectileStatusEffect)
            {
                effectiveChance += Mathf.Max(0f, stats.statusEffectChance);
            }
            effectiveChance = Mathf.Clamp(effectiveChance, 0f, 100f);

            float roll = Random.Range(0f, 100f);
            burnWillApply = roll <= effectiveChance;
        }

        // Slow
        SlowEffect slow = GetComponent<SlowEffect>();
        if (slow != null && !slowRolled)
        {
            slowRolled = true;

            // Snapshot stacks-per-hit for determinism
            slowStacksPerHit = Mathf.Clamp(slow.slowStacksPerHit, 1, 4);

            float effectiveChance = slow.slowChance;
            if (stats != null && stats.hasProjectileStatusEffect)
            {
                effectiveChance += Mathf.Max(0f, stats.statusEffectChance);
            }
            effectiveChance = Mathf.Clamp(effectiveChance, 0f, 100f);

            float roll = Random.Range(0f, 100f);
            slowWillApply = roll <= effectiveChance;
        }
    }
}