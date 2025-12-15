using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ProjectileFireTalon : MonoBehaviour, IInstantModifiable
{
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
    [SerializeField] private int manaCost = 10;
    [SerializeField] private float cooldown = 0.5f;
    [Tooltip("Minimum cooldown after all reductions")]
    [SerializeField] private float minCooldown = 0.2f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private ProjectileType projectileType = ProjectileType.Fire; // FIRE TALON - Always Fire type

    // Instance-based cooldown tracking (per prefab type)
    private static System.Collections.Generic.Dictionary<string, float> lastFireTimes = new System.Collections.Generic.Dictionary<string, float>();
    private string prefabKey;
    private float baseSpeed; private float baseLifetime; private float baseDamage; private Vector3 baseScale;
    public enum SpriteFacing2D { Right = 0, Up = 90, Left = 180, Down = 270 }
    [Header("Rotation")]
    [SerializeField] private SpriteFacing2D spriteFacing = SpriteFacing2D.Right;
    [SerializeField] private float additionalRotationOffsetDeg = 0f;
    [SerializeField] private bool rotateToVelocity = true;
    [SerializeField] private float maxRotationDegreesPerSecond = 1080f;
    [SerializeField] private float minRotateVelocity = 0.01f;
    [SerializeField] private bool keepInitialRotation = false;

    [Header("Hit Effect")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float hitEffectDuration = 1f;
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

    private Rigidbody2D _rigidbody2D;
    private Collider2D _collider2D;
    private AudioSource _trailSource;
    private Coroutine _fadeOutRoutine;
    private PlayerStats cachedPlayerStats;
    private float baseDamageAfterCards;

    public enum ImpactOrientationMode
    {
        SurfaceNormal,
        Opposite,
        ProjectileVelocity,
        None
    }

    private Vector2 initialDirection;
    private bool directionSet = false;
    private float lastDamageTime = -999f;
    [Header("Damage Cooldown")]
    [Tooltip("Minimum time between damage instances")]
    [SerializeField] private float damageCooldown = 0.1f;
    
    [Header("Collider Scaling")]
    [Tooltip("Offset for collider size relative to visual size (0 = same as visual, -0.2 = 20% smaller, 0.2 = 20% larger)")]
    [SerializeField] private float colliderSizeOffset = 0f;
    
    [Header("Enhanced Variant 1 - Multi-Pierce")]
    [Tooltip("Additional projectile count for Enhanced Variant 1")]
    public int enhancedProjectileCountBonus = 1;
    [Tooltip("Additional pierce count for Enhanced Variant 1")]
    public int enhancedPierceBonus = 5;
    
    [Header("Enhanced Variant 2 - Speed & Pierce")]
    [Tooltip("Additional pierce count for Enhanced Variant 2")]
    public int enhancedVariant2PierceBonus = 0;
    [Tooltip("Speed bonus for Enhanced Variant 2 (raw value added, stacks with modifiers)")]
    [SerializeField] private float enhancedVariant2SpeedBonus = 0f;
    [Tooltip("Base cooldown used when Talon is in Enhanced Variant 2 (or higher). If 0, falls back to card spawn interval or projectile cooldown.")]
    [SerializeField] private float variant2BaseCooldown = 0f;
    
    [Header("Spawn Together Settings")]
    [Tooltip("Minimum angle separation between projectiles when spawn together is enabled (in degrees)")]
    public float minAngleSeparation = 0f;
    
    // Flag to track if this is an additional projectile (spawned with skipCooldownCheck = true)
    private bool isAdditionalProjectile = false;
    private bool hasPlayedBreakEffect = false;
    
    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        baseSpeed=speed; baseLifetime=lifetimeSeconds; baseDamage=damage; baseScale=transform.localScale;
        
        // Try to get collider from parent first, then check children
        _collider2D = GetComponent<Collider2D>();
        if (_collider2D == null)
        {
            // Collider might be on children (like Talon with split colliders)
            _collider2D = GetComponentInChildren<Collider2D>();
        }

        // ALWAYS use trigger collider for projectiles (prevents bouncing)
        // Set ALL colliders to trigger (parent and children)
        Collider2D[] allColliders = GetComponentsInChildren<Collider2D>();
        foreach (Collider2D col in allColliders)
        {
            if (!col.isTrigger)
            {
                col.isTrigger = true;
            }
        }

        // Configure rigidbody - always Dynamic for proper movement
        if (_rigidbody2D != null)
        {
            _rigidbody2D.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rigidbody2D.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rigidbody2D.gravityScale = 0f; // No gravity
        }
        
        // Ensure no physics material that could cause bouncing
        if (_collider2D != null && _collider2D.sharedMaterial != null)
        {
            // Physics material with bounciness will interfere with piercing
        }
        
        EnsureTrailAudioSource();
    }
    
    private void Start()
    {
        // Velocity is set in Launch() with modified speed
        // Lifetime destroy is also handled in Launch() with modified lifetime
    }
    
    public void SetDirection(Vector2 direction)
    {
        initialDirection = direction;
        directionSet = true;
        
        // Don't set velocity here - Launch() will set it with modified speed
        // Setting it here would use the unmodified speed value
    }

    private void OnEnable()
    {
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

        // Get velocity for rotation
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
    /// Get the spawn offset for this projectile type based on firing direction
    /// </summary>
    public Vector2 GetSpawnOffset(Vector2 direction)
    {
        direction = direction.normalized;
        
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;
        
        if (direction.x > 0)
        {
            if (angle >= 0f && angle <= 90f)
            {
                return angle > 45f ? offsetRightAbove45 : offsetRightBelow45;
            }
            else if (angle >= 270f && angle <= 360f)
            {
                float relativeAngle = 360f - angle;
                return relativeAngle > 45f ? offsetRightAbove45 : offsetRightBelow45;
            }
        }
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

    private Vector2 GetHitEffectDirectionalOffset()
    {
        if (_rigidbody2D == null) return Vector2.zero;

        Vector2 direction = _rigidbody2D.velocity;
        if (direction.sqrMagnitude < 0.0001f) return Vector2.zero;
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

    public void Launch(Vector2 direction, Collider2D colliderToIgnore, PlayerMana playerMana = null, bool skipCooldownCheck = false)
    {
        if (_rigidbody2D == null)
        {
            Debug.LogWarning("ProjectileTalon missing Rigidbody2D.");
            Destroy(gameObject);
            return;
        }

        // Get card-specific modifiers
        ProjectileCards card = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
        CardModifierStats modifiers = new CardModifierStats(); // Default values
        
        if (card != null)
        {
            modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
        }
        
        // Determine which enhanced variants have EVER been chosen for this card.
        // We want Variant 1 (multi-pierce) and Variant 2 (speed & extra pierce)
        // to STACK regardless of the order they were picked across tiers.
        int enhancedVariant = 0;
        bool hasVariant1 = false;
        bool hasVariant2 = false;
        if (ProjectileCardLevelSystem.Instance != null && card != null)
        {
            enhancedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card);

            // Use the per-card history so that any tier which granted Variant 1 or 2
            // keeps its effect permanently for this card.
            hasVariant1 = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 1);
            hasVariant2 = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 2);

            // Enhanced Variant 3 - guaranteed burn: set BurnEffect chance to 100%
            if (enhancedVariant == 3)
            {
                BurnEffect burnEffect = GetComponent<BurnEffect>();
                if (burnEffect != null)
                {
                    burnEffect.burnChance = 100f;
                }
            }
        }
        
        // Apply enhanced variant bonuses
        // CRITICAL: Don't modify modifiers struct - it's shared across all fires!
        // Instead, track enhanced bonuses separately and add them to final values.
        // NOTE: Projectile-count bonuses are applied in ProjectileSpawner so that the
        // very first enhanced spawn already uses the correct projectile count.
        int enhancedPierceAdd = 0;
        float enhancedSpeedAdd = 0f;

        if (hasVariant1)
        {
            // Variant 1: add its pierce bonus only. Projectile-count bonus
            // is handled centrally in ProjectileSpawner using
            // enhancedProjectileCountBonus so that the very first enhanced
            // spawn gets the correct extra projectile(s).
            enhancedPierceAdd += enhancedPierceBonus;
        }

        if (hasVariant2)
        {
            // Variant 2: add ONLY its own pierce and speed bonuses. This ensures
            // picking Variant 2 also stacks cleanly on top of Variant 1 when both
            // have been chosen across enhancement tiers.
            enhancedPierceAdd += enhancedVariant2PierceBonus;
            enhancedSpeedAdd += enhancedVariant2SpeedBonus;
        }

        // Apply card modifiers using new RAW value system
        float baseSpeedLocal = speed + modifiers.speedIncrease; // RAW value added
        float finalSpeed = baseSpeedLocal + enhancedSpeedAdd;
        float finalLifetime = lifetimeSeconds + modifiers.lifetimeIncrease; // RAW seconds added

        // CRITICAL: Use ProjectileCards spawnInterval if available, otherwise use script cooldown
        float baseCooldown = cooldown;
        if (card != null && card.runtimeSpawnInterval > 0f)
        {
            baseCooldown = card.runtimeSpawnInterval;
        }

        // If Variant 2 has EVER been chosen for this card and a per-variant
        // base cooldown is configured, treat variant2BaseCooldown as the
        // canonical base value before applying any other modifiers. This
        // ensures that when Variant 1 and 2 are stacked together, the unique
        // Variant 2 base cooldown still drives the timing even if the current
        // enhancedVariant is 1 or 0 in this frame.
        if (hasVariant2 && variant2BaseCooldown > 0f)
        {
            baseCooldown = variant2BaseCooldown;
        }

        // Sync card runtime interval with the resolved BASE cooldown so that
        // ProjectileSpawner and boss/enhanced systems use the same canonical value.
        if (card != null)
        {
            card.runtimeSpawnInterval = Mathf.Max(0.1f, baseCooldown);
        }

        // Apply cooldown reduction from card modifiers, calculated from BASE
        float finalCooldown = Mathf.Max(minCooldown, baseCooldown * (1f - modifiers.cooldownReductionPercent / 100f)); // % from base
        int finalManaCost = Mathf.Max(0, Mathf.CeilToInt(manaCost * (1f - modifiers.manaCostReduction)));
        float finalDamage = damage + modifiers.damageFlat; // FLAT damage bonus per hit
        
        // Apply size multiplier
        if (modifiers.sizeMultiplier != 1f)
        {
            transform.localScale *= modifiers.sizeMultiplier;
            
            // Scale collider using utility with colliderSizeOffset
            ColliderScaler.ScaleCollider(_collider2D, modifiers.sizeMultiplier, colliderSizeOffset);
        }
        
        // Pierce count = base modifiers + enhanced bonus (don't modify modifiers struct!)
        int totalPierceCount = modifiers.pierceCount + enhancedPierceAdd;
        
        // Still get PlayerStats for base damage calculation
        PlayerStats stats = null;
        if (colliderToIgnore != null)
        {
            stats = colliderToIgnore.GetComponent<PlayerStats>();
        }
        
        // Apply PlayerStats base damage multiplier
        if (stats != null)
        {
            cachedPlayerStats = stats;
        }
        else
        {
            cachedPlayerStats = FindObjectOfType<PlayerStats>();
        }
        
        baseDamageAfterCards = finalDamage;
        
        // Update variables with modifiers
        damage = finalDamage;
        speed = finalSpeed;
        lifetimeSeconds = finalLifetime;

        // Allow the global "enhanced first spawn" reduction system to bypass this
        // internal cooldown gate exactly once for PASSIVE projectile cards.
        bool bypassEnhancedFirstSpawnCooldown = false;
        if (!skipCooldownCheck && card != null && card.applyEnhancedFirstSpawnReduction && card.pendingEnhancedFirstSpawn)
        {
            if (card.projectileSystem == ProjectileCards.ProjectileSystemType.Passive)
            {
                bypassEnhancedFirstSpawnCooldown = true;
                card.pendingEnhancedFirstSpawn = false;
            }
        }

        // Generate key based on projectile type AND element (Fire/Ice have separate cooldowns)
        prefabKey = $"ProjectileTalon_{projectileType}";
        
        // Only check cooldown/mana for first projectile in multi-spawn
        if (!skipCooldownCheck)
        {
            // When this projectile is managed by a ProjectileCards instance (card != null),
            // its cooldown is already controlled by ProjectileSpawner / AdvancedPlayerController.
            // To avoid double-gating (which can cancel every other shot when global
            // projectileCooldownReduction favours are applied), only apply this internal
            // cooldown gate when no card context exists.
            if (card == null)
            {
                // Check cooldown for this specific projectile type
                if (!bypassEnhancedFirstSpawnCooldown && lastFireTimes.ContainsKey(prefabKey))
                {
                    if (Time.time - lastFireTimes[prefabKey] < finalCooldown)
                    {
                        Destroy(gameObject);
                        return;
                    }
                }

                // Record fire time for this projectile type
                lastFireTimes[prefabKey] = Time.time;
            }

            // Check mana with modified cost
            if (playerMana != null && !playerMana.Spend(finalManaCost))
            {
                Destroy(gameObject);
                return;
            }
        }
        else
        {
            // CRITICAL: Mark as additional projectile to prevent independent firing
            isAdditionalProjectile = true;
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
        
        // CRITICAL: Always setup ProjectilePiercing component (even if count is 0)
        // This ensures the component is properly initialized with modifier values
        ProjectilePiercing piercing = gameObject.GetComponent<ProjectilePiercing>();
        bool wasAdded = false;
        int existingPierceCount = 0;
        int prefabDefaultPierceCount = 0;
        
        if (piercing == null)
        {
            piercing = gameObject.AddComponent<ProjectilePiercing>();
            wasAdded = true;
            
            // CRITICAL: Manually set colliders to trigger since Awake() already ran
            Collider2D[] allColliders = GetComponentsInChildren<Collider2D>(true);
            foreach (Collider2D col in allColliders)
            {
                if (!col.isTrigger)
                {
                    col.isTrigger = true;
                }
            }
        }
        else
        {
            existingPierceCount = piercing.pierceCount;
            prefabDefaultPierceCount = piercing.pierceCount; // Store prefab's default value
        }
        
        // CRITICAL FIX: If prefab has default pierce count, use it as base and ADD modifiers to it
        // This prevents resetting the prefab's pierce value when no modifier is picked
        int finalPierceCount = totalPierceCount;
        if (prefabDefaultPierceCount > 0 && totalPierceCount == 0)
        {
            // Prefab has default pierce, but no modifiers picked - use prefab default
            finalPierceCount = prefabDefaultPierceCount;
        }
        else if (prefabDefaultPierceCount > 0 && totalPierceCount > 0)
        {
            // Prefab has default AND modifiers picked - ADD them together
            finalPierceCount = prefabDefaultPierceCount + totalPierceCount;
        }
        
        // Set pierce count
        piercing.SetMaxPierces(finalPierceCount);

        Destroy(gameObject, finalLifetime);
        StartTrailSfx();
    }

    // OnCollisionEnter2D removed - using triggers only for smooth pierce mechanics
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        // This is called when collider is a trigger (pierce mode)
        // Check for piercing component on parent (not on child colliders)
        ProjectilePiercing piercing = GetComponentInParent<ProjectilePiercing>();
        if (piercing == null)
        {
            piercing = GetComponent<ProjectilePiercing>();
        }
        
        if (((1 << other.gameObject.layer) & enemyLayer) != 0)
        {
            
            // If has piercing and already hit this enemy, ignore
            if (piercing != null && piercing.HasHitEnemy(other.gameObject))
            {
                return;
            }
            
            IDamageable damageable = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
            
            // Only damage if target is alive
            if (damageable != null && damageable.IsAlive)
            {
                // Check damage cooldown
                if (Time.time - lastDamageTime < damageCooldown)
                {
                    return; // Too soon to damage again
                }
                
                // Check if enemy is within damageable area (on-screen or slightly offscreen)
                if (!OffscreenDamageChecker.CanTakeDamage(other.transform.position))
                {
                    return;
                }
                
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Vector3 hitNormal = (transform.position - hitPoint).normalized;
                Vector3 effectBasePosition = hitPoint;
                Collider2D enemyCollider = other;
                if (enemyCollider != null)
                {
                    effectBasePosition = enemyCollider.bounds.center;
                }
                
                // Use damage value that was already modified by cards; apply PlayerStats per hit for crit
                float baseDamageForEnemy = baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
                float finalDamage = baseDamageForEnemy;

                Component damageableComponent = damageable as Component;
                GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : null;

                if (cachedPlayerStats != null)
                {
                    finalDamage = PlayerDamageHelper.ComputeProjectileDamage(cachedPlayerStats, enemyObject, baseDamageForEnemy, gameObject);
                }

                if (enemyObject != null)
                {
                    EnemyHealth enemyHealth = enemyObject.GetComponent<EnemyHealth>() ?? enemyObject.GetComponentInParent<EnemyHealth>();
                    if (enemyHealth != null)
                    {
                        enemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Fire);
                    }
                }

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

                lastDamageTime = Time.time; // Update last damage time

                // Play hit effect on EVERY successful enemy hit
                TryPlayHitEffect(effectBasePosition);
                
                // Register pierce hit
                if (piercing != null)
                {
                    bool shouldContinue = piercing.OnEnemyHit(other.gameObject);

                    // ProjectilePiercing.OnEnemyHit returns false when we have exceeded
                    // the allowed pierceCount (e.g., 1 pierce = hit 2 enemies).
                    if (!shouldContinue)
                    {
                        HandleImpact(hitPoint, hitNormal, other.transform);
                        Destroy(gameObject);
                        return;
                    }
                }
                else
                {
                    // No piercing - destroy projectile
                    HandleImpact(hitPoint, hitNormal, other.transform);
                    Destroy(gameObject);
                    return;
                }
            }
            else if (damageable != null && !damageable.IsAlive)
            {
                // Target is already dead, check if we should pierce through
                if (piercing != null && piercing.GetRemainingPierces() > 0)
                {
                    // Continue through dead enemy
                    return;
                }
                
                // No piercing - destroy
                Destroy(gameObject);
                return;
            }
        }
        else
        {
            // Hit non-enemy object (wall, etc) - always destroy
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 hitNormal = (transform.position - hitPoint).normalized;

            // Play hit effect on impact with environment
            TryPlayHitEffect(hitPoint);

            HandleImpact(hitPoint, hitNormal, other.transform);
            Destroy(gameObject);
        }
    }

    private void HandleImpact(Vector3 point, Vector3 normal, Transform hitParent)
    {
        if (impactClip != null)
        {
            AudioSource.PlayClipAtPoint(impactClip, point, impactVolume);
        }

        StopTrailSfx(false);
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

        float sizeRatio = 1f;
        if (baseScale.x != 0f)
        {
            sizeRatio = transform.localScale.x / baseScale.x;
        }
        else if (baseScale.y != 0f)
        {
            sizeRatio = transform.localScale.y / baseScale.y;
        }

        float finalMultiplier = sizeRatio * hitEffectSizeMultiplier;
        fx.transform.localScale *= finalMultiplier;

        if (hitEffectDuration > 0f)
        {
            Destroy(fx, hitEffectDuration);
        }
    }

    private IEnumerator SpawnHitEffectDelayed(Vector3 position, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (hitEffectPrefab != null)
        {
            SpawnHitEffectImmediate(position);
        }
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

    private IEnumerator RestoreVelocityAfterFrame(Rigidbody2D rb, Vector2 originalVelocity)
    {
        yield return null; // Wait one frame
        if (rb != null)
        {
            rb.velocity = originalVelocity;
        }
    }
    public void ApplyInstantModifiers(CardModifierStats mods) { Debug.Log($"<color=lime>╔ FIRETALON ╗</color>"); float ns=baseSpeed+mods.speedIncrease; if(ns!=speed){speed=ns; if(_rigidbody2D!=null)_rigidbody2D.velocity=_rigidbody2D.velocity.normalized*speed; Debug.Log($"<color=lime>Speed:{baseSpeed:F2}+{mods.speedIncrease:F2}={speed:F2}</color>");} float nl=baseLifetime+mods.lifetimeIncrease; if(nl!=lifetimeSeconds){lifetimeSeconds=nl; Debug.Log($"<color=lime>Lifetime:{baseLifetime:F2}+{mods.lifetimeIncrease:F2}={lifetimeSeconds:F2}</color>");} float nd=baseDamage*mods.damageMultiplier; if(nd!=damage){damage=nd; baseDamageAfterCards=nd; Debug.Log($"<color=lime>Damage:{baseDamage:F2}*{mods.damageMultiplier:F2}x={damage:F2}</color>");} if(mods.sizeMultiplier!=1f){transform.localScale=baseScale*mods.sizeMultiplier; Debug.Log($"<color=lime>Size:{baseScale}*{mods.sizeMultiplier:F2}x={transform.localScale}</color>");} Debug.Log($"<color=lime>╚═══════════════════════════════════════╝</color>"); }
}
