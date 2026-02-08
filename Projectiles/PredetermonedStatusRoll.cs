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

    [Header("Static Pre-Roll")]
    public bool staticRolled;
    public bool staticWillApply;

    [Header("FireBite Pre-Roll")]
    public bool fireBiteRolled;
    public bool fireBiteWillApply;

    // Optional: roll as soon as the projectile instance is initialized (after Awake, before first Update).
    // This helps ensure the snapshot reflects the projectile's configured values for this instance.
    private void Start()
    {
        EnsureRolled();
    }

    public void EnsureRolled()
    {
        PlayerStats stats = Object.FindObjectOfType<PlayerStats>();

        ProjectileCards sourceCard = null;
        if (ProjectileCardModifiers.Instance != null)
        {
            sourceCard = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
        }

        bool isActiveSource =
            sourceCard != null &&
            sourceCard.projectileSystem == ProjectileCards.ProjectileSystemType.Active;

        FireBall fireBall = GetComponent<FireBall>();
        if (fireBall != null && !fireBiteRolled)
        {
            fireBiteRolled = true;

            if (!fireBall.EnableFireBite)
            {
                fireBiteWillApply = false;
            }
            else
            {
                float effectiveChance = Mathf.Max(0f, fireBall.FireBiteChance);
                if (sourceCard != null && ProjectileCardModifiers.Instance != null)
                {
                    CardModifierStats modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(sourceCard);
                    if (modifiers != null)
                    {
                        effectiveChance += Mathf.Max(0f, modifiers.specialChanceBonusPercent);
                    }
                }

                effectiveChance = Mathf.Clamp(effectiveChance, 0f, 100f);
                float roll = Random.Range(0f, 100f);
                fireBiteWillApply = roll <= effectiveChance;
            }
        }

        // Burn
        BurnEffect burn = GetComponent<BurnEffect>();
        if (burn != null && !burnRolled)
        {
            burnRolled = true;

            float effectiveChance = burn.burnChance;
            if (stats != null && stats.hasProjectileStatusEffect)
            {
                effectiveChance += Mathf.Max(0f, stats.statusEffectChance);

                if (isActiveSource)
                {
                    effectiveChance += Mathf.Max(0f, stats.activeProjectileStatusEffectChanceBonus);
                }
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

                if (isActiveSource)
                {
                    effectiveChance += Mathf.Max(0f, stats.activeProjectileStatusEffectChanceBonus);
                }
            }
            effectiveChance = Mathf.Clamp(effectiveChance, 0f, 100f);

            float roll = Random.Range(0f, 100f);
            slowWillApply = roll <= effectiveChance;
        }

        // Static
        StaticEffect stat = GetComponent<StaticEffect>();
        if (stat != null && !staticRolled)
        {
            staticRolled = true;

            float effectiveChance = stat.staticChance;
            if (stats != null && stats.hasProjectileStatusEffect)
            {
                effectiveChance += Mathf.Max(0f, stats.statusEffectChance);

                if (isActiveSource)
                {
                    effectiveChance += Mathf.Max(0f, stats.activeProjectileStatusEffectChanceBonus);
                }
            }
            effectiveChance = Mathf.Clamp(effectiveChance, 0f, 100f);

            float roll = Random.Range(0f, 100f);
            staticWillApply = roll <= effectiveChance;
        }
    }
}