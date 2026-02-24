using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ClawProjectile : MonoBehaviour, IInstantModifiable
{
    [Header("Damage Settings")]
    [SerializeField] private float damage = 20f;
    [SerializeField] private float manaCost = 10f;
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private ProjectileType projectileType = ProjectileType.Fire;

    [Header("Timing")]
    [Tooltip("Delay before the claw actually deals damage to its target.")]
    public float DamageDelay = 0.5f;
    [SerializeField] private float lifetimeSeconds = 1.0f;

    [Header("Hit Effect Offsets - Left Side")]
    [Tooltip("Offset around the enemy's collider center when the claw strikes from the LEFT at angle ABOVE 45 degrees.")]
    [SerializeField] private Vector2 hitEffectOffsetLeftAbove45 = Vector2.zero;
    [Tooltip("Offset around the enemy's collider center when the claw strikes from the LEFT at angle BELOW 45 degrees.")]
    [SerializeField] private Vector2 hitEffectOffsetLeftBelow45 = Vector2.zero;

    [Header("Hit Effect Offsets - Right Side")]
    [Tooltip("Offset around the enemy's collider center when the claw strikes from the RIGHT at angle ABOVE 45 degrees.")]
    [SerializeField] private Vector2 hitEffectOffsetRightAbove45 = Vector2.zero;
    [Tooltip("Offset around the enemy's collider center when the claw strikes from the RIGHT at angle BELOW 45 degrees.")]
    [SerializeField] private Vector2 hitEffectOffsetRightBelow45 = Vector2.zero;

    [Header("Collider Scaling")]
    [SerializeField] private float colliderSizeOffset = 0f;

    [Header("Damage Cooldown")]
    [Tooltip("Minimum time between damage instances (safety for future multi-hit variants).")]
    [SerializeField] private float damageCooldown = 0.1f;

    private Rigidbody2D _rigidbody2D;
    private Collider2D _collider2D;

    private float baseDamage;
    private float baseLifetime;
    private Vector3 baseScale;

    private PlayerStats cachedPlayerStats;
    private float baseDamageAfterCards;
    private float lastDamageTime = -999f;

    private static System.Collections.Generic.Dictionary<string, float> lastFireTimes = new System.Collections.Generic.Dictionary<string, float>();
    private string prefabKey;

    public ProjectileType ProjectileElement
    {
        get { return projectileType; }
    }

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _collider2D = GetComponent<Collider2D>();

        baseDamage = damage;
        baseLifetime = lifetimeSeconds;
        baseScale = transform.localScale;

        if (_rigidbody2D != null)
        {
            _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rigidbody2D.gravityScale = 0f;
            _rigidbody2D.velocity = Vector2.zero;
        }

        if (_collider2D != null)
        {
            _collider2D.isTrigger = true;
        }
    }

    public void Launch(Vector2 direction, Transform target, Collider2D colliderToIgnore, PlayerMana playerMana = null)
    {
        if (_rigidbody2D == null || _collider2D == null)
        {
            Destroy(gameObject);
            return;
        }

        // 50/50: spawn with x scale flipped (mirrored) or not.
        // This is visual-only and does NOT affect damage.
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (Random.value < 0.5f ? -1f : 1f);
        transform.localScale = s;

        // Resolve card + modifiers for this projectile instance.
        ProjectileCards card = null;
        CardModifierStats modifiers = new CardModifierStats();
        if (ProjectileCardModifiers.Instance != null)
        {
            card = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
            if (card != null)
            {
                modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
            }
        }

        float finalLifetime = baseLifetime + modifiers.lifetimeIncrease;
        float finalCooldown = Mathf.Max(0.1f, cooldown * (1f - modifiers.cooldownReductionPercent / 100f));
        int finalManaCost = Mathf.Max(1, Mathf.CeilToInt(manaCost * (1f - modifiers.manaCostReduction)));
        float finalDamage = (baseDamage + modifiers.damageFlat) * modifiers.damageMultiplier;

        // Cache PlayerStats so we can run the shared projectile damage pipeline per hit.
        cachedPlayerStats = null;
        if (colliderToIgnore != null)
        {
            cachedPlayerStats = colliderToIgnore.GetComponent<PlayerStats>();
        }
        if (cachedPlayerStats == null)
        {
            cachedPlayerStats = FindObjectOfType<PlayerStats>();
        }

        baseDamageAfterCards = finalDamage;
        damage = finalDamage;

        // ACTIVE projectile cards rely on AdvancedPlayerController for attack-speed
        // cooldowns; only non-active contexts use an internal prefab cooldown gate.
        bool useInternalCooldown = (card == null || card.projectileSystem != ProjectileCards.ProjectileSystemType.Active);

        prefabKey = "ClawProjectile";

        if (useInternalCooldown)
        {
            if (lastFireTimes.ContainsKey(prefabKey))
            {
                if (GameStateManager.PauseSafeTime - lastFireTimes[prefabKey] < finalCooldown)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            lastFireTimes[prefabKey] = GameStateManager.PauseSafeTime;
        }

        if (_collider2D != null && colliderToIgnore != null)
        {
            Physics2D.IgnoreCollision(_collider2D, colliderToIgnore, true);
        }

        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>() ?? target.GetComponentInParent<EnemyHealth>();
        if (enemyHealth == null || !enemyHealth.IsAlive)
        {
            Destroy(gameObject);
            return;
        }

        Collider2D enemyCollider =
            enemyHealth.GetComponent<Collider2D>() ??
            enemyHealth.GetComponentInChildren<Collider2D>() ??
            target.GetComponent<Collider2D>() ??
            target.GetComponentInChildren<Collider2D>();

        if (enemyCollider == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 enemyCenter = enemyCollider.bounds.center;

        // Respect offscreen rules when selecting/committing a target.
        if (!OffscreenDamageChecker.CanTakeDamage(enemyCenter))
        {
            Destroy(gameObject);
            return;
        }

        // Reposition the claw directly onto the enemy's collider center (plus directional offset)
        // and parent it so any animation stays anchored.
        Vector2 directionalOffset = GetHitEffectDirectionalOffset(direction);
        Vector3 spawnPos = enemyCenter + (Vector3)directionalOffset;
        transform.position = spawnPos;
        transform.SetParent(enemyHealth.transform, true);

        // Apply size modifier + collider scaling from card.
        if (modifiers.sizeMultiplier != 1f)
        {
            // Preserve random x-sign, but scale magnitude by sizeMultiplier.
            Vector3 ls = transform.localScale;
            float xSign = Mathf.Sign(ls.x);

            float xMag = Mathf.Abs(baseScale.x) * modifiers.sizeMultiplier;
            float y = baseScale.y * modifiers.sizeMultiplier;
            float z = baseScale.z * modifiers.sizeMultiplier;
            transform.localScale = new Vector3(xMag * xSign, y, z);

            ColliderScaler.ScaleCollider(_collider2D, modifiers.sizeMultiplier, colliderSizeOffset);
        }

        PauseSafeSelfDestruct.Schedule(gameObject, finalLifetime);

        StartCoroutine(DelayedStrike(enemyHealth, enemyCollider));
    }

    private IEnumerator DelayedStrike(EnemyHealth enemyHealth, Collider2D enemyCollider)
    {
        float delay = Mathf.Max(0f, DamageDelay);
        if (delay > 0f)
        {
            yield return GameStateManager.WaitForPauseSafeSeconds(delay);
        }

        if (enemyHealth == null || !enemyHealth.IsAlive)
        {
            yield break;
        }

        // Damage cooldown safety (mainly for future multi-hit variants).
        if (GameStateManager.PauseSafeTime - lastDamageTime < damageCooldown)
        {
            yield break;
        }

        // Robust: find the IDamageable on the enemy at strike time.
        IDamageable damageable =
            enemyHealth.GetComponent<IDamageable>() ??
            enemyHealth.GetComponentInParent<IDamageable>();

        if (damageable == null || !damageable.IsAlive)
        {
            yield break;
        }

        Vector3 hitPoint = enemyCollider != null ? enemyCollider.bounds.center : enemyHealth.transform.position;

        // IMPORTANT: don't re-block by offscreen at strike-time; we already committed to this target
        // at Launch() time and the claw is explicitly a delayed strike effect anchored to the enemy.
        // If you WANT offscreen to cancel, re-add OffscreenDamageChecker here.

        float baseForHit = baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
        float finalDamage = baseForHit;

        if (cachedPlayerStats != null)
        {
            finalDamage = PlayerDamageHelper.ComputeProjectileDamage(cachedPlayerStats, enemyHealth.gameObject, baseForHit, gameObject);
        }

        if (finalDamage <= 0f)
        {
            yield break;
        }

        // Tag EnemyHealth so EnemyHealth.TakeDamage renders this hit using the correct damage color.
        enemyHealth.SetLastIncomingDamageType(
            projectileType == ProjectileType.Ice
                ? DamageNumberManager.DamageType.Ice
                : DamageNumberManager.DamageType.Fire);

        Vector3 hitNormal = Vector3.zero;
        damageable.TakeDamage(finalDamage, hitPoint, hitNormal);
        lastDamageTime = GameStateManager.PauseSafeTime;

        // IMPORTANT FIX FOR "Burn text appears at player":
        // Many status UI popups anchor to the hitPoint / enemy collider center.
        // Ensure we use the ENEMY position as the anchor when applying effects.
        Vector3 statusAnchor = hitPoint;

        StatusController.TryApplyBurnFromProjectile(gameObject, enemyHealth.gameObject, statusAnchor, finalDamage);

        SlowEffect slowEffect = GetComponent<SlowEffect>();
        if (slowEffect != null)
        {
            slowEffect.TryApplySlow(enemyHealth.gameObject, statusAnchor);
        }

        StaticEffect staticEffect = GetComponent<StaticEffect>();
        if (staticEffect != null)
        {
            staticEffect.TryApplyStatic(enemyHealth.gameObject, statusAnchor);
        }
    }

    public void ApplyInstantModifiers(CardModifierStats mods)
    {
        float newLifetime = baseLifetime + mods.lifetimeIncrease;
        if (!Mathf.Approximately(newLifetime, lifetimeSeconds))
        {
            lifetimeSeconds = newLifetime;
        }

        float newDamage = (baseDamage + mods.damageFlat) * mods.damageMultiplier;
        if (!Mathf.Approximately(newDamage, damage))
        {
            damage = newDamage;
            baseDamageAfterCards = newDamage;
        }

        if (!Mathf.Approximately(mods.sizeMultiplier, 1f))
        {
            // Preserve current x sign (may already be randomized).
            Vector3 ls = transform.localScale;
            float xSign = Mathf.Sign(ls.x);
            transform.localScale = new Vector3(
                Mathf.Abs(baseScale.x) * mods.sizeMultiplier * xSign,
                baseScale.y * mods.sizeMultiplier,
                baseScale.z * mods.sizeMultiplier
            );
        }
    }

    public float GetCurrentDamage()
    {
        return damage;
    }

    private Vector2 GetHitEffectDirectionalOffset(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0f)
        {
            return Vector2.zero;
        }

        direction = direction.normalized;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (angle < 0f)
        {
            angle += 360f;
        }

        if (direction.x > 0f)
        {
            // Right side (0-90 or 270-360)
            if (angle >= 0f && angle <= 90f)
            {
                return angle > 45f ? hitEffectOffsetRightAbove45 : hitEffectOffsetRightBelow45;
            }

            if (angle >= 270f && angle <= 360f)
            {
                float relativeAngle = 360f - angle;
                return relativeAngle > 45f ? hitEffectOffsetRightAbove45 : hitEffectOffsetRightBelow45;
            }
        }
        else
        {
            // Left side (90-270)
            if (angle >= 90f && angle <= 180f)
            {
                float relativeAngle = 180f - angle;
                return relativeAngle > 45f ? hitEffectOffsetLeftAbove45 : hitEffectOffsetLeftBelow45;
            }

            if (angle >= 180f && angle <= 270f)
            {
                float relativeAngle = angle - 180f;
                return relativeAngle > 45f ? hitEffectOffsetLeftAbove45 : hitEffectOffsetLeftBelow45;
            }
        }

        return Vector2.zero;
    }
}