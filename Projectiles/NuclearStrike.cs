using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class NuclearStrike : MonoBehaviour, IInstantModifiable
{
    [Header("Drop Settings")]
    [SerializeField] private float dropSpeed = 5f;
    [Tooltip("Minimum lifetime before explosion")]
    [SerializeField] private float minLifetime = 2f;
    [Tooltip("Maximum lifetime before explosion")]
    [SerializeField] private float maxLifetime = 5f;
    
    public enum SpriteFacing2D { Right = 0, Up = 90, Left = 180, Down = 270 }
    [Header("Rotation")]
    [SerializeField] private SpriteFacing2D spriteFacing = SpriteFacing2D.Down;
    [SerializeField] private float additionalRotationOffsetDeg = 0f;
    [SerializeField] private bool rotateToVelocity = true;
    [SerializeField] private float maxRotationDegreesPerSecond = 1080f;
    [SerializeField] private float minRotateVelocity = 0.01f;
    [SerializeField] private bool keepInitialRotation = false;
    
    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 5f;
    [Tooltip("Offset for explosion detection area in X and Y coordinates")]
    [SerializeField] private Vector2 explosionRadiusOffset = Vector2.zero;
    [SerializeField] private float damage = 100f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private ProjectileType projectileType = ProjectileType.Nuclear;
    
    [Header("Spawn Area")]
    [Tooltip("Tag name for minimum spawn position GameObject (left side)")]
    [SerializeField] private string minPosTag = "NuclearStrike_MinPos";
    [Tooltip("Tag name for maximum spawn position GameObject (right side)")]
    [SerializeField] private string maxPosTag = "NuclearStrike_MaxPos";
    
    private Transform minPos;
    private Transform maxPos;
    
    // Base values for instant modifier recalculation
    private float baseDropSpeed;
    private float baseExplosionRadius;
    private float baseDamage;
    private Vector3 baseScale;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private float explosionEffectDuration = 3f;
    [Tooltip("Fade-out duration for explosion effect (0 = no fade)")]
    [SerializeField] private float explosionFadeOutDuration = 1f;
    [Tooltip("Offset for explosion effect (left side)")]
    [SerializeField] private Vector2 explosionEffectOffsetLeft = Vector2.zero;
    [Tooltip("Offset for explosion effect (right side)")]
    [SerializeField] private Vector2 explosionEffectOffsetRight = Vector2.zero;
    [Tooltip("Size multiplier for explosion effect")]
    [SerializeField] private float explosionEffectSizeMultiplier = 1f;
    [Tooltip("Base animation speed for explosion effect (used for calculations, not applied directly)")]
    public float baseExplosionAnimationSpeed = 1f;
    [Tooltip("Effect timing adjustment: negative = delay effect, positive = play effect early (seconds)")]
    [SerializeField] private float explosionEffectTimingAdjustment = 0f;
    
    [Header("Speed-to-Animation Sync")]
    [Tooltip("Speed increase threshold - when speed increases by this amount, animation speed increases")]
    public float speedIncreaseThreshold = 10f;
    [Tooltip("Animation speed increase per threshold (as percentage of base). E.g., 10 = 10% increase per threshold")]
    [Range(0f, 100f)]
    public float animationSpeedIncreasePercent = 10f;
    
    [Header("Speed-to-Explosion Size Scaling")]
    [Tooltip("Speed threshold for explosion size increase (+10 speed = +5% explosion size)")]
    public float explosionSizeSpeedThreshold = 10f;
    [Tooltip("Explosion size increase per speed threshold (as percentage). E.g., 5 = 5% increase per 10 speed")]
    [Range(0f, 100f)]
    public float explosionSizeIncreasePercent = 5f;
    
    private float finalExplosionAnimationSpeed = 1f;
    [SerializeField] private SpriteRenderer spriteRenderer;
    
    [Header("Shadow System")]
    [Tooltip("Shadow prefab that appears at landing location")]
    [SerializeField] private GameObject shadowPrefab;
    [Tooltip("Offset for shadow position (X, Y)")]
    [SerializeField] private Vector2 shadowOffset = Vector2.zero;
    
    [System.Serializable]
    public class SizeOffsetPair
    {
        [Tooltip("Size percentage (e.g., 10 = +10%, 20 = +20%, 50 = +50%, 100 = +100%, 200 = +200%)")]
        public float sizePercentage = 0f;
        [Tooltip("Offset for left side at this size")]
        public Vector2 offsetLeft = Vector2.zero;
        [Tooltip("Offset for right side at this size")]
        public Vector2 offsetRight = Vector2.zero;
    }
    
    [Header("Per-Size Offsets")]
    [Tooltip("Explosion effect offsets for different size multipliers. Automatically interpolates between values.")]
    [SerializeField] private List<SizeOffsetPair> sizeOffsets = new List<SizeOffsetPair>();
    
    [Header("Collider Scaling")]
    [Tooltip("Offset for collider size relative to visual size (0 = same as visual, -0.2 = 20% smaller, 0.2 = 20% larger)")]
    [SerializeField] private float colliderSizeOffset = 0f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip dropClip;
    [Range(0f, 1f)][SerializeField] private float dropVolume = 0.7f;
    [SerializeField] private AudioClip explosionClip;
    [Range(0f, 1f)][SerializeField] private float explosionVolume = 1f;
    
    [Header("Mana & Cooldown")]
    [SerializeField] private int manaCost = 30;
    [SerializeField] private float cooldown = 3f;
    
    [Header("Enhanced Variant 1 - Rapid Strike")]
    [Tooltip("Speed bonus for Enhanced Variant 1 (raw value added)")]
    [SerializeField] private float enhancedSpeedBonus = 20f;
    [Tooltip("Base cooldown for Enhanced Variant 1 (seconds). If 0, falls back to card/runtime or script cooldown, then behaves like a standard -50% reduction when enhanced.")]
    [SerializeField] private float EnhancedBaseCooldown = 0f;
    [Tooltip("Additional projectile count for Enhanced Variant 1 (e.g., 1 = spawn 1 extra)")]
    [SerializeField] public int enhancedProjectileCountBonus = 1;
    
    private Rigidbody2D _rigidbody2D;
    private AudioSource _audioSource;
    private Collider2D _collider2D;
    private Camera mainCamera;
    private float actualLifetime;
    private bool hasExploded = false;
    private GameObject shadowInstance;
    private Vector3 landingPosition;
    
    // Enhanced system
    private int enhancedVariant = 0; // 0 = basic, 1 = rapid strike, 2-3 = future variants
    
    // Instance-based cooldown tracking
    private static System.Collections.Generic.Dictionary<string, float> lastFireTimes = new System.Collections.Generic.Dictionary<string, float>();
    private string prefabKey;
    
    // Track if enhanced bonus has been added to card modifiers (static per card)
    private static bool hasAddedEnhancedBonus = false;
    
    // Store modifiers for explosion effect
    private CardModifierStats modifiers;
    private float enhancedSpeedAdd = 0f;
    private float baseDropSpeedStored = 0f;
    private float explosionRadiusForEffect = 0f; // Store radius BEFORE modifiers for effect scaling
    private PlayerStats cachedPlayerStats;
    
    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _collider2D = GetComponent<Collider2D>();
        mainCamera = Camera.main;
        
        // Store base values
        baseDropSpeed = dropSpeed;
        baseExplosionRadius = explosionRadius;
        baseDamage = damage;
        baseScale = transform.localScale;
        
        // Get or add audio source
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Get sprite renderer if not assigned
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
        
        // Make projectile kinematic (we'll control movement manually)
        if (_rigidbody2D != null)
        {
            _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody2D.velocity = Vector2.zero;
            _rigidbody2D.gravityScale = 0f;
        }
    }

    public void Initialize(Vector3 spawnPosition, Collider2D playerCollider, bool skipCooldownCheck = false)
    {
        // Find spawn area GameObjects by tag
        if (!string.IsNullOrEmpty(minPosTag))
        {
            GameObject minPosObj = GameObject.FindGameObjectWithTag(minPosTag);
            if (minPosObj != null) minPos = minPosObj.transform;
        }

        if (!string.IsNullOrEmpty(maxPosTag))
        {
            GameObject maxPosObj = GameObject.FindGameObjectWithTag(maxPosTag);
            if (maxPosObj != null) maxPos = maxPosObj.transform;
        }

        // Get card-specific modifiers first
        ProjectileCards card = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);

        // Check for enhanced variant using CARD-based system
        if (ProjectileCardLevelSystem.Instance != null && card != null)
        {
            enhancedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card);
            Debug.Log($"<color=gold>NuclearStrike ({card.cardName}) Enhanced Variant: {enhancedVariant}</color>");
        }

        modifiers = new CardModifierStats(); // Default values

        if (card != null)
        {
            modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
            Debug.Log($"<color=red>NuclearStrike using modifiers from {card.cardName}</color>");
        }


        // Apply enhanced variant modifiers BEFORE card modifiers
        enhancedSpeedAdd = 0f;
        int enhancedProjectileBonus = 0;
        
        if (enhancedVariant == 1)
        {
            enhancedSpeedAdd = enhancedSpeedBonus;

            // Store enhanced projectile count bonus for logging; the actual
            // projectile-count increase is applied per-spawn in ProjectileSpawner.
            enhancedProjectileBonus = enhancedProjectileCountBonus;
            
            Debug.Log($"<color=gold>Enhanced Rapid Strike: Speed +{enhancedSpeedAdd}, Additional Projectiles +{enhancedProjectileBonus}</color>");
        }
        
        // CRITICAL: Use ProjectileCards spawnInterval if available, otherwise use script cooldown.
        // When enhanced, allow a dedicated EnhancedBaseCooldown to override the
        // card/runtime value so that further modifiers work off that base.
        float baseCooldown = cooldown;
        if (card != null && card.runtimeSpawnInterval > 0f)
        {
            baseCooldown = card.runtimeSpawnInterval;
            Debug.Log($"<color=gold>Using ProjectileCards spawnInterval: {baseCooldown:F2}s (overriding script cooldown: {cooldown:F2}s)</color>");
        }

        // If Variant 1 is active and an EnhancedBaseCooldown is configured,
        // treat that as the canonical base value before applying any card
        // cooldown modifiers. This mirrors how other projectiles handle
        // enhanced base cooldowns.
        if (enhancedVariant == 1 && EnhancedBaseCooldown > 0f)
        {
            baseCooldown = EnhancedBaseCooldown;
            Debug.Log($"<color=gold>NuclearStrike Variant 1: Using EnhancedBaseCooldown = {EnhancedBaseCooldown:F2}s as base cooldown</color>");
        }
        
        // Apply card modifiers. All cooldown reductions are applied to the
        // resolved BASE cooldown (which may come from EnhancedBaseCooldown
        // when Variant 1 is active).
        float finalCooldown = Mathf.Max(0.1f, baseCooldown * (1f - modifiers.cooldownReductionPercent / 100f));
        int finalManaCost = Mathf.Max(1, Mathf.CeilToInt(manaCost * (1f - modifiers.manaCostReduction)));
        damage += modifiers.damageFlat; // FLAT damage bonus per hit
        
        // Calculate speed increase and animation speed
        float originalDropSpeed = dropSpeed; // Store original BASE speed before ANY modifications
        baseDropSpeedStored = originalDropSpeed; // Store for explosion effect
        
        // Add card modifier speed increase (RAW value)
        dropSpeed += modifiers.speedIncrease;
        
        // Then add enhanced speed bonus (RAW value)
        dropSpeed += enhancedSpeedAdd;
        
        // Calculate speed multiplier for lifetime adjustment (how many times faster than base)
        float speedMultiplier = dropSpeed / originalDropSpeed;
        
        Debug.Log($"<color=cyan>NuclearStrike Speed Calculation: Base={originalDropSpeed:F1}, Card+={modifiers.speedIncrease:F1}, Enhanced+={enhancedSpeedAdd:F1}, Final={dropSpeed:F1}, Multiplier={speedMultiplier:F2}x</color>");
        
        // Calculate total speed increase (from both modifiers and enhanced variant)
        float totalSpeedIncrease = (dropSpeed - originalDropSpeed);
        
        // Calculate how many thresholds we've crossed
        float thresholdsCrossed = totalSpeedIncrease / speedIncreaseThreshold;
        
        // Calculate animation speed increase (additive, based on base)
        // Each threshold = animationSpeedIncreasePercent% of BASE animation speed
        float animSpeedIncrease = (animationSpeedIncreasePercent / 100f) * thresholdsCrossed;
        finalExplosionAnimationSpeed = baseExplosionAnimationSpeed + (baseExplosionAnimationSpeed * animSpeedIncrease);
        
        Debug.Log($"<color=gold>NuclearStrike Speed Sync: Speed Increase={totalSpeedIncrease:F2}, Thresholds={thresholdsCrossed:F2}, AnimSpeed={finalExplosionAnimationSpeed:F2} (base={baseExplosionAnimationSpeed})</color>");
        
        // Apply size multiplier to visual
        float originalExplosionRadius = explosionRadius;
        if (modifiers.sizeMultiplier != 1f)
        {
            transform.localScale *= modifiers.sizeMultiplier;
            
            // IMPORTANT: Scale explosion radius with size (+10% size = +10% explosion radius)
            explosionRadius *= modifiers.sizeMultiplier;
            
            // CRITICAL: Scale explosion offset X with size (Y stays the same)
            explosionRadiusOffset.x *= modifiers.sizeMultiplier;
            // explosionRadiusOffset.y stays unchanged
            
            // Also scale effect offsets X
            explosionEffectOffsetLeft.x *= modifiers.sizeMultiplier;
            explosionEffectOffsetRight.x *= modifiers.sizeMultiplier;
            
            // Scale collider using utility with colliderSizeOffset
            ColliderScaler.ScaleCollider(_collider2D, modifiers.sizeMultiplier, colliderSizeOffset);
        }

        // After resolving the visual scale and explosion offsets, bind the
        // horizontal visual scale to the explosion radius offset X so that
        // they stay in sync (scale.x = explosionRadiusOffset.x).
        Vector3 nukeScale = transform.localScale;
        nukeScale.x = explosionRadiusOffset.x;
        transform.localScale = nukeScale;
        
        // CRITICAL: Store explosion radius BEFORE modifiers for effect scaling
        explosionRadiusForEffect = explosionRadius;
        
        // CRITICAL: Apply explosion radius modifiers from ProjectileCardModifiers
        // This is SEPARATE from size scaling and applies AFTER size scaling
        float explosionRadiusAfterSize = explosionRadius;
        explosionRadius = (explosionRadius + modifiers.explosionRadiusBonus) * modifiers.explosionRadiusMultiplier;
        
        Debug.Log($"<color=red>NuclearStrike Explosion Radius:</color>");
        Debug.Log($"<color=red>  Original: {originalExplosionRadius:F2}</color>");
        Debug.Log($"<color=red>  After Size: {explosionRadiusAfterSize:F2}</color>");
        Debug.Log($"<color=red>  Bonus: +{modifiers.explosionRadiusBonus:F2}</color>");
        Debug.Log($"<color=red>  Multiplier: x{modifiers.explosionRadiusMultiplier:F2}</color>");
        Debug.Log($"<color=red>  Final: {explosionRadius:F2}</color>");
        
        Debug.Log($"<color=red>NuclearStrike Modifiers Applied: Speed=+{modifiers.speedIncrease:F2}, Size={modifiers.sizeMultiplier:F2}x, DamageFlat=+{modifiers.damageFlat:F1}, ExplosionRadius={explosionRadius:F2}</color>");
        
        // Still get PlayerStats for base damage calculation
        PlayerStats stats = FindObjectOfType<PlayerStats>();
        if (stats != null)
        {
            cachedPlayerStats = stats;
        }
        
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

        // Generate key based ONLY on projectile type (so all NuclearStrikes share same cooldown)
        prefabKey = "NuclearStrike";
        
        // Only check cooldown/mana for first projectile in multi-spawn
        if (!skipCooldownCheck)
        {
            // Check cooldown
            if (!bypassEnhancedFirstSpawnCooldown && lastFireTimes.ContainsKey(prefabKey))
            {
                if (Time.time - lastFireTimes[prefabKey] < finalCooldown)
                {
                    Debug.Log($"<color=yellow>NuclearStrike on cooldown - {Time.time - lastFireTimes[prefabKey]:F2}s / {finalCooldown}s</color>");
                    Destroy(gameObject);
                    return;
                }
            }
            
            // Check mana
            PlayerMana playerMana = FindObjectOfType<PlayerMana>();
            if (playerMana != null && !playerMana.Spend(finalManaCost))
            {
                Debug.Log($"Not enough mana for NuclearStrike (cost: {finalManaCost})");
                Destroy(gameObject);
                return;
            }
            
            // Record fire time
            lastFireTimes[prefabKey] = Time.time;
        }
        else
        {
            Debug.Log($"<color=gold>NuclearStrike: Skipping cooldown/mana check (multi-projectile spawn)</color>");
        }
        
        // Determine spawn position
        if (minPos != null && maxPos != null)
        {
            // Random X position between minPos and maxPos
            float spawnX = Random.Range(minPos.position.x, maxPos.position.x);
            // Y position is fixed at minPos/maxPos Y (they should be at same height - top of screen)
            float spawnY = Mathf.Max(minPos.position.y, maxPos.position.y);
            
            transform.position = new Vector3(spawnX, spawnY, spawnPosition.z);
        }
        else
        {
            // Fallback to provided spawn position
            transform.position = spawnPosition;
        }
        
        // Apply lifetime reduction to BOTH min and max BEFORE randomizing
        // Lifetime is divided by speed multiplier to maintain consistent fall distance
        
        // Apply speed-based divisor to min/max
        float adjustedMinLifetime = minLifetime / speedMultiplier;
        float adjustedMaxLifetime = maxLifetime / speedMultiplier;
        
        // NOW randomize between the adjusted values
        actualLifetime = Random.Range(adjustedMinLifetime, adjustedMaxLifetime);
        
        Debug.Log($"<color=red>NuclearStrike spawned! Speed={dropSpeed:F1} (base={baseDropSpeed:F1}, multiplier={speedMultiplier:F2}x), Lifetime={actualLifetime:F2}s (original range: {minLifetime:F2}-{maxLifetime:F2}s, adjusted range: {adjustedMinLifetime:F2}-{adjustedMaxLifetime:F2}s, speed divisor={speedMultiplier:F2})</color>");
        
        // Calculate landing position (where nuclear will explode)
        // Landing Y = current Y - (dropSpeed * actualLifetime)
        landingPosition = new Vector3(
            transform.position.x,
            transform.position.y - (dropSpeed * actualLifetime),
            transform.position.z
        );
        
        // Apply shadow offset
        Vector3 shadowPosition = landingPosition + (Vector3)shadowOffset;
        
        // Spawn shadow at landing position
        if (shadowPrefab != null)
        {
            shadowInstance = Instantiate(shadowPrefab, shadowPosition, Quaternion.identity);
            
            // Get shadow animator and adjust animation speed
            Animator shadowAnimator = shadowInstance.GetComponent<Animator>();
            if (shadowAnimator != null)
            {
                // Animation speed = 1 / actualLifetime
                // This makes the animation complete exactly when nuclear lands
                // Farther drop = longer lifetime = slower animation
                // Closer drop = shorter lifetime = faster animation
                float animSpeed = 1f / actualLifetime;
                shadowAnimator.speed = animSpeed;
                
                Debug.Log($"<color=yellow>Shadow spawned at {shadowPosition} with animation speed {animSpeed:F2}x (lifetime: {actualLifetime:F2}s)</color>");
            }
            
            // Destroy shadow when nuclear explodes
            Destroy(shadowInstance, actualLifetime);
        }
        
        // Play drop sound
        if (dropClip != null && _audioSource != null)
        {
            _audioSource.clip = dropClip;
            _audioSource.volume = dropVolume;
            _audioSource.loop = true;
            _audioSource.Play();
        }
        
        // Start dropping and explosion countdown
        StartCoroutine(DropRoutine());
        StartCoroutine(ExplodeAfterLifetime());
    }
    
    private void Update()
    {
        if (keepInitialRotation || _rigidbody2D == null || hasExploded) return;
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
    
    private IEnumerator DropRoutine()
    {
        while (!hasExploded)
        {
            // Move straight down
            _rigidbody2D.velocity = Vector2.down * dropSpeed;
            
            // Set initial rotation if not keeping it
            if (!keepInitialRotation && rotateToVelocity)
            {
                float baseAngle = Mathf.Atan2(-1f, 0f) * Mathf.Rad2Deg; // Down direction
                float facingCorrection = (int)spriteFacing;
                float finalAngle = baseAngle + facingCorrection + additionalRotationOffsetDeg;
                transform.rotation = Quaternion.Euler(0f, 0f, finalAngle);
            }
            
            yield return null;
        }
    }
    
    private IEnumerator ExplodeAfterLifetime()
    {
        yield return new WaitForSeconds(actualLifetime);
        
        if (!hasExploded)
        {
            Explode();
        }
    }
    
    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;
        
        Debug.Log($"<color=red>NuclearStrike exploding at {transform.position}!</color>");
        
        // Calculate explosion center with offset
        Vector2 explosionCenter = (Vector2)transform.position + explosionRadiusOffset;
        
        // Find all enemies in explosion radius
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(explosionCenter, explosionRadius, enemyLayer);
        
        Debug.Log($"<color=red>NuclearStrike hit {hitColliders.Length} enemies</color>");
        
        foreach (Collider2D hitCollider in hitColliders)
        {
            IDamageable damageable = hitCollider.GetComponent<IDamageable>() ?? hitCollider.GetComponentInParent<IDamageable>();
            
            if (damageable != null && damageable.IsAlive)
            {
                // Check if enemy is within damageable area (on-screen or slightly offscreen)
                if (!OffscreenDamageChecker.CanTakeDamage(hitCollider.transform.position))
                {
                    continue; // Skip this enemy
                }
                
                Vector3 hitPoint = hitCollider.ClosestPoint(explosionCenter);
                Vector3 hitNormal = (explosionCenter - (Vector2)hitPoint).normalized;

                // Use damage value after card modifiers but roll crit PER ENEMY via PlayerStats
                float baseDamageForEnemy = damage;
                float finalDamage = baseDamageForEnemy;
                
                Component damageableComponent = damageable as Component;
                GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : hitCollider.gameObject;

                if (cachedPlayerStats != null)
                {
                    finalDamage = PlayerDamageHelper.ComputeProjectileDamage(cachedPlayerStats, enemyObject, baseDamageForEnemy, gameObject);
                }

                // Tag EnemyHealth so EnemyHealth.TakeDamage renders the
                // explosion using the correct Fire/Ice damage color.
                if (enemyObject != null)
                {
                    EnemyHealth enemyHealth = enemyObject.GetComponent<EnemyHealth>() ?? enemyObject.GetComponentInParent<EnemyHealth>();
                    if (enemyHealth != null)
                    {
                        DamageNumberManager.DamageType damageType = projectileType == ProjectileType.Fire
                            ? DamageNumberManager.DamageType.Fire
                            : DamageNumberManager.DamageType.Ice;
                        enemyHealth.SetLastIncomingDamageType(damageType);
                    }
                }

                damageable.TakeDamage(finalDamage, hitPoint, hitNormal);

                BurnEffect burnEffect = GetComponent<BurnEffect>();
                if (burnEffect != null)
                {
                    burnEffect.Initialize(finalDamage, projectileType);
                    burnEffect.TryApplyBurn(hitCollider.gameObject, hitPoint);
                }

                SlowEffect slowEffect = GetComponent<SlowEffect>();
                if (slowEffect != null)
                {
                    slowEffect.TryApplySlow(hitCollider.gameObject, hitPoint);
                }

                StaticEffect staticEffect = GetComponent<StaticEffect>();
                if (staticEffect != null)
                {
                    staticEffect.TryApplyStatic(hitCollider.gameObject, hitPoint);
                }
            }
        }
        
        // Spawn explosion effect with offset, size, and timing
        if (explosionEffectPrefab != null)
        {
            // Determine which side of screen we're on (left or right of center)
            Camera mainCam = Camera.main;
            bool isOnLeftSide = mainCam != null && transform.position.x < mainCam.transform.position.x;
            
            // Get offset based on current size (uses per-size offsets if configured)
            Vector2 effectOffset = GetOffsetForCurrentSize(isOnLeftSide);
            Vector3 explosionPosition = transform.position + (Vector3)effectOffset;
            
            // Handle timing adjustment
            if (explosionEffectTimingAdjustment < 0f)
            {
                // Delay effect
                StartCoroutine(SpawnDelayedExplosionEffect(explosionPosition, Mathf.Abs(explosionEffectTimingAdjustment)));
            }
            else
            {
                // Normal or early timing
                SpawnExplosionEffectImmediate(explosionPosition);
            }
        }
        
        // Play explosion sound
        if (explosionClip != null)
        {
            AudioSource.PlayClipAtPoint(explosionClip, transform.position, explosionVolume);
        }
        
        // Destroy projectile
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Gets the appropriate offset based on current size percentage.
    /// Interpolates between configured size offset pairs.
    /// </summary>
    private Vector2 GetOffsetForCurrentSize(bool isLeftSide)
    {
        // If no size offsets configured, use default offsets
        if (sizeOffsets == null || sizeOffsets.Count == 0)
        {
            return isLeftSide ? explosionEffectOffsetLeft : explosionEffectOffsetRight;
        }
        
        // Get current size multiplier from transform scale and convert to percentage
        float currentSizeMultiplier = transform.localScale.x; // e.g., 1.2 for +20%
        float currentSizePercentage = (currentSizeMultiplier - 1f) * 100f; // Convert to percentage (e.g., 1.2 -> 20)
        
        Debug.Log($"<color=red>NuclearStrike: Current size multiplier = {currentSizeMultiplier:F2}, percentage = {currentSizePercentage:F1}%</color>");
        
        // Sort size offsets by percentage (in case they're not in order)
        sizeOffsets.Sort((a, b) => a.sizePercentage.CompareTo(b.sizePercentage));
        
        // Find the two closest size percentages to interpolate between
        SizeOffsetPair lower = null;
        SizeOffsetPair upper = null;
        
        for (int i = 0; i < sizeOffsets.Count; i++)
        {
            if (sizeOffsets[i].sizePercentage <= currentSizePercentage)
            {
                lower = sizeOffsets[i];
            }
            if (sizeOffsets[i].sizePercentage >= currentSizePercentage && upper == null)
            {
                upper = sizeOffsets[i];
            }
        }
        
        // If exact match or only one bound found
        if (lower != null && upper != null && Mathf.Approximately(lower.sizePercentage, upper.sizePercentage))
        {
            Debug.Log($"<color=red>NuclearStrike: Exact match! Using size {lower.sizePercentage}%</color>");
            return isLeftSide ? lower.offsetLeft : lower.offsetRight;
        }
        
        // If below all configured sizes, use the smallest
        if (lower == null && upper != null)
        {
            Debug.Log($"<color=red>NuclearStrike: Below all sizes, using {upper.sizePercentage}%</color>");
            return isLeftSide ? upper.offsetLeft : upper.offsetRight;
        }
        
        // If above all configured sizes, use the largest
        if (upper == null && lower != null)
        {
            Debug.Log($"<color=red>NuclearStrike: Above all sizes, using {lower.sizePercentage}%</color>");
            return isLeftSide ? lower.offsetLeft : lower.offsetRight;
        }
        
        // Interpolate between lower and upper
        if (lower != null && upper != null)
        {
            float t = (currentSizePercentage - lower.sizePercentage) / (upper.sizePercentage - lower.sizePercentage);
            Vector2 lowerOffset = isLeftSide ? lower.offsetLeft : lower.offsetRight;
            Vector2 upperOffset = isLeftSide ? upper.offsetLeft : upper.offsetRight;
            Debug.Log($"<color=red>NuclearStrike: Interpolating between {lower.sizePercentage}% and {upper.sizePercentage}% (t={t:F2})</color>");
            return Vector2.Lerp(lowerOffset, upperOffset, t);
        }
        
        // Fallback to default offsets
        return isLeftSide ? explosionEffectOffsetLeft : explosionEffectOffsetRight;
    }
    
    private void SpawnExplosionEffectImmediate(Vector3 position)
    {
        GameObject explosion = Instantiate(explosionEffectPrefab, position, Quaternion.identity);
        
        // CRITICAL: Scale explosion effect with ORIGINAL radius (before modifiers), size multiplier, AND projectile size
        // Do NOT scale with explosion radius modifiers - only visual size matters for effect
        float baseScale = explosionRadiusForEffect / 5f;
        float projectileSizeMultiplier = transform.localScale.x; // Use projectile's current scale
        
        // Calculate speed-based explosion size increase
        float totalSpeedIncrease = dropSpeed - baseDropSpeedStored;
        float sizeThresholdsCrossed = totalSpeedIncrease / explosionSizeSpeedThreshold;
        float explosionSizeIncrease = (explosionSizeIncreasePercent / 100f) * sizeThresholdsCrossed;
        float finalExplosionSizeMultiplier = explosionEffectSizeMultiplier * (1f + explosionSizeIncrease);
        
        Debug.Log($"<color=gold>NuclearStrike Explosion Size: Base={explosionEffectSizeMultiplier:F2}, Speed Increase={totalSpeedIncrease:F2}, Thresholds={sizeThresholdsCrossed:F2}, Final Size={finalExplosionSizeMultiplier:F2}x</color>");
        
        // Apply final size multiplier (base * speed scaling)
        explosion.transform.localScale = Vector3.one * (baseScale * finalExplosionSizeMultiplier * projectileSizeMultiplier);
        
        // Set animation speed if explosion has Animator
        Animator explosionAnimator = explosion.GetComponent<Animator>();
        if (explosionAnimator != null && finalExplosionAnimationSpeed > 0f)
        {
            explosionAnimator.speed = finalExplosionAnimationSpeed;
            Debug.Log($"<color=cyan>Explosion animation speed set to {finalExplosionAnimationSpeed:F2}x (base={baseExplosionAnimationSpeed})</color>");
        }
        
        // Start fade-out if duration is set
        if (explosionFadeOutDuration > 0f)
        {
            // CRITICAL FIX: Add self-destruct component to explosion so it destroys itself
            // even if NuclearStrike GameObject is destroyed
            ExplosionSelfDestruct selfDestruct = explosion.AddComponent<ExplosionSelfDestruct>();
            selfDestruct.Initialize(explosionEffectDuration, explosionFadeOutDuration);
        }
        else
        {
            // Destroy after duration (Unity automatically destroys all children)
            Destroy(explosion, explosionEffectDuration);
        }
    }
    
    private IEnumerator FadeOutExplosion(GameObject explosion, float totalDuration, float fadeOutDuration)
    {
        // Wait until it's time to start fading
        float waitTime = totalDuration - fadeOutDuration;
        if (waitTime > 0f)
        {
            yield return new WaitForSeconds(waitTime);
        }
        
        // Get all sprite renderers and particle systems
        SpriteRenderer[] sprites = explosion.GetComponentsInChildren<SpriteRenderer>();
        ParticleSystem[] particles = explosion.GetComponentsInChildren<ParticleSystem>();
        
        // Store original alpha values
        Color[] originalColors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            originalColors[i] = sprites[i].color;
        }
        
        // Fade out
        float elapsed = 0f;
        while (elapsed < fadeOutDuration && explosion != null)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / fadeOutDuration);
            
            // Fade sprites
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null)
                {
                    Color c = originalColors[i];
                    c.a = originalColors[i].a * alpha;
                    sprites[i].color = c;
                }
            }
            
            // Fade particles
            foreach (var ps in particles)
            {
                if (ps != null)
                {
                    var main = ps.main;
                    Color c = main.startColor.color;
                    c.a *= alpha;
                    main.startColor = c;
                }
            }
            
            yield return null;
        }
        
        // Destroy after fade (this will automatically destroy all children)
        if (explosion != null)
        {
            // Explicitly destroy all children first to ensure cleanup
            Transform[] children = explosion.GetComponentsInChildren<Transform>();
            foreach (Transform child in children)
            {
                if (child != null && child.gameObject != explosion)
                {
                    Destroy(child.gameObject);
                }
            }
            
            // Then destroy the parent
            Destroy(explosion);
        }
    }
    
    private IEnumerator SpawnDelayedExplosionEffect(Vector3 position, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (explosionEffectPrefab != null)
        {
            SpawnExplosionEffectImmediate(position);
            Debug.Log($"<color=cyan>Explosion effect played {delay}s late</color>");
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw explosion radius in editor with offset
        Vector3 explosionCenter = transform.position + (Vector3)explosionRadiusOffset;
        
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawSphere(explosionCenter, explosionRadius);
        
        Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
        Gizmos.DrawWireSphere(explosionCenter, explosionRadius);


        
        // Draw spawn area if tags are set
        if (!string.IsNullOrEmpty(minPosTag) && !string.IsNullOrEmpty(maxPosTag))
        {
            GameObject minPosObj = GameObject.FindGameObjectWithTag(minPosTag);
            GameObject maxPosObj = GameObject.FindGameObjectWithTag(maxPosTag);
            
            if (minPosObj != null && maxPosObj != null)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
                
                // Draw horizontal spawn line at top
                Vector3 leftPoint = new Vector3(minPosObj.transform.position.x, Mathf.Max(minPosObj.transform.position.y, maxPosObj.transform.position.y), 0f);
                Vector3 rightPoint = new Vector3(maxPosObj.transform.position.x, Mathf.Max(minPosObj.transform.position.y, maxPosObj.transform.position.y), 0f);
                
                Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
                Gizmos.DrawLine(leftPoint, rightPoint);
                
                // Draw vertical drop indicators
                Gizmos.DrawLine(leftPoint, leftPoint + Vector3.down * 2f);
                Gizmos.DrawLine(rightPoint, rightPoint + Vector3.down * 2f);
            }
        }
    }
    
    private void OnDestroy()
    {
        // Stop audio
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
        
        // CRITICAL: Destroy shadow when projectile is destroyed
        if (shadowInstance != null)
        {
            Destroy(shadowInstance);
            Debug.Log($"<color=yellow>NuclearStrike destroyed - cleaning up shadow</color>");
        }
    }
    
    /// <summary>
    /// Apply modifiers instantly (IInstantModifiable interface)
    /// </summary>
    public void ApplyInstantModifiers(CardModifierStats modifiers)
    {
        Debug.Log($"<color=lime>╔═══ NUCLEARSTRIKE INSTANT MODIFIERS ═══╗</color>");
        
        // Recalculate drop speed
        float newSpeed = baseDropSpeed + modifiers.speedIncrease;
        if (newSpeed != dropSpeed)
        {
            dropSpeed = newSpeed;
            Debug.Log($"<color=lime>  Drop Speed: {baseDropSpeed:F2} + {modifiers.speedIncrease:F2} = {dropSpeed:F2}</color>");
        }
        
        // Recalculate explosion radius
        float newRadius = (baseExplosionRadius + modifiers.explosionRadiusBonus) * modifiers.explosionRadiusMultiplier;
        if (newRadius != explosionRadius)
        {
            explosionRadius = newRadius;
            Debug.Log($"<color=lime>  Explosion Radius: ({baseExplosionRadius:F2} + {modifiers.explosionRadiusBonus:F2}) * {modifiers.explosionRadiusMultiplier:F2}x = {explosionRadius:F2}</color>");
        }
        
        // Recalculate damage
        float newDamage = baseDamage * modifiers.damageMultiplier;
        if (newDamage != damage)
        {
            damage = newDamage;
            Debug.Log($"<color=lime>  Damage: {baseDamage:F2} * {modifiers.damageMultiplier:F2}x = {damage:F2}</color>");
        }
        
        // Recalculate size
        if (modifiers.sizeMultiplier != 1f)
        {
            transform.localScale = baseScale * modifiers.sizeMultiplier;
            Debug.Log($"<color=lime>  Size: {baseScale} * {modifiers.sizeMultiplier:F2}x = {transform.localScale}</color>");
        }
        
        Debug.Log($"<color=lime>╚═══════════════════════════════════════╝</color>");
    }
}

/// <summary>
/// Helper component that makes explosion effects destroy themselves independently
/// This prevents explosions from persisting when NuclearStrike is destroyed
/// </summary>
public class ExplosionSelfDestruct : MonoBehaviour
{
    private float totalDuration;
    private float fadeOutDuration;
    
    public void Initialize(float total, float fadeOut)
    {
        totalDuration = total;
        fadeOutDuration = fadeOut;
        StartCoroutine(FadeAndDestroy());
    }
    
    private IEnumerator FadeAndDestroy()
    {
        // Wait until it's time to start fading
        float waitTime = totalDuration - fadeOutDuration;
        if (waitTime > 0f)
        {
            yield return new WaitForSeconds(waitTime);
        }
        
        // Get all sprite renderers and particle systems
        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>();
        ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>();
        
        // Store original alpha values
        Color[] originalColors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            originalColors[i] = sprites[i].color;
        }
        
        // Fade out
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / fadeOutDuration);
            
            // Fade sprites
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null)
                {
                    Color c = originalColors[i];
                    c.a = originalColors[i].a * alpha;
                    sprites[i].color = c;
                }
            }
            
            // Fade particles
            foreach (var ps in particles)
            {
                if (ps != null)
                {
                    var main = ps.main;
                    Color c = main.startColor.color;
                    c.a *= alpha;
                    main.startColor = c;
                }
            }
            
            yield return null;
        }
        
        // Destroy explosion GameObject (this destroys all children automatically)
        Destroy(gameObject);
        Debug.Log($"<color=yellow>Explosion effect destroyed by ExplosionSelfDestruct</color>");
    }
}
