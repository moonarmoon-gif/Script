using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ElementalBeam : MonoBehaviour, IInstantModifiable
{
    [Header("Beam Settings")]
    [SerializeField] private float lifetimeSeconds = 3f;
    [Tooltip("Damage instances per second (1 = once per second, 2 = twice per second, 0.5 = once every 2 seconds)")]
    [SerializeField] private float damageInstancesPerSecond = 2f;

    [Header("Animation Timing")]
    [Tooltip("Delay before setting BeamStart to true (start animation duration)")]
    [SerializeField] private float beamStartDelay = 0.25f;
    [Tooltip("Duration of end animation (BeamEnd is true for this duration)")]
    [SerializeField] private float beamEndDuration = 0.25f;

    [Header("Enhanced Beam Damage")]
    [Tooltip("Damage multiplier during startup delay for enhanced beams (0.5 = 50% damage)")]
    public float enhancedStartupDamageMultiplier = 0.5f;
    [Tooltip("Damage multiplier during end delay for enhanced beams (0.5 = 50% damage)")]
    public float enhancedEndDamageMultiplier = 0.5f;
    [Tooltip("Damage multiplier during startup delay for Enhanced Variant 3 (0.5 = 50% damage)")]
    public float variant3StartupDamageMultiplier = 0.5f;
    [Tooltip("Damage multiplier during end delay for Enhanced Variant 3 (0.5 = 50% damage)")]
    public float variant3EndDamageMultiplier = 0.5f;

    [Header("Damage Settings")]
    [SerializeField] private float damage = 15f;
    [SerializeField] private int manaCost = 10;
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private ProjectileType projectileType = ProjectileType.Fire;

    // Instance-based cooldown tracking (per prefab type)
    private static System.Collections.Generic.Dictionary<string, float> lastFireTimes = new System.Collections.Generic.Dictionary<string, float>();
    private string prefabKey;

    [Header("Beam Positioning")]
    [Tooltip("Offset for beam when spawned on left side")]
    [SerializeField] private Vector2 beamOffsetLeft = Vector2.zero;
    [Tooltip("Offset for beam when spawned on right side")]
    [SerializeField] private Vector2 beamOffsetRight = Vector2.zero;
    [Tooltip("Rotation offset to correct sprite facing (0 = sprite faces right, 180 = sprite faces left)")]
    [SerializeField] private float spriteRotationOffset = 0f;

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

    [Header("Variant 3 - Secondary Beam Offsets")]
    [Tooltip("Spawn offset for the SECONDARY Variant 3 beam when firing left at angle ABOVE 45 degrees")]
    [SerializeField] private Vector2 variant3OffsetLeftAbove45 = Vector2.zero;
    [Tooltip("Spawn offset for the SECONDARY Variant 3 beam when firing left at angle BELOW 45 degrees")]
    [SerializeField] private Vector2 variant3OffsetLeftBelow45 = Vector2.zero;
    [Tooltip("Spawn offset for the SECONDARY Variant 3 beam when firing right at angle ABOVE 45 degrees")]
    [SerializeField] private Vector2 variant3OffsetRightAbove45 = Vector2.zero;
    [Tooltip("Spawn offset for the SECONDARY Variant 3 beam when firing right at angle BELOW 45 degrees")]
    [SerializeField] private Vector2 variant3OffsetRightBelow45 = Vector2.zero;

    [Header("Collider Scaling")]
    [Tooltip("Offset for collider size relative to visual size (0 = same as visual, -0.2 = 20% smaller, 0.2 = 20% larger)")]
    [SerializeField] private float colliderSizeOffset = 0f;

    [Header("Impact VFX")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float hitEffectDuration = 1.0f;

    [Header("Audio - Impact")]
    [SerializeField] private AudioClip impactClip;
    [Range(0f, 1f)][SerializeField] private float impactVolume = 1f;

    [Header("Audio - Beam")]
    [SerializeField] public AudioClip beamClip;
    [Range(0f, 1f)][SerializeField] public float beamVolume = 0.85f;
    [SerializeField] public float beamPitch = 1.0f;
    [SerializeField] public bool beamLoop = true;
    [Tooltip("1 = fully 3D, 0 = 2D UI-like.")]
    [Range(0f, 1f)][SerializeField] public float beamSpatialBlend = 1f;
    [Tooltip("Reduce for arcade feel; 0 turns off Doppler effect.")]
    [SerializeField] public float beamDopplerLevel = 0f;
    [Tooltip("Fade-out time when the beam ends.")]
    [SerializeField] public float beamFadeOutSeconds = 0.12f;

    [Header("Smart Targeting")]
    [Tooltip("When enabled, beam fires at direction that hits most enemies (base version only)")]
    [SerializeField] private bool useSmartTargeting = false;

    public float NoEnemyTargetDelay = 0.5f;

    public enum SmartTargetingMode
    {
        RaycastSampling,      // Original: Sample multiple directions with raycasts
        ClusterAnalysis,      // New: Find densest enemy cluster
        WeightedDistance,     // New: Weight enemies by distance and angle
        GridSweep             // New: Divide screen into grid and find best sweep
    }

    public void MarkAsVariant3SecondaryBeam()
    {
        isVariant3SecondaryBeam = true;
    }

    public void MarkAsVariant13SecondaryBeam()
    {
        isVariant13SecondaryBeam = true;
    }

    public void MarkAsVariant23SecondaryBeam()
    {
        isVariant23SecondaryBeam = true;
    }

    [Tooltip("Smart targeting algorithm to use")]
    [SerializeField] private SmartTargetingMode targetingMode = SmartTargetingMode.RaycastSampling;

    [Tooltip("Number of directions to test for smart targeting (more = more accurate but slower)")]
    [SerializeField] private int smartTargetingSamples = 16;

    [Header("Enhanced Variant 1 - Rotating Beam")]
    [Tooltip("Rotation speed for Enhanced Variant 1 (degrees per second)")]
    [SerializeField] private float enhancedRotationSpeed = 90f;
    [SerializeField] private float variant1Damage = 0f;
    [Tooltip("Base cooldown for Enhanced Variant 1 (seconds)")]
    public float variant1BaseCooldown = 8f;

    [Header("Enhanced Variant 1+2 - Rotating Smart Beam")]
    [Tooltip("Fraction of core lifetime (excluding start delay and end duration) before switching from V2-style static beam to V1-style opposite rotation (0.5 = halfway)")]
    [Range(0f, 1f)]
    public float variant12RotationStartFraction = 0.5f;
    [Tooltip("Rotation speed for Enhanced Variant 1+2 combined (degrees per second)")]
    public float enhancedV1And2RotationSpeed = 10f;

    [Header("Enhanced Variant 2 - Smart Targeting")]
    [Tooltip("Number of directions to test for Variant 2 smart targeting (higher = more accurate)")]
    public int variant2SmartTargetingSamples = 40;
    [Tooltip("Maximum detection range for smart targeting (distance in units)")]
    public float smartTargetingDetectionRange = 30f;
    [Tooltip("Override beam width for smart targeting (0 = use collider size, >0 = manual width)")]
    public float smartTargetingBeamWidthOverride = 0f;
    [Tooltip("Tolerance for on-camera detection (1.0 = exact camera bounds, 1.2 = 20% larger, 0.8 = 20% smaller)")]
    [Range(0.5f, 2f)]
    public float onCameraTolerance = 1.0f; 

    [Header("Enhanced Variant 2 - Smart Beam")]
    [Tooltip("Base cooldown for Enhanced Variant 2 (seconds)")]
    public float variant2BaseCooldown = 6f;
    [Tooltip("Beam start delay for Variant 2 (seconds)")]
    public float variant2BeamStartDelay = 0.25f;
    [Tooltip("Beam end duration for Variant 2 (seconds)")]
    public float variant2BeamEndDuration = 0.25f;
    [Tooltip("Unique lifetime for Variant 2 (seconds)")]
    public float variant2LifetimeSeconds = 0.5f;
    [Tooltip("Reduce base X scale by this percentage (0.5 = 50% reduction)")]
    [Range(0f, 1f)]
    public float variant2ReducedXScale = 0.5f;
    [Tooltip("Damage instances per second for Variant 2")]
    public float variant2DamageInstancesPerSecond = 5f;
    [Tooltip("Unique damage value for Variant 2 (replaces base damage)")]
    public float variant2Damage = 100f;
    [Tooltip("Animation speed multiplier for startup animation (2 = 2x faster)")]
    public float variant2StartAnimationMultiplier = 1f;
    [Tooltip("Animation speed multiplier for end animation (2 = 2x faster)")]
    public float variant2EndAnimationMultiplier = 1f;

    [Header("Enhanced Variant 2 - Modifier Exchange Rates")]
    [Tooltip("Exchange rate: lifetime modifier → Variant 2 lifetime (e.g., 0.25 = each +1 second base lifetime adds +0.25 seconds to Variant 2)")]
    public float variant2LifetimeExchangeRate = 0.25f;

    [Header("Enhanced Variant 3 - Dual Smart Beams")]
    [Tooltip("Base cooldown for Enhanced Variant 3 (seconds)")]
    public float variant3BaseCooldown = 6f;
    [Tooltip("Number of directions to test for Variant 3 dual smart beams (higher = more accurate but more expensive)")]
    public int variant3SmartTargetingSamples = 40;

    private Rigidbody2D _rigidbody2D;
    private Collider2D _collider2D;
    private Animator _animator;
    private AudioSource _beamSource;
    private Coroutine _fadeOutRoutine;
    private float damageInterval;
    private HashSet<GameObject> enemiesInBeam = new HashSet<GameObject>();
    private Dictionary<GameObject, float> enemyNextDamageTime = new Dictionary<GameObject, float>(); // Track per-enemy damage timing
    private HashSet<GameObject> enemiesDamagedOnce = new HashSet<GameObject>(); // Track enemies damaged once (for enhanced beam)

    // Enhanced system
    private int enhancedVariant = 0; // 0 = basic, 1 = rotating beam, 2-3 = future variants
    private bool isOnLeftSide = false;
    private float currentRotation = 0f;
    private float rotationDirection = 1f; // -1 = clockwise (right), 1 = counter-clockwise (left) - Unity uses counter-clockwise as positive
    private bool canDealDamage = false; // Prevent damage during beam start delay
    private bool isInStartupOrEndPhase = true; // Track if in startup or end phase
    private bool isInStartupPhase = true; // Track if specifically in startup phase (vs end phase)
    private bool isInitialized = false; // Track if Launch() has been called
    private Vector3 spawnOffsetWorld = Vector3.zero; // Store the world-space offset calculated in Launch()
    private Transform firePointTransform = null; // Store the firepoint TRANSFORM reference (NOT just position!)
    private bool isVariant12Stacked = false;
    private bool variant12RotationEnabled = false;
    private float variant12RotationStartTime = 0f;

    // Variant 3 secondary beam flag
    private bool isVariant3SecondaryBeam = false;

    // Variant 1 + 3 stacked secondary beam flag
    private bool isVariant13SecondaryBeam = false;

    // Variant 2 + 3 stacked secondary beam flag
    private bool isVariant23SecondaryBeam = false;

    // Static tracking for animation system (same as ThunderBird fix)
    private static int lastEnhancedVariant = -1; // Track enhancement changes to reset animation

    // Base values for instant modifier recalculation
    private float baseLifetimeSeconds;
    private float baseDamage;
    private Vector3 baseScale;
    
    // Scale reduction multipliers (applied in LateUpdate to work with animation)
    private float variant2ScaleMultiplier = 1f;
    private float sizeModifierMultiplier = 1f;

    // Cache PlayerStats and base (post-card) damage so we can roll crits per
    // damage instance/tick instead of once at launch.
    private PlayerStats cachedPlayerStats;
    private float baseDamageAfterCards = 0f;

    // PERFORMANCE: Static cached arrays/collections to reduce GC allocations
    private static Collider2D[] cachedColliders = new Collider2D[200]; // Reusable array for OverlapCircleNonAlloc
    private static HashSet<GameObject> cachedEnemySet = new HashSet<GameObject>(); // Reusable set
    private static List<GameObject> cachedEnemyList = new List<GameObject>(); // Reusable list
    private static List<Vector2> cachedEnemyPositions = new List<Vector2>(); // Reusable position list

    public bool HasAnyOnScreenEnemy(Vector3 origin)
    {
        int colliderCount = Physics2D.OverlapCircleNonAlloc(origin, smartTargetingDetectionRange, cachedColliders, enemyLayer);
        if (colliderCount <= 0)
        {
            return false;
        }

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            return true;
        }

        float cameraHeight = mainCam.orthographicSize * 2f * onCameraTolerance;
        float cameraWidth = cameraHeight * mainCam.aspect;
        float halfWidth = cameraWidth / 2f;
        float halfHeight = cameraHeight / 2f;
        Vector3 camPos = mainCam.transform.position;

        for (int i = 0; i < colliderCount; i++)
        {
            Collider2D collider = cachedColliders[i];
            if (collider == null)
            {
                continue;
            }

            Vector3 enemyPos = collider.transform.position;
            if (Mathf.Abs(enemyPos.x - camPos.x) <= halfWidth && Mathf.Abs(enemyPos.y - camPos.y) <= halfHeight)
            {
                return true;
            }
        }

        return false;
    }

    // Shared smart-targeting batches so that multiple ElementalBeams spawned
    // from the same card/volley can claim different raycast-sampled
    // directions instead of all stacking on the single best angle.
    private class SmartTargetingBatch
    {
        public float minAngle;
        public float maxAngle;
        public List<float> candidateAngles = new List<float>();
        public List<Vector2> candidateDirections = new List<Vector2>();
        public List<int> candidateHits = new List<int>();
        public List<int> unusedHitIndices = new List<int>();
        public List<int> usedHitIndices = new List<int>();
        public bool hasAnyHits;
        public int reuseCursor = 0;
        public int noHitCursor = 0;
    }

    private static Dictionary<int, SmartTargetingBatch> smartTargetingBatches = new Dictionary<int, SmartTargetingBatch>();

    private static Dictionary<string, int> nextSortingOrderByCard = new Dictionary<string, int>();

    private void ApplySortingOrderForBeam(ProjectileCards card)
    {
        if (card == null)
        {
            return;
        }

        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
        if (sprites == null || sprites.Length == 0)
        {
            return;
        }

        int instanceBaseOrder = sprites[0].sortingOrder;
        for (int i = 1; i < sprites.Length; i++)
        {
            if (sprites[i] != null && sprites[i].sortingOrder < instanceBaseOrder)
            {
                instanceBaseOrder = sprites[i].sortingOrder;
            }
        }

        string key = card.cardName;

        if (!nextSortingOrderByCard.TryGetValue(key, out int nextOrder))
        {
            nextSortingOrderByCard[key] = instanceBaseOrder + 1;
            return;
        }

        int delta = nextOrder - instanceBaseOrder;
        if (delta != 0)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null)
                {
                    sprites[i].sortingOrder += delta;
                }
            }
        }

        nextSortingOrderByCard[key] = nextOrder + 1;
    }

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _collider2D = GetComponent<Collider2D>();
        _animator = GetComponent<Animator>();

        // Store base values
        baseLifetimeSeconds = lifetimeSeconds;
        baseDamage = damage;
        baseScale = transform.localScale;

        // Make beam stationary
        if (_rigidbody2D != null)
        {
            _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody2D.velocity = Vector2.zero;
        }

        // Ensure collider is trigger
        if (_collider2D != null)
        {
            _collider2D.isTrigger = true;
        }

        // Initialize animator booleans to false
        if (_animator != null)
        {
            _animator.SetBool("BeamStart", false);
            _animator.SetBool("BeamEnd", false);
        }

        // CRITICAL FIX: Hide beam until Launch() sets proper position
        // This prevents visible "jump" on first few frames during gameplay.
        // Keep beams visible in the editor Scene view by only hiding them while playing.
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        if (Application.isPlaying)
        {
            foreach (SpriteRenderer sr in renderers)
            {
                sr.enabled = false;
            }
        }

        EnsureBeamAudioSource();
    }

    private void OnEnable()
    {
        if (_fadeOutRoutine != null)
        {
            StopCoroutine(_fadeOutRoutine);
            _fadeOutRoutine = null;
        }
        StopBeamSfx(true);
        enemiesInBeam.Clear();
    }

    private void OnDisable()
    {
        StopBeamSfx(true);
        enemiesInBeam.Clear();
    }

    private void LateUpdate()
    {
        // Re-apply Variant 2 X-scale reduction and size modifier AFTER animation updates
        // so that animator changes don't wipe out the narrow beam width.
        Vector3 scale = baseScale;

        // Variant 2: reduce X scale by configured percentage
        if (enhancedVariant == 2 && variant2ReducedXScale > 0f)
        {
            float v2Multiplier = Mathf.Max(0f, 1f - variant2ReducedXScale);
            scale.x *= v2Multiplier;
        }

        // Apply size modifier on top (X-only)
        if (sizeModifierMultiplier != 1f)
        {
            scale.x *= sizeModifierMultiplier;
        }

        transform.localScale = scale;
    }

    private void Update()
    {
        // Don't update position until Launch() has been called
        if (!isInitialized)
        {
            return; // Wait for Launch() to initialize
        }

        // ALL beams stick to firepoint TRANSFORM (NOT player position!)
        // The firepoint transform was set in Launch() from elementalBeamFirePoint
        if (firePointTransform != null)
        {
            // Always follow firepoint transform position WITH the spawn offset
            // This ensures beam fires from elementalBeamFirePoint, not player center
            transform.position = firePointTransform.position + spawnOffsetWorld;
        }

        // Enhanced variant: Rotate around player like clock hand
        bool doRotate = false;
        float rotationSpeed = enhancedRotationSpeed;

        if (enhancedVariant == 1)
        {
            doRotate = true;
            rotationSpeed = enhancedRotationSpeed;
        }
        else if (isVariant12Stacked)
        {
            if (!variant12RotationEnabled && Time.time >= variant12RotationStartTime)
            {
                variant12RotationEnabled = true;
            }

            if (variant12RotationEnabled)
            {
                doRotate = true;
                rotationSpeed = enhancedV1And2RotationSpeed;
            }
        }

        if (doRotate)
        {
            currentRotation += rotationSpeed * rotationDirection * Time.deltaTime;
            transform.rotation = Quaternion.Euler(0f, 0f, currentRotation);
        }
    }

    public void Launch(Vector2 direction, Collider2D colliderToIgnore, PlayerMana playerMana = null, bool skipCooldownCheck = false, Vector3 playerPosition = default)
    {
        // Get PlayerStats for projectile modifications
        PlayerStats stats = null;
        if (colliderToIgnore != null)
        {
            stats = colliderToIgnore.GetComponent<PlayerStats>();
        }

        // Get card-specific modifiers
        ProjectileCards card = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
        CardModifierStats modifiers = new CardModifierStats(); // Default values

        // Track variant stacking via history (HasChosenVariant) separate from the
        // currently selected enhancedVariant. This lets us implement combinations
        // like Variant 1 + 3 while still using a single UI-selected variant index.
        bool hasVariant1 = false;
        bool hasVariant2 = false;
        bool hasVariant3 = false;
        bool isStackedVariant13 = false;
        bool isStackedVariant23 = false;
        bool isStackedVariant12 = false;
        int uiEnhancedVariant = 0;
        isVariant12Stacked = false;

        if (card != null)
        {
            modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
            Debug.Log($"<color=cyan>ElementalBeam using modifiers from {card.cardName}</color>");

            ApplySortingOrderForBeam(card);
        }

        // Check for enhanced variant using CARD-based system
        if (ProjectileCardLevelSystem.Instance != null && card != null)
        {
            int currentLevel = ProjectileCardLevelSystem.Instance.GetLevel(card);
            bool isEnhancedUnlocked = ProjectileCardLevelSystem.Instance.IsEnhancedUnlocked(card);
            int storedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card);
            int unlockLevel = ProjectileCardLevelSystem.Instance.GetEnhancedUnlockLevel();

            // Remember the UI-selected variant separately so we can still know
            // which button was chosen even if we override behaviour for stacking.
            uiEnhancedVariant = storedVariant;

            // Use the UI-selected enhanced variant directly (0 = none, 1/2/3 = specific variant)
            enhancedVariant = storedVariant;

            // If enhancement changed, reset animator state (same fix as ThunderBird)
            if (enhancedVariant != lastEnhancedVariant)
            {
                lastEnhancedVariant = enhancedVariant;
                if (_animator != null)
                {
                    _animator.Rebind();
                    _animator.Update(0f);
                    Debug.Log($"<color=gold>ElementalBeam ({card.cardName}) enhanced variant changed to {enhancedVariant}, resetting animator</color>");
                }
            }

            // Determine if Variant 1/2/3 have EVER been chosen for this card so we
            // can enable stacked behaviour (V1+V3, V2+V3, or V1+V2) on top of the
            // currently selected UI variant.
            hasVariant1 = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 1);
            hasVariant2 = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 2);
            hasVariant3 = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 3);
            isStackedVariant13 = hasVariant1 && hasVariant3;
            isStackedVariant23 = hasVariant2 && hasVariant3;
            isStackedVariant12 = hasVariant1 && hasVariant2 && !hasVariant3;
            isVariant12Stacked = false;

            if (isStackedVariant23)
            {
                enhancedVariant = 2;
                Debug.Log($"<color=magenta>ElementalBeam ({card.cardName}) STACKED Variant 2+3 active: using Variant 2 behaviour.</color>");
            }
            else if (isStackedVariant13)
            {
                enhancedVariant = 1;
                Debug.Log($"<color=magenta>ElementalBeam ({card.cardName}) STACKED Variant 1+3 active: using Variant 1 behaviour.</color>");
            }
            else if (isStackedVariant12)
            {
                // STACKED Variant 1 + 2: As soon as BOTH Variant 1 and Variant 2
                // have ever been chosen for this card (and Variant 3 is not in
                // the mix), always treat the enhanced behaviour as the combined
                // 1+2 mode. This makes the stacked behaviour order-independent:
                // it activates whether the player picked Variant 1 first or
                // Variant 2 first.
                //
                // We still drive the core behaviour through Variant 2 (smart
                // targeting, damage, lifetime) while borrowing Variant 1's
                // unique cooldown and rotation rules further below.
                enhancedVariant = 2;
                isVariant12Stacked = true;
                Debug.Log($"<color=magenta>ElementalBeam ({card.cardName}) STACKED Variant 1+2 active: using Variant 2 behaviour with V1 cooldown and rotation. (UI={uiEnhancedVariant})</color>");
            }

            Debug.Log($"<color=cyan>ElementalBeam ({card.cardName}): Level={currentLevel}, UnlockLevel={unlockLevel}, IsUnlocked={isEnhancedUnlocked}, Variant={enhancedVariant} (UI={uiEnhancedVariant})</color>");
        }

        // Use variant-specific beam timing
        float finalBeamStartDelay = beamStartDelay;
        float finalBeamEndDuration = beamEndDuration;
        float baseLifetime = lifetimeSeconds;
        float baseDamageInstancesPerSecond = damageInstancesPerSecond;

        // CRITICAL: Variants can use their own unique base cooldowns
        float baseCooldown = cooldown;

        // STACKED Variant 1 + 3: Always use Variant 3's base cooldown as the
        // canonical base when both variants have ever been chosen for this card,
        // regardless of which variant is currently selected in the UI.
        if (isStackedVariant23 && enhancedVariant == 2)
        {
            // STACKED Variant 2 + 3: treat as Variant 2 for cooldown/lifetime/damage,
            // but later we will fire dual smart beams.
            baseCooldown = variant2BaseCooldown;
            baseLifetime = variant2LifetimeSeconds;
            baseDamageInstancesPerSecond = variant2DamageInstancesPerSecond;
            finalBeamStartDelay = variant2BeamStartDelay;
            finalBeamEndDuration = variant2BeamEndDuration;

            if (card != null)
            {
                card.runtimeSpawnInterval = variant2BaseCooldown;
                Debug.Log($"<color=magenta>ElementalBeam STACKED 2+3: Using Variant 2 base cooldown {variant2BaseCooldown:F2}s (dual smart beams)</color>");
            }

            Debug.Log($"<color=gold>ElementalBeam STACKED Variant 2+3: Using unique cooldown {baseCooldown:F2}s, lifetime {baseLifetime:F2}s, damageInstances {baseDamageInstancesPerSecond:F2}/s, startDelay {finalBeamStartDelay:F2}s, endDuration {finalBeamEndDuration:F2}s</color>");
        }
        else if (isStackedVariant13 && variant3BaseCooldown > 0f)
        {
            baseCooldown = variant3BaseCooldown;

            if (card != null)
            {
                card.runtimeSpawnInterval = variant3BaseCooldown;
                Debug.Log($"<color=magenta>ElementalBeam STACKED 1+3: Using Variant 3 base cooldown {variant3BaseCooldown:F2}s</color>");
            }
        }
        else if (enhancedVariant == 1 && variant1BaseCooldown > 0f)
        {
            // Pure Variant 1: unique base cooldown
            baseCooldown = variant1BaseCooldown;

            if (card != null)
            {
                card.runtimeSpawnInterval = variant1BaseCooldown;
                Debug.Log($"<color=gold>Variant 1: Updated card.runtimeSpawnInterval to {variant1BaseCooldown:F2}s (ProjectileSpawner will use this)</color>");
            }

            Debug.Log($"<color=gold>ElementalBeam Variant 1: Using unique cooldown {baseCooldown:F2}s</color>");
        }
        else if (enhancedVariant == 2 && isVariant12Stacked)
        {
            // STACKED Variant 1 + 2: use a combined core lifetime from BOTH
            // Variant 1 (lifetimeSeconds) and Variant 2 (variant2LifetimeSeconds),
            // while still using Variant 2's start/end animation timings.
            // coreLifetime = lifetimeSeconds + variant2LifetimeSeconds
            // finalLifetime = coreLifetime + lifetimeModifier (no exchange rate).
            baseLifetime = lifetimeSeconds + variant2LifetimeSeconds;
            baseDamageInstancesPerSecond = variant2DamageInstancesPerSecond;
            finalBeamStartDelay = variant2BeamStartDelay;
            finalBeamEndDuration = variant2BeamEndDuration;

            // STACKED 1+2 shares Variant 1's unique base cooldown
            if (variant1BaseCooldown > 0f)
            {
                baseCooldown = variant1BaseCooldown;
            }
            else if (card != null && card.runtimeSpawnInterval > 0f)
            {
                baseCooldown = card.runtimeSpawnInterval;
            }
            else
            {
                baseCooldown = cooldown;
            }

            if (card != null)
            {
                card.runtimeSpawnInterval = baseCooldown;
            }

            Debug.Log($"<color=gold>ElementalBeam STACKED Variant 1+2: cooldown {baseCooldown:F2}s, lifetime {baseLifetime:F2}s, damageInstances {baseDamageInstancesPerSecond:F2}/s, startDelay {finalBeamStartDelay:F2}s, endDuration {finalBeamEndDuration:F2}s</color>");
        }
        else if (enhancedVariant == 2)
        {
            // Variant 2: Use unique cooldown, lifetime, animation timing, and damage instances
            baseCooldown = variant2BaseCooldown;
            baseLifetime = variant2LifetimeSeconds;
            baseDamageInstancesPerSecond = variant2DamageInstancesPerSecond;
            finalBeamStartDelay = variant2BeamStartDelay;
            finalBeamEndDuration = variant2BeamEndDuration;

            // CRITICAL: Update card's runtimeSpawnInterval so ProjectileSpawner uses variant2BaseCooldown
            if (card != null)
            {
                card.runtimeSpawnInterval = variant2BaseCooldown;
                Debug.Log($"<color=gold>Variant 2: Updated card.runtimeSpawnInterval to {variant2BaseCooldown}s (ProjectileSpawner will use this)</color>");
            }

            Debug.Log($"<color=gold>ElementalBeam Variant 2: Using unique cooldown {baseCooldown:F2}s, lifetime {baseLifetime:F2}s, damageInstances {baseDamageInstancesPerSecond:F2}/s, startDelay {finalBeamStartDelay:F2}s, endDuration {finalBeamEndDuration:F2}s</color>");
        }
        else if (uiEnhancedVariant == 3)
        {
            // Pure Variant 3 (non-stacked): unique cooldown, reuse base lifetime/damage values.
            baseCooldown = variant3BaseCooldown;

            if (card != null)
            {
                card.runtimeSpawnInterval = variant3BaseCooldown;
                Debug.Log($"<color=gold>Variant 3: Updated card.runtimeSpawnInterval to {variant3BaseCooldown:F2}s (ProjectileSpawner will use this)</color>");
            }

            Debug.Log($"<color=gold>ElementalBeam Variant 3: Using unique cooldown {baseCooldown:F2}s (dual smart beams)</color>");
        }
        else if (card != null && card.runtimeSpawnInterval > 0f)
        {
            // Other variants: Use ProjectileCards spawnInterval if available
            baseCooldown = card.runtimeSpawnInterval;
            Debug.Log($"<color=gold>ElementalBeam using ProjectileCards spawnInterval: {baseCooldown:F2}s (overriding script cooldown: {cooldown:F2}s)</color>");
        }

        // Compute final lifetime and cooldown after card modifiers.
        // CRITICAL: Variant 2 uses exchange rate for lifetime modifiers, but this
        // exchange is DISABLED for stacked Variant 1+2 so we can restore the
        // original full-lifetime behaviour.
        float lifetimeModifier = (enhancedVariant == 2 && !isVariant12Stacked)
            ? (modifiers.lifetimeIncrease * variant2LifetimeExchangeRate)
            : modifiers.lifetimeIncrease;
        float finalLifetime = baseLifetime + lifetimeModifier;

        // Only log Variant 2 lifetime exchange when it's actually applied
        if (enhancedVariant == 2 && !isVariant12Stacked && modifiers.lifetimeIncrease > 0f)
        {
            Debug.Log($"<color=gold>Variant 2 Lifetime Exchange: Base modifier +{modifiers.lifetimeIncrease:F2}s * {variant2LifetimeExchangeRate:F2} = +{lifetimeModifier:F2}s (Final: {finalLifetime:F2}s)</color>");
        }

        float finalCooldown = Mathf.Max(0.1f, baseCooldown * (1f - modifiers.cooldownReductionPercent / 100f));
        int finalManaCost = Mathf.Max(0, Mathf.CeilToInt(manaCost * (1f - modifiers.manaCostReduction)));

        // CRITICAL: Variant 2 uses its own unique damage value. For all
        // variants we now keep PlayerStats crit rolls PER DAMAGE INSTANCE by
        // only applying card modifiers here and deferring PlayerStats
        // CalculateDamage to the actual damage routines.
        float baseDamageValue = damage;
        if (enhancedVariant == 2)
        {
            baseDamageValue = variant2Damage;
        }
        else if (enhancedVariant == 1 && variant1Damage > 0f)
        {
            baseDamageValue = variant1Damage;
        }
        float finalDamage = baseDamageValue * modifiers.damageMultiplier;

        if (enhancedVariant == 2)
        {
            Debug.Log($"<color=gold>Variant 2: Using unique damage {variant2Damage} (base) * {modifiers.damageMultiplier:F2} (modifier) = {finalDamage:F2}</color>");
        }

        // Cache post-card base damage (no PlayerStats yet) so we can roll crit
        // per instance in DamageRoutine/Variant 1 contact logic.
        baseDamageAfterCards = finalDamage;

        Debug.Log($"<color=cyan>ElementalBeam Modifiers: Lifetime {baseLifetime:F2}s + {modifiers.lifetimeIncrease:F2}s = {finalLifetime:F2}s, Cooldown {baseCooldown:F2}s * {(1f - modifiers.cooldownReductionPercent / 100f):F2} = {finalCooldown:F2}s</color>");

        // STACKED Variant 1+2: compute when rotation should begin. We treat the
        // "core" lifetime as excluding the startup delay and end duration.
        if (isVariant12Stacked)
        {
            float coreLifetime = Mathf.Max(0f, finalLifetime - finalBeamStartDelay - finalBeamEndDuration);
            float fraction = Mathf.Clamp01(variant12RotationStartFraction);
            float rotationDelayWithinCore = coreLifetime * fraction;
            float coreStartTime = Time.time + finalBeamStartDelay;
            variant12RotationStartTime = coreStartTime + rotationDelayWithinCore;
            variant12RotationEnabled = false;
            Debug.Log($"<color=magenta>ElementalBeam STACKED 1+2: coreLifetime={coreLifetime:F2}s, fraction={fraction:F2}, rotation starts at t={variant12RotationStartTime:F2}</color>");
        }

        // Determine which side of screen we're on (left or right of center)
        Camera mainCam = Camera.main;

        // Store player controller reference for later firepoint selection
        AdvancedPlayerController playerController = FindObjectOfType<AdvancedPlayerController>();

        // We'll select the correct firepoint AFTER determining firing direction
        Vector3 playerPos = playerController != null ? playerController.transform.position : transform.position;
        bool tempIsOnLeftSide = mainCam != null && playerPos.x < mainCam.transform.position.x;

        // Mark as initialized so Update() can run
        isInitialized = true;

        // CRITICAL FIX: Enable renderers after one frame to ensure position is stable
        // This prevents visible repositioning that can happen if Update() runs before render
        StartCoroutine(EnableRenderersNextFrame());

        // Check beam position after offset for rotation direction
        isOnLeftSide = mainCam != null && transform.position.x < mainCam.transform.position.x;

        // CRITICAL: Store scale multipliers for LateUpdate (to work with animation)
        // Reset multipliers first
        variant2ScaleMultiplier = 1f;
        sizeModifierMultiplier = 1f;

        if (enhancedVariant == 2)
        {
            // Store variant 2 scale reduction as multiplier (0.735 = 73.5% reduction)
            variant2ScaleMultiplier = (1f - variant2ReducedXScale);
            
            // CRITICAL: Apply Variant 2 scale IMMEDIATELY so smart targeting uses correct collider size
            Vector3 scale = transform.localScale;
            scale.x *= variant2ScaleMultiplier;
            transform.localScale = scale;
            
            Debug.Log($"<color=gold>Variant 2: Applied X scale multiplier {variant2ScaleMultiplier:F3} ({variant2ReducedXScale * 100f}% reduction) BEFORE smart targeting</color>");
        }

        // Store size multiplier - ONLY X SCALE for ElementalBeam
        if (modifiers.sizeMultiplier != 1f)
        {
            sizeModifierMultiplier = modifiers.sizeMultiplier;
            Debug.Log($"<color=cyan>Size multiplier stored: {sizeModifierMultiplier:F2}</color>");

            // Scale collider X-only using utility with colliderSizeOffset
            ColliderScaler.ScaleColliderXOnly(_collider2D, modifiers.sizeMultiplier, colliderSizeOffset);
        }

        Debug.Log($"<color=lime>Scale multipliers set - Variant2: {variant2ScaleMultiplier:F3}, SizeModifier: {sizeModifierMultiplier:F2}</color>");

        // Cache PlayerStats so we can apply damage multipliers + crit PER
        // INSTANCE instead of once at launch.
        cachedPlayerStats = stats;

        // Store post-card base damage (without PlayerStats multipliers/crit) in
        // the damage field for internal use by Variant 1 and DamageRoutine.
        damage = finalDamage;

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

        // Generate key based ONLY on projectile type (so all ElementalBeams share same cooldown)
        prefabKey = $"ElementalBeam_{projectileType}";

        // Only check cooldown/mana for first projectile in multi-spawn
        if (!skipCooldownCheck)
        {
            // Check cooldown for this specific projectile type
            if (!bypassEnhancedFirstSpawnCooldown && lastFireTimes.ContainsKey(prefabKey))
            {
                if (Time.time - lastFireTimes[prefabKey] < finalCooldown)
                {
                    Debug.Log($"<color=yellow>ElementalBeam ({prefabKey}) on cooldown - {Time.time - lastFireTimes[prefabKey]:F2}s / {finalCooldown}s</color>");
                    Destroy(gameObject);
                    return;
                }
            }

            // Check mana with modified cost
            if (playerMana != null && !playerMana.Spend(finalManaCost))
            {
                Debug.Log($"Not enough mana for ElementalBeam (cost: {finalManaCost})");
                Destroy(gameObject);
                return;
            }

            // Record fire time for this projectile type
            lastFireTimes[prefabKey] = Time.time;
        }
        else
        {
            Debug.Log($"<color=gold>ElementalBeam: Skipping cooldown/mana check (multi-projectile spawn)</color>");
        }

        if (_collider2D != null && colliderToIgnore != null)
        {
            Physics2D.IgnoreCollision(_collider2D, colliderToIgnore, true);
        }

        // Determine initial firing direction based on variant
        Vector2 initialDir;

        // STACKED Variant 1 + 3 secondary beams: use the precomputed mirrored
        // direction passed in via 'direction' instead of re-randomizing.
        if (isStackedVariant13 && isVariant13SecondaryBeam)
        {
            if (direction.sqrMagnitude > 0.0001f)
            {
                initialDir = direction.normalized;
            }
            else
            {
                initialDir = Vector2.up;
            }
        }
        else if (enhancedVariant == 1)
        {
            // VARIANT 1 (and STACKED 1+3 primary): Fire in random direction
            // within the ENHANCED custom angle range (minAngleEnhanced/maxAngleEnhanced).
            float minAngle = -90f;
            float maxAngle = 90f;

            if (card != null)
            {
                minAngle = card.minAngleEnhanced;
                maxAngle = card.maxAngleEnhanced;
                Debug.Log($"<color=magenta>Variant 1: Using ENHANCED custom angles ({minAngle:F0}° to {maxAngle:F0}°)</color>");
            }

            float randomAngle = Random.Range(minAngle, maxAngle);
            float randomAngleRad = randomAngle * Mathf.Deg2Rad;

            if (isStackedVariant13 && !skipCooldownCheck)
            {
                // STACKED Variant 1 + 3 primary: allow the PRIMARY beam to fire
                // at ANY angle (positive or negative) within the configured
                // range. The SECONDARY beam fires at the exact opposite angle
                // (-primaryAngle) so the pair is always mirrored across the
                // horizontal axis.

                // For symmetric angle ranges (e.g. -60°..+60°) this keeps BOTH
                // beams inside the allowed range. If the range is asymmetric,
                // we still honour the primary randomAngle and mirror the
                // secondary.

                float primaryAngle = Mathf.Clamp(randomAngle, minAngle, maxAngle);
                float primaryRad = primaryAngle * Mathf.Deg2Rad;
                initialDir = new Vector2(Mathf.Cos(primaryRad), Mathf.Sin(primaryRad));

                // Secondary beam uses an angle mirrored around 90° instead of 0°.
                // Example: 120° → 60°, 30° → 150°.
                float secondaryAngle = 180f - primaryAngle;
                float secondaryRad = secondaryAngle * Mathf.Deg2Rad;
                Vector2 secondaryDir = new Vector2(Mathf.Cos(secondaryRad), Mathf.Sin(secondaryRad));

                Debug.Log($"<color=magenta>ElementalBeam STACKED 1+3: primary {primaryAngle:F1}°, secondary {secondaryAngle:F1}° (seed {randomAngle:F1}°, range {minAngle:F0}°..{maxAngle:F0}°)</color>");

                SpawnVariant13SecondaryBeam(card, colliderToIgnore, playerMana, playerPosition, secondaryDir);
            }
            else
            {
                // Non-stacked Variant 1 keeps the original random behaviour.
                initialDir = new Vector2(Mathf.Cos(randomAngleRad), Mathf.Sin(randomAngleRad));
                Debug.Log($"<color=magenta>ElementalBeam VARIANT 1: Random direction at {randomAngle:F0}°</color>");
            }
        }
        else if (enhancedVariant == 3)
        {
            // VARIANT 3: Dual smart beams.
            // Any beam that is NOT explicitly marked as a secondary will run the
            // shared SmartTargetingBatch routine and claim its OWN pair of
            // smart-sampled directions for this volley. This guarantees that
            // multiple primary beams from projectileCount fan out across
            // different angles instead of sharing the same pair.
            bool isSecondaryBeam = isVariant3SecondaryBeam || isVariant13SecondaryBeam || isVariant23SecondaryBeam;

            if (!isSecondaryBeam)
            {
                Vector2 primaryDir;
                Vector2 secondaryDir;
                ClaimSmartTargetDirectionPair(card, skipCooldownCheck, out primaryDir, out secondaryDir);

                // Use primary direction for THIS beam
                initialDir = primaryDir;

                // Spawn the secondary beam with the second-best direction for
                // this claim. Each primary/secondary pair consumes unique
                // samples from the batch when possible.
                SpawnVariant3SecondaryBeam(card, colliderToIgnore, playerMana, playerPosition, secondaryDir);
            }
            else
            {
                // Secondary beam: use the provided direction directly (precomputed)
                if (direction.sqrMagnitude > 0.0001f)
                {
                    initialDir = direction.normalized;
                }
                else
                {
                    initialDir = Vector2.up;
                }
            }
        }
        else if (enhancedVariant == 2 && isStackedVariant23)
        {
            // STACKED Variant 2 + 3: Dual smart beams using Variant 2 stats.
            // Same rule as pure Variant 3: any non-secondary beam (including
            // extra beams from projectileCount) claims its own pair of
            // directions from the shared SmartTargetingBatch so volleys do not
            // reuse the same pair for every primary.
            bool isSecondaryBeam = isVariant3SecondaryBeam || isVariant13SecondaryBeam || isVariant23SecondaryBeam;

            if (!isSecondaryBeam)
            {
                Vector2 primaryDir;
                Vector2 secondaryDir;
                ClaimSmartTargetDirectionPair(card, skipCooldownCheck, out primaryDir, out secondaryDir);

                // Use primary direction for THIS beam
                initialDir = primaryDir;

                // Spawn the secondary beam with the second-best direction
                SpawnVariant23SecondaryBeam(card, colliderToIgnore, playerMana, playerPosition, secondaryDir);
            }
            else
            {
                // Secondary beam: use the provided direction directly (precomputed)
                if (direction.sqrMagnitude > 0.0001f)
                {
                    initialDir = direction.normalized;
                }
                else
                {
                    initialDir = Vector2.up;
                }
            }
        }
        else if (enhancedVariant == 2)
        {
            // VARIANT 2 (and STACKED 1+2): Use smart targeting.
            //
            // For stacked Variant 1+2 we want the beam to point exactly where
            // Variant 2's smart-targeting says (e.g., directly at the only
            // enemy on screen), without any extra angle-range clamping that
            // could pull it away from the true best direction.

            if (isStackedVariant12)
            {
                // STACKED 1+2: pure smart-targeted direction, no clamping.
                initialDir = ClaimSmartTargetDirection(card, skipCooldownCheck);
                float smartAngle = Mathf.Atan2(initialDir.y, initialDir.x) * Mathf.Rad2Deg;
                Debug.Log($"<color=magenta>ElementalBeam STACKED 1+2: Using pure smart-targeted angle {smartAngle:F1}° with no angle-range clamp.</color>");
            }
            else
            {
                // PURE Variant 2: Smart targeting WITH optional enhanced angle
                // range clamping, as before.
                float minAngle = -90f;
                float maxAngle = 90f;

                if (card != null && card.useCustomAnglesEnhancedVariant2)
                {
                    minAngle = card.minAngleEnhancedVariant2;
                    maxAngle = card.maxAngleEnhancedVariant2;
                    Debug.Log($"<color=magenta>ElementalBeam VARIANT 2: Using ENHANCED Variant 2 custom angles ({minAngle:F0}° to {maxAngle:F0}°)</color>");
                }

                // Use smart targeting within base detection, then clamp to the
                // selected angle window.
                initialDir = ClaimSmartTargetDirection(card, skipCooldownCheck);
                float smartAngle = Mathf.Atan2(initialDir.y, initialDir.x) * Mathf.Rad2Deg;

                if (smartAngle < minAngle || smartAngle > maxAngle)
                {
                    float clampedAngle = Mathf.Clamp(smartAngle, minAngle, maxAngle);
                    float clampedAngleRad = clampedAngle * Mathf.Deg2Rad;
                    initialDir = new Vector2(Mathf.Cos(clampedAngleRad), Mathf.Sin(clampedAngleRad));
                    Debug.Log($"<color=yellow>ElementalBeam VARIANT 2: Smart angle {smartAngle:F0}° clamped to {clampedAngle:F0}° (range: {minAngle:F0}° to {maxAngle:F0}°)</color>");
                }
                else
                {
                    Debug.Log($"<color=cyan>ElementalBeam VARIANT 2: Smart angle {smartAngle:F0}° within range ({minAngle:F0}° to {maxAngle:F0}°)</color>");
                }
            }
        }
        else if (useSmartTargeting)
        {
            // BASE: Per-beam smart targeting. Each staggered beam recomputes
            // its own best direction at fire time instead of reusing a
            // SmartTargetingBatch from an earlier beam in the volley. This
            // prevents later beams from aiming at spots where enemies have
            // already died.
            initialDir = FindBestTargetingDirection(card);
            float smartAngle = Mathf.Atan2(initialDir.y, initialDir.x) * Mathf.Rad2Deg;
            Debug.Log($"<color=cyan>ElementalBeam BASE: Initial smart direction (per-beam scan): ({initialDir.x:F2}, {initialDir.y:F2}), angle: {smartAngle:F1}°</color>");
        }
        else
        {
            // Fallback
            if (direction.sqrMagnitude > 0.0001f)
            {
                initialDir = direction.normalized;
            }
            else
            {
                initialDir = Vector2.up;
            }
        }

        initialDir = initialDir.normalized;
        float initialAngle = Mathf.Atan2(initialDir.y, initialDir.x) * Mathf.Rad2Deg;

        // CRITICAL: Select firepoint based on firing direction (left or right)
        // Determine if firing left or right based on X component of direction.
        // For stacked Variant 1+3, each beam still uses its OWN direction here,
        // so the mirrored secondary naturally picks the opposite side.
        bool firingRight = initialDir.x >= 0f;

        if (playerController != null)
        {
            Transform selectedFirePoint = firingRight ? playerController.elementalBeamFirePointRight : playerController.elementalBeamFirePointLeft;

            if (selectedFirePoint != null)
            {
                firePointTransform = selectedFirePoint;
                playerPos = selectedFirePoint.position;
                Debug.Log($"<color=lime>ElementalBeam: Firing {(firingRight ? "RIGHT" : "LEFT")} - Using firepoint '{selectedFirePoint.name}' at {selectedFirePoint.position}</color>");
            }
            else
            {
                Debug.LogWarning($"<color=yellow>ElementalBeam: No firepoint for {(firingRight ? "RIGHT" : "LEFT")} side, using player position!</color>");
                firePointTransform = playerController.transform;
                playerPos = playerController.transform.position;
            }
        }

        // STEP 2: Calculate spawn offset based on firing direction and side

        // Determine if firing above or below 45 degrees
        float absAngle = Mathf.Abs(initialAngle);
        bool firingAbove45 = absAngle > 45f && absAngle < 135f; // Between 45° and 135° (or -45° and -135°)

        // Select appropriate offset based on direction and angle
        Vector2 selectedOffset = Vector2.zero;

        // Base offset per SIDE (left/right) that always applies, regardless of
        // variant. Angle-specific offsets are added on top of this so you can
        // tune side placement independently.
        Vector2 sideBaseOffset = firingRight ? beamOffsetRight : beamOffsetLeft;

        // Use Variant 3 secondary offsets for ANY beam that is explicitly
        // marked as a Variant 3-style secondary (pure V3 or stacked modes
        // like Variant 1+3). This keeps stacking future-proof: as long as a
        // secondary beam sets one of these flags, it will use the dedicated
        // secondary offsets.
        bool useVariant3SecondaryOffsets = isVariant3SecondaryBeam || isVariant13SecondaryBeam || isVariant23SecondaryBeam;

        if (firingRight)
        {
            if (useVariant3SecondaryOffsets)
            {
                Vector2 variantOffset = firingAbove45 ? variant3OffsetRightAbove45 : variant3OffsetRightBelow45;
                selectedOffset = sideBaseOffset + variantOffset;
                Debug.Log($"<color=cyan>V3 SECONDARY - Firing RIGHT, {(firingAbove45 ? "ABOVE" : "BELOW")} 45° - Using offset: {selectedOffset} (sideBase={sideBaseOffset}, variantOffset={variantOffset})</color>");
            }
            else
            {
                Vector2 baseOffset = firingAbove45 ? offsetRightAbove45 : offsetRightBelow45;
                selectedOffset = sideBaseOffset + baseOffset;
                Debug.Log($"<color=cyan>Firing RIGHT, {(firingAbove45 ? "ABOVE" : "BELOW")} 45° - Using offset: {selectedOffset} (sideBase={sideBaseOffset}, baseOffset={baseOffset})</color>");
            }
        }
        else
        {
            if (useVariant3SecondaryOffsets)
            {
                Vector2 variantOffset = firingAbove45 ? variant3OffsetLeftAbove45 : variant3OffsetLeftBelow45;
                selectedOffset = sideBaseOffset + variantOffset;
                Debug.Log($"<color=cyan>V3 SECONDARY - Firing LEFT, {(firingAbove45 ? "ABOVE" : "BELOW")} 45° - Using offset: {selectedOffset} (sideBase={sideBaseOffset}, variantOffset={variantOffset})</color>");
            }
            else
            {
                Vector2 baseOffset = firingAbove45 ? offsetLeftAbove45 : offsetLeftBelow45;
                selectedOffset = sideBaseOffset + baseOffset;
                Debug.Log($"<color=cyan>Firing LEFT, {(firingAbove45 ? "ABOVE" : "BELOW")} 45° - Using offset: {selectedOffset} (sideBase={sideBaseOffset}, baseOffset={baseOffset})</color>");
            }
        }

        // Apply spawn offset to world position
        spawnOffsetWorld = selectedOffset;
        transform.position = playerPos + (Vector3)spawnOffsetWorld;
        Debug.Log($"<color=yellow>BEAM POSITIONED AT: {transform.position} (playerPos: {playerPos}, offset: {spawnOffsetWorld})</color>");

        // STEP 3: Use the initial direction (already calculated from player position)
        Vector2 dir = initialDir;
        float angle = initialAngle;

        // CRITICAL FIX: If sprite points along transform.up (green) instead of transform.right (blue),
        // we need to subtract 90° to rotate the sprite to align with transform.right
        // This makes the visual beam match the intended direction (blue/red gizmo)
        // NOTE: Removed spriteRotationOffset as it was causing overshoot issues
        float finalAngle = angle - 90f;

        // Apply rotation IMMEDIATELY (no delay for enhanced)
        transform.rotation = Quaternion.Euler(0f, 0f, finalAngle);

        Debug.Log($"<color=orange>SPRITE ALIGNMENT: Beam rotated to {finalAngle:F1}° (Enhanced: {enhancedVariant})</color>");

        // Debug visualization - draw rays to show beam orientation
        // BLUE = transform.right (sprite's forward direction in Unity) - ACTUAL firing direction
        Debug.DrawRay(transform.position, transform.right * 15f, Color.blue, 5f);

        Debug.Log($"<color=cyan>ElementalBeam FIRING: Dir=({dir.x:F3}, {dir.y:F3}), Angle={angle:F1}°, Final={finalAngle:F1}°, Enhanced={enhancedVariant}</color>");

        // Enhanced Variant 1: Rotating Beam
        if (enhancedVariant == 1)
        {
            // Set initial rotation to MATCH the sprite rotation we just
            // applied (finalAngle). This prevents a 90° snap when the
            // per-frame rotation kicks in.
            currentRotation = finalAngle;
            
            // Normalize the FINAL sprite-aligned angle to -180°..180° for
            // debugging. This matches the actual visual orientation used for
            // the beam.
            float normalizedAngle = finalAngle;
            while (normalizedAngle > 180f) normalizedAngle -= 360f;
            while (normalizedAngle < -180f) normalizedAngle += 360f;

            Debug.Log($"<color=gold>Enhanced Beam Angle Check: FinalAngle={normalizedAngle:F2}°</color>");

            // Variant 1 movement rule: always sweep toward the OPPOSITE side of
            // the screen from which it fired, same as stacked Variant 1+2.
            //  - Fired RIGHT  → rotate LEFT (counter-clockwise, +1)
            //  - Fired LEFT   → rotate RIGHT (clockwise, -1)
            if (firingRight)
            {
                rotationDirection = 1f; // CCW = left
                Debug.Log($"<color=gold>Enhanced Beam V1: Fired RIGHT → rotating LEFT (CCW, +1) at {enhancedRotationSpeed} deg/sec</color>");
            }
            else
            {
                rotationDirection = -1f; // CW = right
                Debug.Log($"<color=gold>Enhanced Beam V1: Fired LEFT → rotating RIGHT (CW, -1) at {enhancedRotationSpeed} deg/sec</color>");
            }
        }
        else if (isVariant12Stacked)
        {
            // STACKED Variant 1+2: start rotation from the SAME sprite
            // orientation used when the beam first fired (finalAngle), so the
            // beam continues smoothly from the red-line firing direction with
            // no mid-beam teleport.
            // Choose rotation direction based purely on which SIDE we fired:
            //  - Fired RIGHT  → rotate LEFT (counter-clockwise, +1)
            //  - Fired LEFT   → rotate RIGHT (clockwise, -1)
            currentRotation = finalAngle;

            if (firingRight)
            {
                rotationDirection = 1f; // CCW = left
                Debug.Log($"<color=gold>STACKED 1+2: Fired RIGHT → rotating LEFT (CCW, +1) at {enhancedV1And2RotationSpeed} deg/sec</color>");
            }
            else
            {
                rotationDirection = -1f; // CW = right
                Debug.Log($"<color=gold>STACKED 1+2: Fired LEFT → rotating RIGHT (CW, -1) at {enhancedV1And2RotationSpeed} deg/sec</color>");
            }
        }

        // Calculate damage interval using variant-specific damage instances
        damageInterval = 1f / baseDamageInstancesPerSecond;
        Debug.Log($"<color=cyan>ElementalBeam: Damage interval = {damageInterval:F3}s (from {baseDamageInstancesPerSecond:F2} instances/sec)</color>");

        // Start animation timing and damage routines
        StartCoroutine(AnimationTimingRoutine(finalLifetime, finalBeamStartDelay, finalBeamEndDuration));

        // Start continuous damage routine for all variants. Enhanced Variant 1
        // still performs an immediate hit on contact via OnTriggerEnter, and
        // then DamageRoutine applies subsequent ticks using damageInterval.
        StartCoroutine(DamageRoutine());
        Debug.Log($"<color=cyan>ElementalBeam: DamageRoutine started (continuous damage)</color>");

        Destroy(gameObject, finalLifetime + finalBeamEndDuration);
        StartBeamSfx();
    }

    private IEnumerator EnableRenderersNextFrame()
    {
        // Wait for end of frame to ensure position is stable
        yield return new WaitForEndOfFrame();

        // Now enable renderers
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer sr in renderers)
        {
            sr.enabled = true;
        }

        Debug.Log($"<color=green>ElementalBeam renderers enabled after position stabilized</color>");
    }

    private IEnumerator AnimationTimingRoutine(float beamLifetime, float startDelay, float endDuration)
    {
        // Get all animators (parent + children)
        Animator[] allAnimators = GetComponentsInChildren<Animator>();

        // Apply animation speed multipliers for Variant 2
        if (enhancedVariant == 2)
        {
            foreach (Animator anim in allAnimators)
            {
                if (anim != null)
                {
                    // Note: Animation speed will be controlled per-phase below
                    Debug.Log($"<color=gold>ElementalBeam Variant 2: Animator found, will apply speed multipliers per phase</color>");
                }
            }
        }

        // For enhanced beams, allow damage during startup at reduced rate
        float startupMult = enhancedStartupDamageMultiplier;
        float endMult = enhancedEndDamageMultiplier;
        if (enhancedVariant == 3)
        {
            startupMult = variant3StartupDamageMultiplier;
            endMult = variant3EndDamageMultiplier;
        }

        if (enhancedVariant > 0)
        {
            canDealDamage = true;
            isInStartupOrEndPhase = true;
            isInStartupPhase = true; // Mark as startup phase
            Debug.Log($"<color=gold>ElementalBeam Enhanced: Damage enabled during startup at {startupMult * 100}%</color>");

            // Apply startup animation speed for Variant 2
            if (enhancedVariant == 2)
            {
                foreach (Animator anim in allAnimators)
                {
                    if (anim != null)
                    {
                        anim.speed = variant2StartAnimationMultiplier;
                        Debug.Log($"<color=gold>Variant 2: Startup animation speed set to {variant2StartAnimationMultiplier}x</color>");
                    }
                }
            }
        }

        // Wait for start animation
        yield return new WaitForSeconds(startDelay);

        // Enable full damage after beam start delay
        canDealDamage = true;
        isInStartupOrEndPhase = false;
        isInStartupPhase = false; // No longer in startup
        Debug.Log($"<color=cyan>ElementalBeam: Full damage enabled after {startDelay}s start delay</color>");

        // Set BeamStart to true for ALL animators (parent + children)
        foreach (Animator anim in allAnimators)
        {
            if (anim != null)
            {
                anim.SetBool("BeamStart", true);

                // Reset animation speed to normal (1.0) for main phase
                if (enhancedVariant == 2)
                {
                    anim.speed = 1f;
                    Debug.Log($"<color=gold>Variant 2: Main animation speed reset to 1.0x</color>");
                }
            }
        }
        Debug.Log($"<color=cyan>ElementalBeam: BeamStart set to TRUE for {allAnimators.Length} animators</color>");

        // Wait for beam lifetime minus start delay
        yield return new WaitForSeconds(beamLifetime - startDelay);

        // For enhanced beams, continue damage during end phase at reduced rate
        if (enhancedVariant > 0)
        {
            isInStartupOrEndPhase = true;
            isInStartupPhase = false; // Mark as end phase (not startup)
            Debug.Log($"<color=gold>ElementalBeam Enhanced: Damage continues during end phase at {endMult * 100}%</color>");
        }
        else
        {
            // DISABLE DAMAGE during beam end animation for basic beams
            canDealDamage = false;
            Debug.Log($"<color=cyan>ElementalBeam: Damage DISABLED for beam end animation</color>");
        }

        // Set BeamEnd to true for ALL animators (parent + children)
        foreach (Animator anim in allAnimators)
        {
            if (anim != null)
            {
                anim.SetBool("BeamStart", false);
                anim.SetBool("BeamEnd", true);

                // Apply end animation speed for Variant 2
                if (enhancedVariant == 2)
                {
                    anim.speed = variant2EndAnimationMultiplier;
                    Debug.Log($"<color=gold>Variant 2: End animation speed set to {variant2EndAnimationMultiplier}x</color>");
                }
            }
        }
        Debug.Log($"<color=cyan>ElementalBeam: BeamEnd set to TRUE for {allAnimators.Length} animators</color>");

        // End animation plays for beamEndDuration, then object is destroyed
    }

    private IEnumerator DamageRoutine()
    {
        while (true)
        {
            // Only deal damage if beam start delay has passed
            if (canDealDamage)
            {
                // Damage all enemies currently in beam
                List<GameObject> enemiesToDamage = new List<GameObject>(enemiesInBeam);

                foreach (GameObject enemy in enemiesToDamage)
                {
                    if (enemy == null)
                    {
                        enemiesInBeam.Remove(enemy);
                        enemyNextDamageTime.Remove(enemy);
                        continue;
                    }

                    // Check if this enemy is ready for damage (respects damagePerSecond)
                    if (enemyNextDamageTime.ContainsKey(enemy) && Time.time < enemyNextDamageTime[enemy])
                    {
                        continue; // Not ready yet, skip this enemy
                    }

                    IDamageable damageable = enemy.GetComponent<IDamageable>() ?? enemy.GetComponentInParent<IDamageable>();

                    if (damageable != null && damageable.IsAlive)
                    {
                        // Check if enemy is within damageable area (on-screen or slightly offscreen)
                        if (!OffscreenDamageChecker.CanTakeDamage(enemy.transform.position))
                        {
                            continue; // Skip this enemy, check next one
                        }

                        // Calculate damage multiplier based on phase
                        float damageMultiplier = 1f;
                        if (enhancedVariant > 0 && isInStartupOrEndPhase)
                        {
                            if (enhancedVariant == 3)
                            {
                                damageMultiplier = isInStartupPhase ? variant3StartupDamageMultiplier : variant3EndDamageMultiplier;
                            }
                            else
                            {
                                damageMultiplier = isInStartupPhase ? enhancedStartupDamageMultiplier : enhancedEndDamageMultiplier;
                            }
                        }

                        float basePerTick = baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
                        float damageBeforeStats = basePerTick * damageMultiplier;

                        if (damageMultiplier <= 0f)
                        {
                            continue;
                        }

                        float finalDamage = damageBeforeStats;

                        Component damageableComponent = damageable as Component;
                        GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : enemy;

                        if (cachedPlayerStats != null)
                        {
                            finalDamage = PlayerDamageHelper.ComputeContinuousProjectileDamage(cachedPlayerStats, enemyObject, damageBeforeStats, gameObject);
                        }

                        Vector3 hitPoint = enemy.transform.position;
                        Vector3 hitNormal = (transform.position - hitPoint).normalized;

                        // Tag EnemyHealth so it can render the beam hit using
                        // the correct elemental color.
                        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>() ?? enemy.GetComponentInParent<EnemyHealth>();
                        if (enemyHealth != null)
                        {
                            DamageNumberManager.DamageType damageType = projectileType == ProjectileType.Fire ?
                                DamageNumberManager.DamageType.Fire : DamageNumberManager.DamageType.Ice;
                            enemyHealth.SetLastIncomingDamageType(damageType);
                        }

                        damageable.TakeDamage(finalDamage, hitPoint, hitNormal);

                        // Set next damage time for this enemy (shared damageInterval)
                        enemyNextDamageTime[enemy] = Time.time + damageInterval;

                        BurnEffect burnEffect = GetComponent<BurnEffect>();
                        if (burnEffect != null)
                        {
                            burnEffect.Initialize(finalDamage, projectileType);
                            burnEffect.TryApplyBurn(enemy, hitPoint);
                        }

                        SlowEffect slowEffect = GetComponent<SlowEffect>();
                        if (slowEffect != null)
                        {
                            slowEffect.TryApplySlow(enemy, hitPoint);
                        }

                        // Spawn hit effect
                        if (hitEffectPrefab != null)
                        {
                            GameObject effect = Instantiate(hitEffectPrefab, hitPoint, Quaternion.identity);
                            Destroy(effect, hitEffectDuration);
                        }

                        // Play impact sound
                        if (impactClip != null)
                        {
                            AudioSource.PlayClipAtPoint(impactClip, hitPoint, impactVolume);
                        }
                    }
                    else if (damageable != null && !damageable.IsAlive)
                    {
                        // Remove dead enemies from tracking
                        enemiesInBeam.Remove(enemy);
                        enemyNextDamageTime.Remove(enemy);
                    }
                }
            }

            // Check frequently but damage is controlled per-enemy
            yield return new WaitForSeconds(0.05f); // Check 20 times per second
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (((1 << collision.gameObject.layer) & enemyLayer) != 0)
        {
            IDamageable damageable = collision.gameObject.GetComponent<IDamageable>() ?? collision.gameObject.GetComponentInParent<IDamageable>();

            if (damageable != null && damageable.IsAlive)
            {
                enemiesInBeam.Add(collision.gameObject);
                Debug.Log($"<color=green>Enemy {collision.gameObject.name} entered beam</color>");

                BurnEffect burnEffect = GetComponent<BurnEffect>();
                SlowEffect slowEffect = GetComponent<SlowEffect>();

                // Resolve the concrete enemy GameObject once so both Variant 1 and base/Variant 2
                // can use the same reference for favour checks and damage application.
                Component damageableComponent = damageable as Component;
                GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : collision.gameObject;

                // ENHANCED VARIANT 1 ONLY: Deal damage ONCE on contact with multiplied damage
                // Variant 2 uses continuous DamageRoutine instead
                if (enhancedVariant == 1)
                {
                    // Check if already damaged this enemy
                    if (enemiesDamagedOnce.Contains(collision.gameObject))
                    {
                        Debug.Log($"<color=yellow>Enhanced beam already damaged {collision.gameObject.name}, skipping</color>");
                        return;
                    }

                    if (!canDealDamage)
                    {
                        Debug.Log($"<color=yellow>ElementalBeam: Damage blocked during start delay</color>");
                        return;
                    }

                    if (!OffscreenDamageChecker.CanTakeDamage(collision.transform.position))
                    {
                        return;
                    }

                    Vector3 hitPoint1 = collision.ClosestPoint(transform.position);
                    Vector3 hitNormal1 = (transform.position - collision.transform.position).normalized;

                    // CRITICAL: Enhanced beam damage uses baseDamageAfterCards with a
                    // phase multiplier (startup/normal/end). For Variant 1 we now
                    // treat this as a single DAMAGE TICK (no extra
                    // damageInstancesPerSecond multiplier) so that the first
                    // contact hit matches the per-tick damage used by
                    // DamageRoutine.
                    float damageMultiplier = 1f;
                    if (isInStartupOrEndPhase)
                    {
                        damageMultiplier = isInStartupPhase ? enhancedStartupDamageMultiplier : enhancedEndDamageMultiplier;
                        string phase = isInStartupPhase ? "startup" : "end";
                        Debug.Log($"<color=orange>Enhanced beam in {phase} phase, damage multiplier: {damageMultiplier:F2}</color>");
                    }

                    float basePerTick = baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
                    float damageBeforeStats = basePerTick * damageMultiplier;

                    float finalDamage1 = damageBeforeStats;
                    if (cachedPlayerStats != null)
                    {
                        finalDamage1 = PlayerDamageHelper.ComputeContinuousProjectileDamage(cachedPlayerStats, enemyObject, damageBeforeStats, gameObject);
                    }

                    // Tag EnemyHealth BEFORE applying damage so the very first
                    // Variant 1 contact hit uses the correct elemental color
                    // in the EnemyHealth damage-number pipeline.
                    EnemyHealth enemyHealth1 = collision.gameObject.GetComponent<EnemyHealth>() ?? collision.gameObject.GetComponentInParent<EnemyHealth>();
                    if (enemyHealth1 != null)
                    {
                        DamageNumberManager.DamageType damageType = projectileType == ProjectileType.Fire ?
                            DamageNumberManager.DamageType.Fire : DamageNumberManager.DamageType.Ice;
                        enemyHealth1.SetLastIncomingDamageType(damageType);
                    }

                    damageable.TakeDamage(finalDamage1, hitPoint1, hitNormal1);
                    enemiesDamagedOnce.Add(collision.gameObject); // Mark as damaged
                    Debug.Log($"<color=orange>Variant 1: ONE-TIME damage {finalDamage1:F2} to {collision.gameObject.name}</color>");

                    // Schedule the next tick for this enemy so that
                    // DamageRoutine continues applying damage every
                    // damageInterval seconds after this initial hit.
                    enemyNextDamageTime[collision.gameObject] = Time.time + damageInterval;

                    if (burnEffect != null)
                    {
                        burnEffect.Initialize(finalDamage1, projectileType);
                        burnEffect.TryApplyBurn(collision.gameObject, hitPoint1);
                    }

                    if (slowEffect != null)
                    {
                        slowEffect.TryApplySlow(collision.gameObject, hitPoint1);
                    }

                    // Spawn hit effect
                    if (hitEffectPrefab != null)
                    {
                        GameObject effect = Instantiate(hitEffectPrefab, hitPoint1, Quaternion.identity);
                        Destroy(effect, hitEffectDuration);
                    }

                    // Play impact sound
                    if (impactClip != null)
                    {
                        AudioSource.PlayClipAtPoint(impactClip, hitPoint1, impactVolume);
                    }

                    return; // Don't add to normal damage routine
                }

                // BASE BEAM & VARIANT 2: Set next damage time to NOW so damage happens immediately on first contact
                // Variant 2 uses DamageRoutine for continuous damage, NOT one-time damage
                enemyNextDamageTime[collision.gameObject] = Time.time;

                // CRITICAL: Variant 2 should NOT deal instant damage here
                // It only uses DamageRoutine for continuous damage
                if (enhancedVariant == 2)
                {
                    Debug.Log($"<color=gold>Variant 2: Enemy entered beam, will be damaged by DamageRoutine (no instant damage)</color>");
                    return; // Skip instant damage for Variant 2
                }

                // NO instant damage here - let DamageRoutine handle it
                if (!canDealDamage)
                {
                    Debug.Log($"<color=yellow>ElementalBeam: Damage blocked during start delay</color>");
                    return;
                }

                if (!OffscreenDamageChecker.CanTakeDamage(collision.transform.position))
                {
                    return;
                }

                Vector3 hitPoint = collision.ClosestPoint(transform.position);
                Vector3 hitNormal = (transform.position - collision.transform.position).normalized;

                // BASE BEAM: single instant hit on first contact. Use
                // baseDamageAfterCards and roll crit PER ENEMY via PlayerStats.
                float basePerHit = baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
                float finalDamage = basePerHit;
                if (cachedPlayerStats != null)
                {
                    finalDamage = PlayerDamageHelper.ComputeContinuousProjectileDamage(cachedPlayerStats, enemyObject, basePerHit, gameObject);
                }

                // Tag EnemyHealth BEFORE applying this base-beam instant hit so
                // the damage number for the very first contact also uses the
                // correct elemental color.
                EnemyHealth enemyHealth2 = collision.gameObject.GetComponent<EnemyHealth>() ?? collision.gameObject.GetComponentInParent<EnemyHealth>();
                if (enemyHealth2 != null)
                {
                    DamageNumberManager.DamageType damageType = projectileType == ProjectileType.Fire ?
                        DamageNumberManager.DamageType.Fire : DamageNumberManager.DamageType.Ice;
                    enemyHealth2.SetLastIncomingDamageType(damageType);
                }

                damageable.TakeDamage(finalDamage, hitPoint, hitNormal);
                Debug.Log($"<color=orange>ElementalBeam: INSTANT damage {finalDamage:F2} to {collision.gameObject.name} on first contact</color>");

                if (burnEffect != null)
                {
                    burnEffect.Initialize(finalDamage, projectileType);
                    burnEffect.TryApplyBurn(collision.gameObject, hitPoint);
                }

                if (slowEffect != null)
                {
                    slowEffect.TryApplySlow(collision.gameObject, hitPoint);
                }

                // Spawn hit effect
                if (hitEffectPrefab != null)
                {
                    GameObject effect = Instantiate(hitEffectPrefab, hitPoint, Quaternion.identity);
                    Destroy(effect, hitEffectDuration);
                }

                // Play impact sound
                if (impactClip != null)
                {
                    AudioSource.PlayClipAtPoint(impactClip, hitPoint, impactVolume);
                }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (((1 << collision.gameObject.layer) & enemyLayer) != 0)
        {
            if (enemiesInBeam.Contains(collision.gameObject))
            {
                enemiesInBeam.Remove(collision.gameObject);
                Debug.Log($"<color=yellow>Enemy {collision.gameObject.name} exited beam</color>");
            }
        }
    }

    private void EnsureBeamAudioSource()
    {
        if (_beamSource == null)
        {
            _beamSource = GetComponent<AudioSource>();
            if (_beamSource == null)
            {
                _beamSource = gameObject.AddComponent<AudioSource>();
            }
        }

        _beamSource.playOnAwake = false;
        _beamSource.loop = beamLoop;
        _beamSource.spatialBlend = beamSpatialBlend;
        _beamSource.dopplerLevel = beamDopplerLevel;
        _beamSource.rolloffMode = AudioRolloffMode.Linear;
        _beamSource.minDistance = 1f;
        _beamSource.maxDistance = 30f;
    }

    private void StartBeamSfx()
    {
        if (beamClip == null) return;

        EnsureBeamAudioSource();

        if (_fadeOutRoutine != null)
        {
            StopCoroutine(_fadeOutRoutine);
            _fadeOutRoutine = null;
        }

        _beamSource.clip = beamClip;
        _beamSource.volume = beamVolume;
        _beamSource.pitch = beamPitch;
        _beamSource.loop = beamLoop;

        if (!_beamSource.isPlaying)
        {
            _beamSource.Play();
        }
    }

    private void StopBeamSfx(bool instant)
    {
        if (_beamSource == null) return;

        if (instant || beamFadeOutSeconds <= 0f || !_beamSource.isPlaying)
        {
            _beamSource.Stop();
            _beamSource.clip = null;
            return;
        }

        if (_fadeOutRoutine != null)
        {
            StopCoroutine(_fadeOutRoutine);
        }
        _fadeOutRoutine = StartCoroutine(FadeOutAndStop(_beamSource, beamFadeOutSeconds));
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
    /// PERFORMANCE: Efficient camera bounds check using precomputed bounds
    /// </summary>
    private bool IsInCameraBounds(Vector3 position, Camera cam, float halfWidth, float halfHeight)
    {
        float distX = Mathf.Abs(position.x - cam.transform.position.x);
        float distY = Mathf.Abs(position.y - cam.transform.position.y);
        return distX <= halfWidth && distY <= halfHeight;
    }

    /// <summary>
    /// Finds the direction that would hit the most enemies based on selected algorithm
    /// </summary>
    private Vector2 FindBestTargetingDirection(ProjectileCards card)
    {
        switch (targetingMode)
        {
            case SmartTargetingMode.RaycastSampling:
                return FindBestDirection_RaycastSampling(card);
            case SmartTargetingMode.ClusterAnalysis:
                return FindBestDirection_ClusterAnalysis(card);
            case SmartTargetingMode.WeightedDistance:
                return FindBestDirection_WeightedDistance(card);
            case SmartTargetingMode.GridSweep:
                return FindBestDirection_GridSweep(card);
            default:
                return FindBestDirection_RaycastSampling(card);
        }
    }

    // Build a raycast-sampling batch for the given card using the same rules
    // as FindBestDirection_RaycastSampling, but keeping ALL sampled angles and
    // hit counts so multiple beams from the same volley can claim different
    // hit-capable directions.
    private SmartTargetingBatch BuildRaycastSmartTargetingBatchForCard(ProjectileCards card)
    {
        SmartTargetingBatch batch = new SmartTargetingBatch();

        // Get angle range from card settings
        float minAngle = -90f;
        float maxAngle = 90f;

        if (card != null)
        {
            // VARIANT 2: Use Enhanced Variant 2 angle settings
            if (enhancedVariant == 2 && card.useCustomAnglesEnhancedVariant2)
            {
                minAngle = card.minAngleEnhancedVariant2;
                maxAngle = card.maxAngleEnhancedVariant2;
            }
            // VARIANT 1: Use Enhanced angle settings
            else if (enhancedVariant == 1 && card.useCustomAnglesEnhanced)
            {
                minAngle = card.minAngleEnhanced;
                maxAngle = card.maxAngleEnhanced;
            }
            // BASE: Use base angle settings
            else if (card.useCustomAngles)
            {
                minAngle = card.minAngle;
                maxAngle = card.maxAngle;
            }
        }

        batch.minAngle = minAngle;
        batch.maxAngle = maxAngle;

        // PERFORMANCE: Use OverlapCircleNonAlloc with cached array (no GC allocation!)
        int colliderCount = Physics2D.OverlapCircleNonAlloc(transform.position, smartTargetingDetectionRange, cachedColliders, enemyLayer);

        // PERFORMANCE: Clear and reuse cached collections (no GC!)
        cachedEnemySet.Clear();
        cachedEnemyList.Clear();

        Camera mainCam = Camera.main;

        if (mainCam != null)
        {
            // Calculate camera bounds with tolerance (precompute half-widths)
            float cameraHeight = mainCam.orthographicSize * 2f * onCameraTolerance;
            float cameraWidth = cameraHeight * mainCam.aspect;
            float halfWidth = cameraWidth / 2f;
            float halfHeight = cameraHeight / 2f;
            Vector3 camPos = mainCam.transform.position;

            // PERFORMANCE: Use for loop with cached array instead of foreach
            for (int i = 0; i < colliderCount; i++)
            {
                Collider2D collider = cachedColliders[i];
                if (collider == null) continue;

                GameObject enemyObj = collider.gameObject;

                // Skip if we've already added this enemy GameObject
                if (cachedEnemySet.Contains(enemyObj)) continue;

                Vector3 enemyPos = enemyObj.transform.position;

                float distX = Mathf.Abs(enemyPos.x - camPos.x);
                float distY = Mathf.Abs(enemyPos.y - camPos.y);

                // PERFORMANCE: Use precomputed half-widths
                bool isOnCamera = distX <= halfWidth && distY <= halfHeight;

                if (isOnCamera)
                {
                    cachedEnemySet.Add(enemyObj);
                    cachedEnemyList.Add(enemyObj);
                }
            }
        }

        int enemyCount = cachedEnemyList.Count;

        // Get beam collider dimensions in WORLD space (derived from current collider)
        float beamWidth;
        float beamLength;
        GetBeamDimensions(out beamWidth, out beamLength);
        float beamHalfWidth = beamWidth / 2f;

        int samplesToUse;
        if (enhancedVariant == 2)
        {
            samplesToUse = variant2SmartTargetingSamples;
        }
        else if (enhancedVariant == 3)
        {
            samplesToUse = variant3SmartTargetingSamples;
        }
        else
        {
            samplesToUse = smartTargetingSamples;
        }

        if (samplesToUse < 1)
        {
            samplesToUse = 1;
        }

        batch.candidateAngles.Clear();
        batch.candidateDirections.Clear();
        batch.candidateHits.Clear();
        batch.unusedHitIndices.Clear();
        batch.usedHitIndices.Clear();
        batch.hasAnyHits = false;
        batch.reuseCursor = 0;
        batch.noHitCursor = 0;

        // If there are no enemies, we still want a deterministic spread of
        // sample directions across the allowed angle window so multiple beams
        // can fan out instead of all stacking on a single random direction.
        if (enemyCount == 0)
        {
            for (int angleIndex = 0; angleIndex < samplesToUse; angleIndex++)
            {
                float angleDeg = Mathf.Lerp(minAngle, maxAngle, (float)angleIndex / (samplesToUse - 1));
                float angleRad = angleDeg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

                batch.candidateAngles.Add(angleDeg);
                batch.candidateDirections.Add(dir);
                batch.candidateHits.Add(0);
            }

            // No hit-capable samples
            batch.hasAnyHits = false;
            return batch;
        }

        // Otherwise, fully evaluate each sample angle and count hits.
        List<int> hitIndices = new List<int>();

        for (int angleIndex = 0; angleIndex < samplesToUse; angleIndex++)
        {
            float angleDeg = Mathf.Lerp(minAngle, maxAngle, (float)angleIndex / (samplesToUse - 1));
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector2 testDirection = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

            int hitsThisAngle = 0;

            for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
            {
                GameObject enemy = cachedEnemyList[enemyIndex];
                if (enemy == null) continue;

                Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
                Vector2 enemyPosition;
                float radius = 0.5f; // Default radius

                if (enemyCollider != null)
                {
                    Bounds bounds = enemyCollider.bounds;
                    enemyPosition = bounds.center;
                    radius = Mathf.Max(bounds.extents.x, bounds.extents.y);
                }
                else
                {
                    enemyPosition = enemy.transform.position;
                }

                Vector2 vectorToEnemy = enemyPosition - (Vector2)transform.position;
                float distAlongBeam = Vector2.Dot(vectorToEnemy, testDirection);

                // Skip if behind or too far
                if (distAlongBeam < 0 || distAlongBeam > beamLength)
                    continue;

                // Calculate perpendicular distance from beam center line
                Vector2 perpDir = new Vector2(-testDirection.y, testDirection.x);
                float perpDist = Mathf.Abs(Vector2.Dot(vectorToEnemy, perpDir));

                // Add tolerance for extreme angles
                float hitTolerance = 0.5f;

                // Check if within beam width (with tolerance)
                if (perpDist <= beamHalfWidth + radius + hitTolerance)
                {
                    hitsThisAngle++;
                }
            }

            batch.candidateAngles.Add(angleDeg);
            batch.candidateDirections.Add(testDirection);
            batch.candidateHits.Add(hitsThisAngle);

            if (hitsThisAngle > 0)
            {
                hitIndices.Add(angleIndex);
            }
        }

        if (hitIndices.Count > 0)
        {
            // Sort hit-capable indices by descending hit count so the earliest
            // beams in a volley get the strongest angles.
            hitIndices.Sort((a, b) => batch.candidateHits[b].CompareTo(batch.candidateHits[a]));
            batch.unusedHitIndices.AddRange(hitIndices);
            batch.hasAnyHits = true;
            return batch;
        }

        // SAFETY FALLBACK: If we found enemies but NONE of the sampled angles
        // registered as hits (e.g., due to extreme narrowing or minor
        // rounding/geometry issues), approximate good directions by aiming at
        // the enemies directly. This prevents cases where Variant 2 beams fan
        // out into empty space even though enemies are clearly on-screen.
        if (enemyCount > 0)
        {
            List<int> fallbackIndices = new List<int>();

            for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
            {
                GameObject enemy = cachedEnemyList[enemyIndex];
                if (enemy == null) continue;

                Vector2 enemyPos = enemy.transform.position;
                Vector2 toEnemy = enemyPos - (Vector2)transform.position;
                if (toEnemy.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                float enemyAngle = Mathf.Atan2(toEnemy.y, toEnemy.x) * Mathf.Rad2Deg;

                // Find the closest sampled angle to this enemy direction.
                int closestIndex = -1;
                float bestDelta = float.PositiveInfinity;

                for (int angleIndex = 0; angleIndex < batch.candidateAngles.Count; angleIndex++)
                {
                    float candidateAngle = batch.candidateAngles[angleIndex];
                    float delta = Mathf.Abs(Mathf.DeltaAngle(candidateAngle, enemyAngle));

                    if (delta < bestDelta)
                    {
                        bestDelta = delta;
                        closestIndex = angleIndex;
                    }
                }

                if (closestIndex >= 0 && !fallbackIndices.Contains(closestIndex))
                {
                    fallbackIndices.Add(closestIndex);
                    // Treat this sample as hit-capable so it participates in
                    // normal distribution logic.
                    batch.candidateHits[closestIndex] = Mathf.Max(1, batch.candidateHits[closestIndex]);
                }
            }

            if (fallbackIndices.Count > 0)
            {
                // Sort by descending hit count (number of enemies mapped to
                // this direction) so the strongest approximations fire first.
                fallbackIndices.Sort((a, b) => batch.candidateHits[b].CompareTo(batch.candidateHits[a]));
                batch.unusedHitIndices.AddRange(fallbackIndices);
                batch.hasAnyHits = true;
                return batch;
            }
        }

        // No usable hit-capable samples; callers will fall back to the
        // no-hit fan-out behaviour.
        batch.hasAnyHits = false;
        return batch;
    }

    // Claim a smart-targeted direction for this beam. When using raycast-based
    // smart targeting, multiple beams from the same card/volley will share a
    // SmartTargetingBatch keyed by the card instance ID. The first beam in a
    // volley (skipCooldownCheck == false) rebuilds the batch; subsequent beams
    // (skipCooldownCheck == true) reuse it and claim different hit-capable
    // samples when possible.
    private Vector2 ClaimSmartTargetDirection(ProjectileCards card, bool skipCooldownCheck)
    {
        // Only the RaycastSampling mode participates in per-beam sample
        // distribution. Other modes fall back to the original behaviour.
        if (targetingMode != SmartTargetingMode.RaycastSampling)
        {
            return FindBestTargetingDirection(card);
        }

        int key = card != null ? card.GetInstanceID() : 0;

        SmartTargetingBatch batch;
        if (!smartTargetingBatches.TryGetValue(key, out batch) || !skipCooldownCheck)
        {
            // First beam of a volley OR no existing batch: rebuild.
            batch = BuildRaycastSmartTargetingBatchForCard(card);
            smartTargetingBatches[key] = batch;
        }

        if (batch == null || batch.candidateDirections.Count == 0)
        {
            // Fallback to single-beam behaviour.
            return FindBestDirection_RaycastSampling(card);
        }

        // Prefer unused hit-capable samples first.
        if (batch.hasAnyHits && batch.unusedHitIndices.Count > 0)
        {
            int idx = batch.unusedHitIndices[0];
            batch.unusedHitIndices.RemoveAt(0);
            batch.usedHitIndices.Add(idx);
            return batch.candidateDirections[idx];
        }

        // All hit-capable samples have been claimed at least once: reuse them
        // in a round-robin fashion so additional beams still aim at good spots
        // instead of degenerating to random angles.
        if (batch.hasAnyHits && batch.usedHitIndices.Count > 0)
        {
            int reuseCount = batch.usedHitIndices.Count;
            int cursor = Mathf.Abs(batch.reuseCursor) % reuseCount;
            int idx = batch.usedHitIndices[cursor];
            batch.reuseCursor++;
            return batch.candidateDirections[idx];
        }

        // No hit-capable samples at all (no enemies inside the beam region).
        // Distribute beams across the available sample directions so they fan
        // out instead of stacking.
        if (batch.noHitCursor < batch.candidateDirections.Count)
        {
            int idx = batch.noHitCursor++;
            return batch.candidateDirections[idx];
        }

        // Absolute fallback: use the original single-beam logic.
        return FindBestDirection_RaycastSampling(card);
    }

    // Claim a PAIR of smart-targeted directions for dual-beam variants (pure
    // Variant 3 and stacked Variant 2+3). This uses the same
    // SmartTargetingBatch as ClaimSmartTargetDirection so that all primary
    // beams from one volley share a single sampling pass and each claim
    // consumes unique hit-capable angles when possible.
    private void ClaimSmartTargetDirectionPair(ProjectileCards card, bool skipCooldownCheck, out Vector2 primaryDir, out Vector2 secondaryDir)
    {
        primaryDir = Vector2.up;
        secondaryDir = Vector2.up;

        if (targetingMode != SmartTargetingMode.RaycastSampling)
        {
            // Non-raycast modes: just reuse the single best direction for both
            // beams to preserve legacy behaviour.
            Vector2 best = FindBestTargetingDirection(card);
            primaryDir = best;
            secondaryDir = best;
            return;
        }

        int key = card != null ? card.GetInstanceID() : 0;

        SmartTargetingBatch batch;
        if (!smartTargetingBatches.TryGetValue(key, out batch) || !skipCooldownCheck)
        {
            // First primary beam of the volley OR missing batch – rebuild.
            batch = BuildRaycastSmartTargetingBatchForCard(card);
            smartTargetingBatches[key] = batch;
        }

        if (batch == null || batch.candidateDirections.Count == 0)
        {
            Vector2 fallback = FindBestDirection_RaycastSampling(card);
            primaryDir = fallback;
            secondaryDir = fallback;
            return;
        }

        // Prefer unused hit-capable samples first so different primaries fan
        // out across the best angles.
        if (batch.hasAnyHits && batch.unusedHitIndices.Count > 0)
        {
            int primaryIndex = batch.unusedHitIndices[0];
            batch.unusedHitIndices.RemoveAt(0);

            int secondaryIndex = -1;

            if (batch.unusedHitIndices.Count > 0)
            {
                float primaryAngle = batch.candidateAngles[primaryIndex];
                int bestHits = -1;
                float bestAngleDelta = -1f;

                // Choose the remaining hit-capable direction that both hits as
                // many enemies as possible and is as far in angle from the
                // primary as we can get.
                for (int i = 0; i < batch.unusedHitIndices.Count; i++)
                {
                    int idx = batch.unusedHitIndices[i];
                    int hits = batch.candidateHits[idx];
                    float angle = batch.candidateAngles[idx];
                    float delta = Mathf.Abs(Mathf.DeltaAngle(angle, primaryAngle));

                    if (hits > bestHits || (hits == bestHits && delta > bestAngleDelta))
                    {
                        bestHits = hits;
                        bestAngleDelta = delta;
                        secondaryIndex = idx;
                    }
                }

                if (secondaryIndex >= 0)
                {
                    batch.unusedHitIndices.Remove(secondaryIndex);
                }
            }

            // If we couldn't find a second unused index (e.g., only one
            // hit-capable sample), fall back to reusing an existing hit index
            // that is far from the primary when possible.
            if (secondaryIndex < 0 && batch.usedHitIndices.Count > 0)
            {
                float primaryAngle = batch.candidateAngles[primaryIndex];
                float bestAngleDelta = -1f;
                int bestIdx = batch.usedHitIndices[0];

                for (int i = 0; i < batch.usedHitIndices.Count; i++)
                {
                    int idx = batch.usedHitIndices[i];
                    float angle = batch.candidateAngles[idx];
                    float delta = Mathf.Abs(Mathf.DeltaAngle(angle, primaryAngle));

                    if (delta > bestAngleDelta)
                    {
                        bestAngleDelta = delta;
                        bestIdx = idx;
                    }
                }

                secondaryIndex = bestIdx;
            }

            if (secondaryIndex < 0)
            {
                secondaryIndex = primaryIndex;
            }

            if (!batch.usedHitIndices.Contains(primaryIndex))
            {
                batch.usedHitIndices.Add(primaryIndex);
            }
            if (!batch.usedHitIndices.Contains(secondaryIndex))
            {
                batch.usedHitIndices.Add(secondaryIndex);
            }

            primaryDir = batch.candidateDirections[primaryIndex];
            secondaryDir = batch.candidateDirections[secondaryIndex];
            return;
        }

        // All hit-capable samples have been used at least once. Reuse them in
        // a round-robin fashion so extra primaries still aim at good spots.
        if (batch.hasAnyHits && batch.usedHitIndices.Count > 0)
        {
            int reuseCount = batch.usedHitIndices.Count;
            int primaryIndex = batch.usedHitIndices[Mathf.Abs(batch.reuseCursor) % reuseCount];
            batch.reuseCursor++;
            int secondaryIndex = batch.usedHitIndices[Mathf.Abs(batch.reuseCursor) % reuseCount];
            batch.reuseCursor++;

            primaryDir = batch.candidateDirections[primaryIndex];
            secondaryDir = batch.candidateDirections[secondaryIndex];
            return;
        }

        // No hit-capable samples at all – fan out across the sample window
        // using the same noHitCursor that ClaimSmartTargetDirection uses, but
        // consume two distinct samples per claim when possible.
        if (batch.candidateDirections.Count > 0)
        {
            int count = batch.candidateDirections.Count;
            int primaryIndex = batch.noHitCursor % count;
            batch.noHitCursor++;

            int secondaryIndex;
            if (batch.noHitCursor < count)
            {
                secondaryIndex = batch.noHitCursor;
                batch.noHitCursor++;
            }
            else
            {
                // Choose the sample that is farthest in angle from the primary
                // to maximize spread.
                float primaryAngle = batch.candidateAngles[primaryIndex];
                float bestAngleDelta = -1f;
                int bestIdx = primaryIndex;

                for (int i = 0; i < count; i++)
                {
                    if (i == primaryIndex) continue;
                    float angle = batch.candidateAngles[i];
                    float delta = Mathf.Abs(Mathf.DeltaAngle(angle, primaryAngle));

                    if (delta > bestAngleDelta)
                    {
                        bestAngleDelta = delta;
                        bestIdx = i;
                    }
                }

                secondaryIndex = bestIdx;
            }

            primaryDir = batch.candidateDirections[primaryIndex];
            secondaryDir = batch.candidateDirections[secondaryIndex];
            return;
        }

        // Absolute fallback: keep the old dual-beam behaviour.
        Vector2 fallbackDir = FindBestDirection_RaycastSampling(card);
        primaryDir = fallbackDir;
        secondaryDir = fallbackDir;
    }

    private void GetBeamDimensions(out float beamWidth, out float beamLength)
    {
        Vector2 beamSizeLocal = Vector2.one;
        if (_collider2D is BoxCollider2D box)
        {
            beamSizeLocal = box.size;
        }
        else if (_collider2D is CapsuleCollider2D capsule)
        {
            beamSizeLocal = capsule.size;
        }
        else if (_collider2D is CircleCollider2D circle)
        {
            float diameter = circle.radius * 2f;
            beamSizeLocal = new Vector2(diameter, diameter);
        }

        Vector2 beamSizeWorld = new Vector2(
            beamSizeLocal.x * Mathf.Abs(transform.localScale.x),
            beamSizeLocal.y * Mathf.Abs(transform.localScale.y)
        );

        float colliderWidth = Mathf.Min(Mathf.Abs(beamSizeWorld.x), Mathf.Abs(beamSizeWorld.y));
        float colliderLength = Mathf.Max(Mathf.Abs(beamSizeWorld.x), Mathf.Abs(beamSizeWorld.y));

        beamWidth = smartTargetingBeamWidthOverride > 0f ? smartTargetingBeamWidthOverride : colliderWidth;
        beamLength = Mathf.Max(colliderLength, smartTargetingDetectionRange);
    }

    /// <summary>
    /// ALGORITHM 1: Raycast Sampling (Optimized)
    /// Test multiple directions with raycasts and count hits
    /// PERFORMANCE: Uses cached arrays, minimal allocations, no Debug.Log spam
    /// </summary>
    private Vector2 FindBestDirection_RaycastSampling(ProjectileCards card)
    {
        // Get angle range from card settings
        float minAngle = -90f;
        float maxAngle = 90f;

        if (card != null)
        {
            // VARIANT 2: Use Enhanced Variant 2 angle settings
            if (enhancedVariant == 2 && card.useCustomAnglesEnhancedVariant2)
            {
                minAngle = card.minAngleEnhancedVariant2;
                maxAngle = card.maxAngleEnhancedVariant2;
            }
            // VARIANT 1: Use Enhanced angle settings
            else if (enhancedVariant == 1 && card.useCustomAnglesEnhanced)
            {
                minAngle = card.minAngleEnhanced;
                maxAngle = card.maxAngleEnhanced;
            }
            // BASE: Use base angle settings
            else if (card.useCustomAngles)
            {
                minAngle = card.minAngle;
                maxAngle = card.maxAngle;
            }
        }

        // PERFORMANCE: Use OverlapCircleNonAlloc with cached array (no GC allocation!)
        int colliderCount = Physics2D.OverlapCircleNonAlloc(transform.position, smartTargetingDetectionRange, cachedColliders, enemyLayer);

        // PERFORMANCE: Clear and reuse cached collections (no GC!)
        cachedEnemySet.Clear();
        cachedEnemyList.Clear();

        Camera mainCam = Camera.main;

        if (mainCam != null)
        {
            // Calculate camera bounds with tolerance (precompute half-widths)
            float cameraHeight = mainCam.orthographicSize * 2f * onCameraTolerance;
            float cameraWidth = cameraHeight * mainCam.aspect;
            float halfWidth = cameraWidth / 2f;
            float halfHeight = cameraHeight / 2f;
            Vector3 camPos = mainCam.transform.position;

            // PERFORMANCE: Use for loop with cached array instead of foreach
            for (int i = 0; i < colliderCount; i++)
            {
                Collider2D collider = cachedColliders[i];
                if (collider == null) continue;

                GameObject enemyObj = collider.gameObject;

                // Skip if we've already added this enemy GameObject
                if (cachedEnemySet.Contains(enemyObj)) continue;

                Vector3 enemyPos = enemyObj.transform.position;

                float distX = Mathf.Abs(enemyPos.x - camPos.x);
                float distY = Mathf.Abs(enemyPos.y - camPos.y);

                // PERFORMANCE: Use precomputed half-widths
                bool isOnCamera = distX <= halfWidth && distY <= halfHeight;

                if (isOnCamera)
                {
                    cachedEnemySet.Add(enemyObj);
                    cachedEnemyList.Add(enemyObj);
                }
            }
        }

        // PERFORMANCE: Use List directly instead of converting to array
        int enemyCount = cachedEnemyList.Count;

        if (enemyCount == 0)
        {
            // No enemies found - fire in random direction within the current
            // [minAngle, maxAngle] range, which already reflects any
            // variant-specific/custom angle settings (including Variant 2 and
            // stacked 1+2).
            float randomAngle = Random.Range(minAngle, maxAngle);
            float randomAngleRad = randomAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(randomAngleRad), Mathf.Sin(randomAngleRad));
        }

        // Get beam collider dimensions in WORLD space (derived from current collider)
        float beamWidth;
        float beamLength;
        GetBeamDimensions(out beamWidth, out beamLength);
        float beamHalfWidth = beamWidth / 2f;

        int bestCount = 0;
        Vector2 bestDirection = Vector2.up;
        float bestAngleDeg = 90f;

        int samplesToUse;
        if (enhancedVariant == 2)
        {
            samplesToUse = variant2SmartTargetingSamples;
        }
        else if (enhancedVariant == 3)
        {
            samplesToUse = variant3SmartTargetingSamples;
        }
        else
        {
            samplesToUse = smartTargetingSamples;
        }

        // Test each angle and count how many enemies it hits
        for (int angleIndex = 0; angleIndex < samplesToUse; angleIndex++)
        {
            // Calculate this angle
            float angleDeg = Mathf.Lerp(minAngle, maxAngle, (float)angleIndex / (samplesToUse - 1));
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector2 testDirection = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

            // Count how many enemies THIS angle hits
            int hitsThisAngle = 0;

            // Test each enemy
            for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
            {
                GameObject enemy = cachedEnemyList[enemyIndex];
                if (enemy == null) continue;

                // Get collider for accurate center position
                Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
                Vector2 enemyPosition;
                float radius = 0.5f; // Default radius

                if (enemyCollider != null)
                {
                    Bounds bounds = enemyCollider.bounds;
                    enemyPosition = bounds.center;
                    radius = Mathf.Max(bounds.extents.x, bounds.extents.y);
                }
                else
                {
                    enemyPosition = enemy.transform.position;
                }

                Vector2 vectorToEnemy = enemyPosition - (Vector2)transform.position;
                float distAlongBeam = Vector2.Dot(vectorToEnemy, testDirection);

                // Skip if behind or too far
                if (distAlongBeam < 0 || distAlongBeam > beamLength)
                    continue;

                // Calculate perpendicular distance from beam center line
                Vector2 perpDir = new Vector2(-testDirection.y, testDirection.x);
                float perpDist = Mathf.Abs(Vector2.Dot(vectorToEnemy, perpDir));

                // Add tolerance for extreme angles
                float hitTolerance = 0.5f;

                // Check if within beam width (with tolerance)
                if (perpDist <= beamHalfWidth + radius + hitTolerance)
                {
                    hitsThisAngle++;
                }
            }

            // Update best if this angle hits more enemies
            if (hitsThisAngle > bestCount)
            {
                bestCount = hitsThisAngle;
                bestDirection = testDirection;
                bestAngleDeg = angleDeg;
            }
        }

        // If no angle hits any enemies, fire straight up
        if (bestCount == 0)
        {
            return Vector2.up;
        }

        return bestDirection;
    }

    /// <summary>
    /// Variant 3 helper: find two best directions using the same raycast-sampling
    /// logic, ensuring the second direction only matches the first when there is
    /// literally no other direction that hits any enemy.
    /// </summary>
    private void FindBestTwoDirections_RaycastSampling(ProjectileCards card, out Vector2 primaryDir, out Vector2 secondaryDir)
    {
        primaryDir = Vector2.up;
        secondaryDir = Vector2.up;

        // Get angle range from card settings (same rules as FindBestDirection_RaycastSampling)
        float minAngle = -90f;
        float maxAngle = 90f;

        if (card != null)
        {
            if (enhancedVariant == 2 && card.useCustomAnglesEnhancedVariant2)
            {
                minAngle = card.minAngleEnhancedVariant2;
                maxAngle = card.maxAngleEnhancedVariant2;
            }
            else if (enhancedVariant == 1 && card.useCustomAnglesEnhanced)
            {
                minAngle = card.minAngleEnhanced;
                maxAngle = card.maxAngleEnhanced;
            }
            else if (card.useCustomAngles)
            {
                minAngle = card.minAngle;
                maxAngle = card.maxAngle;
            }
        }

        // PERFORMANCE: Use OverlapCircleNonAlloc with cached array (no GC allocation!)
        int colliderCount = Physics2D.OverlapCircleNonAlloc(transform.position, smartTargetingDetectionRange, cachedColliders, enemyLayer);

        // PERFORMANCE: Clear and reuse cached collections (no GC!)
        cachedEnemySet.Clear();
        cachedEnemyList.Clear();

        Camera mainCam = Camera.main;

        if (mainCam != null)
        {
            float cameraHeight = mainCam.orthographicSize * 2f * onCameraTolerance;
            float cameraWidth = cameraHeight * mainCam.aspect;
            float halfWidth = cameraWidth / 2f;
            float halfHeight = cameraHeight / 2f;
            Vector3 camPos = mainCam.transform.position;

            for (int i = 0; i < colliderCount; i++)
            {
                Collider2D collider = cachedColliders[i];
                if (collider == null) continue;

                GameObject enemyObj = collider.gameObject;
                if (cachedEnemySet.Contains(enemyObj)) continue;

                Vector3 enemyPos = enemyObj.transform.position;

                float distX = Mathf.Abs(enemyPos.x - camPos.x);
                float distY = Mathf.Abs(enemyPos.y - camPos.y);

                bool isOnCamera = distX <= halfWidth && distY <= halfHeight;

                if (isOnCamera)
                {
                    cachedEnemySet.Add(enemyObj);
                    cachedEnemyList.Add(enemyObj);
                }
            }
        }

        int enemyCount = cachedEnemyList.Count;

        // No enemies: fall back to two deterministic directions in the allowed range
        if (enemyCount == 0)
        {
            // Use the current [minAngle, maxAngle] window, which already
            // incorporates any variant-specific/custom settings (e.g. Variant 2
            // enhanced angles or stacked 1+2 behaviour).
            float centerAngle1 = (minAngle + maxAngle) * 0.5f;

            float halfSpan = (maxAngle - minAngle) * 0.25f;
            float angle1 = Mathf.Clamp(centerAngle1 - halfSpan, minAngle, maxAngle);
            float angle2 = Mathf.Clamp(centerAngle1 + halfSpan, minAngle, maxAngle);

            float rad1 = angle1 * Mathf.Deg2Rad;
            float rad2 = angle2 * Mathf.Deg2Rad;
            primaryDir = new Vector2(Mathf.Cos(rad1), Mathf.Sin(rad1));
            secondaryDir = new Vector2(Mathf.Cos(rad2), Mathf.Sin(rad2));
            return;
        }

        // Get beam collider dimensions in WORLD space (derived from current collider)
        float beamWidth;
        float beamLength;
        GetBeamDimensions(out beamWidth, out beamLength);
        float beamHalfWidth = beamWidth / 2f;

        int samplesToUse;
        if (enhancedVariant == 2)
        {
            samplesToUse = variant2SmartTargetingSamples;
        }
        else if (enhancedVariant == 3)
        {
            samplesToUse = variant3SmartTargetingSamples;
        }
        else
        {
            samplesToUse = smartTargetingSamples;
        }

        if (samplesToUse < 2)
        {
            samplesToUse = 2; // ensure we have at least two distinct samples
        }

        List<float> candidateAngles = new List<float>(samplesToUse);
        List<Vector2> candidateDirections = new List<Vector2>(samplesToUse);
        List<int> candidateHits = new List<int>(samplesToUse);

        for (int angleIndex = 0; angleIndex < samplesToUse; angleIndex++)
        {
            float angleDeg = Mathf.Lerp(minAngle, maxAngle, (float)angleIndex / (samplesToUse - 1));
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector2 testDirection = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

            int hitsThisAngle = 0;

            for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
            {
                GameObject enemy = cachedEnemyList[enemyIndex];
                if (enemy == null) continue;

                Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
                Vector2 enemyPosition;
                float radius = 0.5f;

                if (enemyCollider != null)
                {
                    Bounds bounds = enemyCollider.bounds;
                    enemyPosition = bounds.center;
                    radius = Mathf.Max(bounds.extents.x, bounds.extents.y);
                }
                else
                {
                    enemyPosition = enemy.transform.position;
                }

                Vector2 vectorToEnemy = enemyPosition - (Vector2)transform.position;
                float distAlongBeam = Vector2.Dot(vectorToEnemy, testDirection);

                if (distAlongBeam < 0 || distAlongBeam > beamLength)
                    continue;

                Vector2 perpDir = new Vector2(-testDirection.y, testDirection.x);
                float perpDist = Mathf.Abs(Vector2.Dot(vectorToEnemy, perpDir));

                float hitTolerance = 0.5f;

                if (perpDist <= beamHalfWidth + radius + hitTolerance)
                {
                    hitsThisAngle++;
                }
            }

            candidateAngles.Add(angleDeg);
            candidateDirections.Add(testDirection);
            candidateHits.Add(hitsThisAngle);
        }

        // PRIMARY: use the same global smart-targeting core as base / Variant 2
        // (FindBestTargetingDirection), then snap to the closest sampled angle so
        // both beams share the same notion of "best" direction.
        Vector2 preferredPrimaryDir = FindBestTargetingDirection(card);
        float preferredPrimaryAngle = Mathf.Atan2(preferredPrimaryDir.y, preferredPrimaryDir.x) * Mathf.Rad2Deg;

        int primaryIndex = 0;
        float bestPrimaryDelta = float.PositiveInfinity;
        for (int i = 0; i < candidateAngles.Count; i++)
        {
            float delta = Mathf.Abs(Mathf.DeltaAngle(candidateAngles[i], preferredPrimaryAngle));
            if (delta < bestPrimaryDelta)
            {
                bestPrimaryDelta = delta;
                primaryIndex = i;
            }
        }

        primaryDir = candidateDirections[primaryIndex];

        // SECONDARY: choose the direction that (a) hits as many enemies as
        // possible and (b) is as far in angle from the primary as possible.
        int secondaryMaxHits = -1;
        int secondaryIndex = -1;
        float bestSeparation = -1f;
        float primaryAngle = candidateAngles[primaryIndex];

        for (int i = 0; i < candidateHits.Count; i++)
        {
            if (i == primaryIndex) continue;

            int hits = candidateHits[i];
            if (hits <= 0) continue; // ignore directions that hit nothing

            float separation = Mathf.Abs(Mathf.DeltaAngle(candidateAngles[i], primaryAngle));

            if (hits > secondaryMaxHits || (hits == secondaryMaxHits && separation > bestSeparation))
            {
                secondaryMaxHits = hits;
                bestSeparation = separation;
                secondaryIndex = i;
            }
        }

        if (secondaryIndex < 0)
        {
            // No other sampled direction hits anything; fall back to primary
            secondaryDir = primaryDir;
        }
        else
        {
            secondaryDir = candidateDirections[secondaryIndex];
        }
    }

    private void SpawnVariant3SecondaryBeam(ProjectileCards card, Collider2D colliderToIgnore, PlayerMana playerMana, Vector3 playerPosition, Vector2 secondaryDir)
    {
        if (card == null || card.projectilePrefab == null)
        {
            return;
        }

        Vector3 spawnPos = playerPosition != default(Vector3) ? playerPosition : transform.position;

        GameObject beamObj = Instantiate(card.projectilePrefab, spawnPos, Quaternion.identity);

        if (ProjectileCardModifiers.Instance != null)
        {
            ProjectileCardModifiers.Instance.TagProjectileWithCard(beamObj, card);
        }

        ElementalBeam secondaryBeam = beamObj.GetComponent<ElementalBeam>();
        if (secondaryBeam != null)
        {
            secondaryBeam.MarkAsVariant3SecondaryBeam();
            bool skipCheck = true;
            secondaryBeam.Launch(secondaryDir, colliderToIgnore, playerMana, skipCheck, spawnPos);
        }
    }

    private void SpawnVariant23SecondaryBeam(ProjectileCards card, Collider2D colliderToIgnore, PlayerMana playerMana, Vector3 playerPosition, Vector2 secondaryDir)
    {
        if (card == null || card.projectilePrefab == null)
        {
            return;
        }

        Vector3 spawnPos = playerPosition != default(Vector3) ? playerPosition : transform.position;

        GameObject beamObj = Instantiate(card.projectilePrefab, spawnPos, Quaternion.identity);

        if (ProjectileCardModifiers.Instance != null)
        {
            ProjectileCardModifiers.Instance.TagProjectileWithCard(beamObj, card);
        }

        ElementalBeam secondaryBeam = beamObj.GetComponent<ElementalBeam>();
        if (secondaryBeam != null)
        {
            secondaryBeam.MarkAsVariant23SecondaryBeam();
            bool skipCheck = true;
            secondaryBeam.Launch(secondaryDir, colliderToIgnore, playerMana, skipCheck, spawnPos);
        }
    }

    private void SpawnVariant13SecondaryBeam(ProjectileCards card, Collider2D colliderToIgnore, PlayerMana playerMana, Vector3 playerPosition, Vector2 secondaryDir)
    {
        if (card == null || card.projectilePrefab == null)
        {
            return;
        }

        Vector3 spawnPos = playerPosition != default(Vector3) ? playerPosition : transform.position;

        GameObject beamObj = Instantiate(card.projectilePrefab, spawnPos, Quaternion.identity);

        if (ProjectileCardModifiers.Instance != null)
        {
            ProjectileCardModifiers.Instance.TagProjectileWithCard(beamObj, card);
        }

        ElementalBeam secondaryBeam = beamObj.GetComponent<ElementalBeam>();
        if (secondaryBeam != null)
        {
            secondaryBeam.MarkAsVariant13SecondaryBeam();
            bool skipCheck = true;
            secondaryBeam.Launch(secondaryDir, colliderToIgnore, playerMana, skipCheck, spawnPos);
        }
    }

    private void OnDrawGizmos()
    {
        if (_collider2D == null)
        {
            _collider2D = GetComponent<Collider2D>();
        }

        if (_collider2D != null)
        {
            Gizmos.color = projectileType == ProjectileType.Fire ? new Color(1f, 0.5f, 0f, 0.5f) : new Color(0f, 0.5f, 1f, 0.5f);

            if (_collider2D is BoxCollider2D boxCollider1)
            {
                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(boxCollider1.offset, boxCollider1.size);
                Gizmos.matrix = oldMatrix;
            }
            else if (_collider2D is CircleCollider2D circleCollider)
            {
                Gizmos.DrawWireSphere(transform.position + (Vector3)circleCollider.offset, circleCollider.radius);
            }
        }
        else
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }

        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider == null) return;
        
        Vector3 beamStart = transform.position;
        Vector3 beamEnd = transform.position + transform.right * boxCollider.size.x;
        
        Color beamColor = Color.yellow;
        switch (projectileType)
        {
            case ProjectileType.Fire:
                beamColor = new Color(1f, 0.5f, 0f, 0.8f);
                break;
            case ProjectileType.Ice:
                beamColor = new Color(0f, 0.8f, 1f, 0.8f);
                break;
            case ProjectileType.Thunder:
                beamColor = new Color(1f, 1f, 0f, 0.8f);
                break;
        }
        
        Gizmos.color = beamColor;
        Gizmos.DrawLine(beamStart, beamEnd);
        
        Vector3 perpendicular = transform.up * boxCollider.size.y * 0.5f;
        Gizmos.DrawLine(beamStart + perpendicular, beamEnd + perpendicular);
        Gizmos.DrawLine(beamStart - perpendicular, beamEnd - perpendicular);
        
        Gizmos.DrawLine(beamStart + perpendicular, beamStart - perpendicular);
        Gizmos.DrawLine(beamEnd + perpendicular, beamEnd - perpendicular);
    }
    
    /// <summary>
    /// ALGORITHM 2: Cluster Analysis
    /// Find the densest cluster of enemies and aim at its center
    /// </summary>
    private Vector2 FindBestDirection_ClusterAnalysis(ProjectileCards card)
    {
        
        // Get all enemies on screen
        Collider2D[] allEnemiesRaw = Physics2D.OverlapCircleAll(transform.position, smartTargetingDetectionRange, enemyLayer);
        List<Vector2> enemyPositions = new List<Vector2>();
        
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            float cameraHeight = mainCam.orthographicSize * 2f * onCameraTolerance;
            float cameraWidth = cameraHeight * mainCam.aspect;
            Vector3 camPos = mainCam.transform.position;
            
            foreach (Collider2D col in allEnemiesRaw)
            {
                if (col == null) continue;
                Vector3 enemyPos = col.transform.position;
                float distX = Mathf.Abs(enemyPos.x - camPos.x);
                float distY = Mathf.Abs(enemyPos.y - camPos.y);
                
                if (distX <= cameraWidth / 2f && distY <= cameraHeight / 2f)
                {
                    enemyPositions.Add(enemyPos);
                }
            }
        }
        
        if (enemyPositions.Count == 0)
        {
            float randomAngle = Random.Range(-90f, 90f);
            return new Vector2(Mathf.Cos(randomAngle * Mathf.Deg2Rad), Mathf.Sin(randomAngle * Mathf.Deg2Rad));
        }
        
        // Find densest cluster using a simple grid approach
        float clusterRadius = 5f; // Radius to consider enemies as part of a cluster
        Vector2 bestClusterCenter = Vector2.zero;
        int maxClusterSize = 0;
        
        foreach (Vector2 pos in enemyPositions)
        {
            int clusterSize = 0;
            Vector2 clusterSum = Vector2.zero;
            
            foreach (Vector2 otherPos in enemyPositions)
            {
                if (Vector2.Distance(pos, otherPos) <= clusterRadius)
                {
                    clusterSize++;
                    clusterSum += otherPos;
                }
            }
            
            if (clusterSize > maxClusterSize)
            {
                maxClusterSize = clusterSize;
                bestClusterCenter = clusterSum / clusterSize;
            }
        }
        
        Vector2 direction = (bestClusterCenter - (Vector2)transform.position).normalized;
        return direction;
    }
    
    /// <summary>
    /// ALGORITHM 3: Weighted Distance
    /// Weight enemies by both distance and angle coverage
    /// Closer enemies and better angle coverage get higher priority
    /// </summary>
    private Vector2 FindBestDirection_WeightedDistance(ProjectileCards card)
    {
        
        // Get angle range
        float minAngle = -90f;
        float maxAngle = 90f;
        if (card != null && card.useCustomAngles)
        {
            minAngle = card.minAngle;
            maxAngle = card.maxAngle;
        }
        
        // Get all enemies on screen
        Collider2D[] allEnemiesRaw = Physics2D.OverlapCircleAll(transform.position, smartTargetingDetectionRange, enemyLayer);
        List<GameObject> enemies = new List<GameObject>();
        
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            float cameraHeight = mainCam.orthographicSize * 2f * onCameraTolerance;
            float cameraWidth = cameraHeight * mainCam.aspect;
            Vector3 camPos = mainCam.transform.position;
            
            foreach (Collider2D col in allEnemiesRaw)
            {
                if (col == null) continue;
                Vector3 enemyPos = col.transform.position;
                float distX = Mathf.Abs(enemyPos.x - camPos.x);
                float distY = Mathf.Abs(enemyPos.y - camPos.y);
                
                if (distX <= cameraWidth / 2f && distY <= cameraHeight / 2f)
                {
                    enemies.Add(col.gameObject);
                }
            }
        }
        
        if (enemies.Count == 0)
        {
            float randomAngle = Random.Range(minAngle, maxAngle);
            return new Vector2(Mathf.Cos(randomAngle * Mathf.Deg2Rad), Mathf.Sin(randomAngle * Mathf.Deg2Rad));
        }
        
        // Test angles and calculate weighted score
        float bestScore = 0f;
        Vector2 bestDirection = Vector2.up;
        
        for (int i = 0; i < smartTargetingSamples; i++)
        {
            float angleDeg = Mathf.Lerp(minAngle, maxAngle, (float)i / (smartTargetingSamples - 1));
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector2 testDirection = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
            
            float score = 0f;
            
            foreach (GameObject enemy in enemies)
            {
                if (enemy == null) continue;
                
                Vector2 toEnemy = (Vector2)enemy.transform.position - (Vector2)transform.position;
                float distance = toEnemy.magnitude;
                float dot = Vector2.Dot(toEnemy.normalized, testDirection);
                
                // Weight: closer enemies = higher score, better alignment = higher score
                if (dot > 0.7f) // Only consider enemies roughly in front
                {
                    float distanceWeight = 1f / (1f + distance * 0.1f); // Closer = higher
                    float angleWeight = dot; // Better alignment = higher
                    score += distanceWeight * angleWeight * 10f;
                }
            }
            
            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = testDirection;
            }
        }
        
        return bestDirection;
    }
    
    /// <summary>
    /// ALGORITHM 4: Grid Sweep
    /// Divide the screen into a grid and find the best sweep direction
    /// </summary>
    private Vector2 FindBestDirection_GridSweep(ProjectileCards card)
    {
        
        // Get angle range
        float minAngle = -90f;
        float maxAngle = 90f;
        if (card != null && card.useCustomAngles)
        {
            minAngle = card.minAngle;
            maxAngle = card.maxAngle;
        }
        
        // Get beam dimensions based on current collider size so targeting always
        // matches the actual beam width, even when scaled.
        float beamWidth;
        float beamLength;
        GetBeamDimensions(out beamWidth, out beamLength);
        
        // Get all enemies on screen
        Collider2D[] allEnemiesRaw = Physics2D.OverlapCircleAll(transform.position, smartTargetingDetectionRange, enemyLayer);
        List<Vector2> enemyPositions = new List<Vector2>();
        
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            float cameraHeight = mainCam.orthographicSize * 2f * onCameraTolerance;
            float cameraWidth = cameraHeight * mainCam.aspect;
            Vector3 camPos = mainCam.transform.position;
            
            foreach (Collider2D col in allEnemiesRaw)
            {
                if (col == null) continue;
                Vector3 enemyPos = col.transform.position;
                float distX = Mathf.Abs(enemyPos.x - camPos.x);
                float distY = Mathf.Abs(enemyPos.y - camPos.y);
                
                if (distX <= cameraWidth / 2f && distY <= cameraHeight / 2f)
                {
                    enemyPositions.Add(enemyPos);
                }
            }
        }
        
        if (enemyPositions.Count == 0)
        {
            float randomAngle = Random.Range(minAngle, maxAngle);
            return new Vector2(Mathf.Cos(randomAngle * Mathf.Deg2Rad), Mathf.Sin(randomAngle * Mathf.Deg2Rad));
        }
        
        // Test each angle and count hits using perpendicular distance
        int bestCount = 0;
        Vector2 bestDirection = Vector2.up;
        
        for (int i = 0; i < smartTargetingSamples; i++)
        {
            float angleDeg = Mathf.Lerp(minAngle, maxAngle, (float)i / (smartTargetingSamples - 1));
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector2 testDirection = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
            
            int hitCount = 0;
            
            foreach (Vector2 enemyPos in enemyPositions)
            {
                Vector2 toEnemy = enemyPos - (Vector2)transform.position;
                float distAlongBeam = Vector2.Dot(toEnemy, testDirection);
                
                if (distAlongBeam > 0 && distAlongBeam <= beamLength)
                {
                    Vector2 perpDir = new Vector2(-testDirection.y, testDirection.x);
                    float perpDist = Mathf.Abs(Vector2.Dot(toEnemy, perpDir));
                    
                    if (perpDist <= beamWidth / 2f)
                    {
                        hitCount++;
                    }
                }
            }
            
            if (hitCount > bestCount)
            {
                bestCount = hitCount;
                bestDirection = testDirection;
            }
        }
        
        return bestDirection;
    }
    
    /// <summary>
    /// Apply modifiers instantly (IInstantModifiable interface)
    /// </summary>
    public void ApplyInstantModifiers(CardModifierStats modifiers)
    {
        Debug.Log($"<color=lime>╔═══ ELEMENTALBEAM INSTANT MODIFIERS ═══╗</color>");
        
        // Recalculate lifetime
        float newLifetime = baseLifetimeSeconds + modifiers.lifetimeIncrease;
        if (newLifetime != lifetimeSeconds)
        {
            lifetimeSeconds = newLifetime;
            Debug.Log($"<color=lime>  Lifetime: {baseLifetimeSeconds:F2} + {modifiers.lifetimeIncrease:F2} = {lifetimeSeconds:F2}</color>");
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
