using UnityEngine;
using System.Collections;

/// <summary>
/// Static on-hit effect similar to BurnEffect/SlowEffect.
/// Attach this to a projectile (PlayerProjectiles, Collapse, etc.).
/// On hit, it can apply a StaticStatus to enemies which freezes movement
/// and animations for a short period, with optional re-application while
/// the overall static effect duration is active.
/// </summary>
public class StaticEffect : MonoBehaviour
{
    [Header("Static Settings")]
    [Tooltip("Chance to apply static on first hit (0-100%)")] 
    [Range(0f, 100f)]
    public float staticChance = 30f;

    [Tooltip("Duration of each static freeze period (seconds)")] 
    public float staticPeriod = 0.75f;

    [Tooltip("Total duration the static effect can remain active on the target (seconds)")]
    public float staticDuration = 2.0f;

    [Tooltip("Chance to re-apply a new static period while the effect is active (0-100%)")] 
    [Range(0f, 100f)]
    public float staticReapplyChance = 20f;

    [Tooltip("Interval between automatic reapply rolls while staticDuration is active (seconds)")]
    public float staticReapplyInterval = 0.5f;

    [Header("Visual Settings")]
    [Tooltip("Static VFX prefab to spawn on enemy while static is active")] 
    public GameObject staticVFXPrefab;

    [Tooltip("Optional static VFX offset when enemy is on the LEFT side of the camera")] 
    public Vector2 staticVFXOffsetLeft = Vector2.zero;

    [Tooltip("Optional static VFX offset when enemy is on the RIGHT side of the camera")] 
    public Vector2 staticVFXOffsetRight = Vector2.zero;

    /// <summary>
    /// Try to apply or reapply static to an enemy.
    /// Returns true if a static period was started or refreshed.
    /// </summary>
    public bool TryApplyStatic(GameObject enemy, Vector3 hitPoint)
    {
        if (enemy == null) return false;

        // Don't apply to dead enemies
        EnemyHealth health = enemy.GetComponent<EnemyHealth>() ?? enemy.GetComponentInParent<EnemyHealth>();
        if (health != null && !health.IsAlive)
        {
            return false;
        }

        GameObject ownerGO = health != null ? health.gameObject : enemy;

        bool ignorePreForThisProjectile =
            GetComponent<ElementalBeam>() != null ||
            GetComponent<ProjectileFireTalon>() != null ||
            GetComponent<ProjectileIceTalon>() != null ||
            GetComponent<DwarfStar>() != null ||
            GetComponent<NovaStar>() != null;

        PredeterminedStatusRoll pre = ignorePreForThisProjectile
            ? null
            : GetComponent<PredeterminedStatusRoll>();

        if (pre != null)
        {
            pre.EnsureRolled();
            if (pre.staticRolled && !pre.staticWillApply)
            {
                return false;
            }
        }

        // Resolve player stats for shared elemental chance bonuses.
        PlayerStats stats = Object.FindObjectOfType<PlayerStats>();

        // Determine whether THIS projectile came from an ACTIVE projectile card.
        ProjectileCards sourceCard = null;
        if (ProjectileCardModifiers.Instance != null)
        {
            sourceCard = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
        }

        bool isActiveSource =
            sourceCard != null &&
            sourceCard.projectileSystem == ProjectileCards.ProjectileSystemType.Active;

        // Compute the effective static chance.
        float effectiveChance = staticChance;
        if (stats != null && stats.hasProjectileStatusEffect)
        {
            effectiveChance += Mathf.Max(0f, stats.statusEffectChance);
            if (isActiveSource)
            {
                effectiveChance += Mathf.Max(0f, stats.activeProjectileStatusEffectChanceBonus);
            }
        }
        effectiveChance = Mathf.Clamp(effectiveChance, 0f, 100f);

        StaticStatus staticStatus = ownerGO.GetComponent<StaticStatus>();
        if (staticStatus == null)
        {
            // First-time application uses effectiveChance, unless we already have a
            // predetermined roll on this projectile instance.
            if (pre == null || !pre.staticRolled)
            {
                float roll = Random.Range(0f, 100f);
                if (roll > effectiveChance)
                {
                    return false;
                }
            }

            staticStatus = ownerGO.AddComponent<StaticStatus>();

            // Compute initial VFX position with optional left/right offsets
            // relative to the camera so the static prefab appears on the
            // desired side of the enemy while still following their sprite.
            Vector3 vfxPosition = ownerGO.transform.position;
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                bool enemyOnLeftSide = ownerGO.transform.position.x < mainCam.transform.position.x;
                Vector2 offset = enemyOnLeftSide ? staticVFXOffsetLeft : staticVFXOffsetRight;
                vfxPosition += (Vector3)offset;
            }
            else
            {
                vfxPosition = hitPoint;
            }

            staticStatus.ApplyStatic(staticPeriod, staticDuration, staticReapplyChance, staticReapplyInterval, staticVFXPrefab, vfxPosition);

            // Inform favour effects that the player has successfully
            // inflicted STATIC on this enemy so they can react (e.g., by
            // adding Shocked stacks via StatusEffectsDebuffFavour).
            FavourEffectManager favourManager = Object.FindObjectOfType<FavourEffectManager>();
            if (favourManager != null)
            {
                favourManager.NotifyStatusApplied(ownerGO, StatusId.Static);
            }

            Debug.Log($"<color=yellow>⚡ STATIC APPLIED! Period={staticPeriod:F2}s, Duration={staticDuration:F2}s, Reapply={staticReapplyChance:F0}%</color>");
            return true;
        }
        else
        {
            // Re-application path: roll effectiveChance again while the effect's
            // duration window is still active. staticReapplyChance is reserved
            // for automatic background pulses inside StaticStatus.
            // If we have a predetermined roll, skip this re-roll.
            if (pre == null || !pre.staticRolled)
            {
                float roll = Random.Range(0f, 100f);
                if (roll > effectiveChance)
                {
                    return false;
                }
            }

            bool reapplied = staticStatus.TryReapplyStatic(staticPeriod, hitPoint);
            return reapplied;
        }
    }
}

/// <summary>
/// Component attached to enemies that are under a Static effect.
/// Handles freezing movement and animations during each static period,
/// and keeps track of an overall StaticDuration window. This class is
/// intentionally generic: it uses Rigidbody2D constraints and Animator to freeze
/// most animations. Enemy-specific cooldown logic can optionally be
/// integrated via IStaticInterruptHandler.
/// </summary>
public class StaticStatus : MonoBehaviour
{
    private float remainingDuration = 0f;
    private float staticPeriod = 0.5f;
    private float staticReapplyChance = 0f;
    private float staticReapplyInterval = 0.5f;

    private bool isInStaticPeriod = false;
    private Coroutine staticPeriodRoutine;

    public bool IsInStaticPeriod => isInStaticPeriod;

    private GameObject staticVFXInstance;
    private GameObject staticVFXPrefab;

    private Rigidbody2D enemyRb;
    private Vector2 storedVelocity;
    private float storedAngularVelocity;
    private RigidbodyConstraints2D storedConstraints;

    private Animator animator;
    private float originalAnimatorSpeed = 1f;
    private bool animatorFrozen = false;
    private StatusController statusController;

    private EnemyHealth enemyHealth;

    private int[] storedAnimatorStateHashes;
    private float[] storedAnimatorNormalizedTimes;
    private int storedAnimatorLayerCount = 0;
    private bool hasStoredAnimatorState = false;

    /// <summary>
    /// Apply initial Static effect.
    /// </summary>
    public void ApplyStatic(float period, float duration, float reapplyChance, float reapplyInterval, GameObject vfxPrefab, Vector3 hitPoint)
    {
        staticPeriod = Mathf.Max(0.01f, period);
        remainingDuration = Mathf.Max(0f, duration);
        staticReapplyChance = Mathf.Clamp(reapplyChance, 0f, 100f);
        staticReapplyInterval = Mathf.Max(0.01f, reapplyInterval);
        staticVFXPrefab = vfxPrefab;

        if (remainingDuration <= 0f)
        {
            // Nothing to do
            Destroy(this);
            return;
        }

        statusController = GetComponent<StatusController>();
        if (statusController != null)
        {
            statusController.AddStatus(StatusId.Static, 1);
            if (!statusController.HasStatus(StatusId.Static))
            {
                Destroy(this);
                return;
            }
        }

        enemyRb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();

        enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleOwnerDeath;
            enemyHealth.OnDeath += HandleOwnerDeath;
        }

        // Spawn VFX (lifetime tied to this component) at the provided
        // hitPoint, which may already include left/right offsets, and
        // parent it to the enemy so it follows their movement.
        if (staticVFXPrefab != null && staticVFXInstance == null)
        {
            Vector3 spawnPos = hitPoint;
            if (spawnPos == Vector3.zero)
            {
                spawnPos = transform.position;
            }

            staticVFXInstance = Instantiate(staticVFXPrefab, spawnPos, Quaternion.identity, transform);
        }

        // Start the overall duration routine
        StartCoroutine(StaticDurationRoutine());

        // Start the first static period
        StartStaticPeriod();
    }

    /// <summary>
    /// Try to reapply a new static period while the overall duration is active.
    /// The actual chance roll is handled by StaticEffect (using staticChance).
    /// Here we simply restart the static period if the duration window is not
    /// over yet.
    /// </summary>
    public bool TryReapplyStatic(float period, Vector3 hitPoint)
    {
        if (remainingDuration <= 0f)
        {
            return false;
        }

        staticPeriod = Mathf.Max(0.01f, period);

        // Restart static period timer
        StartStaticPeriod();
        Debug.Log($"<color=yellow>⚡ STATIC REAPPLIED (ON HIT)! New period={staticPeriod:F2}s, RemainingDuration={remainingDuration:F2}s</color>");
        return true;
    }

    private IEnumerator StaticDurationRoutine()
    {
        float reapplyTimer = 0f;

        while (remainingDuration > 0f)
        {
            float dt = GameStateManager.GetPauseSafeDeltaTime();
            remainingDuration -= dt;
            reapplyTimer += dt;

            // Auto-reapply logic: while not currently in a static period, roll
            // staticReapplyChance every staticReapplyInterval to potentially
            // start a new static pulse even if no new hits occur.
            if (!isInStaticPeriod && staticReapplyChance > 0f && staticReapplyInterval > 0f && reapplyTimer >= staticReapplyInterval)
            {
                reapplyTimer = 0f;
                float roll = Random.Range(0f, 100f);
                if (roll <= staticReapplyChance)
                {
                    StartStaticPeriod();
                }
            }

            yield return null;
        }

        // StaticDuration has expired: stop scheduling new pulses, but if a
        // static period is still running, allow it to finish naturally before
        // cleaning up the status.
        while (isInStaticPeriod)
        {
            yield return null;
        }

        EndStaticCompletely();
    }

    private void StartStaticPeriod()
    {
        if (staticPeriodRoutine != null)
        {
            StopCoroutine(staticPeriodRoutine);
        }
        staticPeriodRoutine = StartCoroutine(StaticPeriodCoroutine());
    }

    private IEnumerator StaticPeriodCoroutine()
    {
        bool wasInStaticPeriod = isInStaticPeriod;
        isInStaticPeriod = true;

        if (!wasInStaticPeriod)
        {
            // Freeze movement and animations
            StoreAndFreezeMovement();
            StoreAndFreezeAnimator();
        }

        yield return GameStateManager.WaitForPauseSafeSeconds(staticPeriod);

        // Unfreeze
        RestoreMovement();
        RestoreAnimator();

        isInStaticPeriod = false;

        // When static period ends, we do not destroy the status yet; the
        // StaticDurationRoutine will handle total lifetime. This allows
        // subsequent hits to reapply static within the same duration window.
    }

    private void StoreAndFreezeMovement()
    {
        if (enemyRb != null)
        {
            storedVelocity = enemyRb.velocity;
            storedAngularVelocity = enemyRb.angularVelocity;
            enemyRb.velocity = Vector2.zero;
            enemyRb.angularVelocity = 0f;

            storedConstraints = enemyRb.constraints;

            enemyRb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }

    private void RestoreMovement()
    {
        if (enemyRb != null)
        {
            enemyRb.constraints = storedConstraints;

            enemyRb.velocity = storedVelocity;
            enemyRb.angularVelocity = storedAngularVelocity;
        }
    }

    private void StoreAndFreezeAnimator()
    {
        if (animator == null)
        {
            return;
        }

        if (IsAnimatorInDeathState())
        {
            return;
        }

        originalAnimatorSpeed = animator.speed;

        int layerCount = animator.layerCount;
        if (storedAnimatorStateHashes == null || storedAnimatorStateHashes.Length < layerCount)
        {
            storedAnimatorStateHashes = new int[layerCount];
            storedAnimatorNormalizedTimes = new float[layerCount];
        }
        storedAnimatorLayerCount = layerCount;

        for (int i = 0; i < layerCount; i++)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(i);
            storedAnimatorStateHashes[i] = info.fullPathHash;
            storedAnimatorNormalizedTimes[i] = info.normalizedTime;
        }
        hasStoredAnimatorState = true;

        animator.speed = 0f;
        animatorFrozen = true;
    }

    private void RestoreAnimator()
    {
        if (animator != null && animatorFrozen)
        {
            animator.speed = originalAnimatorSpeed;

            bool isDying = IsAnimatorInDeathState();
            if (hasStoredAnimatorState && !isDying)
            {
                int count = Mathf.Min(storedAnimatorLayerCount, animator.layerCount);
                for (int i = 0; i < count; i++)
                {
                    animator.Play(storedAnimatorStateHashes[i], i, storedAnimatorNormalizedTimes[i]);
                }
                animator.Update(0f);
            }
        }

        hasStoredAnimatorState = false;
        animatorFrozen = false;
    }

    private bool IsAnimatorInDeathState()
    {
        if (animator == null)
        {
            return false;
        }

        bool isDying = false;
        try
        {
            isDying |= animator.GetBool("dead");
        }
        catch { }

        try
        {
            isDying |= animator.GetBool("deadflip");
        }
        catch { }

        try
        {
            isDying |= animator.GetBool("IsDead");
        }
        catch { }

        return isDying;
    }

    private void HandleOwnerDeath()
    {
        hasStoredAnimatorState = false;
        EndStaticCompletely();
    }

    private void EndStaticCompletely()
    {
        // Ensure any active static period is ended cleanly
        if (isInStaticPeriod)
        {
            if (staticPeriodRoutine != null)
            {
                StopCoroutine(staticPeriodRoutine);
                staticPeriodRoutine = null;
            }

            RestoreMovement();
            RestoreAnimator();
            isInStaticPeriod = false;
        }

        if (staticVFXInstance != null)
        {
            Destroy(staticVFXInstance);
        }

        if (statusController != null)
        {
            statusController.ConsumeStacks(StatusId.Static, 1);
        }

        Destroy(this);
    }

    private void OnDestroy()
    {
        if (staticVFXInstance != null)
        {
            Destroy(staticVFXInstance);
        }

        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleOwnerDeath;
        }
    }
}

/// <summary>
/// Optional interface enemies can implement if they want to hook custom
/// behavior into Static start/end (e.g., forcing attack/charge cooldowns
/// when an animation is interrupted). This keeps StaticStatus generic
/// while allowing per-enemy tuning.
/// </summary>
public interface IStaticInterruptHandler
{
    /// <summary>
    /// Called when a static period begins on this enemy.
    /// staticPeriodSeconds is the freeze duration for this pulse.
    /// </summary>
    void OnStaticStart(float staticPeriodSeconds);

    /// <summary>
    /// Called when a static period ends on this enemy.
    /// </summary>
    void OnStaticEnd();
}
