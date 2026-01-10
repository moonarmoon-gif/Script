using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    private const float MeleeRangeThreshold = 4f;
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    [Header("Damage Settings")]
    [SerializeField] private float invulnerabilityDuration = 0.5f;
    [Tooltip("When checked, player is immune to all forms of damage")]
    [SerializeField] public bool immune = false;
    private float lastDamageTime = -999f;
    
    [Header("Health Regeneration")]
    [Tooltip("Time between health regeneration ticks (in seconds)")]
    public float healthRegenInterval = 1f;
    [Tooltip("Enable passive health regeneration")]
    public bool enableHealthRegen = true;
    
    private float nextRegenTime = 0f;
    private PlayerStats playerStats;

    [Header("References")]
    [SerializeField] private Animator animator; // Assign player's Animator in Inspector

    public event Action<float, float> OnHealthChanged; // (currentHealth, maxHealth)
    public event Action OnDeath;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => Mathf.Clamp(currentHealth, 0f, maxHealth);
    public bool IsAlive => CurrentHealth > 0f;

    // Tracks which enemy last registered itself as the attacker before
    // calling IDamageable.TakeDamage on the player. This lets favour
    // effects know exactly which enemy dealt the hit.
    private static GameObject pendingAttacker;

    public static void RegisterPendingAttacker(GameObject attacker)
    {
        pendingAttacker = attacker;
    }

    private void OnValidate()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        RaiseHealthChanged();
    }

    private void Start()
    {
        currentHealth = maxHealth;
        nextRegenTime = Time.time + healthRegenInterval;
        RaiseHealthChanged();

        // Cache PlayerStats so we can incorporate healthRegenPerSecond
        playerStats = GetComponent<PlayerStats>();
    }
    
    private void Update()
    {
        // Passive health regeneration
        if (enableHealthRegen && IsAlive && Time.time >= nextRegenTime)
        {
            // Only regenerate if not at full health
            if (currentHealth < maxHealth)
            {
                float missingHealth = maxHealth - currentHealth;

                // Regen rate is driven solely by PlayerStats.healthRegenPerSecond
                float regenPerSecond = playerStats != null ? playerStats.healthRegenPerSecond : 0f;
                float regenThisTick = regenPerSecond * healthRegenInterval;

                float regenAmount = Mathf.Min(regenThisTick, missingHealth);

                StatusController statusController = GetComponent<StatusController>();
                if (statusController != null && regenAmount > 0f && StatusControllerManager.Instance != null)
                {
                    int bleedStacks = statusController.GetStacks(StatusId.Bleed);
                    int blessingStacks = statusController.GetStacks(StatusId.Blessing);

                    if (bleedStacks > 0)
                    {
                        float bleedPercent = StatusControllerManager.Instance.BleedHealingReductionPercent;
                        float totalReduction = Mathf.Max(0f, bleedPercent * bleedStacks);
                        float mul = Mathf.Max(0f, 1f - totalReduction / 100f);
                        regenAmount *= mul;
                    }

                    if (blessingStacks > 0)
                    {
                        float blessPercent = StatusControllerManager.Instance.BlessingHealingIncreasePercent;
                        float totalBonus = Mathf.Max(0f, blessPercent * blessingStacks);
                        regenAmount *= 1f + totalBonus / 100f;
                    }
                }

                currentHealth = Mathf.Clamp(currentHealth + regenAmount, 0f, maxHealth);
                RaiseHealthChanged();
            }
            
            nextRegenTime = Time.time + healthRegenInterval;
        }
    }

    public void SetMaxHealth(float newMax, bool fillToMax = false)
    {
        maxHealth = Mathf.Max(1f, newMax);
        if (fillToMax) currentHealth = maxHealth;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        RaiseHealthChanged();
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || !IsAlive) return;
        float healAmount = amount;
        StatusController statusController = GetComponent<StatusController>();
        if (statusController != null && StatusControllerManager.Instance != null)
        {
            bool hasBleed = statusController.HasStatus(StatusId.Bleed);
            bool hasBlessing = statusController.HasStatus(StatusId.Blessing);
            
            if (hasBleed)
            {
                float bleedPercent = StatusControllerManager.Instance.BleedHealingReductionPercent;
                float mul = Mathf.Max(0f, 1f - bleedPercent / 100f);
                healAmount *= mul;
            }
            
            if (hasBlessing)
            {
                float blessPercent = StatusControllerManager.Instance.BlessingHealingIncreasePercent;
                healAmount *= 1f + blessPercent / 100f;
            }
        }
    
        currentHealth = Mathf.Clamp(currentHealth + healAmount, 0f, maxHealth);
        RaiseHealthChanged();
    }
    
    /// <summary>
    /// Increase maximum health and heal to full
    /// </summary>
    public void IncreaseMaxHealth(float amount)
    {
        if (amount <= 0f) return;
        maxHealth += amount;
        currentHealth += amount; // Also heal by the same amount
        RaiseHealthChanged();
        Debug.Log($"<color=green>Max health increased by {amount}! New max: {maxHealth}</color>");
    }

    public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!IsAlive) return;

        bool hadIncomingDamage = damage > 0f || pendingAttacker != null;

        float scaledDamage = damage;
        bool reflectedMeleeHit = false;

        GameObject attacker = pendingAttacker;
        pendingAttacker = null;

        if (attacker != null && scaledDamage > 0f)
        {
            EnemyCardTag attackerTag = attacker.GetComponent<EnemyCardTag>() ?? attacker.GetComponentInParent<EnemyCardTag>();
            if (attackerTag != null)
            {
                float bossMul = attackerTag.damageMultiplier;
                if (bossMul > 0f && !Mathf.Approximately(bossMul, 1f))
                {
                    scaledDamage *= bossMul;
                }
            }
        }

        if (EnemyScalingSystem.Instance != null)
        {
            float damageMultiplier = EnemyScalingSystem.Instance.GetDamageMultiplier();
            if (damageMultiplier > 0f && damageMultiplier != 1f)
            {
                scaledDamage *= damageMultiplier;
                Debug.Log($"<color=red>Enemy damage scaled: {damage:F1} x {damageMultiplier:F2} = {scaledDamage:F1}</color>");
            }
        }

        if (attacker != null && scaledDamage > 0f)
        {
            StatusController attackerStatus = attacker.GetComponent<StatusController>() ?? attacker.GetComponentInParent<StatusController>();
            if (attackerStatus != null)
            {
                bool isStatusTick1 = StatusDamageScope.IsStatusTick;
                // Treat the player GameObject as the target so FIRST STRIKE and
                // HATRED can apply correctly on enemy attacks.
                attackerStatus.ModifyOutgoingDamageAgainstTarget(ref scaledDamage, isStatusTick1, gameObject);
                attackerStatus.ApplyWeakOutgoing(ref scaledDamage, isStatusTick1, false);
            }
        }

        // Apply non-boss enemy damage reduction and armor BEFORE favour effects so
        // shields and other OnPlayerHit reactions see the already-reduced value.
        if (scaledDamage > 0f)
        {
            bool isBossAttacker = false;
            if (attacker != null)
            {
                EnemyCardTag tag = attacker.GetComponent<EnemyCardTag>() ?? attacker.GetComponentInParent<EnemyCardTag>();
                if (tag != null && tag.rarity == CardRarity.Boss)
                {
                    isBossAttacker = true;
                }
            }

            // First apply percentage reduction from favours (non-boss enemies only).
            if (!isBossAttacker && playerStats != null && playerStats.nonBossIncomingDamageMultiplier > 0f &&
                !Mathf.Approximately(playerStats.nonBossIncomingDamageMultiplier, 1f))
            {
                float before = scaledDamage;
                scaledDamage *= playerStats.nonBossIncomingDamageMultiplier;
                Debug.Log($"<color=cyan>Non-boss damage reduced: {before:F1} -> {scaledDamage:F1} (multiplier={playerStats.nonBossIncomingDamageMultiplier:F2})</color>");
            }
        }

        // Notify favour effects BEFORE any shield, invulnerability or
        // immunity logic so they can react even if health is not reduced.
        FavourEffectManager favourManager = GetComponent<FavourEffectManager>();
        if (favourManager != null)
        {
            favourManager.NotifyPlayerHit(attacker, ref scaledDamage);
        }

        bool isMeleeLikeHit = false;
        bool isRangedLikeHit = false;
        bool isAoeDamage = DamageAoeScope.IsAoeDamage;
        if (attacker != null && !StatusDamageScope.IsStatusTick && !isAoeDamage)
        {
            float distance = Vector3.Distance(attacker.transform.position, transform.position);
            if (distance <= MeleeRangeThreshold)
            {
                isMeleeLikeHit = true;
            }
            else
            {
                isRangedLikeHit = true;
            }
        }

        if (!isAoeDamage && !StatusDamageScope.IsStatusTick && isMeleeLikeHit && ReflectShield.ActiveShield != null && ReflectShield.ActiveShield.IsAlive)
        {
            ReflectShield.ActiveShield.TryHandleMeleeHit(scaledDamage, attacker);
            if (favourManager != null)
            {
                favourManager.NotifyPlayerDamageFinalized(attacker, 0f, false, isAoeDamage);
            }
            lastDamageTime = Time.time;
            return;
        }

        if (!isAoeDamage && !StatusDamageScope.IsStatusTick && isRangedLikeHit && NullifyShield.ActiveShield != null && NullifyShield.ActiveShield.IsAlive)
        {
            if (NullifyShield.ActiveShield.TryNullifyProjectileHit())
            {
                if (favourManager != null)
                {
                    favourManager.NotifyPlayerDamageFinalized(attacker, 0f, false, isAoeDamage);
                }
                lastDamageTime = Time.time;
                return;
            }
        }

        if (HolyShield.ActiveShield != null && HolyShield.ActiveShield.IsAlive)
        {
            HolyShield.ActiveShield.TakeDamage(scaledDamage, hitPoint, hitNormal);
            if (favourManager != null)
            {
                favourManager.NotifyPlayerDamageFinalized(attacker, 0f, StatusDamageScope.IsStatusTick, isAoeDamage);
            }
            return;
        }

        // Check invulnerability (before immune check so invuln frames still work)
        if (Time.time - lastDamageTime < invulnerabilityDuration)
        {
            return;
        }

        StatusController statusController = GetComponent<StatusController>();
        bool isStatusTick = StatusDamageScope.IsStatusTick;
        bool hasImmuneStatus = statusController != null && statusController.HasStatus(StatusId.Immune);
        bool woundAppliedToPlayerDamage = false;

        if (statusController != null && scaledDamage > 0f)
        {
            // Player-side NULLIFY: fully negate ranged-like enemy hits (outside
            // melee radius), consume one stack, and show Nullify text on the
            // player. This runs before other incoming-damage modifiers so no
            // further logic processes the negated hit.
            if (isRangedLikeHit && !isStatusTick && !isAoeDamage)
            {
                int nullifyStacks = statusController.GetStacks(StatusId.Nullify);
                if (nullifyStacks > 0)
                {
                    statusController.ConsumeStacks(StatusId.Nullify, 1);
                    scaledDamage = 0f;
                    if (DamageNumberManager.Instance != null)
                    {
                        DamageNumberManager.Instance.ShowNullify(transform.position);
                    }
                    if (favourManager != null)
                    {
                        favourManager.NotifyPlayerDamageFinalized(attacker, 0f, false, isAoeDamage);
                    }
                    lastDamageTime = Time.time;
                    return;
                }
            }

            // PlayerHealth.TakeDamage is used for ENEMY melee-style hits.
            // These are never player projectiles.
            IncomingDamageContext ctx = new IncomingDamageContext
            {
                isStatusTick = isStatusTick,
                isAoeDamage = DamageAoeScope.IsAoeDamage,
                isPlayerProjectile = false,
                wasWoundApplied = false
            };
            statusController.ModifyIncomingDamage(ref scaledDamage, ref ctx);
            woundAppliedToPlayerDamage = ctx.wasWoundApplied;

            // hasImmuneStatus is computed unconditionally above so that it can
            // also protect status-tick damage from being forced to minimum 1.
        }

        // This value represents the damage after all status-based modifiers
        // (Vulnerable, Defense, Decay, Armor status, etc.) but BEFORE the
        // player's flat armor and Absorption are applied. Reflect should use
        // this value so it is not reduced by the player's armor or Absorption.
        float damageBeforeArmorAndAbsorption = scaledDamage;

        // Armor should never reduce damage absorbed by shields. Apply it only
        // after all shield-like effects (OnPlayerHit hooks, HolyShield) have
        // already processed the incoming value, so it only mitigates HP damage.
        bool reducedToZeroByArmor = false;
        if (scaledDamage > 0f && playerStats != null && playerStats.armor > 0f)
        {
            float beforeArmor = scaledDamage;
            scaledDamage = Mathf.Max(0f, scaledDamage - playerStats.armor);
            reducedToZeroByArmor = scaledDamage <= 0f && beforeArmor > 0f;
            Debug.Log($"<color=cyan>Armor absorbed {beforeArmor - scaledDamage:F1} damage (armor={playerStats.armor:F1}). Final HP damage={scaledDamage:F1}</color>");
        }

        if (statusController != null && attacker != null && !isStatusTick && !isAoeDamage)
        {
            int thornStacks = statusController.GetStacks(StatusId.Thorn);
            if (thornStacks > 0 && isMeleeLikeHit)
            {
                float perStackFlat = 1f;
                if (StatusControllerManager.Instance != null)
                {
                    perStackFlat = StatusControllerManager.Instance.ThornReflectFlatDamagePerStack;
                }

                float reflectDamage = Mathf.Max(0f, perStackFlat * thornStacks);
                if (reflectDamage > 0f)
                {
                    IDamageable attackerDamageable = attacker.GetComponent<IDamageable>() ?? attacker.GetComponentInParent<IDamageable>();
                    if (attackerDamageable != null && attackerDamageable.IsAlive)
                    {
                        Vector3 attackerPos = attacker.transform.position;
                        Vector3 normal = (attackerPos - transform.position).normalized;
                        attackerDamageable.TakeDamage(reflectDamage, attackerPos, normal);

                        if (DamageNumberManager.Instance != null)
                        {
                            DamageNumberManager.Instance.ShowDamage(reflectDamage, attackerPos, DamageNumberManager.DamageType.Thorn);
                        }
                    }
                }
            }

            int reflectStacks = statusController.GetStacks(StatusId.Reflect);
            if (reflectStacks > 0 && damageBeforeArmorAndAbsorption > 0f && isMeleeLikeHit)
            {
                IDamageable attackerDamageable = attacker.GetComponent<IDamageable>() ?? attacker.GetComponentInParent<IDamageable>();
                if (attackerDamageable != null && attackerDamageable.IsAlive)
                {
                    float reflectDamage = damageBeforeArmorAndAbsorption;
                    Vector3 attackerPos = attacker.transform.position;
                    Vector3 normal = (attackerPos - transform.position).normalized;

                    // Tag the attacker EnemyHealth so its own damage pipeline
                    // renders this hit using the Reflect damage color.
                    EnemyHealth enemyHealth = attacker.GetComponent<EnemyHealth>() ?? attacker.GetComponentInParent<EnemyHealth>();
                    if (enemyHealth != null)
                    {
                        enemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Reflect);
                    }

                    attackerDamageable.TakeDamage(reflectDamage, attackerPos, normal);

                    if (DamageNumberManager.Instance != null)
                    {
                        // Only show the Reflect status text on the player; the
                        // numeric damage popup is handled by EnemyHealth.
                        DamageNumberManager.Instance.ShowReflect(transform.position);
                    }

                    reflectedMeleeHit = true;
                    scaledDamage = 0f;
                    statusController.ConsumeStacks(StatusId.Reflect, 1);
                }
            }
        }

        if (statusController != null && scaledDamage > 0f && !reflectedMeleeHit)
        {
            scaledDamage = statusController.ApplyAbsorption(scaledDamage);
        }

        // DoT ticks (burn, poison, bleed, future statuses) should always deal
        // at least 1 damage after all reductions, unless they are fully
        // negated (e.g., by Immunity). This check runs after Absorption.
        if (isStatusTick && scaledDamage > 0f && scaledDamage < 1f)
        {
            scaledDamage = 1f;
        }

        if (scaledDamage > 0f && StatusControllerManager.Instance != null)
        {
            if (isStatusTick)
            {
                scaledDamage = StatusControllerManager.Instance.RoundDamage(scaledDamage);
                if (scaledDamage > 0f && scaledDamage < 1f)
                {
                    scaledDamage = 1f;
                }
            }
            else
            {
                scaledDamage = StatusControllerManager.Instance.RoundDamage(scaledDamage);
            }
        }

        if (scaledDamage <= 0f)
        {
            if (isStatusTick)
            {
                StatusDamageScope.LastResolvedDamage = 0f;
            }

            // Preserve complete negation systems.
            if (hasImmuneStatus || immune || reflectedMeleeHit)
            {
                if (!isStatusTick && hasImmuneStatus)
                {
                    if (DamageNumberManager.Instance != null)
                    {
                        DamageNumberManager.Instance.ShowImmune(transform.position);
                    }
                    lastDamageTime = Time.time;
                }

                if (favourManager != null)
                {
                    favourManager.NotifyPlayerDamageFinalized(attacker, 0f, isStatusTick, isAoeDamage);
                }
                return;
            }

            // Minimum damage rule: if this TakeDamage call was a real hit
            // (caller passed damage > 0) and it was reduced to 0 by normal
            // mitigation, clamp to 1.
            if (hadIncomingDamage)
            {
                if (reducedToZeroByArmor)
                {
                    if (favourManager != null)
                    {
                        favourManager.NotifyPlayerDamageFinalized(attacker, 0f, isStatusTick, isAoeDamage);
                    }
                    return;
                }
                scaledDamage = 1f;
            }
            else
            {
                if (favourManager != null)
                {
                    favourManager.NotifyPlayerDamageFinalized(attacker, 0f, isStatusTick, isAoeDamage);
                }
                return;
            }
        }

        // When immune, do NOT show numeric damage – only the Immune status text.
        if (immune)
        {
            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.ShowImmune(transform.position);
            }
            Debug.Log($"<color=cyan>Player is IMMUNE - took {scaledDamage} damage but health unchanged!</color>");
            if (favourManager != null)
            {
                favourManager.NotifyPlayerDamageFinalized(attacker, 0f, isStatusTick, isAoeDamage);
            }
            lastDamageTime = Time.time;
            return;
        }
        
        if (DamageNumberManager.Instance != null)
        {
            // For status-tick damage (Poison/Wound/etc.), the status system
            // already shows its own damage numbers with the correct color, so
            // avoid duplicating a generic Player damage number.
            if (!isStatusTick)
            {
                var damageType = woundAppliedToPlayerDamage
                    ? DamageNumberManager.DamageType.Wound
                    : DamageNumberManager.DamageType.Player;
                DamageNumberManager.Instance.ShowDamage(scaledDamage, hitPoint, damageType);
            }
        }

        if (isStatusTick)
        {
            StatusDamageScope.LastResolvedDamage = scaledDamage;
        }

        lastDamageTime = Time.time;
        currentHealth = Mathf.Clamp(currentHealth - scaledDamage, 0f, maxHealth);
        RaiseHealthChanged();

        if (favourManager != null)
        {
            favourManager.NotifyPlayerDamageFinalized(attacker, scaledDamage, isStatusTick, isAoeDamage);
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Damage(float amount)
    {
        if (amount <= 0f || !IsAlive) return;

        if (amount > 0f && amount < 1f)
        {
            amount = 1f;
        }

        // Check invulnerability
        if (Time.time - lastDamageTime < invulnerabilityDuration) return;
        
        if (immune)
        {
            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.ShowImmune(transform.position);
            }
            Debug.Log($"<color=cyan>Player is IMMUNE - damage blocked!</color>");
            lastDamageTime = Time.time;
            return;
        }

        lastDamageTime = Time.time;
        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
        RaiseHealthChanged();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void TakeDamage1(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        // Delegate to the main TakeDamage pipeline so immunity, armour,
        // shields, and damage-number rules are applied consistently.
        TakeDamage(amount, hitPoint, hitNormal);
    }

    private void Die()
    {
        StatusController statusController = GetComponent<StatusController>();
        if (statusController != null && StatusControllerManager.Instance != null && statusController.GetStacks(StatusId.Revival) > 0)
        {
            statusController.ConsumeStacks(StatusId.Revival, 1);
            float healPercent = StatusControllerManager.Instance.RevivalHealPercent;
            float healAmount = Mathf.Max(0f, healPercent) / 100f * maxHealth;
            currentHealth = Mathf.Clamp(healAmount, 1f, maxHealth);
            lastDamageTime = Time.time;
            RaiseHealthChanged();
            return;
        }

        // Play death animation
        if (animator != null)
        {
            animator.SetTrigger("dead");
            animator.Play("Player_Death", 0, 0f);
        }

        // Disable all player behaviors
        var controller = GetComponent<PlayerController>();
        if (controller != null) controller.enabled = false;

        // Disable other components
        var collider = GetComponent<Collider2D>();
        if (collider != null) collider.enabled = false;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = false;

        OnDeath?.Invoke();
    }

    private void RaiseHealthChanged()
    {
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        lastDamageTime = -999f;
        RaiseHealthChanged();
    }
}