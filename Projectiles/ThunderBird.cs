using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ThunderBird : MonoBehaviour, IInstantModifiable
{
    [Header("Bird Settings")]
    [SerializeField] private float flySpeed = 8f;
    [Tooltip("Time before bird is destroyed after spawning")]
    [SerializeField] private float lifetimeSeconds = 10f;
    
    [Header("Strike Zone")]
    [Tooltip("Radius of the damage zone around the bird")]
    [SerializeField] private float strikeZoneRadius = 2f;
    [Tooltip("Offset for strike zone detection area in X and Y coordinates")]
    [SerializeField] private Vector2 strikeZoneOffset = Vector2.zero;
    [Tooltip("Delay before damage is registered when enemy enters strike zone (in seconds)")]
    [SerializeField] private float damageDelay = 0.25f;
    [SerializeField] private float damage = 30f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private ProjectileType projectileType = ProjectileType.Thunder;
    
    [Header("Spawn Area")]
    [Tooltip("Tag name for minimum spawn position GameObject (left side)")]
    [SerializeField] private string minPosTag = "ThunderBird_MinPos";
    [Tooltip("Tag name for maximum spawn position GameObject (right side)")]
    [SerializeField] private string maxPosTag = "ThunderBird_MaxPos";
    
    private Transform minPos;
    private Transform maxPos;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject strikeEffectPrefab;
    [SerializeField] private float strikeEffectDuration = 1f;
    [Tooltip("Size multiplier for strike effect")]
    [SerializeField] private float strikeEffectSizeMultiplier = 1f;
    [Tooltip("Effect timing adjustment: negative = delay effect, positive = play effect early (relative to damage delay)")]
    [SerializeField] private float strikeEffectTimingAdjustment = 0f;
    [SerializeField] private SpriteRenderer spriteRenderer;
    
    [Header("Audio")]
    [SerializeField] private AudioClip flyClip;
    [Range(0f, 1f)][SerializeField] private float flyVolume = 0.7f;
    [SerializeField] private AudioClip strikeClip;
    [Range(0f, 1f)][SerializeField] private float strikeVolume = 0.8f;
    
    [Header("Mana & Cooldown")]
    [SerializeField] private int manaCost = 20;
    [SerializeField] private float cooldown = 2f;
    
    [Header("Collider Scaling")]
    [Tooltip("Offset for collider size relative to visual size (0 = same as visual, -0.2 = 20% smaller, 0.2 = 20% larger)")]
    [SerializeField] private float colliderSizeOffset = 0f;
    
    [Header("Enhanced Variant 1 - Dual Thunder")]
    [Tooltip("Strike radius increase for Enhanced Variant 1 (0.25 = +25%)")]
    [SerializeField] private float enhancedStrikeRadiusIncrease = 0.25f;

    [Tooltip("Speed bonus for Enhanced Variant 1")]
    public float variant1SpeedBonus = 10f;

    [Tooltip("Cooldown reduction for Enhanced Variant 1 (0.25 = 25% reduction)")]
    public float variant1CooldownReduction = 0.25f;

    [Tooltip("Base cooldown for Enhanced Variant 1 (seconds). If 0, falls back to ProjectileCards.runtimeSpawnInterval or script cooldown.")]
    public float variant1BaseCooldown = 0f;

    [Tooltip("Size multiplier for Enhanced Variant 1 (Dual Thunder). Applies on top of normal size modifiers.")]
    public float variant1SizeMultiplier = 1f;

    // NOTE: Variant 1 spawning is handled by the spawner
    // Base Variant 1 spawns 2 birds (left + right)
    // Each +1 projectileCount adds 2 more birds (1 per side)

    [Header("Enhanced Variant 2 - Slow Striker")]
    [Tooltip("Movement speed for Variant 2 (slow horizontal flight)")]
    public float variant2Speed = 3f;
    
    [Tooltip("Strike interval for Variant 2 (periodic strikes in seconds)")]
    public float variant2StrikeInterval = 3f;
    
    [Tooltip("Base cooldown for Variant 2 (in seconds)")]
    public float variant2BaseCooldown = 30f;
    public float variant12BaseCooldown = 0f;

    public float variant2MinStrikeInterval = 1f;
    public float variant2MinSpeed = 1f;

    
    [Tooltip("Size increase for Variant 2 (1.25 = 25% larger)")]
    public float variant2SizeMultiplier = 1.25f;
    
    [Tooltip("Animation speed decrease for Variant 2 (0.8 = 20% slower)")]
    public float variant2AnimationSpeed = 0.8f;
    
    [Tooltip("Unique strike effect size multiplier for Variant 2 (1.5 = 50% larger)")]
    public float variant2StrikeEffectSizeMultiplier = 1.5f;
    
    [Tooltip("Camera offset for 'on camera' detection (positive = bird needs to be further in, negative = bird can be further out)")]
    public float variant2CameraOffset = 0f;
    
    [Header("Enhanced Variant 2 - Modifier Exchange Rates")]
    [Tooltip("Exchange rate: strikeZoneRadius → strikeInterval reduction (e.g., 1.0 = each +1 radius reduces interval by 1 second)")]
    public float variant2StrikeZoneToIntervalRate = 1f;
    
    [Tooltip("Exchange rate: speed modifier → speed reduction for Variant 2 (e.g., 0.1 = each +1 speed reduces Variant 2 speed by 0.1)")]
    public float variant2SpeedReductionRate = 0.1f;
    
    [SerializeField] private int variant2MaxEffectsPerStrike = 999;
    [SerializeField] private bool debugVariant2Logging = false;
    
    // Variant 2 runtime variables
    private float nextStrikeTime = 0f;
    private bool isVariant2Active = false;
    private bool isVariant12Active = false;
    private bool isVariant12TopBird = false;
    private bool hasPerformedFirstStrike = false; // Track if first strike has been performed
    private int damageNumbersShownThisStrike = 0;
    private int effectsSpawnedThisStrike = 0;
    
    private Rigidbody2D _rigidbody2D;
    private Collider2D _collider2D;
    private AudioSource _audioSource;
    private Animator _animator;
    private bool isMovingRight = true;
    private Camera mainCamera;
    private float spawnTime; // Track when bird spawned
    
    // Track enemies that have been damaged to prevent multiple hits
    private HashSet<GameObject> damagedEnemies = new HashSet<GameObject>();
    
    // Track enemies waiting for damage delay
    private Dictionary<GameObject, float> pendingDamageEnemies = new Dictionary<GameObject, float>();

    private class CachedEnemyData
    {
        public IDamageable damageable;
        public EnemyHealth enemyHealth;
        public GameObject damageObject;
    }

    private readonly Dictionary<GameObject, CachedEnemyData> cachedEnemyData = new Dictionary<GameObject, CachedEnemyData>();
    
    // Per-enemy strike effect offsets (customizable per enemy type)
    [System.Serializable]
    public class EnemyStrikeOffset
    {
        public string enemyName;
        [Header("Left to Right (spawns at minPos)")]
        [Tooltip("Offset when bird goes LEFT to RIGHT and enemy is on LEFT side of camera")]
        public Vector2 offsetLeftToRightLeft;
        [Tooltip("Offset when bird goes LEFT to RIGHT and enemy is on RIGHT side of camera")]
        public Vector2 offsetLeftToRightRight;
        [Header("Right to Left (spawns at maxPos)")]
        [Tooltip("Offset when bird goes RIGHT to LEFT and enemy is on LEFT side of camera")]
        public Vector2 offsetRightToLeftLeft;
        [Tooltip("Offset when bird goes RIGHT to LEFT and enemy is on RIGHT side of camera")]
        public Vector2 offsetRightToLeftRight;
    }
    
    [Header("Per-Enemy Strike Offsets")]
    [Tooltip("Custom strike effect offsets for specific enemy types. 4 offsets per enemy based on bird direction and enemy camera position.")]
    [SerializeField] private List<EnemyStrikeOffset> perEnemyOffsets = new List<EnemyStrikeOffset>();

    [Header("Strike Effect Placement")]
    [SerializeField] private bool usePerEnemyStrikeOffsets = true;
    
    // Track if bird spawned from left (moving right) or right (moving left)
    private bool spawnedFromLeft = false;
    
    // Base values for instant modifier recalculation
    private float baseFlySpeed;
    private float baseStrikeZoneRadius;
    private float baseLifetimeSeconds;
    private float baseDamage;
    private Vector3 baseScale;
    private PlayerStats cachedPlayerStats;
    private float baseDamageAfterCards;

    [Header("Offscreen Destruction Grace Periods")]
    [Tooltip("Grace period before destruction for BASE variant (seconds)")]
    public float baseGracePeriod = 10f;
    [Tooltip("Grace period before destruction for Enhanced Variant 1 (seconds)")]
    public float variant1GracePeriod = 10f;
    [Tooltip("Grace period before destruction for Enhanced Variant 2 (seconds)")]
    public float variant2GracePeriod = 10f;
    
    // Get offset for specific enemy based on bird spawn direction AND enemy camera position
    private Vector2 GetStrikeOffsetForEnemy(GameObject enemy)
    {
        string enemyName = enemy.name.Replace("(Clone)", "").Trim();
        
        foreach (var offsetData in perEnemyOffsets)
        {
            if (offsetData.enemyName == enemyName)
            {
                // Determine if enemy is on left or right side of camera
                Camera mainCam = Camera.main;
                bool enemyOnLeftSide = mainCam != null && enemy.transform.position.x < mainCam.transform.position.x;
                
                // Return offset based on bird's spawn direction AND enemy's camera position
                if (spawnedFromLeft) // Bird goes LEFT to RIGHT
                {
                    return enemyOnLeftSide ? offsetData.offsetLeftToRightLeft : offsetData.offsetLeftToRightRight;
                }
                else // Bird goes RIGHT to LEFT
                {
                    return enemyOnLeftSide ? offsetData.offsetRightToLeftLeft : offsetData.offsetRightToLeftRight;
                }
            }
        }
        
        return Vector2.zero; // Default offset
    }

    private Vector2 GetStrikeEffectOffset(GameObject enemy)
    {
        if (!usePerEnemyStrikeOffsets)
        {
            return Vector2.zero;
        }

        GameObject enemyRoot = ResolveEnemyRootForStrike(enemy);
        if (enemyRoot == null)
        {
            return Vector2.zero;
        }

        return GetStrikeOffsetForEnemy(enemyRoot);
    }

    private GameObject ResolveEnemyRootForStrike(GameObject candidate)
    {
        if (candidate == null)
        {
            return null;
        }

        EnemyHealth enemyHealth = candidate.GetComponentInParent<EnemyHealth>();
        if (enemyHealth != null)
        {
            return enemyHealth.gameObject;
        }

        IDamageable damageable = candidate.GetComponentInParent<IDamageable>();
        if (damageable is Component damageableComponent)
        {
            return damageableComponent.gameObject;
        }

        return candidate;
    }

    private Vector3 GetStrikeEffectAnchorPosition(GameObject enemy, Collider2D colliderHint)
    {
        if (colliderHint != null)
        {
            return colliderHint.bounds.center;
        }

        Collider2D enemyCollider = null;
        if (enemy != null)
        {
            enemyCollider = enemy.GetComponent<Collider2D>() ?? enemy.GetComponentInChildren<Collider2D>() ?? enemy.GetComponentInParent<Collider2D>();
        }

        if (enemyCollider != null)
        {
            return enemyCollider.bounds.center;
        }

        if (enemy != null)
        {
            return enemy.transform.position;
        }

        return Vector3.zero;
    }
    
    private bool TryGetCachedEnemyData(GameObject source, out IDamageable damageable, out EnemyHealth enemyHealth, out GameObject damageObject)
    {
        damageable = null;
        enemyHealth = null;
        damageObject = source;

        if (source == null)
        {
            return false;
        }

        if (cachedEnemyData.TryGetValue(source, out CachedEnemyData cached))
        {
            damageable = cached.damageable;
            enemyHealth = cached.enemyHealth;
            damageObject = cached.damageObject;
            return damageable != null;
        }

        IDamageable resolvedDamageable = source.GetComponent<IDamageable>() ?? source.GetComponentInParent<IDamageable>();
        GameObject resolvedDamageObject = source;
        EnemyHealth resolvedEnemyHealth = null;

        if (resolvedDamageable is Component damageableComponent)
        {
            resolvedDamageObject = damageableComponent.gameObject;
        }

        if (resolvedDamageObject != null)
        {
            resolvedEnemyHealth = resolvedDamageObject.GetComponent<EnemyHealth>() ?? resolvedDamageObject.GetComponentInParent<EnemyHealth>();
        }

        CachedEnemyData newEntry = new CachedEnemyData
        {
            damageable = resolvedDamageable,
            enemyHealth = resolvedEnemyHealth,
            damageObject = resolvedDamageObject
        };

        cachedEnemyData[source] = newEntry;

        damageable = resolvedDamageable;
        enemyHealth = resolvedEnemyHealth;
        damageObject = resolvedDamageObject;

        return damageable != null;
    }
    
    // Enhanced system
    private int enhancedVariant = 0; // 0 = basic, 1 = dual thunder, 2-3 = future variants
    
    // Static tracking for dual spawn alternation (per side)
    private static bool leftBirdSpawnTop = true; // true = top zone, false = bottom zone
    private static bool rightBirdSpawnTop = false; // Opposite of left (they must be opposite)
    private static bool isFirstDualSpawn = true; // Track if this is the first spawn after enhanced unlock
    private static int variant1SpawnCounter = 0; // Counter to alternate left/right spawns
    
    // Static tracking for cross-direction collision avoidance
    private static List<Vector3> currentFrameSpawnPositions = new List<Vector3>();
    private static int lastSpawnFrame = -1;
    
    [Header("Collision Avoidance")]
    [Tooltip("Minimum Y distance between birds on opposite sides (prevents head-on collision)")]
    public static float crossDirectionMinDistance = 2f;
    [Tooltip("Minimum distance between birds on same side (prevents overlap)")]
    public static float sameSideMinDistance = 2f;
    
    // Instance-based cooldown tracking
    private static System.Collections.Generic.Dictionary<string, float> lastFireTimes = new System.Collections.Generic.Dictionary<string, float>();
    private string prefabKey;
    
    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _collider2D = GetComponent<Collider2D>();
        mainCamera = Camera.main;
        spawnTime = Time.time; // Record spawn time
        
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
        
        // Get animator component
        _animator = GetComponent<Animator>();
        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }
        
        // Make bird kinematic (we'll control movement manually)
        if (_rigidbody2D != null)
        {
            _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody2D.velocity = Vector2.zero;
        }
    }
    
    public void Initialize(Vector3 spawnPosition, Collider2D playerCollider, bool skipCooldownCheck = false)
    {
        // Get card reference to check enhancement
        ProjectileCards card = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
        
        bool hasVariant1History = false;
        bool hasVariant2History = false;

        // Check for enhanced variant FIRST to detect resets
        if (ProjectileCardLevelSystem.Instance != null && card != null)
        {
            enhancedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card);

            hasVariant1History = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 1);
            hasVariant2History = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 2);
        }

        isVariant12Active = hasVariant1History && hasVariant2History;
        
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
        
        // Enhanced variant already checked above
        Debug.Log($"<color=gold>ThunderBird Enhanced Variant: {enhancedVariant}</color>");
        
        // Store base values for instant modifier recalculation
        baseFlySpeed = flySpeed;
        baseStrikeZoneRadius = strikeZoneRadius;
        baseLifetimeSeconds = lifetimeSeconds;
        baseDamage = damage;
        baseScale = transform.localScale;
        
        // Get card-specific modifiers
        CardModifierStats modifiers = new CardModifierStats(); // Default values
        
        if (card != null)
        {
            modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
            Debug.Log($"<color=purple>ThunderBird using modifiers from {card.cardName}</color>");
        }

        // Check if Variant 2 is active (only when enhanced)
        // Variant selection is now driven purely by the numeric enhancedVariant
        // index from ProjectileCardLevelSystem: 1 = Dual Thunder, 2 = Slow Striker.
        isVariant2Active = (enhancedVariant == 2) || isVariant12Active;
        
        float enhancedStrikeRadiusMult = 1f;
        
        if (enhancedVariant == 1 && !isVariant2Active)
        {
            enhancedStrikeRadiusMult = 1f + enhancedStrikeRadiusIncrease;
            Debug.Log($"<color=gold>Enhanced Dual Thunder: Strike Radius x{enhancedStrikeRadiusMult}</color>");
        }
        else if (isVariant2Active)
        {
            Debug.Log($"<color=gold>Enhanced Variant 2: Slow Striker Mode</color>");
        }
        
        // CRITICAL: Variant 2 ALWAYS uses variant2BaseCooldown (highest priority)
        float baseCooldown = cooldown;
        if (isVariant12Active && variant12BaseCooldown > 0f)
        {
            baseCooldown = variant12BaseCooldown;

            if (card != null)
            {
                card.runtimeSpawnInterval = variant12BaseCooldown;
                Debug.Log($"<color=gold>Variant 1+2: Updated card.runtimeSpawnInterval to {variant12BaseCooldown}s (ProjectileSpawner will use this)</color>");
            }

            Debug.Log($"<color=gold>Variant 1+2: Using base cooldown {baseCooldown}s (IGNORING previous spawnInterval)</color>");
        }
        else if (isVariant2Active)
        {
            baseCooldown = variant2BaseCooldown;
            
            // CRITICAL: Update card's runtimeSpawnInterval so ProjectileSpawner uses variant2BaseCooldown
            if (card != null)
            {
                card.runtimeSpawnInterval = variant2BaseCooldown;
                Debug.Log($"<color=gold>Variant 2: Updated card.runtimeSpawnInterval to {variant2BaseCooldown}s (ProjectileSpawner will use this)</color>");
            }
            
            Debug.Log($"<color=gold>Variant 2: Using base cooldown {baseCooldown}s (IGNORING previous spawnInterval)</color>");
        }
        else if (card != null && card.runtimeSpawnInterval > 0f)
        {
            // Only use card spawnInterval if NOT Variant 2
            baseCooldown = card.runtimeSpawnInterval;
            Debug.Log($"<color=gold>ThunderBird using ProjectileCards spawnInterval: {baseCooldown:F2}s (overriding script cooldown: {cooldown:F2}s)</color>");
        }

        // If Enhanced Variant 1 is active, allow a dedicated base cooldown to
        // override the card/runtime value.
        if (enhancedVariant == 1 && variant1BaseCooldown > 0f && !isVariant12Active)
        {
            baseCooldown = variant1BaseCooldown;
            if (card != null)
            {
                card.runtimeSpawnInterval = Mathf.Max(0.1f, variant1BaseCooldown);
            }
            Debug.Log($"<color=gold>Variant 1: Using variant1BaseCooldown = {variant1BaseCooldown:F2}s as base cooldown</color>");
        }
        
        // Apply Variant 1 cooldown reduction
        if (enhancedVariant == 1)
        {
            baseCooldown *= (1f - variant1CooldownReduction);
            Debug.Log($"<color=gold>Variant 1 Cooldown Reduction: -{variant1CooldownReduction * 100f}% (new base: {baseCooldown:F2}s)</color>");
        }
        
        float finalLifetime = lifetimeSeconds + modifiers.lifetimeIncrease;
        
        float minCooldown = isVariant2Active ? 5f : 0.1f;
        float finalCooldown = Mathf.Max(minCooldown, baseCooldown * (1f - modifiers.cooldownReductionPercent / 100f)); // % from base
        int finalManaCost = Mathf.Max(0, Mathf.CeilToInt(manaCost * (1f - modifiers.manaCostReduction)));
        damage += modifiers.damageFlat; // FLAT damage bonus from projectile modifiers
        
        // Start from the same base speed calculation for all variants: prefab
        // speed plus any card-based speedIncrease. Variant 1 will then add its
        // own bonus ON TOP of this, so it never discards pre-existing
        // modifiers when switching from base → Variant 1.
        float baseVersionSpeed = baseFlySpeed + modifiers.speedIncrease;
        float currentStrikeZoneRadius = strikeZoneRadius; // Use local variable to avoid hiding field
        float baseRadiusForStrike = baseStrikeZoneRadius;

        if (enhancedVariant == 1)
        {
            baseRadiusForStrike = baseStrikeZoneRadius * enhancedStrikeRadiusMult;
            Debug.Log($"<color=gold>Enhanced Dual Thunder: Base Strike Radius {baseStrikeZoneRadius:F2} → {baseRadiusForStrike:F2} (+{enhancedStrikeRadiusIncrease * 100f:F1}% over base)</color>");

            // Apply Variant 1 speed bonus on top of the base-version speed so
            // any speedIncrease modifiers from cards are preserved.
            if (variant1SpeedBonus != 0f)
            {
                baseVersionSpeed += variant1SpeedBonus;
                Debug.Log($"<color=gold>Variant 1 Speed Bonus: +{variant1SpeedBonus} → BaseVersionSpeed={baseVersionSpeed:F2}</color>");
            }
        }

        float baseVersionStrikeZone = (baseRadiusForStrike + modifiers.strikeZoneRadiusBonus) * modifiers.strikeZoneRadiusMultiplier;
        
        Debug.Log($"<color=yellow>BASE VERSION CALCULATED: Speed={baseVersionSpeed:F2}, StrikeZone={baseVersionStrikeZone:F2}</color>");
        
        // Variant 2: Convert BASE modifiers using exchange rates
        if (isVariant2Active)
        {
            float speedIncrease = baseVersionSpeed - baseFlySpeed;
            float speedReduction = speedIncrease * variant2SpeedReductionRate;
            float targetSpeed = variant2Speed - speedReduction;
            flySpeed = Mathf.Max(variant2MinSpeed, targetSpeed);
            Debug.Log($"<color=cyan>Variant 2 Speed Exchange: Base +{speedIncrease:F1} → Variant 2 -{speedReduction:F1} (rate: {variant2SpeedReductionRate:F2}) = {flySpeed:F2}</color>");
            
            // Strike Zone: Base modifier is converted to interval reduction
            float strikeZoneIncrease = baseVersionStrikeZone - currentStrikeZoneRadius;
            float intervalReduction = strikeZoneIncrease * variant2StrikeZoneToIntervalRate;
            variant2StrikeInterval = variant2StrikeInterval - intervalReduction;
            variant2StrikeInterval = Mathf.Max(variant2MinStrikeInterval, variant2StrikeInterval);
            
            Debug.Log($"<color=cyan>Variant 2 Strike Zone Exchange: Base +{strikeZoneIncrease:F2} → Interval -{intervalReduction:F2}s (rate: {variant2StrikeZoneToIntervalRate:F2}) = {variant2StrikeInterval:F2}s</color>");
            
            // Instant first strike when spawning on-screen
            nextStrikeTime = Time.time; // Fire immediately
            Debug.Log($"<color=cyan>Variant 2: First strike INSTANT, then every {variant2StrikeInterval:F2}s</color>");
        }
        else
        {
            strikeZoneRadius = baseVersionStrikeZone * Mathf.Sqrt(modifiers.sizeMultiplier);
            Debug.Log($"<color=purple>BASE/Variant 1 Strike Zone: {strikeZoneRadius:F2}</color>");
            flySpeed = baseVersionSpeed;
            Debug.Log($"<color=purple>BASE/Variant 1 Speed: {flySpeed:F2}</color>");
        }
        
        // Apply size starting from BASE scale, then variant-specific size multiplier,
        // and finally the generic card size multiplier. Variant 1/2 size multipliers
        // should NOT stack on top of an already size-modified scale.
        float variantSizeMultiplier = 1f;
        if (enhancedVariant == 1 && variant1SizeMultiplier != 1f)
        {
            variantSizeMultiplier = variant1SizeMultiplier;
        }
        else if (isVariant2Active && variant2SizeMultiplier != 1f)
        {
            variantSizeMultiplier = variant2SizeMultiplier;
        }

        float finalSizeMultiplier = variantSizeMultiplier * modifiers.sizeMultiplier;

        if (!Mathf.Approximately(finalSizeMultiplier, 1f))
        {
            transform.localScale = baseScale * finalSizeMultiplier;

            // Scale collider using utility with colliderSizeOffset
            ColliderScaler.ScaleCollider(_collider2D, finalSizeMultiplier, colliderSizeOffset);
        }
        else
        {
            // Revert to base scale when neither variant nor modifiers change size.
            transform.localScale = baseScale;
            // Leave collider at its prefab/base size when no visual scaling is applied.
        }

        // After resolving the visual size, keep the strike zone offset's Y in sync
        // with the bird's vertical scale using a 1 : 1.5 ratio (scaleY : offsetY).
        float scaleY = transform.localScale.y;
        strikeZoneOffset = new Vector2(strikeZoneOffset.x, scaleY * 1.5f);
        
        Debug.Log($"<color=purple>ThunderBird Modifiers Applied: Speed=+{modifiers.speedIncrease:F2}, Size={modifiers.sizeMultiplier:F2}x, DamageFlat=+{modifiers.damageFlat:F1}, Lifetime=+{modifiers.lifetimeIncrease:F2}s</color>");
        
        // Cache PlayerStats for per-enemy damage calculation and store damage after card modifiers
        cachedPlayerStats = null;
        if (playerCollider != null)
        {
            cachedPlayerStats = playerCollider.GetComponent<PlayerStats>();
        }
        if (cachedPlayerStats == null)
        {
            cachedPlayerStats = FindObjectOfType<PlayerStats>();
        }

        // Damage here includes all card-based modifiers and serves as the base
        // value before PlayerStats and favour effects are applied per enemy.
        baseDamageAfterCards = damage;
        
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

        // Generate key based ONLY on projectile type (so all ThunderBirds share same cooldown)
        prefabKey = "ThunderBird";
        
        // Only check cooldown/mana for first projectile in multi-spawn
        if (!skipCooldownCheck)
        {
            // Check cooldown
            if (!bypassEnhancedFirstSpawnCooldown && lastFireTimes.ContainsKey(prefabKey))
            {
                if (Time.time - lastFireTimes[prefabKey] < finalCooldown)
                {
                    Debug.Log($"<color=yellow>ThunderBird on cooldown - {Time.time - lastFireTimes[prefabKey]:F2}s / {finalCooldown}s</color>");
                    Destroy(gameObject);
                    return;
                }
            }
            
            // Check mana
            PlayerMana playerMana = FindObjectOfType<PlayerMana>();
            if (playerMana != null && !playerMana.Spend(finalManaCost))
            {
                Debug.Log($"Not enough mana for ThunderBird (cost: {finalManaCost})");
                Destroy(gameObject);
                return;
            }
            
            // Record fire time
            lastFireTimes[prefabKey] = Time.time;
        }
        else
        {
            Debug.Log($"<color=gold>ThunderBird: Skipping cooldown/mana check (multi-projectile spawn)</color>");
        }
        
        // Ignore collision with player
        if (_collider2D != null && playerCollider != null)
        {
            Physics2D.IgnoreCollision(_collider2D, playerCollider, true);
        }
        
        // Determine spawn position and direction
        if (minPos != null && maxPos != null)
        {
            if (isVariant2Active && !isVariant12Active)
            {
                // ENHANCED VARIANT 2: Slow Striker - spawn at CENTER Y, random left/right
                float minY = minPos.position.y;
                float maxY = maxPos.position.y;
                float centerY = (minY + maxY) / 2f;
                
                // 50/50 spawn left or right
                bool spawnLeft = Random.value < 0.5f;
                float spawnX = spawnLeft ? minPos.position.x : maxPos.position.x;
                isMovingRight = spawnLeft; // Left spawns move right, right spawns move left
                spawnedFromLeft = spawnLeft;
                
                transform.position = new Vector3(spawnX, centerY, spawnPosition.z);
                
                // NOTE: Variant 2 size is now applied exclusively via the unified
                // baseScale → variantSizeMultiplier → modifiers.sizeMultiplier
                // pipeline above, so we do NOT scale transform.localScale here.
                
                // Apply Variant 2 animation speed decrease
                if (_animator != null)
                {
                    _animator.speed = variant2AnimationSpeed;
                }
                
                string side = spawnLeft ? "LEFT" : "RIGHT";
                Debug.Log($"<color=gold>Variant 2: Spawned at {side} side, center Y={centerY:F2}, AnimSpeed={variant2AnimationSpeed}x</color>");
                
                // Flip sprite based on direction
                if (spriteRenderer != null)
                {
                    spriteRenderer.flipX = !isMovingRight;
                }
            }
            else if (enhancedVariant == 1 || isVariant12Active)
            {
                // ENHANCED VARIANT 1: Multi-bird spawn with zone division
                // Spawner handles multiple birds based on projectileCount
                // Each bird alternates between left/right and top/bottom zones
                
                // Calculate Y-axis midpoint (dividing line)
                float minY = minPos.position.y;
                float maxY = maxPos.position.y;
                float midY = (minY + maxY) / 2f;
                
                // Use static counter to alternate between left and right
                // This ensures birds don't spawn on same side
                bool isLeftBird = (variant1SpawnCounter % 2 == 0);
                variant1SpawnCounter++;
                
                // For first spawn after enhanced unlock, randomize both sides
                if (isFirstDualSpawn)
                {
                    leftBirdSpawnTop = Random.value < 0.5f;
                    rightBirdSpawnTop = !leftBirdSpawnTop; // Always opposite
                    isFirstDualSpawn = false;
                    string leftZone = leftBirdSpawnTop ? "TOP" : "BOTTOM";
                    string rightZone = rightBirdSpawnTop ? "TOP" : "BOTTOM";
                    Debug.Log($"<color=gold>First Multi Spawn: Left={leftZone}, Right={rightZone}</color>");
                }
                
                // Determine spawn zone based on which bird this is and alternation pattern
                bool spawnInTopZone;
                float spawnX;
                
                if (isLeftBird)
                {
                    // Left bird
                    spawnX = minPos.position.x;
                    spawnInTopZone = leftBirdSpawnTop;
                    isMovingRight = true; // Left bird moves right
                    spawnedFromLeft = true; // Spawned from left (LEFT to RIGHT)
                    
                    // Alternate for next spawn
                    leftBirdSpawnTop = !leftBirdSpawnTop;
                }
                else
                {
                    // Right bird
                    spawnX = maxPos.position.x;
                    spawnInTopZone = rightBirdSpawnTop;
                    isMovingRight = false; // Right bird moves left
                    spawnedFromLeft = false; // Spawned from right (RIGHT to LEFT)
                    
                    // Alternate for next spawn
                    rightBirdSpawnTop = !rightBirdSpawnTop;
                }
                
                // Calculate spawn Y within the assigned zone with collision avoidance
                float spawnY;
                int maxAttempts = 10;
                int attempt = 0;
                bool validPosition = false;
                
                do
                {
                    if (spawnInTopZone)
                    {
                        spawnY = Random.Range(midY, maxY); // Top half
                    }
                    else
                    {
                        spawnY = Random.Range(minY, midY); // Bottom half
                    }
                    
                    // Check if this position overlaps with existing birds
                    Vector3 testPos = new Vector3(spawnX, spawnY, spawnPosition.z);
                    validPosition = !CheckBirdOverlap(testPos);
                    attempt++;
                    
                } while (!validPosition && attempt < maxAttempts);
                
                if (!validPosition)
                {
                    // Fallback: use the last calculated position anyway
                    Debug.LogWarning($"<color=yellow>Could not find non-overlapping position after {maxAttempts} attempts, using last position</color>");
                }
                
                Vector3 finalPosition = new Vector3(spawnX, spawnY, spawnPosition.z);
                transform.position = finalPosition;

                if (isVariant12Active)
                {
                    // Stacked V1+V2 should respect Variant 2's animation speed
                    // so the bird visually matches the slow-striker cadence.
                    if (_animator != null)
                    {
                        _animator.speed = variant2AnimationSpeed;
                    }

                    isVariant12TopBird = spawnInTopZone;
                }
                
                // Add this position to the current frame's spawn list
                currentFrameSpawnPositions.Add(finalPosition);
                
                string birdSide = isLeftBird ? "LEFT" : "RIGHT";
                string zoneType = spawnInTopZone ? "TOP" : "BOTTOM";
                Debug.Log($"<color=gold>Multi Thunder: {birdSide} bird #{variant1SpawnCounter} spawned in {zoneType} zone at Y={spawnY:F2} (mid={midY:F2})</color>");
                
                // Flip sprite based on direction
                if (spriteRenderer != null)
                {
                    spriteRenderer.flipX = !isMovingRight; // Flip when moving left
                }
                
                // NOTE: Spawner handles multiple birds - we don't spawn additional birds here
            }
            else
            {
                // BASIC VARIANT: Original single bird spawn with collision avoidance
                // CRITICAL: Use GetInstanceID() to ensure each bird gets unique random values
                Random.InitState(System.DateTime.Now.Millisecond + GetInstanceID());
                
                // 50/50 chance to spawn from left or right
                bool spawnFromLeft = Random.value < 0.5f;
                
                float spawnX = spawnFromLeft ? minPos.position.x : maxPos.position.x;
                float spawnY;
                
                // Try to find non-overlapping position
                int maxAttempts = 10;
                int attempt = 0;
                bool validPosition = false;
                
                do
                {
                    spawnY = Random.Range(minPos.position.y, maxPos.position.y);
                    Vector3 testPos = new Vector3(spawnX, spawnY, spawnPosition.z);
                    validPosition = !CheckBirdOverlap(testPos);
                    attempt++;
                } while (!validPosition && attempt < maxAttempts);
                
                if (!validPosition)
                {
                    Debug.LogWarning($"<color=yellow>Base ThunderBird: Could not find non-overlapping position after {maxAttempts} attempts</color>");
                }
                
                transform.position = new Vector3(spawnX, spawnY, spawnPosition.z);
                
                // Set direction based on spawn side
                isMovingRight = spawnFromLeft; // If spawned from left, move right
                spawnedFromLeft = spawnFromLeft; // Track spawn direction for offset calculation
                
                // Flip sprite if moving left
                if (spriteRenderer != null)
                {
                    spriteRenderer.flipX = !isMovingRight; // Flip when moving left
                }
            }
        }
        else
        {
            // Fallback to provided spawn position
            transform.position = spawnPosition;
            isMovingRight = true;
        }
        
        // Play fly sound
        if (flyClip != null && _audioSource != null)
        {
            _audioSource.clip = flyClip;
            _audioSource.volume = flyVolume;
            _audioSource.loop = true;
            _audioSource.Play();
        }
        
        // Initialize Variant 2 strike timing (each bird gets its own timing)
        if (isVariant2Active)
        {
            // Start first strike after interval
            nextStrikeTime = Time.time + variant2StrikeInterval;
            Debug.Log($"<color=gold>Variant 2: Bird initialized, first strike at {nextStrikeTime:F2}s (interval: {variant2StrikeInterval}s)</color>");
        }
        
        // Start flying and damage detection
        StartCoroutine(FlyRoutine(finalLifetime));
        StartCoroutine(DamageDetectionRoutine());
    }
    
    private IEnumerator FlyRoutine(float lifetime)
    {
        float elapsedTime = 0f;
        
        // Determine grace period based on active variant
        float gracePeriod = baseGracePeriod;
        if (isVariant2Active)
        {
            gracePeriod = variant2GracePeriod;
        }
        else if (enhancedVariant == 1)
        {
            gracePeriod = variant1GracePeriod;
        }
        
        while (elapsedTime < lifetime)
        {
            // Move in the determined direction
            Vector2 direction = isMovingRight ? Vector2.right : Vector2.left;
            _rigidbody2D.velocity = direction * flySpeed;
            
            // Check if off-screen (but only after grace period)
            if (Time.time - spawnTime > gracePeriod && IsOffScreen())
            {
                Debug.Log($"<color=purple>ThunderBird destroyed - went offscreen after grace period ({gracePeriod:F1}s)</color>");
                Destroy(gameObject);
                yield break;
            }
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Destroy after lifetime
        Destroy(gameObject);
    }
    
    private IEnumerator DamageDetectionRoutine()
    {
        while (true)
        {
            // Determine if the bird itself is currently in a damageable (on-screen) area.
            // If not, we skip all strike and pending-damage processing until it comes back
            // on-screen. This prevents any strikes or delayed damage from occurring while
            // the Thunderbird projectile is off-camera.
            bool birdCanDamageHere = OffscreenDamageChecker.CanTakeDamage(transform.position);

            // Variant 2: Periodic strikes across entire camera view
            if (isVariant2Active)
            {
                if (!birdCanDamageHere)
                {
                    yield return new WaitForSeconds(0.1f); // Check again shortly
                    continue;
                }
                // CRITICAL: First strike happens only when bird is considered ON-SCREEN
                // according to camera view plus variant2CameraOffset. After that, respect
                // normal cooldown but still never strike while off-screen.
                bool shouldStrike = false;

                if (!hasPerformedFirstStrike && !IsOffScreenForVariant2())
                {
                    if (isVariant12Active && !isVariant12TopBird)
                    {
                        hasPerformedFirstStrike = true;
                        nextStrikeTime = Time.time + (variant2StrikeInterval * 0.5f);
                        if (debugVariant2Logging)
                        {
                            Debug.Log($"<color=gold>Variant 1+2: Bottom bird scheduled first strike in {variant2StrikeInterval * 0.5f:F2}s</color>");
                        }
                    }
                    else
                    {
                        // First strike - immediate when entering on-screen region
                        shouldStrike = true;
                        hasPerformedFirstStrike = true;
                        nextStrikeTime = Time.time + variant2StrikeInterval;
                        if (debugVariant2Logging)
                        {
                            Debug.Log($"<color=gold>Variant 2: FIRST STRIKE (on camera view with offset)!</color>");
                        }
                    }
                }
                else if (Time.time >= nextStrikeTime)
                {
                    // Normal strikes - respect cooldown
                    shouldStrike = true;
                    nextStrikeTime = Time.time + variant2StrikeInterval;
                }

                if (shouldStrike)
                {
                    // Final safety: never strike while off-screen
                    if (!IsOffScreenForVariant2())
                    {
                        PerformVariant2Strike();
                    }
                    else
                    {
                        if (debugVariant2Logging)
                        {
                            Debug.Log($"<color=yellow>Variant 2: Bird off-screen (camera check), skipping strike</color>");
                        }
                    }
                }
                
                // CRITICAL: Apply pending damage for V2
                List<GameObject> v2EnemiesToDamage = new List<GameObject>();
                foreach (var kvp in pendingDamageEnemies)
                {
                    if (Time.time >= kvp.Value)
                    {
                        v2EnemiesToDamage.Add(kvp.Key);
                    }
                }
                
                if (v2EnemiesToDamage.Count > 0)
                {
                    if (debugVariant2Logging)
                    {
                        Debug.Log($"<color=magenta>��� V2 APPLYING PENDING DAMAGE: {v2EnemiesToDamage.Count} enemies ready ���</color>");
                    }
                    
                    // Calculate strike center for V2 (bird's current position)
                    Vector2 v2StrikeCenter = (Vector2)transform.position;
                    
                    foreach (GameObject enemy in v2EnemiesToDamage)
                    {
                        if (enemy == null)
                        {
                            pendingDamageEnemies.Remove(enemy);
                            continue;
                        }

                        IDamageable damageable;
                        EnemyHealth enemyHealth;
                        GameObject enemyObject;

                        if (!TryGetCachedEnemyData(enemy, out damageable, out enemyHealth, out enemyObject) || damageable == null || !damageable.IsAlive)
                        {
                            pendingDamageEnemies.Remove(enemy);
                            continue;
                        }

                        // Check if enemy is within damageable area (on-screen)
                        if (!OffscreenDamageChecker.CanTakeDamage(enemy.transform.position))
                        {
                            if (debugVariant2Logging)
                            {
                                Debug.Log($"<color=yellow>    � V2: Enemy offscreen, cannot take damage</color>");
                            }
                            pendingDamageEnemies.Remove(enemy);
                            continue;
                        }

                        // Use enemy's actual position
                        Vector3 enemyPosition = enemy.transform.position;
                        Vector3 hitNormal = (transform.position - enemyPosition).normalized;

                        // Damage has already had card modifiers applied; now apply PlayerStats
                        // and favour effects per enemy via PlayerDamageHelper.
                        float baseDamageForEnemy = baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
                        float finalDamage = baseDamageForEnemy;

                        GameObject damageTarget = enemyObject != null ? enemyObject : enemy;

                        if (cachedPlayerStats != null)
                        {
                            finalDamage = PlayerDamageHelper.ComputeContinuousProjectileDamage(cachedPlayerStats, damageTarget, baseDamageForEnemy, gameObject);
                        }

                        if (debugVariant2Logging)
                        {
                            Debug.Log($"<color=green>    � V2 APPLYING DAMAGE: {finalDamage:F1} to {enemy.name}</color>");
                        }

                        // Tag EnemyHealth so it can render the numeric
                        // damage using the Thunder color.
                        if (enemyHealth != null)
                        {
                            enemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Thunder);
                        }

                        damageable.TakeDamage(finalDamage, enemyPosition, hitNormal);

                        // Remove from pending (V2 can hit same enemy again next strike)
                        pendingDamageEnemies.Remove(enemy);
                    }
                }
                
                yield return new WaitForSeconds(0.1f); // Check every 0.1s
                continue;
            }
            
            // If the bird is off-screen, do not process normal/Variant 1 strikes or
            // apply any pending damage.
            if (!birdCanDamageHere)
            {
                yield return new WaitForFixedUpdate();
                continue;
            }
            
            // Normal/Variant 1: Single-hit strike zone
            // Calculate strike zone center with offset
            Vector2 strikeCenter = (Vector2)transform.position + strikeZoneOffset;
            
            // Find all enemies in strike zone
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(strikeCenter, strikeZoneRadius, enemyLayer);
            
            if (hitColliders.Length > 0)
            {
                Debug.Log($"<color=yellow>ThunderBird: Found {hitColliders.Length} enemies in strike zone</color>");
            }
            
            foreach (Collider2D hitCollider in hitColliders)
            {
                // Skip if already damaged this enemy
                if (damagedEnemies.Contains(hitCollider.gameObject))
                {
                    continue;
                }
                
                // Skip if already pending damage for this enemy
                if (pendingDamageEnemies.ContainsKey(hitCollider.gameObject))
                {
                    continue;
                }
                
                IDamageable damageable = hitCollider.GetComponent<IDamageable>() ?? hitCollider.GetComponentInParent<IDamageable>();
                
                if (damageable == null)
                {
                    Debug.LogWarning($"<color=yellow>ThunderBird: {hitCollider.gameObject.name} has no IDamageable component</color>");
                }
                
                if (damageable != null && damageable.IsAlive)
                {
                    // Only schedule pending damage if this enemy is currently allowed to
                    // take damage (i.e., on-screen according to the shared rules). This
                    // ensures that enemies which were offscreen at strike time don't
                    // later receive delayed damage without having shown a strike effect.
                    if (!OffscreenDamageChecker.CanTakeDamage(hitCollider.transform.position))
                    {
                        Debug.Log($"<color=yellow>ThunderBird: Enemy {hitCollider.gameObject.name} offscreen at strike time, skipping pending damage</color>");
                        continue;
                    }

                    // Add to pending damage with timestamp
                    pendingDamageEnemies[hitCollider.gameObject] = Time.time + damageDelay;
                    Debug.Log($"<color=yellow>ThunderBird: {hitCollider.gameObject.name} will take damage in {damageDelay}s</color>");
                    
                    // Play strike effect early if timing adjustment is positive
                    if (strikeEffectTimingAdjustment > 0f && strikeEffectPrefab != null)
                    {
                        // Use enemy's actual position, not closest point to strike center
                        Vector3 anchorPosition = GetStrikeEffectAnchorPosition(hitCollider.gameObject, hitCollider);
                        Vector2 effectOffset = GetStrikeEffectOffset(hitCollider.gameObject);
                        Vector3 effectPosition = anchorPosition + (Vector3)effectOffset;
                        
                        GameObject effect = Instantiate(strikeEffectPrefab, effectPosition, Quaternion.identity);
                        
                        // Parent effect to enemy so it follows them
                        effect.transform.SetParent(hitCollider.transform, true);
                        
                        if (strikeEffectSizeMultiplier != 1f)
                        {
                            effect.transform.localScale *= strikeEffectSizeMultiplier;
                        }
                        
                        Destroy(effect, strikeEffectDuration);
                        Debug.Log($"<color=cyan>Strike effect played {strikeEffectTimingAdjustment}s early and attached to enemy</color>");
                    }
                }
            }
            
            // Process pending damage enemies
            List<GameObject> enemiesToDamage = new List<GameObject>();
            foreach (var kvp in pendingDamageEnemies)
            {
                if (Time.time >= kvp.Value)
                {
                    enemiesToDamage.Add(kvp.Key);
                }
            }
            
            // Apply damage to enemies whose delay has passed
            Debug.Log($"<color=magenta> APPLYING PENDING DAMAGE: {enemiesToDamage.Count} enemies ready </color>");
            
            foreach (GameObject enemy in enemiesToDamage)
            {
                // Check if enemy is destroyed/null first
                if (enemy == null)
                {
                    Debug.LogWarning($"<color=yellow>  Skipping enemy (destroyed/null)</color>");
                    continue;
                }
                
                if (!pendingDamageEnemies.ContainsKey(enemy))
                {
                    Debug.LogWarning($"<color=yellow>  Skipping enemy (not in pending): {enemy.name}</color>");
                    continue;
                }
                
                Debug.Log($"<color=magenta>  → Processing pending damage for: {enemy.name}</color>");
                
                IDamageable damageable = enemy.GetComponent<IDamageable>() ?? enemy.GetComponentInParent<IDamageable>();
                
                if (damageable == null)
                {
                    Debug.LogError($"<color=red>     No IDamageable component!</color>");
                    continue;
                }
                
                if (!damageable.IsAlive)
                {
                    Debug.Log($"<color=yellow>     Enemy is dead, skipping</color>");
                    continue;
                }
                
                if (damageable != null && damageable.IsAlive)
                {
                    // Check if enemy is within damageable area (on-screen)
                    if (!OffscreenDamageChecker.CanTakeDamage(enemy.transform.position))
                    {
                        Debug.Log($"<color=yellow>     Enemy offscreen, cannot take damage</color>");
                        continue; // Skip this enemy
                    }
                    
                    // Use enemy's actual position (they may have moved outside strike zone)
                    Vector3 enemyPosition = enemy.transform.position;
                    Vector3 hitNormal = (strikeCenter - (Vector2)enemyPosition).normalized;
                    
                    // Damage has already had card modifiers applied; now apply PlayerStats
                    // and favour effects per enemy via PlayerDamageHelper.
                    float baseDamageForEnemy = baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
                    float finalDamage = baseDamageForEnemy;

                    Component damageableComponent = damageable as Component;
                    GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : enemy;

                    if (cachedPlayerStats != null)
                    {
                        finalDamage = PlayerDamageHelper.ComputeContinuousProjectileDamage(cachedPlayerStats, enemyObject, baseDamageForEnemy, gameObject);
                    }

                    // Tag EnemyHealth with Thunder so its own damage-number
                    // pipeline uses the correct color for this strike.
                    EnemyHealth enemyHealth1 = enemy.GetComponent<EnemyHealth>() ?? enemy.GetComponentInParent<EnemyHealth>();
                    if (enemyHealth1 != null)
                    {
                        enemyHealth1.SetLastIncomingDamageType(DamageNumberManager.DamageType.Thunder);
                    }

                    Debug.Log($"<color=green>    ￾ APPLYING DAMAGE: {finalDamage:F1} to {enemy.name}</color>");
                    damageable.TakeDamage(finalDamage, enemyPosition, hitNormal);
                    
                    // Mark this enemy as damaged (ONLY for Variant 1, not Variant 2)
                    // Variant 2 needs to hit enemies multiple times
                    if (!isVariant2Active)
                    {
                        damagedEnemies.Add(enemy);
                        Debug.Log($"<color=cyan>    → Added to damagedEnemies (Variant 1)</color>");
                    }
                    else
                    {
                        Debug.Log($"<color=cyan>    → NOT added to damagedEnemies (Variant 2 - can hit again)</color>");
                    }
                    
                    // Spawn strike effect at enemy's current position (only if not played early)
                    if (strikeEffectPrefab != null && strikeEffectTimingAdjustment <= 0f)
                    {
                        // Apply per-enemy offset
                        Vector3 anchorPosition = GetStrikeEffectAnchorPosition(enemy, null);
                        Vector2 effectOffset = GetStrikeEffectOffset(enemy);
                        Vector3 effectPosition = anchorPosition + (Vector3)effectOffset;
                        
                        // Delay effect if timing adjustment is negative
                        if (strikeEffectTimingAdjustment < 0f)
                        {
                            StartCoroutine(SpawnDelayedEffect(enemy, effectOffset, Mathf.Abs(strikeEffectTimingAdjustment)));
                        }
                        else
                        {
                            GameObject effect = Instantiate(strikeEffectPrefab, effectPosition, Quaternion.identity);
                            
                            // Parent effect to enemy so it follows them
                            effect.transform.SetParent(enemy.transform, true);
                            
                            // Apply size multiplier
                            if (strikeEffectSizeMultiplier != 1f)
                            {
                                effect.transform.localScale *= strikeEffectSizeMultiplier;
                            }
                            
                            Destroy(effect, strikeEffectDuration);
                        }
                    }
                    
                    // Play strike sound at enemy's current position
                    if (strikeClip != null)
                    {
                        AudioSource.PlayClipAtPoint(strikeClip, enemyPosition, strikeVolume);
                    }
                }
                
                pendingDamageEnemies.Remove(enemy);
            }
            
            yield return new WaitForFixedUpdate();
        }
    }
    
    private IEnumerator SpawnDelayedEffect(GameObject enemy, Vector2 effectOffset, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (strikeEffectPrefab != null && enemy != null)
        {
            // Use enemy's current position (they may have moved)
            Vector3 anchorPosition = GetStrikeEffectAnchorPosition(enemy, null);
            Vector3 effectPosition = anchorPosition + (Vector3)effectOffset;
            
            GameObject effect = Instantiate(strikeEffectPrefab, effectPosition, Quaternion.identity);
            
            // Parent effect to enemy so it follows them
            effect.transform.SetParent(enemy.transform, true);
            
            if (strikeEffectSizeMultiplier != 1f)
            {
                effect.transform.localScale *= strikeEffectSizeMultiplier;
            }
            
            Destroy(effect, strikeEffectDuration);
            Debug.Log($"<color=cyan>Strike effect played {delay}s late and attached to enemy at their current position</color>");
        }
    }
    
    private IEnumerator SpawnDelayedVariant2Effect(GameObject enemy, Vector2 effectOffset, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (strikeEffectPrefab != null && enemy != null)
        {
            // Use enemy's current position (they may have moved)
            Vector3 anchorPosition = GetStrikeEffectAnchorPosition(enemy, null);
            Vector3 effectPosition = anchorPosition + (Vector3)effectOffset;
            
            GameObject effect = Instantiate(strikeEffectPrefab, effectPosition, Quaternion.identity);
            
            // Parent effect to enemy so it follows them
            effect.transform.SetParent(enemy.transform, true);
            
            // Use Variant 2 unique strike effect size multiplier
            effect.transform.localScale *= variant2StrikeEffectSizeMultiplier;
            
            Destroy(effect, strikeEffectDuration);
            Debug.Log($"<color=cyan>Variant 2: Strike effect played {delay}s late</color>");
        }
    }
    
    /// <summary>
    /// Variant 2: Perform periodic strike on all on-screen enemies
    /// </summary>
    private void PerformVariant2Strike()
    {
        if (debugVariant2Logging)
        {
            Debug.Log($"<color=cyan></color>");
            Debug.Log($"<color=cyan>Thunderbird V2 STRIKE at Time={Time.time:F2}s</color>");
            Debug.Log($"<color=cyan>  Bird: {gameObject.name}</color>");
            Debug.Log($"<color=cyan>  Next Strike Time: {nextStrikeTime:F2}s</color>");
            Debug.Log($"<color=cyan>  Damage: {damage:F2}</color>");
            Debug.Log($"<color=cyan>  Damage Delay: {damageDelay:F2}s</color>");
        }
        
        if (mainCamera == null)
        {
            Debug.LogError($"<color=red>  ERROR: mainCamera is NULL!</color>");
            return;
        }
        
        // Get camera bounds
        float camHeight = mainCamera.orthographicSize * 2f;
        float camWidth = camHeight * mainCamera.aspect;
        Vector3 camPos = mainCamera.transform.position;
        
        if (debugVariant2Logging)
        {
            Debug.Log($"<color=cyan>  Camera: Size={camHeight:F2}x{camWidth:F2}, Pos={camPos}</color>");
        }
        
        // Find all enemies in the entire camera view
        Collider2D[] allEnemies = Physics2D.OverlapBoxAll(
            camPos,
            new Vector2(camWidth, camHeight),
            0f,
            enemyLayer
        );
        
        if (debugVariant2Logging)
        {
            Debug.Log($"<color=cyan>  Found {allEnemies.Length} enemy colliders in camera view</color>");
        }
        
        int hitCount = 0;
        int onScreenCount = 0;
        effectsSpawnedThisStrike = 0;
        damageNumbersShownThisStrike = 0;
        foreach (Collider2D enemyCollider in allEnemies)
        {
            // Check if enemy is actually on-screen
            Vector3 enemyViewport = mainCamera.WorldToViewportPoint(enemyCollider.transform.position);
            bool enemyOnScreen = enemyViewport.x >= 0f && enemyViewport.x <= 1f &&
                                 enemyViewport.y >= 0f && enemyViewport.y <= 1f &&
                                 enemyViewport.z > 0f;
            
            if (!enemyOnScreen)
            {
                continue; // Skip off-screen enemies
            }
            
            onScreenCount++;
            
            GameObject enemyRoot = enemyCollider.gameObject;

            IDamageable damageable;
            EnemyHealth enemyHealth;
            GameObject damageObject;

            if (!TryGetCachedEnemyData(enemyRoot, out damageable, out enemyHealth, out damageObject) || damageable == null)
            {
                if (debugVariant2Logging)
                {
                    Debug.LogWarning($"<color=yellow>  Enemy {enemyRoot.name} has NO IDamageable!</color>");
                }
                continue;
            }

            if (!damageable.IsAlive)
            {
                if (debugVariant2Logging)
                {
                    Debug.Log($"<color=yellow>  Enemy {enemyRoot.name} is DEAD, skipping</color>");
                }
                continue;
            }

            Vector3 enemyPosition = enemyCollider.transform.position;
            
            // Respect damageDelay - add to pending damage
            if (damageDelay > 0f)
            {
                // Only schedule pending damage if this enemy is currently eligible
                // for damage (on-screen / within allowed damage area).
                if (!OffscreenDamageChecker.CanTakeDamage(enemyPosition))
                {
                    if (debugVariant2Logging)
                    {
                        Debug.Log($"<color=yellow>Variant 2: Enemy {enemyRoot.name} offscreen at strike time, skipping pending damage</color>");
                    }
                    continue;
                }

                if (!pendingDamageEnemies.ContainsKey(enemyRoot))
                {
                    pendingDamageEnemies[enemyRoot] = Time.time + damageDelay;
                    if (debugVariant2Logging)
                    {
                        Debug.Log($"<color=yellow>Variant 2: {enemyRoot.name} will take damage in {damageDelay}s</color>");
                    }
                    
                    // Play strike effect early if timing adjustment is positive
                    if (strikeEffectTimingAdjustment > 0f && strikeEffectPrefab != null && effectsSpawnedThisStrike < variant2MaxEffectsPerStrike)
                    {
                        Vector3 anchorPosition = GetStrikeEffectAnchorPosition(enemyRoot, enemyCollider);
                        Vector2 effectOffset = GetStrikeEffectOffset(enemyRoot);
                        Vector3 effectPosition = anchorPosition + (Vector3)effectOffset;
                        
                        GameObject effect = Instantiate(strikeEffectPrefab, effectPosition, Quaternion.identity);
                        effect.transform.SetParent(enemyCollider.transform, true);
                        
                        // Use Variant 2 unique strike effect size multiplier
                        effect.transform.localScale *= variant2StrikeEffectSizeMultiplier;
                        
                        Destroy(effect, strikeEffectDuration);
                        effectsSpawnedThisStrike++;
                        if (debugVariant2Logging)
                        {
                            Debug.Log($"<color=cyan>Variant 2: Strike effect played {strikeEffectTimingAdjustment}s early</color>");
                        }
                    }
                }
            }
            else
            {
                // Instant damage (no delay)
                Vector3 hitNormal = (transform.position - enemyPosition).normalized;
                
                // Damage has already had card modifiers applied; now apply PlayerStats
                // and favour effects per enemy via PlayerDamageHelper.
                float baseDamageForEnemy = baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
                float finalDamage = baseDamageForEnemy;

                GameObject enemyObject = damageObject != null ? damageObject : enemyRoot;

                if (cachedPlayerStats != null)
                {
                    finalDamage = PlayerDamageHelper.ComputeContinuousProjectileDamage(cachedPlayerStats, enemyObject, baseDamageForEnemy, gameObject);
                }

                // Tag EnemyHealth with Thunder so EnemyHealth handles the
                // numeric popup with the correct elemental color.
                EnemyHealth targetEnemyHealth = enemyHealth;
                if (targetEnemyHealth == null && enemyObject != null)
                {
                    targetEnemyHealth = enemyObject.GetComponent<EnemyHealth>() ?? enemyObject.GetComponentInParent<EnemyHealth>();
                }

                if (targetEnemyHealth != null)
                {
                    targetEnemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Thunder);
                }

                damageable.TakeDamage(finalDamage, enemyPosition, hitNormal);
                hitCount++;
                
                // Spawn strike effect (respecting timing adjustment)
                if (strikeEffectPrefab != null && effectsSpawnedThisStrike < variant2MaxEffectsPerStrike)
                {
                    Vector3 anchorPosition = GetStrikeEffectAnchorPosition(enemyRoot, enemyCollider);
                    Vector2 effectOffset = GetStrikeEffectOffset(enemyRoot);
                    Vector3 effectPosition = anchorPosition + (Vector3)effectOffset;
                    
                    if (strikeEffectTimingAdjustment < 0f)
                    {
                        // Delayed effect
                        StartCoroutine(SpawnDelayedVariant2Effect(enemyRoot, effectOffset, Mathf.Abs(strikeEffectTimingAdjustment)));
                    }
                    else
                    {
                        GameObject effect = Instantiate(strikeEffectPrefab, effectPosition, Quaternion.identity);
                        effect.transform.SetParent(enemyCollider.transform, true);
                        
                        // Use Variant 2 unique strike effect size multiplier
                        effect.transform.localScale *= variant2StrikeEffectSizeMultiplier;
                        
                        Destroy(effect, strikeEffectDuration);
                        effectsSpawnedThisStrike++;
                    }
                }
            }
        }
        
        if (debugVariant2Logging)
        {
            Debug.Log($"<color=cyan>  On-Screen Enemies: {onScreenCount}</color>");
            Debug.Log($"<color=cyan>  Enemies Hit: {hitCount}</color>");
            Debug.Log($"<color=cyan>  Pending Damage Enemies: {pendingDamageEnemies.Count}</color>");
        }
        
        if (hitCount > 0)
        {
            if (debugVariant2Logging)
            {
                Debug.Log($"<color=yellow>Variant 2: Periodic strike hit {hitCount} enemies</color>");
            }
            
            // Play strike sound
            if (strikeClip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(strikeClip, strikeVolume);
            }
        }
        else if (pendingDamageEnemies.Count > 0)
        {
            if (debugVariant2Logging)
            {
                Debug.Log($"<color=yellow>Variant 2: {pendingDamageEnemies.Count} enemies added to pending damage (will apply after {damageDelay}s delay)</color>");
            }
        }
        else
        {
            if (debugVariant2Logging)
            {
                Debug.LogWarning($"<color=red>  WARNING: No enemies hit! On-screen: {onScreenCount}, Pending: {pendingDamageEnemies.Count}</color>");
            }
        }
        
        // DON'T clear pending damage here! Let DamageDetectionRoutine apply it after delay
        // For V2, we clear in DamageDetectionRoutine AFTER applying damage
        if (debugVariant2Logging)
        {
            Debug.Log($"<color=cyan></color>");
        }
    }
    
    private bool IsOffScreen()
    {
        // If we don't have spawn bounds yet, fall back to not destroying
        if (minPos == null || maxPos == null)
        {
            return false;
        }

        // World-space boundaries taken directly from spawn markers (horizontal pass)
        float leftBoundary = minPos.position.x;
        float rightBoundary = maxPos.position.x;

        float x = transform.position.x;

        // Moving left-to-right: offscreen once we pass the right boundary
        if (isMovingRight)
        {
            return x > rightBoundary;
        }
        // Moving right-to-left: offscreen once we pass the left boundary
        else
        {
            return x < leftBoundary;
        }
    }

    /// <summary>
    /// Camera-based off-screen check used ONLY for Variant 2 strike logic.
    /// variant2CameraOffset shrinks/expands the on-screen rectangle:
    ///   positive = bird must be further inside the camera before counting as on-screen,
    ///   negative = bird can be slightly outside and still be treated as on-screen.
    /// </summary>
    private bool IsOffScreenForVariant2()
    {
        if (mainCamera == null)
        {
            return false;
        }

        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;
        Vector3 camPos = mainCamera.transform.position;
        Vector3 pos = transform.position;

        float offset = variant2CameraOffset;

        float left = camPos.x - halfWidth + offset;
        float right = camPos.x + halfWidth - offset;
        float bottom = camPos.y - halfHeight + offset;
        float top = camPos.y + halfHeight - offset;

        // Safeguard: if offset is so large it inverts bounds, fall back to full camera rect
        if (left > right)
        {
            left = camPos.x - halfWidth;
            right = camPos.x + halfWidth;
        }
        if (bottom > top)
        {
            bottom = camPos.y - halfHeight;
            top = camPos.y + halfHeight;
        }

        return pos.x < left || pos.x > right || pos.y < bottom || pos.y > top;
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw strike zone radius in editor with offset
        Vector3 strikeCenter = transform.position + (Vector3)strikeZoneOffset;
        
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawSphere(strikeCenter, strikeZoneRadius);
        
        Gizmos.color = new Color(1f, 1f, 0f, 0.8f);
        Gizmos.DrawWireSphere(strikeCenter, strikeZoneRadius);
        
        // Draw spawn area if tags are set
        if (!string.IsNullOrEmpty(minPosTag) && !string.IsNullOrEmpty(maxPosTag))
        {
            GameObject minPosObj = GameObject.FindGameObjectWithTag(minPosTag);
            GameObject maxPosObj = GameObject.FindGameObjectWithTag(maxPosTag);
            
            if (minPosObj != null && maxPosObj != null)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
                
                // Draw left spawn line
                Vector3 leftTop = new Vector3(minPosObj.transform.position.x, maxPosObj.transform.position.y, 0f);
                Vector3 leftBottom = new Vector3(minPosObj.transform.position.x, minPosObj.transform.position.y, 0f);
                Gizmos.DrawLine(leftTop, leftBottom);
                
                // Draw right spawn line
                Vector3 rightTop = new Vector3(maxPosObj.transform.position.x, maxPosObj.transform.position.y, 0f);
                Vector3 rightBottom = new Vector3(maxPosObj.transform.position.x, minPosObj.transform.position.y, 0f);
                Gizmos.DrawLine(rightTop, rightBottom);
                
                // Draw horizontal lines
                Gizmos.DrawLine(leftTop, rightTop);
                Gizmos.DrawLine(leftBottom, rightBottom);
            }
        }
    }
    
    /// <summary>
    /// Check if a position overlaps with existing ThunderBird colliders
    /// Also checks for cross-direction collision (birds on opposite sides with same Y)
    /// </summary>
    private bool CheckBirdOverlap(Vector3 testPosition)
    {
        // Clear frame list if this is a new frame
        if (Time.frameCount != lastSpawnFrame)
        {
            currentFrameSpawnPositions.Clear();
            lastSpawnFrame = Time.frameCount;
        }
        
        // Check against current frame's spawn positions (birds that just spawned)
        foreach (Vector3 spawnPos in currentFrameSpawnPositions)
        {
            // Check same-side overlap (full 3D distance)
            float distance = Vector3.Distance(testPosition, spawnPos);
            if (distance < sameSideMinDistance)
            {
                Debug.Log($"<color=yellow>Frame-spawn overlap detected: Distance={distance:F2} < {sameSideMinDistance:F2}</color>");
                return true;
            }
            
            // Check cross-direction collision (opposite sides, similar Y position)
            bool testIsLeft = testPosition.x < 0f;
            bool spawnIsLeft = spawnPos.x < 0f;
            
            if (testIsLeft != spawnIsLeft)
            {
                float yDistance = Mathf.Abs(testPosition.y - spawnPos.y);
                if (yDistance < crossDirectionMinDistance)
                {
                    Debug.Log($"<color=orange>Frame-spawn cross-direction collision: Y distance={yDistance:F2} < {crossDirectionMinDistance:F2}</color>");
                    return true;
                }
            }
        }
        
        // Find all ThunderBirds in scene
        ThunderBird[] allBirds = FindObjectsOfType<ThunderBird>();
        
        foreach (ThunderBird bird in allBirds)
        {
            if (bird == this) continue; // Skip self
            
            Vector3 birdPos = bird.transform.position;
            
            // Check same-side overlap (full 3D distance)
            float distance = Vector3.Distance(testPosition, birdPos);
            if (distance < sameSideMinDistance)
            {
                Debug.Log($"<color=yellow>Same-side overlap detected: Distance={distance:F2} < {sameSideMinDistance:F2}</color>");
                return true; // Overlap detected
            }
            
            // Check cross-direction collision (opposite sides, similar Y position)
            bool testIsLeft = testPosition.x < 0f; // Assuming center is at x=0
            bool birdIsLeft = birdPos.x < 0f;
            
            if (testIsLeft != birdIsLeft) // Birds on opposite sides
            {
                float yDistance = Mathf.Abs(testPosition.y - birdPos.y);
                if (yDistance < crossDirectionMinDistance)
                {
                    Debug.Log($"<color=orange>Cross-direction collision detected: Y distance={yDistance:F2} < {crossDirectionMinDistance:F2}</color>");
                    return true; // Collision course detected
                }
            }
        }
        
        return false; // No overlap or collision
    }
    
    /// <summary>
    /// Spawns the second bird for Dual Thunder enhanced variant
    /// </summary>
    private void SpawnDualBird(Vector3 originalSpawnPos, Collider2D playerCollider, bool isLeft, bool spawnInTop, float minY, float midY, float maxY)
    {
        // Create a new ThunderBird instance
        GameObject secondBird = Instantiate(gameObject);
        ThunderBird secondBirdScript = secondBird.GetComponent<ThunderBird>();
        
        if (secondBirdScript != null)
        {
            // Manually set position and direction
            float spawnX = isLeft ? minPos.position.x : maxPos.position.x;
            float spawnY = spawnInTop ? Random.Range(midY, maxY) : Random.Range(minY, midY);
            
            secondBird.transform.position = new Vector3(spawnX, spawnY, originalSpawnPos.z);
            
            // Set movement direction
            secondBirdScript.isMovingRight = isLeft; // Left bird moves right, right bird moves left
            
            // Flip sprite
            if (secondBirdScript.spriteRenderer != null)
            {
                secondBirdScript.spriteRenderer.flipX = !secondBirdScript.isMovingRight;
            }
            
            // Copy enhanced variant
            secondBirdScript.enhancedVariant = this.enhancedVariant;
            
            // Ignore player collision
            if (secondBirdScript._collider2D != null && playerCollider != null)
            {
                Physics2D.IgnoreCollision(secondBirdScript._collider2D, playerCollider, true);
            }
            
            // Start flying
            secondBirdScript.StartCoroutine(secondBirdScript.FlyRoutine(lifetimeSeconds));
            secondBirdScript.StartCoroutine(secondBirdScript.DamageDetectionRoutine());
            
            // Play fly sound
            if (flyClip != null && secondBirdScript._audioSource != null)
            {
                secondBirdScript._audioSource.clip = flyClip;
                secondBirdScript._audioSource.volume = flyVolume;
                secondBirdScript._audioSource.loop = true;
                secondBirdScript._audioSource.Play();
            }
            
            string birdSide = isLeft ? "LEFT" : "RIGHT";
            string zoneType = spawnInTop ? "TOP" : "BOTTOM";
            Debug.Log($"<color=gold>Dual Thunder: {birdSide} bird spawned in {zoneType} zone at Y={spawnY:F2}</color>");
        }
    }
    
    /// <summary>
    /// Apply modifiers instantly (IInstantModifiable interface)
    /// </summary>
    public void ApplyInstantModifiers(CardModifierStats modifiers)
    {
        Debug.Log($"<color=lime>╔═══ THUNDERBIRD INSTANT MODIFIERS ═══╗</color>");
        
        float newSpeed;
        if (isVariant2Active)
        {
            float baseVersionSpeed = baseFlySpeed + modifiers.speedIncrease;
            float speedIncrease = baseVersionSpeed - baseFlySpeed;
            float speedReduction = speedIncrease * variant2SpeedReductionRate;
            float targetSpeed = variant2Speed - speedReduction;
            newSpeed = Mathf.Max(variant2MinSpeed, targetSpeed);
            Debug.Log($"<color=lime>  Variant 2 Speed Exchange (instant): base+{speedIncrease:F1} → {newSpeed:F2}</color>");
        }
        else if (enhancedVariant == 1)
        {
            float baseVersionSpeed = baseFlySpeed + modifiers.speedIncrease;
            newSpeed = baseVersionSpeed + variant1SpeedBonus;
            Debug.Log($"<color=lime>  Variant 1 Speed: {baseFlySpeed:F2} + {modifiers.speedIncrease:F2} + {variant1SpeedBonus:F2} = {newSpeed:F2}</color>");
        }
        else
        {
            newSpeed = baseFlySpeed + modifiers.speedIncrease;
            Debug.Log($"<color=lime>  Base Speed: {baseFlySpeed:F2} + {modifiers.speedIncrease:F2} = {newSpeed:F2}</color>");
        }

        if (newSpeed != flySpeed)
        {
            float minSpeed = isVariant2Active ? variant2MinSpeed : 1f;
            flySpeed = Mathf.Max(minSpeed, newSpeed);
            if (_rigidbody2D != null)
            {
                Vector2 direction = isMovingRight ? Vector2.right : Vector2.left;
                _rigidbody2D.velocity = direction * flySpeed;
            }
        }
        
        // Recalculate strike zone radius
        float newStrikeZone = (baseStrikeZoneRadius + modifiers.strikeZoneRadiusBonus) * modifiers.strikeZoneRadiusMultiplier;
        if (newStrikeZone != strikeZoneRadius)
        {
            strikeZoneRadius = newStrikeZone;
            Debug.Log($"<color=lime>  Strike Zone: ({baseStrikeZoneRadius:F2} + {modifiers.strikeZoneRadiusBonus:F2}) * {modifiers.strikeZoneRadiusMultiplier:F2}x = {strikeZoneRadius:F2}</color>");
        }
        
        // Recalculate size using the same pipeline as Initialize:
        //   baseScale → variant size multiplier (V1/V2) → generic card size multiplier.
        float instantVariantSizeMultiplier = 1f;
        if (enhancedVariant == 1 && variant1SizeMultiplier != 1f)
        {
            instantVariantSizeMultiplier = variant1SizeMultiplier;
        }
        else if (isVariant2Active && variant2SizeMultiplier != 1f)
        {
            instantVariantSizeMultiplier = variant2SizeMultiplier;
        }

        float instantFinalSizeMultiplier = instantVariantSizeMultiplier * modifiers.sizeMultiplier;

        if (!Mathf.Approximately(instantFinalSizeMultiplier, 1f))
        {
            transform.localScale = baseScale * instantFinalSizeMultiplier;
            Debug.Log($"<color=lime>  Size: {baseScale} * {instantFinalSizeMultiplier:F2}x = {transform.localScale}</color>");
        }
        else
        {
            transform.localScale = baseScale;
            Debug.Log($"<color=lime>  Size: reverted to baseScale {baseScale}</color>");
        }

        // Keep strike zone offset's Y component in sync with current visual scale
        // using the same 1 : 1.5 ratio (scaleY : offsetY) used in Initialize.
        float instantScaleY = transform.localScale.y;
        strikeZoneOffset = new Vector2(strikeZoneOffset.x, instantScaleY * 1.5f);
        
        // Recalculate damage with FLAT bonus instead of multiplier
        float newDamage = baseDamage + modifiers.damageFlat;
        if (Mathf.Abs(newDamage - damage) > 0.001f)
        {
            damage = newDamage;
            Debug.Log($"<color=lime>  Damage: {baseDamage:F2} + {modifiers.damageFlat:F2} = {damage:F2}</color>");
        }
        
        Debug.Log($"<color=lime>╚═══════════════════════════════════╝</color>");
    }
    
    private void OnDestroy()
    {
        // Stop audio
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
    }
}
