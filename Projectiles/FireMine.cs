using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class FireMine : MonoBehaviour, IInstantModifiable
{
    [Header("Mine Settings")]
    [SerializeField] private float armDelay = 0.5f;
    [Tooltip("Time before mine explodes after being armed")]
    [SerializeField] private float lifetimeSeconds = 5f;

    public int SpawnLimit = 50;

    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 3f;
    [Tooltip("Offset for explosion detection area in X and Y coordinates")]
    [SerializeField] private Vector2 explosionRadiusOffset = Vector2.zero;
    [Range(0f, 1f)] public float RuntimeGizmoAlphaMultiplier = 1f;
    [SerializeField] private float damage = 40f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private ProjectileType projectileType = ProjectileType.Fire;

    [Header("Explosion Timing")]
    [Tooltip("Delay before explosion deals damage after enemy contact (in seconds)")]
    public float explosionDelay = 0.2f;
    [Tooltip("Delay before showing damage numbers after explosion (in seconds)")]
    public float damageNumberDelay = 0f;
    public float ExplosionDamageDelay = 0f;

    [Header("Spawn Area - 6 Point System")]
    [Tooltip("Tag for point A (top-left): determines polygon vertex A")]
    [SerializeField] private string pointATag = "FireMine_PointA";
    [Tooltip("Tag for point B (top-right): determines polygon vertex B")]
    [SerializeField] private string pointBTag = "FireMine_PointB";
    [Tooltip("Tag for point C (bottom-right): determines polygon vertex C")]
    [SerializeField] private string pointCTag = "FireMine_PointC";
    [Tooltip("Tag for point D (bottom-left): determines polygon vertex D")]
    [SerializeField] private string pointDTag = "FireMine_PointD";
    [Tooltip("Tag for point E (optional extra vertex)")]
    [SerializeField] private string pointETag = "FireMine_PointE";
    [Tooltip("Tag for point F (optional extra vertex)")]
    [SerializeField] private string pointFTag = "FireMine_PointF";

    [Header("Overlap Prevention")]
    [Tooltip("Minimum distance allowed between FireMines (multiplier of collider radius)")]
    [SerializeField] private float minDistanceBetweenMines = 2f;

    [Header("Collider Scaling")]
    [Tooltip("Offset for collider size relative to visual size (0 = same as visual, -0.2 = 20% smaller, 0.2 = 20% larger)")]
    [SerializeField] private float colliderSizeOffset = 0f;

    private Transform pointA;
    private Transform pointB;
    private Transform pointC;
    private Transform pointD;
    private Transform pointE;
    private Transform pointF;

    [Header("Visual Effects")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private float explosionEffectDuration = 2f;
    [Tooltip("Offset for explosion effect (left side)")]
    [SerializeField] private Vector2 explosionEffectOffsetLeft = Vector2.zero;
    [Tooltip("Offset for explosion effect (right side)")]
    [SerializeField] private Vector2 explosionEffectOffsetRight = Vector2.zero;
    [Tooltip("Size multiplier for explosion effect")]
    [SerializeField] private float explosionEffectSizeMultiplier = 1f;
    [Tooltip("Effect timing adjustment: negative = delay effect, positive = play effect early (seconds)")]
    [SerializeField] private float explosionEffectTimingAdjustment = 0f;
    [Header("Explosion Effect Scaling")]
    [Tooltip("Multiplier for scaling the explosion effect with explosionRadius. 1 = normal proportional scaling; <1 = weaker scaling; >1 = stronger scaling.")]
    public float ExplosionEffectScaling = 1f;
    [SerializeField] private GameObject armingEffectPrefab;
    [SerializeField] private float fadeInDuration = 0.5f;
    public float FadeAwayDuration = 1f;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Audio")]
    [SerializeField] private AudioClip explosionClip;
    [Range(0f, 1f)][SerializeField] private float explosionVolume = 1f;
    [SerializeField] private AudioClip armingClip;
    [Range(0f, 1f)][SerializeField] private float armingVolume = 0.5f;

    [Header("Mana & Cooldown")]
    [SerializeField] private int manaCost = 15;
    [SerializeField] private float cooldown = 1f;

    [Header("Multi-Tier Enhancement System")]
    [Tooltip("Enhanced prefab for Tier 1 (e.g., level 10) - will swap to this prefab when enhanced")]
    [SerializeField] private GameObject enhancedTier1Prefab;
    [Tooltip("Enhanced prefab for Tier 2 (e.g., level 20) - will swap to this prefab at higher enhancement")]
    [SerializeField] private GameObject enhancedTier2Prefab;
    [Tooltip("Level multiplier for tier thresholds (e.g., 2 means Tier 2 = Tier 1 level * 2)")]
    [SerializeField] private float tierLevelMultiplier = 2;

    [Header("Enhanced Variant 1 - Mega Mine")]
    [Tooltip("Size increase for Enhanced Variant 1 (0.25 = +25%)")]
    [SerializeField] private float enhancedSizeIncrease = 0.25f;
    [Tooltip("Lifetime bonus for Enhanced Variant 1 (in seconds)")]
    [SerializeField] private float enhancedLifetimeBonus = 30f;
    [Tooltip("Cooldown reduction for Enhanced Variant 1 (0.25 = 25% reduction)")]
    [SerializeField] private float enhancedCooldownReduction = 0.25f;
    [Tooltip("Additional projectile count for Enhanced Variant 1 (e.g., 1 = spawn 1 extra)")]
    [SerializeField] public int enhancedProjectileCountBonus = 1;

    [Header("Enhanced Variant 2 - Ultra Mine")]
    [Tooltip("Size increase for Enhanced Variant 2 (0.5 = +50%)")]
    [SerializeField] private float enhancedTier2SizeIncrease = 0.5f;
    [Tooltip("Lifetime bonus for Enhanced Variant 2 (in seconds)")]
    [SerializeField] private float enhancedTier2LifetimeBonus = 60f;
    [Tooltip("Cooldown reduction for Enhanced Variant 2 (0.4 = 40% reduction)")]
    [SerializeField] private float enhancedTier2CooldownReduction = 0.4f;
    [Tooltip("Additional projectile count for Enhanced Variant 2 (e.g., 2 = spawn 2 extra)")]
    [SerializeField] public int enhancedTier2ProjectileCountBonus = 2;

    [Header("Enhanced Variant Base Cooldowns")]
    [Tooltip("Base cooldown used when FireMine is in Enhanced Variant 1 (Mega Mine). If 0, falls back to ProjectileCards.runtimeSpawnInterval or script cooldown.")]
    [SerializeField] private float variant1BaseCooldown = 0f;

    [Tooltip("Base cooldown used when FireMine is in Enhanced Variant 2 (Ultra Mine). If 0, falls back to ProjectileCards.runtimeSpawnInterval or script cooldown.")]
    [SerializeField] private float variant2BaseCooldown = 0f;

    private Rigidbody2D _rigidbody2D;
    private Collider2D _collider2D;
    private bool isArmed = false;
    private bool hasExploded = false;
    private bool isExploding = false;

    private Coroutine explodeDelayCoroutine = null;

    // Enhanced system
    private int enhancedVariant = 0; // 0 = basic, 1 = mega mine, 2-3 = future variants
    private float explosionRadiusForEffect = 0f; // Store radius BEFORE modifiers for effect scaling

    // Instance-based cooldown tracking
    private static System.Collections.Generic.Dictionary<string, float> lastFireTimes = new System.Collections.Generic.Dictionary<string, float>();
    private string prefabKey;

    // Base values for instant modifier recalculation
    private float baseLifetimeSeconds;
    private float baseExplosionRadius;
    private float baseDamage;
    private Vector3 baseScale;
    private PlayerStats cachedPlayerStats;
    private float baseDamageAfterCards;

    private float scheduledDespawnTime = -1f;
    private bool registeredInSpawnLimit = false;
    private static System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<FireMine>> activeMinesByKey = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<FireMine>>();

    private static bool bossPauseActive = false;
    private static System.Action<bool> bossPauseChanged;

    private float pausedRemainingLifetime = 0f;
    private bool hasPausedLifetime = false;

    public static bool IsBossPauseActive()
    {
        return bossPauseActive;
    }

    public static void SetBossPauseActive(bool active)
    {
        if (bossPauseActive == active)
        {
            return;
        }

        bossPauseActive = active;
        bossPauseChanged?.Invoke(active);
    }

    private void OnEnable()
    {
        bossPauseChanged += HandleBossPauseChanged;

        if (bossPauseActive)
        {
            HandleBossPauseChanged(true);
        }
    }

    private void OnDisable()
    {
        bossPauseChanged -= HandleBossPauseChanged;
    }

    private void HandleBossPauseChanged(bool paused)
    {
        if (paused)
        {
            if (explodeDelayCoroutine != null && !hasExploded)
            {
                StopCoroutine(explodeDelayCoroutine);
                explodeDelayCoroutine = null;
                isExploding = false;
            }

            if (scheduledDespawnTime > 0f)
            {
                pausedRemainingLifetime = Mathf.Max(0f, scheduledDespawnTime - GameStateManager.PauseSafeTime);
                hasPausedLifetime = true;
            }
        }
        else
        {
            if (hasPausedLifetime && scheduledDespawnTime > 0f)
            {
                scheduledDespawnTime = GameStateManager.PauseSafeTime + pausedRemainingLifetime;
            }

            hasPausedLifetime = false;

            if (isArmed && !hasExploded && !isExploding)
            {
                TryTriggerExplosionFromCurrentOverlaps();
            }
        }
    }

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _collider2D = GetComponent<Collider2D>();

        if (_collider2D != null)
        {
            _collider2D.isTrigger = true;
        }

        // Store base values
        baseLifetimeSeconds = lifetimeSeconds;
        baseExplosionRadius = explosionRadius;
        baseDamage = damage;

        baseScale = transform.localScale;

        // Make mine stationary
        if (_rigidbody2D != null)
        {
            _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody2D.velocity = Vector2.zero;
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (spriteRenderer != null && fadeInDuration > 0f)
        {
            Color c = spriteRenderer.color;
            c.a = 0f;
            spriteRenderer.color = c;
        }
    }

    private void OnDestroy()
    {
        if (!registeredInSpawnLimit)
        {
            return;
        }

        string key = GetType().Name;
        if (activeMinesByKey.TryGetValue(key, out var list) && list != null)
        {
            list.Remove(this);
            if (list.Count == 0)
            {
                activeMinesByKey.Remove(key);
            }
        }
    }

    private float GetRemainingLifetimeSeconds()
    {
        if (scheduledDespawnTime <= 0f)
        {
            return float.MaxValue;
        }

        if (bossPauseActive && hasPausedLifetime)
        {
            return Mathf.Max(0f, pausedRemainingLifetime);
        }

        return Mathf.Max(0f, scheduledDespawnTime - GameStateManager.PauseSafeTime);
    }

    private void RegisterAndEnforceSpawnLimit()
    {
        if (SpawnLimit <= 0)
        {
            return;
        }

        string key = GetType().Name;
        if (!activeMinesByKey.TryGetValue(key, out var list) || list == null)
        {
            list = new System.Collections.Generic.List<FireMine>();
            activeMinesByKey[key] = list;
        }

        list.RemoveAll(m => m == null);

        if (!list.Contains(this))
        {
            list.Add(this);
        }

        registeredInSpawnLimit = true;

        int limit = Mathf.Max(1, SpawnLimit);
        while (list.Count > limit)
        {
            FireMine candidate = null;
            float lowestRemaining = float.MaxValue;

            for (int i = 0; i < list.Count; i++)
            {
                FireMine mine = list[i];
                if (mine == null || mine == this)
                {
                    continue;
                }

                float remaining = mine.GetRemainingLifetimeSeconds();
                if (remaining < lowestRemaining)
                {
                    lowestRemaining = remaining;
                    candidate = mine;
                }
            }

            if (candidate == null)
            {
                break;
            }

            list.Remove(candidate);
            if (candidate.gameObject != null)
            {
                candidate.gameObject.SetActive(false);
            }
            Destroy(candidate.gameObject);
        }
    }

    /// <summary>
    /// Initialize enhanced mine with transferred stats (skips enhancement check to prevent recursion)
    /// </summary>
    public void InitializeEnhanced(Vector3 spawnPosition, Collider2D playerCollider, CardModifierStats modifiers, ProjectileCards card, int tier, bool skipCooldownCheck = false)
    {
        // Set enhanced variant to prevent re-enhancement
        this.enhancedVariant = tier;

        // Continue with normal initialization but skip enhancement check
        InitializeInternal(spawnPosition, playerCollider, true, skipCooldownCheck);
    }

    public void Initialize(Vector3 spawnPosition, Collider2D playerCollider, bool skipCooldownCheck = false)
    {
        InitializeInternal(spawnPosition, playerCollider, false, skipCooldownCheck);
    }

    private void InitializeInternal(Vector3 spawnPosition, Collider2D playerCollider, bool skipEnhancementCheck, bool skipCooldownCheck = false)
    {
        // Find spawn area GameObjects by tag (supports up to 6 points: A-F)
        if (!string.IsNullOrEmpty(pointATag))
        {
            GameObject pointAObj = GameObject.FindGameObjectWithTag(pointATag);
            if (pointAObj != null) pointA = pointAObj.transform;
        }

        if (!string.IsNullOrEmpty(pointBTag))
        {
            GameObject pointBObj = GameObject.FindGameObjectWithTag(pointBTag);
            if (pointBObj != null) pointB = pointBObj.transform;
        }

        if (!string.IsNullOrEmpty(pointCTag))
        {
            GameObject pointCObj = GameObject.FindGameObjectWithTag(pointCTag);
            if (pointCObj != null) pointC = pointCObj.transform;
        }

        if (!string.IsNullOrEmpty(pointDTag))
        {
            GameObject pointDObj = GameObject.FindGameObjectWithTag(pointDTag);
            if (pointDObj != null) pointD = pointDObj.transform;
        }

        if (!string.IsNullOrEmpty(pointETag))
        {
            GameObject pointEObj = GameObject.FindGameObjectWithTag(pointETag);
            if (pointEObj != null) pointE = pointEObj.transform;
        }

        if (!string.IsNullOrEmpty(pointFTag))
        {
            GameObject pointFObj = GameObject.FindGameObjectWithTag(pointFTag);
            if (pointFObj != null) pointF = pointFObj.transform;
        }

        // Determine spawn position using polygon system (3-6 points)
        System.Collections.Generic.List<Vector2> spawnPolygon = new System.Collections.Generic.List<Vector2>(6);
        if (pointA != null) spawnPolygon.Add(pointA.position);
        if (pointB != null) spawnPolygon.Add(pointB.position);
        if (pointC != null) spawnPolygon.Add(pointC.position);
        if (pointD != null) spawnPolygon.Add(pointD.position);
        if (pointE != null) spawnPolygon.Add(pointE.position);
        if (pointF != null) spawnPolygon.Add(pointF.position);

        if (spawnPolygon.Count >= 3)
        {
            float minX = spawnPolygon[0].x;
            float maxX = spawnPolygon[0].x;
            float minY = spawnPolygon[0].y;
            float maxY = spawnPolygon[0].y;

            for (int i = 1; i < spawnPolygon.Count; i++)
            {
                Vector2 v = spawnPolygon[i];
                if (v.x < minX) minX = v.x;
                if (v.x > maxX) maxX = v.x;
                if (v.y < minY) minY = v.y;
                if (v.y > maxY) maxY = v.y;
            }

            Vector3 finalPosition = spawnPosition;
            bool foundValidPosition = false;
            int maxAttempts = 20;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                float spawnX = Random.Range(minX, maxX);
                float spawnY = Random.Range(minY, maxY);
                Vector2 test2D = new Vector2(spawnX, spawnY);

                if (!IsPointInsidePolygon(test2D, spawnPolygon))
                {
                    continue;
                }

                Vector3 testPosition = new Vector3(spawnX, spawnY, spawnPosition.z);

                if (!IsOverlappingWithOtherMines(testPosition))
                {
                    finalPosition = testPosition;
                    foundValidPosition = true;
                    Debug.Log($"<color=orange>FireMine spawned at ({spawnX:F2}, {spawnY:F2}) inside polygon - no overlap (attempt {attempt + 1})</color>");
                    break;
                }
            }

            if (!foundValidPosition)
            {
                // Last resort: pick any point inside the polygon, ignoring overlap if needed
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    float spawnX = Random.Range(minX, maxX);
                    float spawnY = Random.Range(minY, maxY);
                    Vector2 test2D = new Vector2(spawnX, spawnY);
                    if (IsPointInsidePolygon(test2D, spawnPolygon))
                    {
                        finalPosition = new Vector3(spawnX, spawnY, spawnPosition.z);
                        Debug.LogWarning($"<color=yellow>FireMine: Using inside-polygon position after overlap attempts failed ({spawnX:F2}, {spawnY:F2})</color>");
                        break;
                    }
                }
            }

            transform.position = finalPosition;
        }
        else
        {
            transform.position = spawnPosition;
            Debug.LogWarning("<color=yellow>FireMine: Not enough spawn points found (need at least 3), using default position</color>");
        }

        // Get card-specific modifiers FIRST
        ProjectileCards card = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
        CardModifierStats modifiers = new CardModifierStats(); // Default values

        if (card != null)
        {
            modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
            Debug.Log($"<color=orange>FireMine using modifiers from {card.cardName}</color>");
        }

        // Declare final stat variables early so they can be used in prefab swapping
        float finalLifetime = lifetimeSeconds;
        float finalCooldown = cooldown;
        int finalManaCost = manaCost;

        // Check for enhanced variant using CARD-based system with prefab swapping
        if (!skipEnhancementCheck && ProjectileCardLevelSystem.Instance != null && card != null)
        {
            int currentLevel = ProjectileCardLevelSystem.Instance.GetLevel(card);
            int unlockLevel = ProjectileCardLevelSystem.Instance.GetEnhancedUnlockLevel();
            enhancedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card);

            // Check if we should swap to a higher tier prefab
            GameObject targetPrefab = null;
            int targetTier = 0;

            // Tier 2: level >= unlockLevel * tierLevelMultiplier (e.g., level 20 if unlock is 10)
            if (enhancedTier2Prefab != null && currentLevel >= unlockLevel * tierLevelMultiplier)
            {
                targetPrefab = enhancedTier2Prefab;
                targetTier = 2;
            }
            // Tier 1: level >= unlockLevel (e.g., level 10)
            else if (enhancedTier1Prefab != null && currentLevel >= unlockLevel)
            {
                targetPrefab = enhancedTier1Prefab;
                targetTier = 1;
            }

            // Swap to enhanced prefab if needed
            if (targetPrefab != null && targetTier > 0)
            {
                Debug.Log($"<color=gold>FireMine TIER {targetTier} UPGRADE: Level {currentLevel} >= threshold, swapping to {targetPrefab.name}</color>");

                // Instantiate the enhanced prefab at current position
                GameObject enhancedObj = Instantiate(targetPrefab, transform.position, transform.rotation);

                FireMine enhancedMine = enhancedObj.GetComponent<FireMine>();
                if (enhancedMine != null)
                {
                    // Ensure the enhanced instance is tagged with the same card so modifiers carry over
                    if (card != null)
                    {
                        ProjectileCardModifiers.Instance.TagProjectileWithCard(enhancedObj, card);
                    }

                    enhancedMine.enhancedVariant = targetTier;
                    enhancedMine.InitializeEnhanced(transform.position, playerCollider, modifiers, card, targetTier, skipCooldownCheck);
                }

                // Destroy this instance and return
                Destroy(gameObject);
                return;
            }

            Debug.Log($"<color=gold>FireMine ({card.cardName}) Enhanced Variant: {enhancedVariant}, Level: {currentLevel}/{unlockLevel}</color>");
        }

        // Apply enhanced variant modifiers BEFORE card modifiers
        float enhancedSizeMult = 1f;
        float enhancedLifetimeAdd = 0f;
        float enhancedCooldownRed = 0f;
        int enhancedProjectileBonus = 0;

        if (enhancedVariant == 1)
        {
            enhancedSizeMult = 1f + enhancedSizeIncrease; // e.g., 1.25 for +25%
            enhancedLifetimeAdd = enhancedLifetimeBonus;
            enhancedCooldownRed = enhancedCooldownReduction;

            // Store enhanced projectile count bonus (don't modify modifiers directly!)
            enhancedProjectileBonus = enhancedProjectileCountBonus;

            Debug.Log($"<color=gold>Enhanced Tier 1 Mega Mine: Size x{enhancedSizeMult}, Lifetime +{enhancedLifetimeAdd}s, Cooldown -{enhancedCooldownRed * 100}%, Additional Projectiles +{enhancedProjectileBonus}</color>");
        }
        else if (enhancedVariant == 2)
        {
            enhancedSizeMult = 1f + enhancedTier2SizeIncrease; // e.g., 1.5 for +50%
            enhancedLifetimeAdd = enhancedTier2LifetimeBonus;
            enhancedCooldownRed = enhancedTier2CooldownReduction;

            // Store enhanced projectile count bonus (don't modify modifiers directly!)
            enhancedProjectileBonus = enhancedTier2ProjectileCountBonus;

            Debug.Log($"<color=gold>Enhanced Tier 2 Ultra Mine: Size x{enhancedSizeMult}, Lifetime +{enhancedLifetimeAdd}s, Cooldown -{enhancedCooldownRed * 100}%, Additional Projectiles +{enhancedProjectileBonus}</color>");
        }

        // Apply card modifiers using new RAW value system
        finalLifetime = lifetimeSeconds + modifiers.lifetimeIncrease + enhancedLifetimeAdd; // RAW seconds + enhanced

        // CRITICAL: Use ProjectileCards spawnInterval if available, otherwise use script cooldown
        float baseCooldown;
        if (card != null && card.runtimeSpawnInterval > 0f)
        {
            baseCooldown = card.runtimeSpawnInterval;
            Debug.Log($"<color=gold>FireMine using ProjectileCards spawnInterval: {baseCooldown:F2}s (overriding script cooldown: {cooldown:F2}s)</color>");
        }
        else
        {
            baseCooldown = cooldown;
        }

        // If we are in an enhanced variant and a per-variant base cooldown is configured,
        // override the baseCooldown with that value. This value will then be the basis
        // for BOTH card modifiers and the FireMine-specific enhancedCooldownReduction.
        if (enhancedVariant == 1 && variant1BaseCooldown > 0f)
        {
            baseCooldown = variant1BaseCooldown;
            Debug.Log($"<color=gold>FireMine Variant 1: Using variant1BaseCooldown = {variant1BaseCooldown:F2}s as base cooldown</color>");
        }
        else if (enhancedVariant == 2 && variant2BaseCooldown > 0f)
        {
            baseCooldown = variant2BaseCooldown;
            Debug.Log($"<color=gold>FireMine Variant 2: Using variant2BaseCooldown = {variant2BaseCooldown:F2}s as base cooldown</color>");
        }

        // Sync card runtime interval with the resolved BASE cooldown so that
        // ProjectileSpawner and any other systems use the same canonical interval.
        if (card != null)
        {
            card.runtimeSpawnInterval = Mathf.Max(0.1f, baseCooldown);
        }

        // Apply cooldown reduction: BOTH card modifiers AND enhanced, calculated from BASE
        float totalCooldownReduction = (modifiers.cooldownReductionPercent / 100f) + enhancedCooldownRed;
        finalCooldown = baseCooldown * (1f - totalCooldownReduction);
        if (MinCooldownManager.Instance != null)
        {
            finalCooldown = MinCooldownManager.Instance.ClampCooldown(card, finalCooldown);
        }
        else
        {
            finalCooldown = Mathf.Max(0.1f, finalCooldown);
        }
        Debug.Log($"<color=orange>FireMine Cooldown: Base={baseCooldown:F2}s, Card Reduction={modifiers.cooldownReductionPercent:F1}%, Enhanced Reduction={enhancedCooldownRed * 100f:F1}%, Total Reduction={totalCooldownReduction * 100f:F1}%, Final={finalCooldown:F2}s</color>");

        finalManaCost = Mathf.Max(1, Mathf.CeilToInt(manaCost * (1f - modifiers.manaCostReduction)));
        damage = (baseDamage + modifiers.damageFlat) * modifiers.damageMultiplier;

        // Reset explosion radius to base before applying size and modifiers
        explosionRadius = baseExplosionRadius;

        // Apply size multiplier (card + enhanced)
        float totalSizeMultiplier = modifiers.sizeMultiplier * enhancedSizeMult;

        Debug.Log($"<color=orange>═══════════════════════════════════════════════════════</color>");
        Debug.Log($"<color=orange>FireMine SCALING:</color>");
        Debug.Log($"<color=orange>  Card Size Multiplier: {modifiers.sizeMultiplier:F2}x</color>");
        Debug.Log($"<color=orange>  Enhanced Size Multiplier: {enhancedSizeMult:F2}x</color>");
        Debug.Log($"<color=orange>  Total Size Multiplier: {totalSizeMultiplier:F2}x</color>");

        // Store original values for logging
        float originalExplosionRadius = explosionRadius;
        Vector3 originalScale = transform.localScale;

        if (totalSizeMultiplier != 1f)
        {
            // Scale visual (transform)
            transform.localScale *= totalSizeMultiplier;

            // CRITICAL: Scale explosion radius by SQUARE of size multiplier
            float explosionRadiusMultiplier = totalSizeMultiplier;
            explosionRadius = baseExplosionRadius * explosionRadiusMultiplier;

            Debug.Log($"<color=orange>  Visual Scale: {originalScale} → {transform.localScale}</color>");
            Debug.Log($"<color=orange>  Explosion Radius (after size): {originalExplosionRadius:F2} → {explosionRadius:F2}</color>");
            Debug.Log($"<color=orange>  Visual Multiplier: x{totalSizeMultiplier:F2}</color>");
            Debug.Log($"<color=orange>  Explosion Multiplier: x{explosionRadiusMultiplier:F2} (squared for area)</color>");

            // Scale explosion offset Y with size (X stays the same)
            explosionRadiusOffset.y *= totalSizeMultiplier;

            // Also scale effect offsets Y
            explosionEffectOffsetLeft.y *= totalSizeMultiplier;
            explosionEffectOffsetRight.y *= totalSizeMultiplier;

            // Collider scaling: Since transform.localScale already scales the collider automatically,
            // we DON'T need to scale it again. The collider will match the visual size.
            // If colliderSizeOffset is set, apply it as a simple multiplier
            if (colliderSizeOffset != 0f)
            {
                float colliderScale = 1f + colliderSizeOffset;

                if (_collider2D is CircleCollider2D circle)
                {
                    circle.radius *= colliderScale;
                }
                else if (_collider2D is BoxCollider2D box)
                {
                    box.size *= colliderScale;
                }
                else if (_collider2D is CapsuleCollider2D capsule)
                {
                    capsule.size *= colliderScale;
                }

                Debug.Log($"<color=orange>FireMine Collider Offset Applied: x{colliderScale:F2}</color>");
            }
        }

        // After resolving the visual scale and explosion offsets, bind the
        // vertical visual scale to the explosion radius offset Y so that
        // they stay in sync (scale.y = explosionRadiusOffset.y).
        Vector3 mineScale = transform.localScale;
        mineScale.y = explosionRadiusOffset.y;
        transform.localScale = mineScale;

        // CRITICAL: Store explosion radius BEFORE modifiers for effect scaling
        explosionRadiusForEffect = explosionRadius;

        // CRITICAL: Apply explosion radius modifiers from ProjectileCardModifiers
        float explosionRadiusAfterSize = explosionRadius;
        explosionRadius = (explosionRadius + modifiers.explosionRadiusBonus) * modifiers.explosionRadiusMultiplier;

        Debug.Log($"<color=orange>  Explosion Radius Modifiers:</color>");
        Debug.Log($"<color=orange>    After Size: {explosionRadiusAfterSize:F2}</color>");
        Debug.Log($"<color=orange>    Bonus: +{modifiers.explosionRadiusBonus:F2}</color>");
        Debug.Log($"<color=orange>    Multiplier: x{modifiers.explosionRadiusMultiplier:F2}</color>");
        Debug.Log($"<color=orange>    Final: {explosionRadius:F2}</color>");

        Debug.Log($"<color=orange>FireMine Modifiers Applied: Size={modifiers.sizeMultiplier:F2}x, Damage={modifiers.damageMultiplier:F2}x, Lifetime=+{modifiers.lifetimeIncrease:F2}s, ExplosionRadius={explosionRadius:F2}</color>");

        // Still get PlayerStats for base damage calculation
        cachedPlayerStats = FindObjectOfType<PlayerStats>();
        baseDamageAfterCards = (baseDamage + modifiers.damageFlat) * modifiers.damageMultiplier;

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

        // Generate key based ONLY on projectile type (so all FireMines share same cooldown)
        prefabKey = GetType().Name;

        // Only check cooldown/mana for first projectile in multi-spawn
        if (!skipCooldownCheck)
        {
            // Check cooldown
            if (!bypassEnhancedFirstSpawnCooldown && lastFireTimes.ContainsKey(prefabKey))
            {
                if (GameStateManager.PauseSafeTime - lastFireTimes[prefabKey] < effectiveCooldown)
                {
                    Debug.Log($"<color=yellow>FireMine on cooldown - {GameStateManager.PauseSafeTime - lastFireTimes[prefabKey]:F2}s / {effectiveCooldown}s</color>");
                    Destroy(gameObject);
                    return;
                }
            }

            // Record fire time
            lastFireTimes[prefabKey] = GameStateManager.PauseSafeTime;
        }
        else
        {
            Debug.Log($"<color=gold>FireMine: Skipping cooldown/mana check (multi-projectile spawn)</color>");
        }

        // Ignore collision with player
        if (_collider2D != null && playerCollider != null)
        {
            Physics2D.IgnoreCollision(_collider2D, playerCollider, true);
        }

        scheduledDespawnTime = GameStateManager.PauseSafeTime + finalLifetime;
        RegisterAndEnforceSpawnLimit();

        // Start arming sequence
        StartCoroutine(ArmingSequence(finalLifetime));

        // Start visual fade-in (if configured)
        if (fadeInDuration > 0f)
        {
            StartCoroutine(FadeInRoutine());
        }

        if (FadeAwayDuration > 0f)
        {
            StartCoroutine(FadeAwayRoutine());
        }
    }

    private IEnumerator FadeInRoutine()
    {
        if (spriteRenderer == null || fadeInDuration <= 0f)
        {
            yield break;
        }

        Color color = spriteRenderer.color;
        float startAlpha = color.a;
        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += GameStateManager.GetPauseSafeDeltaTime();
            float t = Mathf.Clamp01(elapsed / fadeInDuration);
            color.a = Mathf.Lerp(startAlpha, 1f, t);
            spriteRenderer.color = color;
            yield return null;
        }

        color.a = 1f;
        spriteRenderer.color = color;
    }

    private IEnumerator FadeAwayRoutine()
    {
        if (spriteRenderer == null || FadeAwayDuration <= 0f)
        {
            yield break;
        }

        while (!hasExploded)
        {
            if (bossPauseActive)
            {
                yield return null;
                continue;
            }

            float remaining = GetRemainingLifetimeSeconds();
            if (remaining <= FadeAwayDuration)
            {
                break;
            }

            yield return null;
        }

        if (hasExploded)
        {
            yield break;
        }

        Color color = spriteRenderer.color;
        float startAlpha = color.a;

        while (!hasExploded)
        {
            if (bossPauseActive)
            {
                yield return null;
                continue;
            }

            float remaining = GetRemainingLifetimeSeconds();
            float t = 1f;
            if (FadeAwayDuration > 0f)
            {
                t = 1f - Mathf.Clamp01(remaining / FadeAwayDuration);
            }

            color.a = Mathf.Lerp(startAlpha, 0f, t);
            spriteRenderer.color = color;

            if (remaining <= 0f)
            {
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator WaitPausableSeconds(float seconds)
    {
        if (seconds <= 0f)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (bossPauseActive)
            {
                yield return null;
                continue;
            }

            elapsed += GameStateManager.GetPauseSafeDeltaTime();
            yield return null;
        }
    }

    private IEnumerator ArmingSequence(float lifetime)
    {
        // Wait for arming delay
        yield return StartCoroutine(WaitPausableSeconds(armDelay));

        isArmed = true;

        TryTriggerExplosionFromCurrentOverlaps();

        // Play arming effect
        if (armingEffectPrefab != null)
        {
            GameObject armEffect = Instantiate(armingEffectPrefab, transform.position, Quaternion.identity, transform);
            PauseSafeSelfDestruct.Schedule(armEffect, lifetime);
        }

        // Play arming sound
        if (armingClip != null)
        {
            AudioSource.PlayClipAtPoint(armingClip, transform.position, armingVolume);
        }

        // Mine is now armed; detonation is driven purely by trigger overlap
        // events (OnTriggerEnter2D/OnTriggerStay2D), not by an automatic radius
        // check at the moment of arming.

        // Wait for lifetime, then destroy without exploding
        yield return StartCoroutine(WaitPausableSeconds(lifetime - armDelay));

        if (!hasExploded)
        {
            Debug.Log("<color=yellow>FireMine lifetime expired - destroying without explosion</color>");
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (bossPauseActive) return;
        if (!isArmed || hasExploded || isExploding) return;

        Transform t = other != null ? other.transform : null;
        while (t != null)
        {
            if (t.name == "ClickHitbox")
            {
                return;
            }
            t = t.parent;
        }

        // Explode when enemy enters trigger
        if (IsEnemyCollider(other))
        {
            TryStartExplodeWithDelay();
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (bossPauseActive) return;
        if (!isArmed || hasExploded || isExploding) return;

        Transform t = other != null ? other.transform : null;
        while (t != null)
        {
            if (t.name == "ClickHitbox")
            {
                return;
            }
            t = t.parent;
        }

        if (IsEnemyCollider(other))
        {
            TryStartExplodeWithDelay();
        }
    }

    private bool IsEnemyCollider(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (((1 << other.gameObject.layer) & enemyLayer) != 0)
        {
            return true;
        }

        if (other.CompareTag("Enemy"))
        {
            return true;
        }

        EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>() ?? other.GetComponentInParent<EnemyHealth>();
        return enemyHealth != null && enemyHealth.IsAlive;
    }

    private void TryTriggerExplosionFromCurrentOverlaps()
    {
        if (bossPauseActive)
        {
            return;
        }

        if (!isArmed || hasExploded || isExploding)
        {
            return;
        }

        if (_collider2D == null)
        {
            return;
        }

        float overlapRadius = Mathf.Max(_collider2D.bounds.extents.x, _collider2D.bounds.extents.y);
        if (overlapRadius <= 0f)
        {
            overlapRadius = 0.1f;
        }

        Collider2D[] overlaps = Physics2D.OverlapCircleAll(transform.position, overlapRadius);
        if (overlaps == null || overlaps.Length == 0)
        {
            return;
        }

        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider2D other = overlaps[i];
            if (other == null || other == _collider2D)
            {
                continue;
            }

            if (other.transform == transform || other.transform.IsChildOf(transform))
            {
                continue;
            }

            if (IsEnemyCollider(other))
            {
                TryStartExplodeWithDelay();
                return;
            }
        }
    }

    private void TryStartExplodeWithDelay()
    {
        if (bossPauseActive)
        {
            return;
        }

        if (hasExploded || isExploding)
        {
            return;
        }

        if (explodeDelayCoroutine != null)
        {
            return;
        }

        explodeDelayCoroutine = StartCoroutine(ExplodeWithDelay());
    }

    private IEnumerator ExplodeWithDelay()
    {
        if (hasExploded || isExploding)
        {
            yield break;
        }

        if (bossPauseActive)
        {
            explodeDelayCoroutine = null;
            yield break;
        }

        isExploding = true;

        // Wait for explosion delay
        if (explosionDelay > 0f)
        {
            yield return StartCoroutine(WaitPausableSeconds(explosionDelay));
        }

        float effectDelay = 0f;
        if (explosionEffectTimingAdjustment < 0f)
        {
            effectDelay = Mathf.Abs(explosionEffectTimingAdjustment);
        }

        if (effectDelay > 0f)
        {
            yield return StartCoroutine(WaitPausableSeconds(effectDelay));
        }

        if (!hasExploded)
        {
            if (!HasAnyValidDamageTargetInExplosionRadius())
            {
                isExploding = false;
                explodeDelayCoroutine = null;

                if (GetRemainingLifetimeSeconds() <= 0f)
                {
                    Destroy(gameObject);
                }

                yield break;
            }
        }

        if (!hasExploded)
        {
            yield return StartCoroutine(ExplodeRoutine());
        }

        explodeDelayCoroutine = null;
    }

    private bool HasAnyValidDamageTargetInExplosionRadius()
    {
        Vector2 explosionCenter = (Vector2)transform.position + explosionRadiusOffset;
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(explosionCenter, explosionRadius, enemyLayer);
        if (hitColliders == null || hitColliders.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < hitColliders.Length; i++)
        {
            Collider2D hitCollider = hitColliders[i];
            if (hitCollider == null)
            {
                continue;
            }

            IDamageable damageable = hitCollider.GetComponent<IDamageable>() ?? hitCollider.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive)
            {
                continue;
            }

            if (!OffscreenDamageChecker.CanTakeDamage(hitCollider.transform.position))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private IEnumerator ExplodeRoutine()
    {
        if (hasExploded)
        {
            yield break;
        }
        hasExploded = true;

        // Calculate explosion center with offset
        Vector2 explosionCenter = (Vector2)transform.position + explosionRadiusOffset;

        Debug.Log($"<color=red>═══════════════════════════════════════════════════════</color>");
        Debug.Log($"<color=red>FireMine EXPLOSION:</color>");
        Debug.Log($"<color=red>  Position: {transform.position}</color>");
        Debug.Log($"<color=red>  Explosion Center: {explosionCenter}</color>");
        Debug.Log($"<color=red>  Explosion Radius: {explosionRadius:F2}</color>");
        Debug.Log($"<color=red>  Visual Scale: {transform.localScale}</color>");

        // Spawn explosion effect with offset, size, and timing
        if (explosionEffectPrefab != null)
        {
            // Determine which side of screen we're on (left or right of center)
            Camera mainCam = Camera.main;
            bool isOnLeftSide = mainCam != null && transform.position.x < mainCam.transform.position.x;

            // Apply offset based on side
            Vector2 effectOffset = isOnLeftSide ? explosionEffectOffsetLeft : explosionEffectOffsetRight;

            // Recompute the vertical offset based on the EFFECT'S effective visual
            // size so that:
            //  - explosionEffectSizeMultiplier = 2  => Y offset ≈ -1
            //  - explosionEffectSizeMultiplier = 4  => Y offset ≈ -2
            // and any additional size changes coming from ExplosionEffectScaling
            // (radius-driven) are also respected.
            float sizeRatio = 1f;
            if (baseScale.x != 0f)
            {
                sizeRatio = transform.localScale.x / baseScale.x;
            }

            // Base visual multiplier from prefab scale and inspector multiplier.
            float baseVisualMultiplier = sizeRatio * explosionEffectSizeMultiplier;

            // Radius-driven scaling (same logic used by SpawnExplosionEffectImmediate)
            float radiusRatio = 1f;
            if (baseExplosionRadius > 0f)
            {
                radiusRatio = explosionRadius / baseExplosionRadius;
            }

            float radiusScaleFactor = 1f + (radiusRatio - 1f) * ExplosionEffectScaling;
            radiusScaleFactor = Mathf.Max(0.1f, radiusScaleFactor);

            float effectiveSize = baseVisualMultiplier * radiusScaleFactor;

            // Map effective size to vertical offset. Using -0.5x gives the
            // requested examples: size=2 → -1, size=4 → -2.
            effectOffset.y = -0.5f * effectiveSize;

            Vector3 explosionPosition = transform.position + (Vector3)effectOffset;

            // Handle timing adjustment
            if (explosionEffectTimingAdjustment > 0f)
            {
                // Play effect early (not applicable for FireMine since it explodes instantly)
                // But we'll support it anyway
                SpawnExplosionEffectImmediate(explosionPosition);
            }
            else
            {
                // Normal timing
                SpawnExplosionEffectImmediate(explosionPosition);
            }
        }

        // Play explosion sound
        if (explosionClip != null)
        {
            AudioSource.PlayClipAtPoint(explosionClip, transform.position, explosionVolume);
        }

        float delayBeforeDamage = Mathf.Max(0f, ExplosionDamageDelay);
        if (delayBeforeDamage > 0f)
        {
            yield return StartCoroutine(WaitPausableSeconds(delayBeforeDamage));
        }

        // Find all enemies in explosion radius
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(explosionCenter, explosionRadius, enemyLayer);

        Debug.Log($"<color=red>  Enemies Hit: {hitColliders.Length}</color>");

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
                Vector3 hitNormal = ((Vector2)explosionCenter - (Vector2)hitPoint).normalized;

                float baseDamageForEnemy = baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
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

                StatusController.TryApplyBurnFromProjectile(gameObject, hitCollider.gameObject, hitPoint, finalDamage);

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

        // Destroy mine
        Destroy(gameObject);
    }

    private void SpawnExplosionEffectImmediate(Vector3 position)
    {
        GameObject explosion = Instantiate(explosionEffectPrefab, position, Quaternion.identity);

        float sizeRatio = 1f;
        if (baseScale.x != 0f)
        {
            sizeRatio = transform.localScale.x / baseScale.x;
        }

        // Base visual multiplier from prefab scale and inspector multiplier.
        float baseVisualMultiplier = sizeRatio * explosionEffectSizeMultiplier;

        // Radius-driven scaling (same logic used by SpawnExplosionEffectImmediate)
        float radiusRatio = 1f;
        if (baseExplosionRadius > 0f)
        {
            radiusRatio = explosionRadius / baseExplosionRadius;
        }

        float radiusScaleFactor = 1f + (radiusRatio - 1f) * ExplosionEffectScaling;
        radiusScaleFactor = Mathf.Max(0.1f, radiusScaleFactor);

        float finalMultiplier = baseVisualMultiplier * radiusScaleFactor;
        explosion.transform.localScale *= finalMultiplier;

        PauseSafeSelfDestruct.Schedule(explosion, explosionEffectDuration);
    }

    private IEnumerator SpawnDelayedExplosionEffect(Vector3 position, float delay)
    {
        yield return StartCoroutine(WaitPausableSeconds(delay));

        if (explosionEffectPrefab != null)
        {
            SpawnExplosionEffectImmediate(position);
            Debug.Log($"<color=cyan>Explosion effect played {delay}s late</color>");
        }
    }

    private bool IsOverlappingWithOtherMines(Vector3 testPosition)
    {
        FireMine[] allMines = FindObjectsOfType<FireMine>();

        float checkRadius = 1f; // Default radius
        if (_collider2D != null)
        {
            if (_collider2D is CircleCollider2D circle)
            {
                checkRadius = circle.radius * transform.localScale.x;
            }
            else if (_collider2D is BoxCollider2D box)
            {
                checkRadius = Mathf.Max(box.size.x, box.size.y) * 0.5f * transform.localScale.x;
            }
            else if (_collider2D is CapsuleCollider2D capsule)
            {
                checkRadius = Mathf.Max(capsule.size.x, capsule.size.y) * 0.5f * transform.localScale.x;
            }
        }

        foreach (FireMine otherMine in allMines)
        {
            if (otherMine == this) continue; // Skip self

            float distance = Vector3.Distance(testPosition, otherMine.transform.position);
            float minDistance = checkRadius * minDistanceBetweenMines; // Configurable minimum distance

            if (distance < minDistance)
            {
                return true; // Overlapping!
            }
        }

        return false; // No overlap
    }

    private bool IsPointInsidePolygon(Vector2 point, System.Collections.Generic.List<Vector2> polygon)
    {
        int count = polygon.Count;
        if (count < 3)
        {
            return false;
        }

        bool inside = false;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Vector2 pi = polygon[i];
            Vector2 pj = polygon[j];

            bool intersect = ((pi.y > point.y) != (pj.y > point.y)) &&
                             (point.x < (pj.x - pi.x) * (point.y - pi.y) / (((pj.y - pi.y) == 0f) ? 0.0001f : (pj.y - pi.y)) + pi.x);

            if (intersect)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private void OnDrawGizmosSelected()
    {
        // Draw explosion radius in editor with offset
        Vector3 explosionCenter = transform.position + (Vector3)explosionRadiusOffset;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawSphere(explosionCenter, explosionRadius);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        Gizmos.DrawWireSphere(explosionCenter, explosionRadius);

        // Draw spawn area points A, B, C, D
        GameObject pointAObj = GameObject.FindGameObjectWithTag(pointATag);
        GameObject pointBObj = GameObject.FindGameObjectWithTag(pointBTag);
        GameObject pointCObj = GameObject.FindGameObjectWithTag(pointCTag);
        GameObject pointDObj = GameObject.FindGameObjectWithTag(pointDTag);
        GameObject pointEObj = null;
        GameObject pointFObj = null;

        if (!string.IsNullOrEmpty(pointETag))
        {
            pointEObj = GameObject.FindGameObjectWithTag(pointETag);
        }

        if (!string.IsNullOrEmpty(pointFTag))
        {
            pointFObj = GameObject.FindGameObjectWithTag(pointFTag);
        }

        System.Collections.Generic.List<Vector3> spawnPoints = new System.Collections.Generic.List<Vector3>(6);
        Vector3 posA = Vector3.zero;
        Vector3 posB = Vector3.zero;
        Vector3 posC = Vector3.zero;
        Vector3 posD = Vector3.zero;

        if (pointAObj != null)
        {
            posA = pointAObj.transform.position;
            spawnPoints.Add(posA);
        }
        if (pointBObj != null)
        {
            posB = pointBObj.transform.position;
            spawnPoints.Add(posB);
        }
        if (pointCObj != null)
        {
            posC = pointCObj.transform.position;
            spawnPoints.Add(posC);
        }
        if (pointDObj != null)
        {
            posD = pointDObj.transform.position;
            spawnPoints.Add(posD);
        }
        if (pointEObj != null)
        {
            spawnPoints.Add(pointEObj.transform.position);
        }
        if (pointFObj != null)
        {
            spawnPoints.Add(pointFObj.transform.position);
        }

        if (spawnPoints.Count >= 3)
        {
            // Draw points
            Gizmos.color = Color.green;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                Gizmos.DrawSphere(spawnPoints[i], 0.3f);
            }

            // Draw lines connecting the points
            Gizmos.color = Color.cyan;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                Vector3 from = spawnPoints[i];
                Vector3 to = spawnPoints[(i + 1) % spawnPoints.Count];
                Gizmos.DrawLine(from, to);
            }

            // Draw diagonal lines to show the area
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            if (spawnPoints.Count >= 4)
            {
                Gizmos.DrawLine(spawnPoints[0], spawnPoints[2 % spawnPoints.Count]);
                Gizmos.DrawLine(spawnPoints[1 % spawnPoints.Count], spawnPoints[3 % spawnPoints.Count]);
            }

            // Draw labels using UnityEditor (only in editor)
#if UNITY_EDITOR
            if (pointAObj != null)
            {
                UnityEditor.Handles.Label(posA + Vector3.up * 0.5f, "A (Top-Left)");
            }
            if (pointBObj != null)
            {
                UnityEditor.Handles.Label(posB + Vector3.up * 0.5f, "B (Top-Right)");
            }
            if (pointCObj != null)
            {
                UnityEditor.Handles.Label(posC + Vector3.down * 0.5f, "C (Bottom-Left)");
            }
            if (pointDObj != null)
            {
                UnityEditor.Handles.Label(posD + Vector3.down * 0.5f, "D (Bottom-Right)");
            }
            if (pointEObj != null)
            {
                UnityEditor.Handles.Label(pointEObj.transform.position, "E");
            }
            if (pointFObj != null)
            {
                UnityEditor.Handles.Label(pointFObj.transform.position, "F");
            }
#endif
        }
    }

    public void ApplyInstantModifiers(CardModifierStats modifiers)
    {
        Debug.Log($"<color=lime>╔═══ FIREMINE INSTANT MODIFIERS ═══╗</color>");

        float newLifetime = baseLifetimeSeconds + modifiers.lifetimeIncrease;
        if (newLifetime != lifetimeSeconds)
        {
            lifetimeSeconds = newLifetime;
            Debug.Log($"<color=lime>  Lifetime: {baseLifetimeSeconds:F2} + {modifiers.lifetimeIncrease:F2} = {lifetimeSeconds:F2}</color>");
        }

        float newRadius = (baseExplosionRadius + modifiers.explosionRadiusBonus) * modifiers.explosionRadiusMultiplier;
        if (newRadius != explosionRadius)
        {
            explosionRadius = newRadius;
            Debug.Log($"<color=lime>  Explosion Radius: ({baseExplosionRadius:F2} + {modifiers.explosionRadiusBonus:F2}) * {modifiers.explosionRadiusMultiplier:F2}x = {explosionRadius:F2}</color>");
        }

        float newDamage = (baseDamage + modifiers.damageFlat) * modifiers.damageMultiplier;
        if (newDamage != damage)
        {
            damage = newDamage;
            baseDamageAfterCards = newDamage;
            Debug.Log($"<color=lime>  Damage: ({baseDamage:F2} + {modifiers.damageFlat:F2}) * {modifiers.damageMultiplier:F2}x = {damage:F2}</color>");
        }

        if (modifiers.sizeMultiplier != 1f)
        {
            transform.localScale = baseScale * modifiers.sizeMultiplier;

            Debug.Log($"<color=lime>  Size: {baseScale} * {modifiers.sizeMultiplier:F2}x = {transform.localScale}</color>");
        }

        Debug.Log($"<color=lime>╚═══════════════════════════════════╝</color>");
    }
}