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
        EnemyHealth health = enemy.GetComponent<EnemyHealth>();
        if (health != null && !health.IsAlive)
        {
            return false;
        }

        // Resolve player stats for shared elemental chance bonuses.
        PlayerStats stats = Object.FindObjectOfType<PlayerStats>();

        // Compute the effective static chance, including any global
        // statusEffectChance from projectile modifiers and favours.
        float effectiveChance = staticChance;
        if (stats != null && stats.hasProjectileStatusEffect)
        {
            effectiveChance += Mathf.Max(0f, stats.statusEffectChance);
        }
        effectiveChance = Mathf.Clamp(effectiveChance, 0f, 100f);

        StaticStatus staticStatus = enemy.GetComponent<StaticStatus>();
        if (staticStatus == null)
        {
            // First-time application uses effectiveChance
            float roll = Random.Range(0f, 100f);
            if (roll > effectiveChance)
            {
                return false;
            }

            staticStatus = enemy.AddComponent<StaticStatus>();

            // Compute initial VFX position with optional left/right offsets
            // relative to the camera so the static prefab appears on the
            // desired side of the enemy while still following their sprite.
            Vector3 vfxPosition = enemy.transform.position;
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                bool enemyOnLeftSide = enemy.transform.position.x < mainCam.transform.position.x;
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
                favourManager.NotifyStatusApplied(enemy, StatusId.Static);
            }

            Debug.Log($"<color=yellow>⚡ STATIC APPLIED! Period={staticPeriod:F2}s, Duration={staticDuration:F2}s, Reapply={staticReapplyChance:F0}%</color>");
            return true;
        }
        else
        {
            // Re-application path: roll effectiveChance again while the effect's
            // duration window is still active. staticReapplyChance is reserved
            // for automatic background pulses inside StaticStatus.
            float roll = Random.Range(0f, 100f);
            if (roll > effectiveChance)
            {
                return false;
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
/// intentionally generic: it uses reflection to find common movement
/// speed fields (moveSpeed / walkSpeed) and an Animator to freeze
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

    private GameObject staticVFXInstance;
    private GameObject staticVFXPrefab;

    // Movement/animation state
    private bool hasStoredSpeed = false;
    private float originalSpeed = 0f;
    private MonoBehaviour speedOwner = null;
    private string speedFieldName = null;

    private Rigidbody2D enemyRb;
    private Vector2 storedVelocity;

    private Animator animator;
    private float originalAnimatorSpeed = 1f;
    private StatusController statusController;

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
            remainingDuration -= Time.deltaTime;
            reapplyTimer += Time.deltaTime;

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
        isInStaticPeriod = true;

        // Freeze movement and animations
        StoreAndFreezeMovement();
        StoreAndFreezeAnimator();
        NotifyInterruptHandlersStart();

        yield return new WaitForSeconds(staticPeriod);

        // Unfreeze
        RestoreMovement();
        RestoreAnimator();
        NotifyInterruptHandlersEnd();

        isInStaticPeriod = false;

        // When static period ends, we do not destroy the status yet; the
        // StaticDurationRoutine will handle total lifetime. This allows
        // subsequent hits to reapply static within the same duration window.
    }

    private void StoreAndFreezeMovement()
    {
        if (!hasStoredSpeed)
        {
            // Find a movement speed field on any attached MonoBehaviour
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            foreach (var b in behaviours)
            {
                if (b == null) continue;
                var type = b.GetType();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

                var moveField = type.GetField("moveSpeed", flags) ?? type.GetField("walkSpeed", flags);
                if (moveField != null && moveField.FieldType == typeof(float))
                {
                    originalSpeed = (float)moveField.GetValue(b);
                    speedOwner = b;
                    speedFieldName = moveField.Name;
                    hasStoredSpeed = true;
                    Debug.Log($"<color=yellow>⚡ StaticStatus: Stored {type.Name}.{speedFieldName} = {originalSpeed:F2}</color>");
                    break;
                }
            }
        }

        // Set movement speed to zero
        if (hasStoredSpeed && speedOwner != null && !string.IsNullOrEmpty(speedFieldName))
        {
            var type = speedOwner.GetType();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var moveField = type.GetField(speedFieldName, flags);
            if (moveField != null && moveField.FieldType == typeof(float))
            {
                moveField.SetValue(speedOwner, 0f);
                Debug.Log($"<color=yellow>⚡ StaticStatus: Set {type.Name}.{speedFieldName} to 0</color>");
            }
        }

        // Also zero out current velocity so momentum stops immediately
        if (enemyRb != null)
        {
            storedVelocity = enemyRb.velocity;
            enemyRb.velocity = Vector2.zero;
        }
    }

    private void RestoreMovement()
    {
        if (hasStoredSpeed && speedOwner != null && !string.IsNullOrEmpty(speedFieldName))
        {
            var type = speedOwner.GetType();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var moveField = type.GetField(speedFieldName, flags);
            if (moveField != null && moveField.FieldType == typeof(float))
            {
                moveField.SetValue(speedOwner, originalSpeed);
                Debug.Log($"<color=yellow>⚡ StaticStatus: Restored {type.Name}.{speedFieldName} to {originalSpeed:F2}</color>");
            }
        }

        if (enemyRb != null)
        {
            // Do not restore storedVelocity directly; enemy AI will set its own
            // velocity from moveSpeed again on next FixedUpdate.
        }
    }

    private void StoreAndFreezeAnimator()
    {
        if (animator == null)
        {
            return;
        }

        // Avoid freezing death animations: if common death booleans are set,
        // we simply skip animator freezing.
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

        if (isDying)
        {
            return;
        }

        originalAnimatorSpeed = animator.speed;
        animator.speed = 0f;
    }

    private void RestoreAnimator()
    {
        if (animator != null)
        {
            animator.speed = originalAnimatorSpeed;
        }
    }

    private void NotifyInterruptHandlersStart()
    {
        var handlers = GetComponents<IStaticInterruptHandler>();
        foreach (var h in handlers)
        {
            if (h != null)
            {
                h.OnStaticStart(staticPeriod);
            }
        }
    }

    private void NotifyInterruptHandlersEnd()
    {
        var handlers = GetComponents<IStaticInterruptHandler>();
        foreach (var h in handlers)
        {
            if (h != null)
            {
                h.OnStaticEnd();
            }
        }
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
            NotifyInterruptHandlersEnd();
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
