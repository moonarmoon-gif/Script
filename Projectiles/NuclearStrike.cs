using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class NuclearStrike : MonoBehaviour, IInstantModifiable
{
    [Header("Drop Settings")]
    [SerializeField] private float dropSpeed = 5f;

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

    [Header("Damage Timing")]
    public float damageDelay = 0.2f;

    [Header("Spawn Area")]
    [Tooltip("Tag name for minimum spawn position GameObject (left side)")]
    [SerializeField] private string minPosTag = "NuclearStrike_MinPos";
    [Tooltip("Tag name for maximum spawn position GameObject (right side)")]
    [SerializeField] private string maxPosTag = "NuclearStrike_MaxPos";

    [Header("Detonation Area - 6 Point System")]
    [SerializeField] private string pointATag = "HellBeam_PointA";
    [SerializeField] private string pointBTag = "HellBeam_PointB";
    [SerializeField] private string pointCTag = "HellBeam_PointC";
    [SerializeField] private string pointDTag = "HellBeam_PointD";
    [SerializeField] private string pointETag = "HellBeam_PointE";
    [SerializeField] private string pointFTag = "HellBeam_PointF";

    public float minStrikeDistance = 2f;

    private Transform minPos;
    private Transform maxPos;

    private Transform pointA;
    private Transform pointB;
    private Transform pointC;
    private Transform pointD;
    private Transform pointE;
    private Transform pointF;

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

    [Header("Radius-to-Effect Size Scaling")]
    [Tooltip("Explosion effect size scales proportionally with the final explosionRadius.")]
    [Range(0.01f, 10f)]
    public float radiusToEffectBaseRadius = 5f;

    private float finalExplosionAnimationSpeed = 1f;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Shadow System")]
    [Tooltip("Shadow prefab that appears at landing location")]
    [SerializeField] private GameObject shadowPrefab;
    [Tooltip("Offset for shadow position (X, Y)")]
    [SerializeField] private Vector2 shadowOffset = Vector2.zero;
    public float ShadowFadeAwayDuration = 0.5f;

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

    private bool TryPickLandingPositionVariant3(Vector3 startPosition, float spawnMinX, float spawnMaxX, bool isPrimarySpawn, out Vector3 landing)
    {
        landing = startPosition;

        if (Time.frameCount != lastLandingFrame)
        {
            currentFrameLandingPositions.Clear();
            currentFrameTargetedEnemyIds.Clear();
            usedSingleEnemyTargetThisFrame = false;
            lastLandingFrame = Time.frameCount;
        }

        EnemyHealth[] enemies = Object.FindObjectsOfType<EnemyHealth>();
        List<EnemyHealth> alive = new List<EnemyHealth>();
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyHealth e = enemies[i];
            if (e == null || !e.IsAlive)
            {
                continue;
            }

            if (!OffscreenDamageChecker.CanTakeDamage(e.transform.position))
            {
                continue;
            }

            alive.Add(e);
        }

        if (alive.Count == 0)
        {
            return TryPickLandingPosition(startPosition, spawnMinX, spawnMaxX, out landing);
        }

        if (alive.Count == 1)
        {
            if (isPrimarySpawn && !usedSingleEnemyTargetThisFrame)
            {
                usedSingleEnemyTargetThisFrame = true;

                Vector3 pos = alive[0].transform.position;
                pos.z = startPosition.z;

                currentFrameTargetedEnemyIds.Add(alive[0].gameObject.GetInstanceID());
                currentFrameLandingPositions.Add(pos);
                landing = pos;
                return true;
            }

            return TryPickLandingPosition(startPosition, spawnMinX, spawnMaxX, out landing);
        }

        float minDist = Mathf.Max(0f, minStrikeDistance);
        float minDistSqr = minDist * minDist;

        List<EnemyHealth> candidates = new List<EnemyHealth>(alive.Count);
        for (int i = 0; i < alive.Count; i++)
        {
            EnemyHealth e = alive[i];
            if (e == null)
            {
                continue;
            }

            int id = e.gameObject.GetInstanceID();
            if (currentFrameTargetedEnemyIds.Contains(id))
            {
                continue;
            }

            candidates.Add(e);
        }

        if (candidates.Count == 0)
        {
            return TryPickLandingPosition(startPosition, spawnMinX, spawnMaxX, out landing);
        }

        int attempts = Mathf.Min(12, candidates.Count);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            int pickIndex = Random.Range(0, candidates.Count);
            EnemyHealth chosen = candidates[pickIndex];
            candidates.RemoveAt(pickIndex);

            if (chosen == null)
            {
                continue;
            }

            Vector3 candidate = chosen.transform.position;
            candidate.z = startPosition.z;

            if (minDistSqr > 0f)
            {
                bool tooClose = false;
                for (int i = 0; i < currentFrameLandingPositions.Count; i++)
                {
                    if ((candidate - currentFrameLandingPositions[i]).sqrMagnitude < minDistSqr)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                {
                    continue;
                }
            }

            int id = chosen.gameObject.GetInstanceID();
            currentFrameTargetedEnemyIds.Add(id);
            currentFrameLandingPositions.Add(candidate);
            landing = candidate;
            return true;
        }

        return TryPickLandingPosition(startPosition, spawnMinX, spawnMaxX, out landing);
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

    [Header("Enhanced Variant 2 - Hell Beam")]
    public GameObject HellBeamPrefab;
    public float HellBeamBaseDamage = 100f;
    public float HellBeamBaseLifetime = 1f;
    public float HellBeamDamageTickInterval = 0.25f;

    [Header("Enhanced Variant 3 - Targeted Strike")]
    public float SpeedBonus = 25f;
    [Range(0f, 100f)]
    public float IncreasedBurnChance = 25f;

    private Rigidbody2D _rigidbody2D;
    private AudioSource _audioSource;
    private Collider2D _collider2D;
    private Camera mainCamera;
    private bool hasExploded = false;
    private GameObject shadowInstance;
    private Vector3 landingPosition;

    private Vector2 dropDirection = Vector2.down;

    private static List<Vector3> currentFrameLandingPositions = new List<Vector3>();
    private static int lastLandingFrame = -1;

    private static HashSet<int> currentFrameTargetedEnemyIds = new HashSet<int>();
    private static bool usedSingleEnemyTargetThisFrame = false;

    // Enhanced system
    private int enhancedVariant = 0; // 0 = basic, 1 = rapid strike, 2-3 = future variants
    private bool isVariant3Active;

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

        FindDetonationAreaPoints();

        // Get card-specific modifiers first
        ProjectileCards card = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);

        bool hasVariant2History = false;
        bool hasVariant3History = false;

        // Check for enhanced variant using CARD-based system
        if (ProjectileCardLevelSystem.Instance != null && card != null)
        {
            enhancedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card);
            hasVariant2History = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 2);
            hasVariant3History = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 3);
            Debug.Log($"<color=gold>NuclearStrike ({card.cardName}) Enhanced Variant: {enhancedVariant}</color>");
        }

        bool hasVariant2Context = enhancedVariant == 2 || hasVariant2History;
        bool hasVariant3Context = enhancedVariant == 3 || hasVariant3History;

        isVariant3Active = hasVariant3Context;

        modifiers = new CardModifierStats(); // Default values

        if (card != null)
        {
            modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
            Debug.Log($"<color=red>NuclearStrike using modifiers from {card.cardName}</color>");
        }

        // Apply enhanced variant modifiers BEFORE card modifiers
        enhancedSpeedAdd = 0f;
        float enhancedVariant3SpeedAdd = 0f;
        int enhancedProjectileBonus = 0;

        if (enhancedVariant == 1)
        {
            enhancedSpeedAdd = enhancedSpeedBonus;

            // Store enhanced projectile count bonus for logging; the actual
            // projectile-count increase is applied per-spawn in ProjectileSpawner.
            enhancedProjectileBonus = enhancedProjectileCountBonus;

            Debug.Log($"<color=gold>Enhanced Rapid Strike: Speed +{enhancedSpeedAdd}, Additional Projectiles +{enhancedProjectileBonus}</color>");
        }
        else if (isVariant3Active)
        {
            enhancedVariant3SpeedAdd = SpeedBonus;

            ProjectileStatusChanceAdditiveBonus additive = GetComponent<ProjectileStatusChanceAdditiveBonus>();
            if (additive == null)
            {
                additive = gameObject.AddComponent<ProjectileStatusChanceAdditiveBonus>();
            }
            additive.burnBonusPercent = Mathf.Max(0f, IncreasedBurnChance);
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
        float finalCooldown = baseCooldown * (1f - modifiers.cooldownReductionPercent / 100f);
        if (MinCooldownManager.Instance != null)
        {
            finalCooldown = MinCooldownManager.Instance.ClampCooldown(card, finalCooldown);
        }
        else
        {
            finalCooldown = Mathf.Max(0.1f, finalCooldown);
        }
        damage = (baseDamage + modifiers.damageFlat) * modifiers.damageMultiplier;

        // Calculate speed increase and animation speed
        float originalDropSpeed = dropSpeed; // Store original BASE speed before ANY modifications
        baseDropSpeedStored = originalDropSpeed; // Store for explosion effect

        // Add card modifier speed increase (RAW value)
        dropSpeed += modifiers.speedIncrease;

        // Then add enhanced speed bonus (RAW value)
        dropSpeed += enhancedSpeedAdd;

        // Variant 3 speed bonus (RAW value)
        dropSpeed += enhancedVariant3SpeedAdd;

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

        float effectiveCooldown = finalCooldown;
        if (cachedPlayerStats != null && cachedPlayerStats.projectileCooldownReduction > 0f)
        {
            float totalCdr = Mathf.Max(0f, cachedPlayerStats.projectileCooldownReduction);
            effectiveCooldown = finalCooldown / (1f + totalCdr);
            if (MinCooldownManager.Instance != null && card != null)
            {
                effectiveCooldown = MinCooldownManager.Instance.ClampCooldown(card, effectiveCooldown);
            }
            else
            {
                effectiveCooldown = Mathf.Max(0.1f, effectiveCooldown);
            }
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
                if (GameStateManager.PauseSafeTime - lastFireTimes[prefabKey] < effectiveCooldown)
                {
                    Debug.Log($"<color=yellow>NuclearStrike on cooldown - {GameStateManager.PauseSafeTime - lastFireTimes[prefabKey]:F2}s / {effectiveCooldown}s</color>");
                    Destroy(gameObject);
                    return;
                }
            }

            // Record fire time
            lastFireTimes[prefabKey] = GameStateManager.PauseSafeTime;
        }
        else
        {
            Debug.Log($"<color=gold>NuclearStrike: Skipping cooldown/mana check (multi-projectile spawn)</color>");
        }

        float spawnY = spawnPosition.y;
        if (minPos != null && maxPos != null)
        {
            spawnY = Mathf.Max(minPos.position.y, maxPos.position.y);
        }

        Vector3 landingSeed = new Vector3(spawnPosition.x, spawnPosition.y, spawnPosition.z);
        Vector3 chosenLanding;
        bool picked = isVariant3Active
            ? TryPickLandingPositionVariant3(landingSeed, float.NegativeInfinity, float.PositiveInfinity, !skipCooldownCheck, out chosenLanding)
            : TryPickLandingPosition(landingSeed, float.NegativeInfinity, float.PositiveInfinity, out chosenLanding);
        if (!picked)
        {
            Transform fallbackPoint = pointA != null ? pointA
                : (pointB != null ? pointB
                : (pointC != null ? pointC
                : (pointD != null ? pointD
                : (pointE != null ? pointE
                : pointF))));

            if (fallbackPoint != null)
            {
                chosenLanding = new Vector3(fallbackPoint.position.x, fallbackPoint.position.y, spawnPosition.z);
            }
            else
            {
                chosenLanding = new Vector3(spawnPosition.x, spawnPosition.y, spawnPosition.z);
            }
        }

        if (hasVariant3Context)
        {
            chosenLanding.x += hasVariant2Context ? 2f : -2f;
        }

        Vector3 startPosition = new Vector3(chosenLanding.x, spawnY, spawnPosition.z);
        transform.position = startPosition;

        landingPosition = new Vector3(chosenLanding.x, chosenLanding.y, spawnPosition.z);
        float travelDistance = Vector2.Distance(startPosition, landingPosition);
        float safeDropSpeed = Mathf.Max(0.01f, dropSpeed);
        float estimatedTravelSeconds = travelDistance / safeDropSpeed;

        Vector2 dir = (Vector2)(landingPosition - startPosition);
        dropDirection = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.down;

        Debug.Log($"<color=red>NuclearStrike spawned! Speed={dropSpeed:F1} (base={baseDropSpeed:F1}, multiplier={speedMultiplier:F2}x), TravelTime={estimatedTravelSeconds:F2}s</color>");

        // Apply shadow offset
        Vector3 shadowPosition = landingPosition + (Vector3)shadowOffset;

        // Spawn shadow at landing position
        if (shadowPrefab != null)
        {
            shadowInstance = Instantiate(shadowPrefab, shadowPosition, Quaternion.identity);

            Vector3 shadowScale = shadowInstance.transform.localScale;
            shadowScale.x = explosionRadius;
            shadowScale.y = explosionRadius;
            shadowInstance.transform.localScale = shadowScale;

            // Get shadow animator and adjust animation speed
            Animator shadowAnimator = shadowInstance.GetComponent<Animator>();
            if (shadowAnimator != null)
            {
                // Shadow animation uses its configured playback speed.
                shadowAnimator.speed = 1f;

                Debug.Log($"<color=yellow>Shadow spawned at {shadowPosition} with animation speed {shadowAnimator.speed:F2}x (travel time: {estimatedTravelSeconds:F2}s)</color>");
            }
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
        float step = maxRotationDegreesPerSecond * GameStateManager.GetPauseSafeDeltaTime();
        float newAngle = Mathf.MoveTowardsAngle(current, desired, step);
        transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
    }

    private IEnumerator DropRoutine()
    {
        while (!hasExploded)
        {
            float dt = GameStateManager.GetPauseSafeDeltaTime();
            if (dt > 0f)
            {
                transform.position += (Vector3)(dropDirection * dropSpeed * dt);
            }

            if (_rigidbody2D != null)
            {
                _rigidbody2D.velocity = Vector2.zero;
            }

            // Set initial rotation if not keeping it
            if (!keepInitialRotation && rotateToVelocity)
            {
                float baseAngle = Mathf.Atan2(dropDirection.y, dropDirection.x) * Mathf.Rad2Deg;
                float facingCorrection = (int)spriteFacing;
                float finalAngle = baseAngle + facingCorrection + additionalRotationOffsetDeg;
                transform.rotation = Quaternion.Euler(0f, 0f, finalAngle);
            }

            Vector2 toTarget = (Vector2)(landingPosition - transform.position);
            if (toTarget.sqrMagnitude <= 0.0025f || Vector2.Dot(toTarget, dropDirection) <= 0f)
            {
                transform.position = landingPosition;
                if (_rigidbody2D != null)
                {
                    _rigidbody2D.velocity = Vector2.zero;
                }
                Explode();
                yield break;
            }

            yield return null;
        }
    }

    private void FindDetonationAreaPoints()
    {
        if (!string.IsNullOrEmpty(pointATag))
        {
            GameObject o = GameObject.FindGameObjectWithTag(pointATag);
            if (o != null) pointA = o.transform;
        }
        if (!string.IsNullOrEmpty(pointBTag))
        {
            GameObject o = GameObject.FindGameObjectWithTag(pointBTag);
            if (o != null) pointB = o.transform;
        }
        if (!string.IsNullOrEmpty(pointCTag))
        {
            GameObject o = GameObject.FindGameObjectWithTag(pointCTag);
            if (o != null) pointC = o.transform;
        }
        if (!string.IsNullOrEmpty(pointDTag))
        {
            GameObject o = GameObject.FindGameObjectWithTag(pointDTag);
            if (o != null) pointD = o.transform;
        }
        if (!string.IsNullOrEmpty(pointETag))
        {
            GameObject o = GameObject.FindGameObjectWithTag(pointETag);
            if (o != null) pointE = o.transform;
        }
        if (!string.IsNullOrEmpty(pointFTag))
        {
            GameObject o = GameObject.FindGameObjectWithTag(pointFTag);
            if (o != null) pointF = o.transform;
        }
    }

    private bool TryPickLandingPosition(Vector3 startPosition, float spawnMinX, float spawnMaxX, out Vector3 landing)
    {
        landing = startPosition;

        List<Vector2> poly = new List<Vector2>(6);
        if (pointA != null) poly.Add(pointA.position);
        if (pointB != null) poly.Add(pointB.position);
        if (pointC != null) poly.Add(pointC.position);
        if (pointD != null) poly.Add(pointD.position);
        if (pointE != null) poly.Add(pointE.position);
        if (pointF != null) poly.Add(pointF.position);

        if (poly.Count < 3)
        {
            return false;
        }

        float minX = poly[0].x;
        float maxX = poly[0].x;
        float minY = poly[0].y;
        float maxY = poly[0].y;

        for (int i = 1; i < poly.Count; i++)
        {
            Vector2 v = poly[i];
            if (v.x < minX) minX = v.x;
            if (v.x > maxX) maxX = v.x;
            if (v.y < minY) minY = v.y;
            if (v.y > maxY) maxY = v.y;
        }

        float sampleMinX = minX;
        float sampleMaxX = maxX;
        if (!float.IsNegativeInfinity(spawnMinX)) sampleMinX = Mathf.Max(sampleMinX, spawnMinX);
        if (!float.IsPositiveInfinity(spawnMaxX)) sampleMaxX = Mathf.Min(sampleMaxX, spawnMaxX);

        if (sampleMinX > sampleMaxX)
        {
            return false;
        }

        if (Time.frameCount != lastLandingFrame)
        {
            currentFrameLandingPositions.Clear();
            lastLandingFrame = Time.frameCount;
        }

        float minDist = Mathf.Max(0f, minStrikeDistance);
        float minDistSqr = minDist * minDist;

        int maxAttempts = 60;
        int strictMinDistAttempts = 40;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float x = Random.Range(sampleMinX, sampleMaxX);
            float y = Random.Range(minY, maxY);
            Vector2 p = new Vector2(x, y);

            if (!IsPointInsidePolygon(p, poly))
            {
                continue;
            }

            Vector3 candidate = new Vector3(x, y, startPosition.z);

            if (attempt < strictMinDistAttempts && minDistSqr > 0f)
            {
                bool tooClose = false;
                for (int i = 0; i < currentFrameLandingPositions.Count; i++)
                {
                    Vector3 other = currentFrameLandingPositions[i];
                    if ((candidate - other).sqrMagnitude < minDistSqr)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                {
                    continue;
                }
            }

            currentFrameLandingPositions.Add(candidate);
            landing = candidate;
            return true;
        }

        List<Vector2> fallbackCandidates = null;
        for (int i = 0; i < poly.Count; i++)
        {
            Vector2 v = poly[i];
            if (v.x < sampleMinX || v.x > sampleMaxX)
            {
                continue;
            }

            if (fallbackCandidates == null) fallbackCandidates = new List<Vector2>();
            fallbackCandidates.Add(v);
        }

        if (fallbackCandidates != null && fallbackCandidates.Count > 0)
        {
            Vector2 v = fallbackCandidates[Random.Range(0, fallbackCandidates.Count)];
            Vector3 candidate = new Vector3(v.x, v.y, startPosition.z);
            currentFrameLandingPositions.Add(candidate);
            landing = candidate;
            return true;
        }

        return false;
    }

    private bool IsPointInsidePolygon(Vector2 p, List<Vector2> poly)
    {
        bool inside = false;
        int count = poly.Count;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[j];

            bool intersect = ((a.y > p.y) != (b.y > p.y)) &&
                             (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y + 0.000001f) + a.x);
            if (intersect)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        Debug.Log($"<color=red>NuclearStrike exploding at {transform.position}!</color>");

        // Calculate explosion center with offset for visual and damage logic
        Vector2 explosionCenter = (Vector2)transform.position + explosionRadiusOffset;

        // Snapshot all targets in the explosion radius immediately at impact.
        // Damage application will be delayed, but the target set is decided now.
        Collider2D[] hitCollidersAtImpact = Physics2D.OverlapCircleAll(explosionCenter, explosionRadius, enemyLayer);
        List<Collider2D> impactTargets = new List<Collider2D>(hitCollidersAtImpact != null ? hitCollidersAtImpact.Length : 0);
        if (hitCollidersAtImpact != null)
        {
            for (int i = 0; i < hitCollidersAtImpact.Length; i++)
            {
                if (hitCollidersAtImpact[i] != null)
                {
                    impactTargets.Add(hitCollidersAtImpact[i]);
                }
            }
        }

        // Create a detached runner that will handle delayed damage and any
        // delayed explosion VFX timing even after this projectile is destroyed.
        GameObject runnerObj = new GameObject("NuclearStrikeDamageRunner");
        NuclearStrikeDamageRunner runner = runnerObj.AddComponent<NuclearStrikeDamageRunner>();

        if (ProjectileCardModifiers.Instance != null)
        {
            ProjectileCards card = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
            if (card != null)
            {
                ProjectileCardModifiers.Instance.TagProjectileWithCard(runnerObj, card);
            }
        }

        BurnEffect burnEffect = GetComponent<BurnEffect>();
        SlowEffect slowEffect = GetComponent<SlowEffect>();
        StaticEffect staticEffect = GetComponent<StaticEffect>();

        bool expectsExplosionEffect = explosionEffectPrefab != null;

        runner.Initialize(
            impactTargets,
            explosionCenter,
            explosionRadius,
            damage,
            projectileType,
            damageDelay,
            cachedPlayerStats,
            burnEffect,
            slowEffect,
            staticEffect,
            expectsExplosionEffect
        );

        // Spawn explosion effect with offset, size, and timing.
        // If the effect is configured to be delayed, schedule it via the runner
        // so it still happens even though this projectile will be destroyed.
        if (explosionEffectPrefab != null)
        {
            Camera mainCam = Camera.main;
            bool isOnLeftSide = mainCam != null && transform.position.x < mainCam.transform.position.x;
            Vector2 effectOffset = GetOffsetForCurrentSize(isOnLeftSide);
            Vector3 explosionPosition = transform.position + (Vector3)effectOffset;

            float effectDelay = explosionEffectTimingAdjustment < 0f ? Mathf.Abs(explosionEffectTimingAdjustment) : 0f;

            runner.ConfigureExplosionEffect(
                explosionEffectPrefab,
                explosionPosition,
                effectDelay,
                explosionEffectDuration,
                explosionFadeOutDuration,
                explosionEffectSizeMultiplier,
                radiusToEffectBaseRadius,
                baseExplosionRadius,
                finalExplosionAnimationSpeed
            );

            if (effectDelay <= 0f)
            {
                // Immediate spawn to preserve current behavior.
                runner.SpawnConfiguredExplosionEffectNow();
            }
        }

        runner.StartWork();

        // Play explosion sound
        if (explosionClip != null)
        {
            AudioSource.PlayClipAtPoint(explosionClip, transform.position, explosionVolume);
        }

        // Destroy the missile immediately on impact. Any delayed damage/effect
        // timing is handled by NuclearStrikeDamageRunner.
        if (shadowInstance != null)
        {
            ShadowSelfDestruct selfDestruct = shadowInstance.AddComponent<ShadowSelfDestruct>();
            selfDestruct.Initialize(ShadowFadeAwayDuration);
            shadowInstance = null;
        }
        Destroy(gameObject);
    }

    private class NuclearStrikeDamageRunner : MonoBehaviour
    {
        private List<Collider2D> targets;
        private Vector2 explosionCenter;
        private float explosionRadius;
        private float damage;
        private ProjectileType projectileType;
        private float damageDelay;
        private PlayerStats cachedPlayerStats;

        private BurnEffect burnEffect;
        private SlowEffect slowEffect;
        private StaticEffect staticEffect;

        private GameObject explosionEffectPrefab;
        private Vector3 explosionEffectPosition;
        private float explosionEffectDelay;
        private float explosionEffectDuration;
        private float explosionFadeOutDuration;
        private float explosionEffectSizeMultiplier;
        private float radiusToEffectBaseRadius;
        private float baseExplosionRadius;
        private float finalExplosionAnimationSpeed;
        private bool hasExplosionEffectConfig;

        private bool damageRoutineFinished;
        private bool effectRoutineFinished;

        private bool expectsExplosionEffect;

        public void Initialize(
            List<Collider2D> impactTargets,
            Vector2 center,
            float radius,
            float baseDamage,
            ProjectileType type,
            float delay,
            PlayerStats stats,
            BurnEffect sourceBurn,
            SlowEffect sourceSlow,
            StaticEffect sourceStatic,
            bool expectsExplosionEffect)
        {
            targets = impactTargets;
            explosionCenter = center;
            explosionRadius = radius;
            damage = baseDamage;
            projectileType = type;
            damageDelay = delay;
            cachedPlayerStats = stats;

            this.expectsExplosionEffect = expectsExplosionEffect;
            effectRoutineFinished = !expectsExplosionEffect;

            if (sourceBurn != null)
            {
                burnEffect = gameObject.AddComponent<BurnEffect>();
                burnEffect.burnChance = sourceBurn.burnChance;
                burnEffect.burnStacksPerHit = sourceBurn.burnStacksPerHit;
                burnEffect.burnDamageMultiplier = sourceBurn.burnDamageMultiplier;
                burnEffect.burnDuration = sourceBurn.burnDuration;
                burnEffect.burnVFXPrefab = sourceBurn.burnVFXPrefab;
                burnEffect.burnVFXOffsetLeft = sourceBurn.burnVFXOffsetLeft;
                burnEffect.burnVFXOffsetRight = sourceBurn.burnVFXOffsetRight;
            }

            if (sourceSlow != null)
            {
                slowEffect = gameObject.AddComponent<SlowEffect>();
                slowEffect.slowChance = sourceSlow.slowChance;
                slowEffect.slowStacksPerHit = sourceSlow.slowStacksPerHit;
                slowEffect.slowDuration = sourceSlow.slowDuration;
                slowEffect.slowVFXPrefab = sourceSlow.slowVFXPrefab;
                slowEffect.slowTintColor = sourceSlow.slowTintColor;
            }

            if (sourceStatic != null)
            {
                staticEffect = gameObject.AddComponent<StaticEffect>();
                staticEffect.staticChance = sourceStatic.staticChance;
                staticEffect.staticPeriod = sourceStatic.staticPeriod;
                staticEffect.staticDuration = sourceStatic.staticDuration;
                staticEffect.staticReapplyChance = sourceStatic.staticReapplyChance;
                staticEffect.staticReapplyInterval = sourceStatic.staticReapplyInterval;
                staticEffect.staticVFXPrefab = sourceStatic.staticVFXPrefab;
                staticEffect.staticVFXOffsetLeft = sourceStatic.staticVFXOffsetLeft;
                staticEffect.staticVFXOffsetRight = sourceStatic.staticVFXOffsetRight;
            }

        }

        public void StartWork()
        {
            StartCoroutine(ApplyDelayedDamageRoutine());
        }

        public void ConfigureExplosionEffect(
            GameObject prefab,
            Vector3 position,
            float delaySeconds,
            float effectDurationSeconds,
            float fadeOutSeconds,
            float sizeMultiplier,
            float baseRadiusForScaling,
            float baseRadiusFromProjectile,
            float animSpeed)
        {
            explosionEffectPrefab = prefab;
            explosionEffectPosition = position;
            explosionEffectDelay = Mathf.Max(0f, delaySeconds);
            explosionEffectDuration = Mathf.Max(0f, effectDurationSeconds);
            explosionFadeOutDuration = Mathf.Max(0f, fadeOutSeconds);
            explosionEffectSizeMultiplier = sizeMultiplier;
            radiusToEffectBaseRadius = baseRadiusForScaling;
            baseExplosionRadius = baseRadiusFromProjectile;
            finalExplosionAnimationSpeed = animSpeed;
            hasExplosionEffectConfig = explosionEffectPrefab != null;

            effectRoutineFinished = false;
            if (explosionEffectDelay > 0f)
            {
                StartCoroutine(SpawnExplosionEffectAfterDelay());
            }
        }

        public void SpawnConfiguredExplosionEffectNow()
        {
            if (!hasExplosionEffectConfig)
            {
                effectRoutineFinished = true;
                TryCleanup();
                return;
            }

            if (explosionEffectDelay > 0f)
            {
                return;
            }

            SpawnExplosionEffectImmediate(explosionEffectPosition);
            effectRoutineFinished = true;
            TryCleanup();
        }

        private IEnumerator SpawnExplosionEffectAfterDelay()
        {
            if (explosionEffectDelay > 0f)
            {
                yield return GameStateManager.WaitForPauseSafeSeconds(explosionEffectDelay);
            }

            if (hasExplosionEffectConfig)
            {
                SpawnExplosionEffectImmediate(explosionEffectPosition);
            }

            effectRoutineFinished = true;
            TryCleanup();
        }

        private void SpawnExplosionEffectImmediate(Vector3 position)
        {
            if (explosionEffectPrefab == null) return;

            GameObject explosion = Instantiate(explosionEffectPrefab, position, Quaternion.identity);

            float baseRadius = radiusToEffectBaseRadius > 0f ? radiusToEffectBaseRadius : baseExplosionRadius;
            if (baseRadius <= 0f)
            {
                baseRadius = explosionRadius;
            }

            float radiusRatio = baseRadius > 0f ? (explosionRadius / baseRadius) : 1f;
            float finalEffectScale = explosionEffectSizeMultiplier * radiusRatio;
            explosion.transform.localScale = Vector3.one * finalEffectScale;

            Animator explosionAnimator = explosion.GetComponent<Animator>();
            if (explosionAnimator != null && finalExplosionAnimationSpeed > 0f)
            {
                explosionAnimator.speed = finalExplosionAnimationSpeed;
            }

            if (explosionFadeOutDuration > 0f)
            {
                ExplosionSelfDestruct selfDestruct = explosion.AddComponent<ExplosionSelfDestruct>();
                selfDestruct.Initialize(explosionEffectDuration, explosionFadeOutDuration);
            }
            else
            {
                ExplosionSelfDestruct selfDestruct = explosion.AddComponent<ExplosionSelfDestruct>();
                selfDestruct.Initialize(explosionEffectDuration, 0f);
            }
        }

        private IEnumerator ApplyDelayedDamageRoutine()
        {
            if (damageDelay > 0f)
            {
                yield return GameStateManager.WaitForPauseSafeSeconds(damageDelay);
            }
            else
            {
                yield return null;
            }

            bool burnRollDetermined = false;
            bool applyBurnToAll = false;

            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    Collider2D hitCollider = targets[i];
                    if (hitCollider == null) continue;

                    IDamageable damageable = hitCollider.GetComponent<IDamageable>() ?? hitCollider.GetComponentInParent<IDamageable>();
                    if (damageable == null || !damageable.IsAlive) continue;

                    if (!OffscreenDamageChecker.CanTakeDamage(hitCollider.transform.position))
                    {
                        continue;
                    }

                    Vector3 hitPoint = hitCollider.ClosestPoint(explosionCenter);
                    Vector3 hitNormal = (explosionCenter - (Vector2)hitPoint).normalized;

                    float baseDamageForEnemy = damage;
                    float finalDamage = baseDamageForEnemy;

                    Component damageableComponent = damageable as Component;
                    GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : hitCollider.gameObject;

                    if (cachedPlayerStats != null)
                    {
                        finalDamage = PlayerDamageHelper.ComputeProjectileDamage(cachedPlayerStats, enemyObject, baseDamageForEnemy, gameObject);
                    }

                    if (enemyObject != null)
                    {
                        EnemyHealth enemyHealth = enemyObject.GetComponent<EnemyHealth>() ?? enemyObject.GetComponentInParent<EnemyHealth>();
                        if (enemyHealth != null)
                        {
                            DamageNumberManager.DamageType dmgType = projectileType == ProjectileType.Fire
                                ? DamageNumberManager.DamageType.Fire
                                : (projectileType == ProjectileType.Thunder || projectileType == ProjectileType.ThunderDisc
                                    ? DamageNumberManager.DamageType.Thunder
                                    : DamageNumberManager.DamageType.Ice);
                            enemyHealth.SetLastIncomingDamageType(dmgType);
                        }
                    }

                    DamageAoeScope.BeginAoeDamage();
                    damageable.TakeDamage(finalDamage, hitPoint, hitNormal);
                    DamageAoeScope.EndAoeDamage();

                    if (!burnRollDetermined)
                    {
                        StatusController immuneCheck = hitCollider.GetComponent<StatusController>() ?? hitCollider.GetComponentInParent<StatusController>();
                        if (immuneCheck == null || !immuneCheck.HasStatus(StatusId.Immune))
                        {
                            applyBurnToAll = StatusController.TryApplyBurnFromProjectile(gameObject, hitCollider.gameObject, hitPoint, finalDamage);
                            burnRollDetermined = true;
                        }
                    }
                    else if (applyBurnToAll)
                    {
                        StatusController.TryApplyBurnFromProjectile(gameObject, hitCollider.gameObject, hitPoint, finalDamage, true);
                    }

                    if (slowEffect != null)
                    {
                        slowEffect.TryApplySlow(hitCollider.gameObject, hitPoint);
                    }

                    if (staticEffect != null)
                    {
                        staticEffect.TryApplyStatic(hitCollider.gameObject, hitPoint);
                    }
                }
            }

            damageRoutineFinished = true;
            TryCleanup();
        }

        private void TryCleanup()
        {
            if (damageRoutineFinished && effectRoutineFinished)
            {
                Destroy(gameObject);
            }
        }
    }

    private IEnumerator ApplyExplosionDamageAfterDelay(Vector2 explosionCenter)
    {
        if (damageDelay > 0f)
        {
            yield return GameStateManager.WaitForPauseSafeSeconds(damageDelay);
        }

        // Find all enemies in explosion radius at the time damage is applied
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(explosionCenter, explosionRadius, enemyLayer);

        Debug.Log($"<color=red>NuclearStrike damage pass: {hitColliders.Length} enemies in radius after {damageDelay:F2}s delay</color>");

        // We want burn chance to be rolled ONCE per explosion. The first
        // valid enemy we hit determines whether this explosion will burn, and
        // that result is then applied uniformly to all other enemies hit.
        bool burnRollDetermined = false;
        bool applyBurnToAll = false;

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
                            : (projectileType == ProjectileType.Thunder || projectileType == ProjectileType.ThunderDisc
                                ? DamageNumberManager.DamageType.Thunder
                                : DamageNumberManager.DamageType.Ice);
                        enemyHealth.SetLastIncomingDamageType(damageType);
                    }
                }

                // === AOE DAMAGE CLASSIFICATION (bypasses enemy NULLIFY) ===
                DamageAoeScope.BeginAoeDamage();
                damageable.TakeDamage(finalDamage, hitPoint, hitNormal);
                DamageAoeScope.EndAoeDamage();

                // First valid enemy: perform the actual burn chance roll
                // and cache the outcome, then apply to all others.
                if (!burnRollDetermined)
                {
                    StatusController immuneCheck = hitCollider.GetComponent<StatusController>() ?? hitCollider.GetComponentInParent<StatusController>();
                    if (immuneCheck == null || !immuneCheck.HasStatus(StatusId.Immune))
                    {
                        applyBurnToAll = StatusController.TryApplyBurnFromProjectile(gameObject, hitCollider.gameObject, hitPoint, finalDamage);
                        burnRollDetermined = true;
                    }
                }
                else if (applyBurnToAll)
                {
                    StatusController.TryApplyBurnFromProjectile(gameObject, hitCollider.gameObject, hitPoint, finalDamage, true);
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

        // After applying all delayed damage and status effects, the
        // NuclearStrike projectile can safely be destroyed.
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

        // Radius-to-effect scaling: treat explosionEffectSizeMultiplier as the
        // base visual size when explosionRadius equals the base radius. When
        // explosionRadius grows (from size modifiers, explosionRadiusBonus,
        // or multipliers), scale the effect proportionally.

        // Determine the baseline radius used for visual scaling.
        float baseRadius = radiusToEffectBaseRadius > 0f ? radiusToEffectBaseRadius : baseExplosionRadius;
        if (baseRadius <= 0f)
        {
            baseRadius = explosionRadius;
        }

        float radiusRatio = baseRadius > 0f ? (explosionRadius / baseRadius) : 1f;
        float finalEffectScale = explosionEffectSizeMultiplier * radiusRatio;

        Debug.Log($"<color=gold>NuclearStrike Explosion Size (Radius-Scaled): baseRadius={baseRadius:F2}, explosionRadius={explosionRadius:F2}, ratio={radiusRatio:F2}, effectSizeMult={explosionEffectSizeMultiplier:F2}, finalScale={finalEffectScale:F2}</color>");

        explosion.transform.localScale = Vector3.one * finalEffectScale;

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
            ExplosionSelfDestruct selfDestruct = explosion.AddComponent<ExplosionSelfDestruct>();
            selfDestruct.Initialize(explosionEffectDuration, 0f);
        }
    }

    private IEnumerator FadeOutExplosion(GameObject explosion, float totalDuration, float fadeOutDuration)
    {
        // Wait until it's time to start fading
        float waitTime = totalDuration - fadeOutDuration;
        if (waitTime > 0f)
        {
            yield return GameStateManager.WaitForPauseSafeSeconds(waitTime);
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
            elapsed += GameStateManager.GetPauseSafeDeltaTime();
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
        yield return GameStateManager.WaitForPauseSafeSeconds(delay);

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
        if (shadowInstance != null && !hasExploded)
        {
            ShadowSelfDestruct selfDestruct = shadowInstance.AddComponent<ShadowSelfDestruct>();
            selfDestruct.Initialize(ShadowFadeAwayDuration);
            shadowInstance = null;
            Debug.Log($"<color=yellow>NuclearStrike destroyed - fading out shadow</color>");
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

 if (shadowInstance != null && !hasExploded)
 {
     Vector3 shadowScale = shadowInstance.transform.localScale;
     shadowScale.x = explosionRadius;
     shadowScale.y = explosionRadius;
     shadowInstance.transform.localScale = shadowScale;
 }

// Recalculate damage
float newDamage = (baseDamage + modifiers.damageFlat) * modifiers.damageMultiplier;
if (newDamage != damage)
{
    damage = newDamage;
    Debug.Log($"<color=lime>  Damage: ({baseDamage:F2} + {modifiers.damageFlat:F2}) * {modifiers.damageMultiplier:F2}x = {damage:F2}</color>");
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

public class ShadowSelfDestruct : MonoBehaviour
{
private float fadeOutDuration;

public void Initialize(float fadeOut)
{
    fadeOutDuration = Mathf.Max(0f, fadeOut);
    StartCoroutine(FadeAndDestroy());
}

private IEnumerator FadeAndDestroy()
{
    if (fadeOutDuration <= 0f)
    {
        Destroy(gameObject);
        yield break;
    }

    SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>();
    ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>();

    Color[] originalColors = new Color[sprites.Length];
    for (int i = 0; i < sprites.Length; i++)
    {
        originalColors[i] = sprites[i].color;
    }

    float elapsed = 0f;
    while (elapsed < fadeOutDuration)
    {
        elapsed += GameStateManager.GetPauseSafeDeltaTime();
        float alpha = 1f - (elapsed / fadeOutDuration);

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
            {
                Color c = originalColors[i];
                c.a = originalColors[i].a * alpha;
                sprites[i].color = c;
            }
        }

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

    Destroy(gameObject);
}
}
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
        float waitTime = totalDuration - fadeOutDuration;
        if (waitTime > 0f)
        {
            yield return GameStateManager.WaitForPauseSafeSeconds(waitTime);
        }

        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>();
        ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>();

        Color[] originalColors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            originalColors[i] = sprites[i].color;
        }

        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += GameStateManager.GetPauseSafeDeltaTime();
            float alpha = 1f - (elapsed / fadeOutDuration);

            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null)
                {
                    Color c = originalColors[i];
                    c.a = originalColors[i].a * alpha;
                    sprites[i].color = c;
                }
            }

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

        Destroy(gameObject);
    }
}