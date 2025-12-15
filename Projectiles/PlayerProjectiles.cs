using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerProjectiles : MonoBehaviour
{
    [Header("Spawn Point")]
    [Tooltip("Custom spawn point for this projectile (overrides FirePoint from AdvancedPlayerController)")]
    [SerializeField] private Transform customSpawnPoint;
    
    [Header("Motion")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float lifetimeSeconds = 5f;
    
    [Header("Spawn Offset - Left Side")]
    [Tooltip("Spawn offset when firing left at angle ABOVE 45 degrees")]
    [SerializeField] private Vector2 offsetLeftAbove45 = Vector2.zero;
    [Tooltip("Spawn offset when firing left at angle BELOW 45 degrees")]
    [SerializeField] private Vector2 offsetLeftBelow45 = Vector2.zero;
    
    [Header("Spawn Offset - Right Side")]
    [Tooltip("Spawn offset when firing right at angle ABOVE 45 degrees")]
    [SerializeField] private Vector2 offsetRightAbove45 = Vector2.zero;
    [Tooltip("Spawn offset when firing right at angle BELOW 45 degrees")]
    [SerializeField] private Vector2 offsetRightBelow45 = Vector2.zero;

    [Header("Damage Settings")]
    [SerializeField] private float damage = 20f;
    [SerializeField] private float manaCost = 10f;
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private ProjectileType projectileType = ProjectileType.Fire;
    public ProjectileType ProjectileElement => projectileType;

    // Instance-based cooldown tracking (per prefab type)
    private static System.Collections.Generic.Dictionary<string, float> lastFireTimes = new System.Collections.Generic.Dictionary<string, float>();
    private string prefabKey;
    public enum SpriteFacing2D { Right = 0, Up = 90, Left = 180, Down = 270 }
    [Header("Rotation")]
    [SerializeField] private SpriteFacing2D spriteFacing = SpriteFacing2D.Right;
    [SerializeField] private float additionalRotationOffsetDeg = 0f;
    [SerializeField] private bool rotateToVelocity = true;
    [SerializeField] private float maxRotationDegreesPerSecond = 1080f;
    [SerializeField] private float minRotateVelocity = 0.01f;
    [SerializeField] private bool keepInitialRotation = false;

    [Header("Impact VFX")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float hitEffectDuration = 1.0f;
    [SerializeField] private Vector2 hitEffectOffset = Vector2.zero;
    [SerializeField] private Vector2 hitEffectOffsetLeftAbove45 = Vector2.zero;
    [SerializeField] private Vector2 hitEffectOffsetLeftBelow45 = Vector2.zero;
    [SerializeField] private Vector2 hitEffectOffsetRightAbove45 = Vector2.zero;
    [SerializeField] private Vector2 hitEffectOffsetRightBelow45 = Vector2.zero;
    [SerializeField] private float hitEffectSizeMultiplier = 1f;
    [SerializeField] private float hitEffectTimingAdjustment = 0f;
    [SerializeField] private SpriteFacing2D hitEffectSpriteFacing = SpriteFacing2D.Right;
    [SerializeField] private float hitEffectAdditionalRotationOffsetDeg = 0f;
    [SerializeField] private bool hitEffectRotateToVelocity = true;
    [SerializeField] private bool hitEffectKeepInitialRotation = false;

    [Header("Impact Orientation")]
    [SerializeField] private ImpactOrientationMode impactOrientation = ImpactOrientationMode.SurfaceNormal;
    [SerializeField] private float impactZOffset = 0f;
    [SerializeField] private bool parentImpactToHit = false;

    [Header("Audio - Impact")]
    [SerializeField] private AudioClip impactClip;
    [Range(0f, 1f)][SerializeField] private float impactVolume = 1f;

    [Header("Audio - Trail")]
    [SerializeField] public AudioClip trailClip;
    [Range(0f, 1f)][SerializeField] public float trailVolume = 0.85f;
    [SerializeField] public float trailPitch = 1.0f;
    [SerializeField] public bool trailLoop = true;
    [Tooltip("1 = fully 3D, 0 = 2D UI-like.")]
    [Range(0f, 1f)][SerializeField] public float trailSpatialBlend = 1f;
    [Tooltip("Reduce for arcade feel; 0 turns off Doppler effect.")]
    [SerializeField] public float trailDopplerLevel = 0f;
    [Tooltip("Fade-out time when the projectile ends.")]
    [SerializeField] public float trailFadeOutSeconds = 0.12f;

    [Header("Collider Scaling")]
    [Tooltip("Offset for collider size relative to visual size (0 = same as visual, -0.2 = 20% smaller, 0.2 = 20% larger)")]
    [SerializeField] private float colliderSizeOffset = 0f;

    [Header("Damage Cooldown")]
    [Tooltip("Minimum time between damage instances to prevent AOE spam")]
    [SerializeField] private float damageCooldown = 0.1f;

    private Rigidbody2D _rigidbody2D;
    private Collider2D _collider2D;
    private AudioSource _trailSource;
    private Coroutine _fadeOutRoutine;
    private float lastDamageTime = -999f;
    private bool hasHitEnemy = false; // Track if we've already hit an enemy (for non-pierce projectiles)
    private bool hasLaunched = false; // Track if Launch() has been called
    private GameObject player;
    private FavourEffectManager favourEffectManager;
    private PlayerStats cachedPlayerStats;
    private float baseDamageAfterCards;

    public enum ImpactOrientationMode
    {
        SurfaceNormal,
        Opposite,
        ProjectileVelocity,
        None
    }

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _collider2D = GetComponent<Collider2D>();
        
        // Configure rigidbody for smooth piercing
        if (_rigidbody2D != null)
        {
            _rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            // Don't freeze rotation for PlayerProjectiles as they need to rotate to velocity
        }
        
        // Ensure no physics material that could cause bouncing
        if (_collider2D != null && _collider2D.sharedMaterial != null)
        {
            if (_collider2D.sharedMaterial.bounciness > 0f)
            {
                Debug.LogWarning($"<color=yellow>Projectile {gameObject.name} has bouncy physics material! This will interfere with piercing.</color>");
            }
        }
        
        EnsureTrailAudioSource();
    }

    private void OnEnable()
    {
        // _launched = false;
        if (_fadeOutRoutine != null)
        {
            StopCoroutine(_fadeOutRoutine);
            _fadeOutRoutine = null;
        }
        StopTrailSfx(true);
    }

    private void OnDisable()
    {
        StopTrailSfx(true);
    }

    private void Update()
    {
        if (keepInitialRotation || _rigidbody2D == null) return;
        if (!rotateToVelocity) return;

        Vector2 v = _rigidbody2D.velocity;
        if (v.sqrMagnitude < (minRotateVelocity * minRotateVelocity)) return;

        float targetAngle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        float facingCorrection = (int)spriteFacing;
        float desired = targetAngle + facingCorrection + additionalRotationOffsetDeg;

        float current = transform.eulerAngles.z;
        float step = maxRotationDegreesPerSecond * Time.deltaTime;
        float newAngle = Mathf.MoveTowardsAngle(current, desired, step);
        transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
    }

    /// <summary>
    /// Get the custom spawn point for this projectile (overrides FirePoint if set)
    /// </summary>
    public Transform GetCustomSpawnPoint()
    {
        return customSpawnPoint;
    }
    
    /// <summary>
    /// Get the spawn offset for this projectile type based on direction
    /// IMPORTANT: This should ONLY be called BEFORE instantiating the projectile!
    /// </summary>
    public Vector2 GetSpawnOffset(Vector2 direction)
    {
        if (hasLaunched)
        {
            Debug.LogError("<color=red>GetSpawnOffset() called AFTER Launch()! This causes mid-flight repositioning! Offset should be calculated BEFORE Instantiate()!</color>");
            return Vector2.zero;
        }
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
        // Normalize angle to 0-360
        if (angle < 0) angle += 360f;
        
        // Right side (0-90 or 270-360)
        if ((angle >= 0f && angle <= 90f) || (angle >= 270f && angle <= 360f))
        {
            if (angle >= 0f && angle <= 90f)
            {
                float relativeAngle = angle;
                return relativeAngle > 45f ? offsetRightAbove45 : offsetRightBelow45;
            }
            else
            {
                float relativeAngle = 360f - angle;
                return relativeAngle > 45f ? offsetRightAbove45 : offsetRightBelow45;
            }
        }
        // Left side (90-270)
        else
        {
            if (angle >= 90f && angle <= 180f)
            {
                float relativeAngle = 180f - angle;
                return relativeAngle > 45f ? offsetLeftAbove45 : offsetLeftBelow45;
            }
            else if (angle >= 180f && angle <= 270f)
            {
                float relativeAngle = angle - 180f;
                return relativeAngle > 45f ? offsetLeftAbove45 : offsetLeftBelow45;
            }
        }
        
        return Vector2.zero;
    }

    public void Launch(Vector2 direction, Collider2D colliderToIgnore, PlayerMana playerMana = null, bool skipCooldownCheck = false)
    {
        hasLaunched = true; // Mark as launched to prevent offset adjustments
        
        if (_rigidbody2D == null)
        {
            Debug.LogWarning("FireBolt missing Rigidbody2D.");
            Destroy(gameObject);
            return;
        }

        // Get card-specific modifiers
        ProjectileCards card = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
        CardModifierStats modifiers = new CardModifierStats(); // Default values
        
        if (card != null)
        {
            modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
            Debug.Log($"<color=cyan>PlayerProjectiles using modifiers from {card.cardName}</color>");
        }

        // Apply card modifiers (NO PlayerStats here – we defer stats+favours per hit
        // via PlayerDamageHelper so distance-based favours can use the actual
        // enemy position on impact).
        float finalSpeed = speed + modifiers.speedIncrease; // RAW value
        float finalLifetime = lifetimeSeconds + modifiers.lifetimeIncrease; // RAW seconds
        float finalCooldown = Mathf.Max(0.1f, cooldown * (1f - modifiers.cooldownReductionPercent / 100f)); // % from base
        int finalManaCost = Mathf.Max(0, Mathf.CeilToInt(manaCost * (1f - modifiers.manaCostReduction)));
        float finalDamage = damage + modifiers.damageFlat; // FLAT damage bonus per hit
        
        // Apply size multiplier
        if (modifiers.sizeMultiplier != 1f)
        {
            transform.localScale *= modifiers.sizeMultiplier;
            
            // Scale collider using utility with colliderSizeOffset
            ColliderScaler.ScaleCollider(_collider2D, modifiers.sizeMultiplier, colliderSizeOffset);
        }
        
        Debug.Log($"<color=cyan>PlayerProjectile Modifiers Applied: Speed=+{modifiers.speedIncrease:F2}, Size={modifiers.sizeMultiplier:F2}x, DamageFlat=+{modifiers.damageFlat:F1}, Lifetime=+{modifiers.lifetimeIncrease:F2}s</color>");

        // Cache player + stats for the unified damage pipeline.
        cachedPlayerStats = null;
        if (colliderToIgnore != null)
        {
            player = colliderToIgnore.gameObject;
            if (player != null)
            {
                favourEffectManager = player.GetComponent<FavourEffectManager>();
            }
            cachedPlayerStats = colliderToIgnore.GetComponent<PlayerStats>();
        }

        // Store damage after all card modifiers; PlayerDamageHelper will apply
        // PlayerStats and favour effects per enemy hit.
        damage = finalDamage;
        baseDamageAfterCards = damage;

        // Generate key based ONLY on projectile type (so all FireBolts/IceBlasts share same cooldown)
        // CRITICAL: Don't include mana/damage in key - those change with modifiers!
        prefabKey = $"PlayerProjectile_{projectileType}";
        
        // Determine whether to use internal prefab cooldown (only when no card is attached)
        bool useInternalCooldown = (card == null);

        // Only check cooldown/mana for first projectile in multi-spawn
        if (!skipCooldownCheck)
        {
            // When no card drives this projectile (no ProjectileCards tag), fall back to prefab cooldown
            if (useInternalCooldown)
            {
                // Check cooldown for this specific projectile type
                if (lastFireTimes.ContainsKey(prefabKey))
                {
                    if (Time.time - lastFireTimes[prefabKey] < finalCooldown)
                    {
                        Debug.Log($"FireBolt ({prefabKey}) on cooldown");
                        Destroy(gameObject);
                        return;
                    }
                }
            }

            // Check mana with modified cost
            if (playerMana != null && !playerMana.Spend(finalManaCost))
            {
                Debug.Log($"Not enough mana for FireBolt (cost: {finalManaCost})");
                Destroy(gameObject);
                return;
            }

            // Record fire time only when using internal prefab cooldown
            if (useInternalCooldown)
            {
                lastFireTimes[prefabKey] = Time.time;
            }
        }
        else
        {
            Debug.Log($"<color=gold>PlayerProjectiles: Skipping cooldown/mana check (multi-projectile spawn)</color>");
        }

        if (_collider2D != null && colliderToIgnore != null)
        {
            Physics2D.IgnoreCollision(_collider2D, colliderToIgnore, true);
        }

        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        _rigidbody2D.velocity = dir * finalSpeed;

        if (!keepInitialRotation)
        {
            float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float facingCorrection = (int)spriteFacing;
            float finalAngle = baseAngle + facingCorrection + additionalRotationOffsetDeg;
            transform.rotation = Quaternion.Euler(0f, 0f, finalAngle);
        }

        Destroy(gameObject, finalLifetime);
        StartTrailSfx();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Trigger-based collision for piercing projectiles (no bouncing)
        ProjectilePiercing piercing = GetComponent<ProjectilePiercing>();
        bool allowPierce = piercing != null && piercing.pierceCount > 0;
        
        if (((1 << other.gameObject.layer) & enemyLayer) != 0)
        {
            // For NON-piercing projectiles (no ProjectilePiercing or pierceCount <= 0),
            // ensure we only ever damage a single enemy. This prevents a single
            // projectile from "AOE"-ing multiple tightly packed enemies just because
            // multiple trigger events fire in the same frame.
            if (!allowPierce && hasHitEnemy)
            {
                return;
            }

            // If has piercing and already hit this enemy, ignore
            if (allowPierce && piercing.HasHitEnemy(other.gameObject))
            {
                return;
            }
            
            IDamageable damageable = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
            
            // Only damage if target is alive AND on-screen
            if (damageable != null && damageable.IsAlive)
            {
                // Check if enemy is within damageable area (on-screen or slightly offscreen)
                if (!OffscreenDamageChecker.CanTakeDamage(other.transform.position))
                {
                    Debug.Log($"<color=yellow>PlayerProjectile: Enemy {other.gameObject.name} too far offscreen, no damage dealt</color>");
                    return;
                }
                
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Vector3 hitNormal = (transform.position - other.transform.position).normalized;
                Vector3 effectBasePosition = hitPoint;
                Collider2D enemyCollider = other;
                if (enemyCollider != null)
                {
                    effectBasePosition = enemyCollider.bounds.center;
                }

                float baseForHit = baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
                float finalDamage = baseForHit;

                if (cachedPlayerStats != null)
                {
                    Component damageableComponent = damageable as Component;
                    GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : other.gameObject;
                    finalDamage = PlayerDamageHelper.ComputeProjectileDamage(cachedPlayerStats, enemyObject, baseForHit, gameObject);
                }

                // Tag EnemyHealth with the elemental damage type so it can
                // display the correct-colored damage number (including 0 when
                // armor/defense fully negate the hit).
                EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>() ?? other.GetComponentInParent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    DamageNumberManager.DamageType damageType = projectileType == ProjectileType.Fire
                        ? DamageNumberManager.DamageType.Fire
                        : DamageNumberManager.DamageType.Ice;
                    enemyHealth.SetLastIncomingDamageType(damageType);
                }

                // Let EnemyHealth.ApplyDamage handle the final health change
                // and numeric damage-number display based on the fully
                // mitigated value.
                damageable.TakeDamage(finalDamage, hitPoint, hitNormal);

                BurnEffect burnEffect = GetComponent<BurnEffect>();
                if (burnEffect != null)
                {
                    burnEffect.Initialize(finalDamage, projectileType);
                    burnEffect.TryApplyBurn(other.gameObject, hitPoint);
                }

                SlowEffect slowEffect = GetComponent<SlowEffect>();
                if (slowEffect != null)
                {
                    slowEffect.TryApplySlow(other.gameObject, hitPoint);
                }

                StaticEffect staticEffect = GetComponent<StaticEffect>();
                if (staticEffect != null)
                {
                    staticEffect.TryApplyStatic(other.gameObject, hitPoint);
                }
                
                TryPlayHitEffect(effectBasePosition);
                
                // Handle piercing
                if (allowPierce)
                {
                    bool shouldContinue = piercing.OnEnemyHit(other.gameObject);
                    if (shouldContinue)
                    {
                        // Continue through enemy - don't destroy
                        Debug.Log($"<color=green>Projectile piercing through enemy. Remaining: {piercing.GetRemainingPierces()}</color>");
                        return;
                    }
                }
                
                // No piercing or max pierces reached - mark as having hit and destroy projectile
                hasHitEnemy = true;
                HandleImpact(hitPoint, hitNormal, other.transform);
                Destroy(gameObject);
                return;
            }
        }
    }
    
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Collision-based handler for non-piercing projectiles
        ProjectilePiercing piercing = GetComponent<ProjectilePiercing>();

        // If has piercing component, use trigger-based collision instead
        if (piercing != null)
        {
            // Ignore this collision, let OnTriggerEnter2D handle it
            return;
        }

        // Enemy hit
        if (((1 << collision.gameObject.layer) & enemyLayer) != 0)
        {
            // If we've already hit an enemy (no pierce), ignore subsequent collisions
            if (hasHitEnemy)
            {
                Debug.Log($"<color=yellow>Projectile already hit an enemy, ignoring {collision.gameObject.name}</color>");
                return;
            }

            IDamageable damageable = collision.gameObject.GetComponent<IDamageable>() ??
                                     collision.gameObject.GetComponentInParent<IDamageable>();

            // Check rigidbody state BEFORE collision
            Rigidbody2D enemyRb = collision.gameObject.GetComponent<Rigidbody2D>();
            if (enemyRb != null && enemyRb.bodyType != RigidbodyType2D.Kinematic)
            {
                Debug.LogWarning($"<color=red>⚠️ [KNOCKBACK] {collision.gameObject.name} has BodyType: {enemyRb.bodyType} (should be Kinematic!)</color>");
            }

            if (damageable != null && damageable.IsAlive)
            {
                // Get hit point and normal from collision
                Vector3 hitPoint = collision.GetContact(0).point;
                Vector3 hitNormal = collision.GetContact(0).normal;
                Vector3 effectBasePosition = hitPoint;
                Collider2D enemyCollider = collision.collider;
                if (enemyCollider != null)
                {
                    effectBasePosition = enemyCollider.bounds.center;
                }

                float baseForHit = baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
                float finalDamage = baseForHit;

                if (cachedPlayerStats != null)
                {
                    Component damageableComponent = damageable as Component;
                    GameObject enemyObject = damageableComponent != null
                        ? damageableComponent.gameObject
                        : collision.gameObject;
                    finalDamage = PlayerDamageHelper.ComputeProjectileDamage(cachedPlayerStats, enemyObject, baseForHit, gameObject);
                }

                // Tag EnemyHealth with the elemental damage type so it can
                // show the final damage (or 0) with the correct color.
                EnemyHealth enemyHealth = collision.gameObject.GetComponent<EnemyHealth>() ?? collision.gameObject.GetComponentInParent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    DamageNumberManager.DamageType damageType = projectileType == ProjectileType.Fire
                        ? DamageNumberManager.DamageType.Fire
                        : DamageNumberManager.DamageType.Ice;
                    enemyHealth.SetLastIncomingDamageType(damageType);
                }

                damageable.TakeDamage(finalDamage, hitPoint, hitNormal);
                lastDamageTime = Time.time;
                hasHitEnemy = true;

                BurnEffect burnEffect = GetComponent<BurnEffect>();
                if (burnEffect != null)
                {
                    burnEffect.Initialize(finalDamage, projectileType);
                    burnEffect.TryApplyBurn(collision.gameObject, hitPoint);
                }

                SlowEffect slowEffect = GetComponent<SlowEffect>();
                if (slowEffect != null)
                {
                    slowEffect.TryApplySlow(collision.gameObject, hitPoint);
                }

                StaticEffect staticEffect = GetComponent<StaticEffect>();
                if (staticEffect != null)
                {
                    staticEffect.TryApplyStatic(collision.gameObject, hitPoint);
                }

                TryPlayHitEffect(hitPoint);

                // No piercing behaviour here; collision path is single-hit only
                HandleImpact(hitPoint, hitNormal, collision.collider.transform);
                Destroy(gameObject);
                return;
            }
            else if (damageable != null && !damageable.IsAlive)
            {
                // Target is already dead, just destroy unless some future pierce logic is added
                Debug.Log($"<color=yellow>FireBolt hit dead enemy {collision.gameObject.name}, destroying</color>");
                Destroy(gameObject);
                return;
            }
        }

        // Hit non-enemy object (wall, etc) - always destroy
        if (collision.contacts.Length > 0)
        {
            Vector3 hitPoint = collision.GetContact(0).point;
            Vector3 hitNormal = collision.GetContact(0).normal;
            TryPlayHitEffect(hitPoint);
            HandleImpact(hitPoint, hitNormal, collision.collider.transform);
        }
        Destroy(gameObject);
    }

    private void HandleImpact(Vector3 point, Vector3 normal, Transform hitParent)
    {
        if (impactClip != null)
        {
            AudioSource.PlayClipAtPoint(impactClip, point, impactVolume);
        }

        StopTrailSfx(false);
    }

    private Quaternion ComputeImpactRotation(Vector3 surfaceNormal)
    {
        switch (impactOrientation)
        {
            case ImpactOrientationMode.SurfaceNormal:
                return Quaternion.LookRotation(Vector3.forward, surfaceNormal);
            case ImpactOrientationMode.Opposite:
                return Quaternion.LookRotation(Vector3.forward, -surfaceNormal);
            case ImpactOrientationMode.ProjectileVelocity:
                Vector2 v = _rigidbody2D != null ? _rigidbody2D.velocity : Vector2.right;
                if (v.sqrMagnitude < 0.0001f) v = Vector2.right;
                return Quaternion.LookRotation(Vector3.forward, v.normalized);
            case ImpactOrientationMode.None:
            default:
                return Quaternion.identity;
        }
    }

    private Vector2 GetHitEffectDirectionalOffset()
    {
        if (_rigidbody2D == null) return Vector2.zero;

        Vector2 direction = _rigidbody2D.velocity;
        if (direction.sqrMagnitude < (minRotateVelocity * minRotateVelocity)) return Vector2.zero;
        direction = direction.normalized;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        if (direction.x > 0f)
        {
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

    private void TryPlayHitEffect(Vector3 basePosition)
    {
        if (hitEffectPrefab == null) return;

        Vector2 directionalOffset = GetHitEffectDirectionalOffset();
        Vector3 effectPosition = basePosition + (Vector3)hitEffectOffset + (Vector3)directionalOffset;

        if (hitEffectTimingAdjustment < 0f)
        {
            StartCoroutine(SpawnHitEffectDelayed(effectPosition, -hitEffectTimingAdjustment));
        }
        else
        {
            SpawnHitEffectImmediate(effectPosition);
        }
    }

    private void SpawnHitEffectImmediate(Vector3 position)
    {
        Quaternion rotation = transform.rotation;

        if (!hitEffectKeepInitialRotation)
        {
            bool appliedFromVelocity = false;
            if (hitEffectRotateToVelocity && _rigidbody2D != null)
            {
                Vector2 v = _rigidbody2D.velocity;
                if (v.sqrMagnitude >= (minRotateVelocity * minRotateVelocity))
                {
                    float targetAngle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                    float facingCorrection = (int)hitEffectSpriteFacing;
                    float desired = targetAngle + facingCorrection + hitEffectAdditionalRotationOffsetDeg;
                    rotation = Quaternion.Euler(0f, 0f, desired);
                    appliedFromVelocity = true;
                }
            }

            if (!appliedFromVelocity)
            {
                float baseAngle = transform.eulerAngles.z;
                float desired = baseAngle + (int)hitEffectSpriteFacing + hitEffectAdditionalRotationOffsetDeg;
                rotation = Quaternion.Euler(0f, 0f, desired);
            }
        }

        GameObject fx = Instantiate(hitEffectPrefab, position, rotation);

        if (hitEffectSizeMultiplier != 1f)
        {
            fx.transform.localScale *= hitEffectSizeMultiplier;
        }

        if (hitEffectDuration > 0f)
        {
            Destroy(fx, hitEffectDuration);
        }
    }

    private IEnumerator SpawnHitEffectDelayed(Vector3 position, float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnHitEffectImmediate(position);
    }

    private void EnsureTrailAudioSource()
    {
        if (_trailSource == null)
        {
            _trailSource = GetComponent<AudioSource>();
            if (_trailSource == null)
            {
                _trailSource = gameObject.AddComponent<AudioSource>();
            }
        }

        _trailSource.playOnAwake = false;
        _trailSource.loop = trailLoop;
        _trailSource.spatialBlend = trailSpatialBlend;
        _trailSource.dopplerLevel = trailDopplerLevel;
        _trailSource.rolloffMode = AudioRolloffMode.Linear;
        _trailSource.minDistance = 1f;
        _trailSource.maxDistance = 30f;
    }

    private void StartTrailSfx()
    {
        if (trailClip == null) return;

        EnsureTrailAudioSource();

        if (_fadeOutRoutine != null)
        {
            StopCoroutine(_fadeOutRoutine);
            _fadeOutRoutine = null;
        }

        _trailSource.clip = trailClip;
        _trailSource.volume = trailVolume;
        _trailSource.pitch = trailPitch;
        _trailSource.loop = trailLoop;

        if (!_trailSource.isPlaying)
        {
            _trailSource.Play();
        }
    }

    private void StopTrailSfx(bool instant)
    {
        if (_trailSource == null) return;

        if (instant || trailFadeOutSeconds <= 0f || !_trailSource.isPlaying)
        {
            _trailSource.Stop();
            _trailSource.clip = null;
            return;
        }

        if (_fadeOutRoutine != null)
        {
            StopCoroutine(_fadeOutRoutine);
        }
        _fadeOutRoutine = StartCoroutine(FadeOutAndStop(_trailSource, trailFadeOutSeconds));
    }

    private IEnumerator FadeOutAndStop(AudioSource source, float duration)
    {
        float startVolume = source.volume;
        float t = 0f;

        while (t < duration && source != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(1f - (t / duration));
            source.volume = startVolume * k;
            yield return null;
        }

        if (source != null)
        {
            source.Stop();
            source.clip = null;
            source.volume = startVolume;
        }

        _fadeOutRoutine = null;
    }

    /// <summary>
    /// Get the current damage value this projectile will deal on hit
    /// (after all modifiers and PlayerStats have been applied).
    /// </summary>
    public float GetCurrentDamage()
    {
        return damage;
    }
}
