// EnemyHealth.cs
using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 30f;
    [SerializeField] private float currentHealth = 30f;
    [SerializeField] public bool ignoreScalingFromEnemyScalingSystem = false;

    private bool hasStarted = false;
    private float pendingPostScalingHealthMultiplier = 1f;

    [Header("Damage Settings")]
    [Tooltip("If true, enemy can only take damage when visible on camera")]
    [SerializeField] private bool requireOnCameraForDamage = true;

    public event Action<float, float> OnHealthChanged; // (current, max)
    public event Action OnDeath;
    public event Action<float, Vector3, Vector3> OnDamageTaken; // (amount, hitPoint, hitNormal)

    public float MaxHealth => Mathf.Max(1f, maxHealth);
    public float CurrentHealth => Mathf.Clamp(currentHealth, 0f, MaxHealth);
    public bool IsAlive => CurrentHealth > 0f;

    /// <summary>
    /// Multiply max health by a value (used by EnemySpawner)
    /// </summary>
    public void MultiplyMaxHealth(float multiplier)
    {
        maxHealth *= multiplier;
        currentHealth *= multiplier;
    }

    public void RegisterPostScalingHealthMultiplier(float multiplier)
    {
        if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f)) return;

        if (hasStarted)
        {
            MultiplyMaxHealth(multiplier);
        }
        else
        {
            pendingPostScalingHealthMultiplier *= multiplier;
        }
    }

    private Camera mainCamera;
    private bool immuneToPlayerDeath = false;
    private bool immuneToBossMenace = false;

    // Tracks the damage-number color that should be used if this hit ends up
    // dealing exactly 0 damage (e.g., fully blocked by Defense/Armor). For
    // player projectiles, this is set by the projectile scripts so that the
    // fallback 0 number respects the projectile's elemental damage color.
    private DamageNumberManager.DamageType lastIncomingDamageType = DamageNumberManager.DamageType.Fire;

    public void SetLastIncomingDamageType(DamageNumberManager.DamageType damageType)
    {
        lastIncomingDamageType = damageType;
    }

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Start()
    {
        // Initialize current health to max health if not already set
        if (currentHealth <= 0f || currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        // Apply enemy scaling system
        if (EnemyScalingSystem.Instance != null && !ignoreScalingFromEnemyScalingSystem)
        {
            float originalMax = maxHealth;
            float originalCurrent = currentHealth;
            float multiplier = EnemyScalingSystem.Instance.GetHealthMultiplier();

            // Scale both max and current health proportionally
            maxHealth *= multiplier;
            currentHealth *= multiplier;
        }

        hasStarted = true;

        if (!Mathf.Approximately(pendingPostScalingHealthMultiplier, 1f))
        {
            MultiplyMaxHealth(pendingPostScalingHealthMultiplier);
            pendingPostScalingHealthMultiplier = 1f;
        }
    }

    private void OnValidate()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, MaxHealth);
        // Don't raise events in editor - only at runtime
        if (Application.isPlaying)
        {
            RaiseChanged();
        }
    }

    private bool IsOnCamera()
    {
        if (mainCamera == null) return true; // If no camera, allow damage

        Vector3 viewportPos = mainCamera.WorldToViewportPoint(transform.position);

        // Check if within camera bounds (with small buffer)
        return viewportPos.x >= -0.1f && viewportPos.x <= 1.1f &&
               viewportPos.y >= -0.1f && viewportPos.y <= 1.1f &&
               viewportPos.z > 0f;
    }

    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (amount < 0f || !IsAlive) return;

        // Check if player is dead - enemies are immune
        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            return; // Immune to damage when player is dead
        }

        // Check if this enemy was made immune due to player death
        if (immuneToPlayerDeath)
        {
            return; // Immune to damage
        }

        if (immuneToBossMenace)
        {
            return;
        }

        // Check if enemy is on camera (if required)
        if (requireOnCameraForDamage && !IsOnCamera())
        {
            return; // Don't take damage when off-camera
        }

        float finalAmount = amount;
        float woundBonusDamage = 0f;

        StatusController statusController = GetComponent<StatusController>();

        bool isStatusTickLocal = StatusDamageScope.IsStatusTick;
        bool isPlayerProjectile = !isStatusTickLocal;

        if (statusController != null)
        {
            // Cache NULLIFY stacks so we can detect when a projectile hit was
            // fully cancelled by this status and show the appropriate popup.
            int nullifyStacksBefore = statusController.GetStacks(StatusId.Nullify);

            // EnemyHealth.TakeDamage is called primarily by player projectiles
            // and status effects. When not in a status tick, treat as
            // player-projectile damage so NULLIFY and similar effects can
            // react appropriately.
            IncomingDamageContext ctx = new IncomingDamageContext
            {
                isStatusTick = isStatusTickLocal,
                isAoeDamage = DamageAoeScope.IsAoeDamage,
                isPlayerProjectile = isPlayerProjectile,
                wasWoundApplied = false
            };

            float beforeStatuses = finalAmount;
            statusController.ModifyIncomingDamage(ref finalAmount, ref ctx);

            // If WOUND contributed bonus damage, compute that bonus portion so
            // we can render it as a separate damage number later.
            if (ctx.wasWoundApplied && finalAmount > beforeStatuses)
            {
                woundBonusDamage = finalAmount - beforeStatuses;
            }

            if (!isStatusTickLocal && finalAmount <= 0f)
            {
                // IMMUNE takes priority over NULLIFY for fully negated hits.
                if (statusController.HasStatus(StatusId.Immune))
                {
                    if (DamageNumberManager.Instance != null)
                    {
                        Vector3 anchor = DamageNumberManager.Instance.GetAnchorWorldPosition(gameObject, transform.position);
                        DamageNumberManager.Instance.ShowImmune(anchor);
                    }
                    return;
                }

                // If damage was fully negated and at least one NULLIFY stack
                // was consumed by this player-projectile hit, show the
                // Nullify status text instead of a numeric 0.
                int nullifyStacksAfter = statusController.GetStacks(StatusId.Nullify);
                if (!DamageAoeScope.IsAoeDamage && isPlayerProjectile && nullifyStacksBefore > 0 && nullifyStacksAfter < nullifyStacksBefore)
                {
                    if (DamageNumberManager.Instance != null)
                    {
                        Vector3 anchor = DamageNumberManager.Instance.GetAnchorWorldPosition(gameObject, transform.position);
                        DamageNumberManager.Instance.ShowNullify(anchor);
                    }
                    return;
                }
            }

            // Apply Absorption AFTER all normal mitigation but BEFORE any
            // downstream special-case logic. This should affect both direct
            // hits and status-tick damage.
            if (finalAmount > 0f)
            {
                finalAmount = statusController.ApplyAbsorption(finalAmount);
            }

            // DoT ticks (burn, poison, bleed) should always deal at least 1
            // damage after all reductions, unless they are fully negated.
            if (isStatusTickLocal && finalAmount > 0f && finalAmount < 1f)
            {
                finalAmount = 1f;
            }

            // Shield absorbs AFTER status mitigation + absorption (+ DoT min-1),
            // but BEFORE health is reduced.
            if (finalAmount > 0f)
            {
                statusController.ApplyFinalIncomingDamageMitigation(ref finalAmount, isStatusTickLocal);
            }
        }

        if (!StatusDamageScope.IsStatusTick)
        {
            if (statusController != null)
            {
                int thornStacks = statusController.GetStacks(StatusId.Thorn);
                if (thornStacks > 0)
                {
                    float reflectPerStack = 1f;
                    if (StatusControllerManager.Instance != null)
                    {
                        reflectPerStack = StatusControllerManager.Instance.ThornReflectFlatDamagePerStack;
                    }
                    float reflectDamage = Mathf.Max(0f, reflectPerStack * thornStacks);
                    if (reflectDamage > 0f)
                    {
                        GameObject player = GameObject.FindGameObjectWithTag("Player");
                        if (player != null)
                        {
                            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
                            if (playerHealth != null && playerHealth.IsAlive)
                            {
                                // Apply Thorn reflection to the player using the
                                // simplified Damage path so it does not trigger
                                // player-side statuses twice, and show a
                                // Thorn-colored damage number at the player.
                                playerHealth.Damage(reflectDamage);
                                if (DamageNumberManager.Instance != null)
                                {
                                    DamageNumberManager.Instance.ShowDamage(reflectDamage, playerHealth.transform.position, DamageNumberManager.DamageType.Thorn);
                                }
                            }
                        }
                    }
                }
            }

            GameObject execPlayer = GameObject.FindGameObjectWithTag("Player");
            if (execPlayer != null && StatusControllerManager.Instance != null)
            {
                StatusController playerStatus = execPlayer.GetComponent<StatusController>();
                if (playerStatus != null && playerStatus.GetStacks(StatusId.Execute) > 0)
                {
                    float thresholdPercent = StatusControllerManager.Instance.ExecuteThresholdPercent;
                    if (thresholdPercent > 0f && MaxHealth > 0f)
                    {
                        float hpPercent = (CurrentHealth / MaxHealth) * 100f;
                        if (hpPercent <= thresholdPercent)
                        {
                            float killDamage = MaxHealth * 1000f;
                            finalAmount = killDamage;
                            playerStatus.ConsumeStacks(StatusId.Execute, 1);

                            if (DamageNumberManager.Instance != null)
                            {
                                Vector3 anchor = DamageNumberManager.Instance.GetAnchorWorldPosition(gameObject, transform.position);
                                DamageNumberManager.Instance.ShowExecuted(anchor);
                            }
                        }
                    }
                }
            }
        }

        // At this point, finalAmount is the fully resolved damage after all
        // mitigation (Defense, Armor, Vulnerable, Condemn, DeathMark, etc.)
        // AND after shield absorption.
        if (DamageNumberManager.Instance != null && !StatusDamageScope.IsStatusTick)
        {
            float baseDamageForPopup = finalAmount;

            if (woundBonusDamage > 0f && finalAmount > 0f)
            {
                baseDamageForPopup = Mathf.Max(0f, finalAmount - woundBonusDamage);
            }

            Vector3 popupAnchor = DamageNumberManager.Instance.GetAnchorWorldPosition(gameObject, hitPoint);
            DamageNumberManager.Instance.ShowDamage(baseDamageForPopup, popupAnchor, lastIncomingDamageType);

            if (woundBonusDamage > 0f)
            {
                DamageNumberManager.Instance.ShowDamage(woundBonusDamage, popupAnchor, DamageNumberManager.DamageType.Wound);
            }
        }

        if (finalAmount <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Clamp(currentHealth - finalAmount, 0f, MaxHealth);
        OnDamageTaken?.Invoke(finalAmount, hitPoint, hitNormal);
        RaiseChanged();

        if (currentHealth <= 0f)
        {
            bool grantExp = GameStateManager.Instance == null || !GameStateManager.Instance.PlayerIsDead;
            if (!grantExp)
            {
                Debug.Log($"<color=yellow>{gameObject.name} died but no EXP granted (player is dead)</color>");
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                FavourEffectManager favourManager = player.GetComponent<FavourEffectManager>();
                if (favourManager != null)
                {
                    favourManager.NotifyEnemyKilled(gameObject);
                }
            }

            OnDeath?.Invoke();
        }
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

        currentHealth = Mathf.Clamp(currentHealth + healAmount, 0f, MaxHealth);
        RaiseChanged();
    }

    private void RaiseChanged() => OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

    public bool IsCurrentlyImmune()
    {
        if (!IsAlive)
        {
            return true;
        }

        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            return true;
        }

        if (immuneToPlayerDeath || immuneToBossMenace)
        {
            return true;
        }

        return false;
    }

    public void SetImmuneToPlayerDeath(bool immune)
    {
        immuneToPlayerDeath = immune;
        if (immune)
        {
            Debug.Log($"<color=yellow>{gameObject.name} is now immune to damage (player died)</color>");
        }
    }

    public void SetImmuneToBossMenace(bool immune)
    {
        immuneToBossMenace = immune;
    }
}