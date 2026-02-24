using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DwarfStar - Ice orbital projectile that moves counterclockwise (right to left) around the player
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class DwarfStar : MonoBehaviour, IInstantModifiable
{
    [Header("Star Type")]
    [Tooltip("Always DwarfStar (ice, counterclockwise)")]
    public ProjectileType starType = ProjectileType.DwarfStar;

    [Header("Orbital Settings")]
    [Tooltip("Current orbital level (1-6)")]
    public int currentLevel = 1;

    [Tooltip("Loop levels 1→2→3→4→5→6→1... instead of staying at level 6")]
    public bool loopLevels = false;

    [Tooltip("Radius for Level 1 orbit (smallest)")]
    public float level1Radius = 3f;

    [Tooltip("Radius for Level 2 orbit")]
    public float level2Radius = 6f;

    [Tooltip("Radius for Level 3 orbit (middle)")]
    public float level3Radius = 9f;

    [Tooltip("Radius for Level 4 orbit")]
    public float level4Radius = 12f;

    [Tooltip("Radius for Level 5 orbit")]
    public float level5Radius = 15f;

    [Tooltip("Radius for Level 6 orbit (largest)")]
    public float level6Radius = 18f;

    [Tooltip("Gap between each star in the same orbit")]
    public float starSpacing = 2f;

    [Tooltip("Base movement speed along the orbit path (Level 1)")]
    public float baseOrbitSpeed = 3f;

    [Tooltip("RUNTIME ONLY: actual orbit speed used by this star instance after synchronization/adaptive calculations.")]
    public float SyncSpeed;

    [Tooltip("Speed multiplier per level (Level 2 = base * multiplier, Level 3 = base * multiplier^2)")]
    public float speedScalingPerLevel = 1.2f;

    [Header("Speed Systems")]
    [Tooltip("Enable adaptive speed - all levels complete orbit in same time (uses target duration)")]
    public bool useAdaptiveSpeed = false;

    [Tooltip("Use synchronized speed calculation for enhanced mode (all levels complete at same time, based on speed ratio)")]
    public bool useSynchronizedSpeed = false;

    [Tooltip("Additional delay added to orbit completion time (for staggered enhanced spawning)")]
    [HideInInspector]
    public float additionalOrbitDelay = 0f;

    [Tooltip("Target orbit completion time in seconds (when adaptive speed is enabled)")]
    public float targetOrbitDuration = 10f;

    [Tooltip("Tolerance multiplier for on-screen detection (1.0 = exact camera bounds, 1.5 = 50% larger area)")]
    [Range(0.5f, 3f)]
    public float onScreenTolerance = 1.2f;

    [Header("Orbit Tilt")]
    [Tooltip("Positive = right side lower, left side higher. Negative = opposite.")]
    public float orbitTiltDegrees = 0f;

    [Header("Off-Screen Destruction (Level 1 Only)")]
    [Tooltip("Extra margin beyond camera bounds before Level 1 stars are destroyed (in world units)")]
    public float level1OffScreenMargin = 2f;

    // Calculated speed based on level
    private float currentSpeed;
    private bool isOnCamera = false;

    // Orbit extension below camera (set by manager per level)
    private float orbitBelowCameraExtension = 30f;

    [Tooltip("Downtime before respawning at next level (in seconds)")]
    public float levelDowntime = 5f;

    [Header("Damage Radius")]
    [Tooltip("Constant damage radius throughout orbit (affected by size modifiers)")]
    public float damageRadius = 2f;

    [Tooltip("Radius offset X")]
    public float radiusOffsetX = 0f;

    [Tooltip("Radius offset Y")]
    public float radiusOffsetY = 0f;

    // Size multiplier from projectile modifiers
    private float sizeMultiplier = 1f;

    [Header("Damage Settings")]
    [Tooltip("Base damage per instance (Level 1)")]
    public float baseDamage = 10f;

    [Tooltip("Damage multiplier per level (Level 2 = base * multiplier, Level 3 = base * multiplier^2)")]
    public float damageScalingPerLevel = 1.5f;

    [Tooltip("Damage instances per second")]
    public float damageInstancesPerSecond = 2f;

    // Calculated damage based on level
    private float currentDamage;

    [Tooltip("Layer mask for enemies")]
    [SerializeField] private LayerMask enemyLayer;

    [Header("Visual Effects")]
    [Tooltip("Fade in duration when spawning from off-camera")]
    public float fadeInDuration = 1f;

    [Tooltip("Trail renderer for movement trail")]
    public TrailRenderer trailRenderer;

    [Tooltip("Particle system for ambient particles")]
    public ParticleSystem ambientParticles;

    [Tooltip("Particle system for center burst effect")]
    public ParticleSystem centerBurstParticles;

    [Tooltip("Enable pulsing glow effect based on radius")]
    public bool enablePulsingGlow = true;

    [Tooltip("Glow intensity multiplier")]
    public float glowIntensity = 1.5f;

    [Header("Damage Radius Visualization")]
    [Tooltip("Name of the child GameObject that shows damage radius (e.g., 'RadiusIndicator')")]
    public string radiusIndicatorName = "RadiusIndicator";

    [Tooltip("Gizmo color for damage radius (scene view)")]
    public Color damageRadiusGizmoColor = Color.cyan;

    // Runtime reference to radius indicator child
    private Transform radiusIndicator;
    private SpriteRenderer radiusIndicatorSprite;
    private float radiusIndicatorFadeInY = -5f;
    private float radiusIndicatorBaseAlpha = 1f;

    // Runtime variables
    private Transform playerTransform;
    private Rigidbody2D rb;
    private Collider2D damageCollider;
    private SpriteRenderer spriteRenderer;
    private CircleCollider2D radiusCollider;

    private float currentAngle; // Current angle in the orbit (0-180 for semicircle)
    private float currentRadius; // Current damage radius
    private float damageInterval;
    private float nextDamageTime;
    private bool isPaused = false;
    private bool isMoving = true;

    // DwarfStar is always counterclockwise (right to left)
    private bool isClockwise => false;

    // Projectile modifiers support
    private List<ProjectileModifierData> activeModifiers = new List<ProjectileModifierData>();

    public bool useSharedModifiers = true;

    // Base values for instant modifier recalculation
    private float baseDamageRadius;
    private float baseSpeed;
    private Vector3 baseScale;
    private float baseBaseDamage; // Base of baseDamage (for damage multiplier)

    private int enhancedVariantIndex = 0;
    private float variantScaleMultiplier = 1f;
    private float Variant3ScaleMultiplier = 1.25f;
    private float Variant3ExtraSlowChancePercent = 40f;
    private bool variant3BonusApplied = false;

    private float GetVisualDeltaTime()
    {
        if (CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionActive())
        {
            return 0f;
        }

        return GameStateManager.GetPauseSafeDeltaTime();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        damageCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Setup rigidbody
        rb.gravityScale = 0f;
        rb.isKinematic = true;

        // Setup collider as trigger
        damageCollider.isTrigger = true;

        // Create radius collider for damage detection
        radiusCollider = gameObject.AddComponent<CircleCollider2D>();
        radiusCollider.isTrigger = true;
        radiusCollider.radius = damageRadius;
        radiusCollider.offset = new Vector2(radiusOffsetX, radiusOffsetY);
        baseSpeed = baseOrbitSpeed;
    }

    private float CalculateFinalDamageWithPlayerStats(float baseDamage, PlayerStats stats)
    {
        if (stats == null)
        {
            return baseDamage;
        }

        // Use the standard PlayerStats damage calculation so flatDamage is
        // applied 1:1 to each hit, just like other projectiles.
        float damage = stats.CalculateDamage(baseDamage, true);

        return damage;
    }

    public void Initialize(Transform player, int level, float angleOffset, float orbitExtension = 30f)
    {
        playerTransform = player;
        currentLevel = Mathf.Clamp(level, 1, 6);
        orbitBelowCameraExtension = orbitExtension; // Set from manager

        float baseExtension = orbitBelowCameraExtension;
        float halfTilt = orbitTiltDegrees * 0.5f;
        float rightExtension = Mathf.Max(0f, baseExtension + halfTilt);
        float level1ExtraExtension = (currentLevel == 1) ? 30f : 0f;
        float startAngle = 0f - rightExtension - level1ExtraExtension;
        currentAngle = startAngle - angleOffset;

        // Calculate level-based stats
        if (useSynchronizedSpeed)
        {
            float level1ExtraExt = 30f;
            float level1ArcAngle = (180f + 2f * (orbitBelowCameraExtension + level1ExtraExt)) * Mathf.Deg2Rad;
            float level1ArcLength = level1Radius * level1ArcAngle;

            float thisLevelExtraExt = (currentLevel == 1) ? level1ExtraExt : 0f;
            float thisArcAngleDeg = 180f + 2f * (orbitBelowCameraExtension + thisLevelExtraExt);
            float thisArcAngleRad = thisArcAngleDeg * Mathf.Deg2Rad;
            float thisArcLength = GetCurrentLevelRadius() * thisArcAngleRad;

            float speedRatio = thisArcLength / level1ArcLength;
            currentSpeed = baseOrbitSpeed * speedRatio;
            SyncSpeed = currentSpeed;

            Debug.Log($"<color=cyan>DwarfStar Synchronized Speed: Level {currentLevel}, Radius={GetCurrentLevelRadius():F2}, Arc={thisArcAngleDeg}°, Speed={currentSpeed:F2}</color>");
        }
        else if (useAdaptiveSpeed)
        {
            float level1ExtraExt = (currentLevel == 1) ? 30f : 0f;
            float arcAngleDeg = 180f + 2f * (orbitBelowCameraExtension + level1ExtraExt);
            float arcAngleRad = arcAngleDeg * Mathf.Deg2Rad;
            float arcLength = GetCurrentLevelRadius() * arcAngleRad;
            currentSpeed = arcLength / targetOrbitDuration;
            SyncSpeed = currentSpeed;
            Debug.Log($"<color=cyan>DwarfStar Adaptive Speed: Level {currentLevel}, Speed={currentSpeed:F2}</color>");
        }
        else
        {
            currentSpeed = baseOrbitSpeed * Mathf.Pow(speedScalingPerLevel, currentLevel - 1);
            SyncSpeed = currentSpeed;
        }

        currentDamage = baseDamage * Mathf.Pow(damageScalingPerLevel, currentLevel - 1);

        damageInterval = 1f / damageInstancesPerSecond;
        nextDamageTime = GameStateManager.PauseSafeTime + damageInterval;

        baseDamageRadius = damageRadius;
        if (baseSpeed <= 0f)
        {
            baseSpeed = baseOrbitSpeed;
        }
        baseScale = transform.localScale;
        baseBaseDamage = baseDamage;

        enhancedVariantIndex = 0;
        variantScaleMultiplier = 1f;
        variant3BonusApplied = false;
        sizeMultiplier = 1f;

        ProjectileCards card = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
        if (card != null)
        {
            CardModifierStats modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);

            OrbitalStarManager manager1 = FindObjectOfType<OrbitalStarManager>();
            if (manager1 != null && useSharedModifiers)
            {
                modifiers = manager1.GetEffectiveStarModifiers(card);
            }

            sizeMultiplier = modifiers.sizeMultiplier;

            if (ProjectileCardLevelSystem.Instance != null)
            {
                enhancedVariantIndex = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card);
                OrbitalStarManager manager2 = FindObjectOfType<OrbitalStarManager>();
                if (manager2 != null)
                {
                    Variant3ScaleMultiplier = manager2.NewScale;
                    Variant3ExtraSlowChancePercent = manager2.BonusStatusChance;
                }

                bool hasVariant3Stack = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 3);
                if (hasVariant3Stack)
                {
                    variantScaleMultiplier = Variant3ScaleMultiplier;

                    SlowEffect slow = GetComponent<SlowEffect>();
                    if (slow == null)
                    {
                        slow = gameObject.AddComponent<SlowEffect>();
                    }
                    if (!variant3BonusApplied)
                    {
                        slow.slowChance = Mathf.Clamp(slow.slowChance + Variant3ExtraSlowChancePercent, 0f, 100f);
                        variant3BonusApplied = true;
                    }
                }
            }

            transform.localScale = baseScale * (sizeMultiplier * variantScaleMultiplier);
            radiusOffsetY = transform.localScale.y;
            baseDamageRadius = 2f * Mathf.Abs(baseScale.x * variantScaleMultiplier);

            // CRITICAL: Apply flat damage modifier immediately so the very first
            // DwarfStar ticks include mythic/rare damage bonuses.
            float newBaseDamage = baseBaseDamage + modifiers.damageFlat;
            if (Mathf.Abs(newBaseDamage - baseDamage) > 0.001f)
            {
                baseDamage = newBaseDamage;
                currentDamage = baseDamage * Mathf.Pow(damageScalingPerLevel, currentLevel - 1);
            }

            float damageRadiusBonus = modifiers.damageRadiusIncrease;
            currentRadius = baseDamageRadius * sizeMultiplier + damageRadiusBonus;

            Debug.Log($"<color=yellow>DwarfStar Initialize: Card={card.cardName}, BaseDamageRadius={baseDamageRadius:F2}, Modifier=+{damageRadiusBonus:F2}, Size={sizeMultiplier:F2}x, FinalRadius={currentRadius:F2}</color>");
        }
        else
        {
            baseDamageRadius = 2f * Mathf.Abs(baseScale.x);
            transform.localScale = baseScale;
            radiusOffsetY = transform.localScale.y;
            currentRadius = baseDamageRadius * sizeMultiplier;
            Debug.Log($"<color=red>DwarfStar Initialize: NO CARD FOUND! Using base radius={damageRadius:F2}</color>");
        }

        UpdateRadiusCollider();

        UpdatePosition();

        if (spriteRenderer != null)
        {
            StartCoroutine(FadeIn());
        }

        StartCoroutine(OrbitalMovement());

        Debug.Log($"<color=cyan>DwarfStar initialized: Level {currentLevel}, Start Angle: {currentAngle:F1}°, Speed: {currentSpeed:F1}, Damage: {currentDamage:F1}</color>");

        SetupVisualEffects();

        FindRadiusIndicator();

        OrbitalStarManager manager = FindObjectOfType<OrbitalStarManager>();
        if (manager != null)
        {
            radiusIndicatorFadeInY = manager.yAxisRadiusFadeIn;
        }
    }

    private IEnumerator FadeIn()
    {
        Color color = spriteRenderer.color;
        color.a = 0f;
        spriteRenderer.color = color;

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += GetVisualDeltaTime();
            color.a = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
            spriteRenderer.color = color;
            yield return null;
        }

        color.a = 1f;
        spriteRenderer.color = color;
    }

    private IEnumerator OrbitalMovement()
    {
        while (isMoving)
        {
            if (!isPaused)
            {
                UpdateOnCameraStatus();

                float effectiveSpeed = currentSpeed;

                float angleStep = (effectiveSpeed / GetCurrentLevelRadius()) * Mathf.Rad2Deg * GetVisualDeltaTime();

                currentAngle += angleStep;

                if (currentAngle >= 90f && currentAngle - angleStep < 90f)
                {
                    currentAngle = 90f;
                    TriggerCenterBurst();
                }

                float level1ExtraExtension = (currentLevel == 1) ? 30f : 0f;
                float baseExtension = orbitBelowCameraExtension;
                float halfTilt = orbitTiltDegrees * 0.5f;
                float leftExtension = Mathf.Max(0f, baseExtension - halfTilt);
                float endAngle = 180f + leftExtension + level1ExtraExtension;
                if (currentAngle >= endAngle)
                {
                    yield return StartCoroutine(CompleteOrbit());
                    yield break;
                }

                UpdatePosition();
            }

            yield return null;
        }
    }

    private IEnumerator CompleteOrbit()
    {
        Debug.Log($"<color=lime>DwarfStar completed Level {currentLevel} orbit</color>");

        if (currentLevel == 1)
        {
            Debug.Log($"<color=yellow>DwarfStar Level 1: Waiting to go off-screen before destruction</color>");

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                float cameraHeight = mainCam.orthographicSize * 2f;
                float cameraWidth = cameraHeight * mainCam.aspect;
                Vector3 camPos = mainCam.transform.position;
                float margin = level1OffScreenMargin;

                while (true)
                {
                    float distX = Mathf.Abs(transform.position.x - camPos.x);
                    float distY = Mathf.Abs(transform.position.y - camPos.y);

                    bool isOffScreen = distX > (cameraWidth / 2f + margin) || distY > (cameraHeight / 2f + margin);

                    if (isOffScreen)
                    {
                        Debug.Log($"<color=yellow>DwarfStar Level 1: Now off-screen, destroying</color>");

                        OrbitalStarManager manager = FindObjectOfType<OrbitalStarManager>();
                        if (manager != null)
                        {
                            manager.OnOrbitComplete(starType, currentLevel);
                        }

                        break;
                    }

                    float angleChange = currentSpeed * GetVisualDeltaTime();
                    currentAngle += angleChange;

                    float angleRad = currentAngle * Mathf.Deg2Rad;
                    float radius = GetCurrentLevelRadius();
                    Vector3 offset = new Vector3(Mathf.Cos(angleRad) * radius, Mathf.Sin(angleRad) * radius, 0f);
                    transform.position = playerTransform.position + offset;

                    yield return null;
                }
            }
        }

        if (additionalOrbitDelay > 0f)
        {
            Debug.Log($"<color=magenta>DwarfStar: Waiting additional {additionalOrbitDelay:F2}s before completion</color>");
            yield return GameStateManager.WaitForPauseSafeSeconds(additionalOrbitDelay);
        }

        if (currentLevel > 1)
        {
            OrbitalStarManager manager = FindObjectOfType<OrbitalStarManager>();
            if (manager != null)
            {
                manager.OnOrbitComplete(starType, currentLevel);
            }
        }

        Destroy(gameObject);

        yield return null;
    }

    private void UpdatePosition()
    {
        if (playerTransform == null) return;

        float radius = GetCurrentLevelRadius();
        float angleRad = currentAngle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Cos(angleRad) * radius,
            Mathf.Sin(angleRad) * radius,
            0f
        );

        transform.position = playerTransform.position + offset;

        UpdateRadiusIndicatorAlpha();
    }

    private void UpdateRadiusCollider()
    {
        if (radiusCollider != null)
        {
            Vector3 s = transform.localScale;
            float sx = Mathf.Max(0.0001f, Mathf.Abs(s.x));
            float sy = Mathf.Max(0.0001f, Mathf.Abs(s.y));

            radiusCollider.radius = currentRadius / sx;
            radiusCollider.offset = new Vector2(radiusOffsetX / sx, radiusOffsetY / sy);
        }
    }

    private void UpdateRadiusIndicatorAlpha()
    {
        if (radiusIndicatorSprite == null)
        {
            return;
        }

        float fadeRange = 5f;
        float y = transform.position.y;

        float t = Mathf.InverseLerp(radiusIndicatorFadeInY - fadeRange, radiusIndicatorFadeInY, y);
        float alphaFactor = Mathf.Clamp01(t);

        Color c = radiusIndicatorSprite.color;
        c.a = radiusIndicatorBaseAlpha * alphaFactor;
        radiusIndicatorSprite.color = c;
    }

    private float GetCurrentLevelRadius()
    {
        switch (currentLevel)
        {
            case 1: return level1Radius;
            case 2: return level2Radius;
            case 3: return level3Radius;
            case 4: return level4Radius;
            case 5: return level5Radius;
            case 6: return level6Radius;
            default: return level1Radius;
        }
    }

    private void Update()
    {
        if (GameStateManager.PauseSafeTime >= nextDamageTime)
        {
            DealDamageToEnemiesInRadius();
            nextDamageTime = GameStateManager.PauseSafeTime + damageInterval;
        }
    }

    /// <summary>
    /// DwarfStar damage is considered AOE so it bypasses enemy NULLIFY.
    /// Keep all future DwarfStar damage going through this wrapper.
    /// </summary>
    private void DealAoeDamage(IDamageable target, float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (target == null || !target.IsAlive || damage <= 0f) return;

        DamageAoeScope.BeginAoeDamage();
        target.TakeDamage(damage, hitPoint, hitNormal);
        DamageAoeScope.EndAoeDamage();
    }

    private void DealDamageToEnemiesInRadius()
    {
        Vector2 damageCenter = (Vector2)transform.position + new Vector2(radiusOffsetX, radiusOffsetY);

        Collider2D[] hits = Physics2D.OverlapCircleAll(damageCenter, currentRadius, enemyLayer);

        int damageCount = 0;
        foreach (Collider2D hit in hits)
        {
            IDamageable damageable = hit.GetComponent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
                float finalDamage = currentDamage;

                foreach (var modifier in activeModifiers)
                {
                    if (modifier != null)
                    {
                        finalDamage = modifier.ApplyDamageModifier(finalDamage);
                    }
                }

                if (playerTransform != null)
                {
                    PlayerStats stats = playerTransform.GetComponent<PlayerStats>();
                    if (stats != null)
                    {
                        Component damageableComponent = damageable as Component;
                        GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : hit.gameObject;

                        finalDamage = PlayerDamageHelper.ComputeContinuousProjectileDamage(stats, enemyObject, finalDamage, gameObject);
                    }
                }

                EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>() ?? hit.GetComponentInParent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Ice);
                }

                Vector3 enemyPos = hit.bounds.center;

                // IMPORTANT: use AOE wrapper so enemy NULLIFY does NOT negate it.
                DealAoeDamage(damageable, finalDamage, enemyPos, Vector3.zero);

                damageCount++;

                SlowEffect slowEffect = GetComponent<SlowEffect>();
                if (slowEffect != null)
                {
                    slowEffect.TryApplySlow(hit.gameObject, hit.transform.position);
                }

                StatusController.TryApplyBurnFromProjectile(gameObject, hit.gameObject, hit.transform.position, finalDamage);

                StaticEffect staticEffect = GetComponent<StaticEffect>();
                if (staticEffect != null)
                {
                    staticEffect.TryApplyStatic(hit.gameObject, hit.transform.position);
                }
            }
        }

        if (damageCount > 0)
        {
            Debug.Log($"<color=lime>DwarfStar dealt {currentDamage:F1} damage to {damageCount} enemies</color>");
        }
    }

    public void ApplyModifiers(List<ProjectileModifierData> modifiers)
    {
        if (modifiers != null)
        {
            activeModifiers.AddRange(modifiers);
            Debug.Log($"<color=yellow>DwarfStar applied {modifiers.Count} modifiers</color>");
        }
    }

    private void SetupVisualEffects()
    {
        if (trailRenderer != null)
        {
            trailRenderer.enabled = true;
            Color trailColor = new Color(0f, 0.7f, 1f, 0.8f);
            trailRenderer.startColor = trailColor;
            trailRenderer.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
            trailRenderer.time = 0.5f;
            trailRenderer.startWidth = 0.3f;
            trailRenderer.endWidth = 0.05f;
        }

        if (ambientParticles != null)
        {
            var main = ambientParticles.main;
            main.startColor = new Color(0.3f, 0.8f, 1f, 0.8f);
            main.startSize = 0.2f;
            main.startLifetime = 1f;
            ambientParticles.Play();
        }
    }

    private void TriggerCenterBurst()
    {
        if (centerBurstParticles != null)
        {
            centerBurstParticles.Play();
            Debug.Log($"<color=magenta>DwarfStar center burst effect triggered!</color>");
        }
    }

    private void FindRadiusIndicator()
    {
        if (string.IsNullOrEmpty(radiusIndicatorName)) return;

        radiusIndicator = transform.Find(radiusIndicatorName);

        if (radiusIndicator != null)
        {
            Debug.Log($"<color=cyan>DwarfStar: Found RadiusIndicator child '{radiusIndicatorName}'</color>");
            UpdateRadiusIndicatorScale();
            radiusIndicatorSprite = radiusIndicator.GetComponent<SpriteRenderer>();
            if (radiusIndicatorSprite != null)
            {
                radiusIndicatorBaseAlpha = radiusIndicatorSprite.color.a;
            }
            UpdateRadiusIndicatorAlpha();
        }
    }

    private void UpdateRadiusIndicatorScale()
    {
        if (radiusIndicator == null) return;

        float worldDiameter = currentRadius * 2f;
        float sx = Mathf.Max(0.0001f, Mathf.Abs(transform.localScale.x));
        float sy = Mathf.Max(0.0001f, Mathf.Abs(transform.localScale.y));
        radiusIndicator.localScale = new Vector3(worldDiameter / sx, worldDiameter / sy, 1f);
    }

    private void UpdateOnCameraStatus()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            isOnCamera = true;
            return;
        }

        float cameraHeight = mainCam.orthographicSize * 2f * onScreenTolerance;
        float cameraWidth = cameraHeight * mainCam.aspect;
        Vector3 camPos = mainCam.transform.position;

        float distX = Mathf.Abs(transform.position.x - camPos.x);
        float distY = Mathf.Abs(transform.position.y - camPos.y);

        isOnCamera = distX <= cameraWidth / 2f && distY <= cameraHeight / 2f;
    }

    public void ApplyInstantModifiers(CardModifierStats modifiers)
    {
        ProjectileCards card = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
        if (card != null && useSharedModifiers)
        {
            OrbitalStarManager manager = FindObjectOfType<OrbitalStarManager>();
            if (manager != null)
            {
                modifiers = manager.GetEffectiveStarModifiers(card);
            }
        }

        if (card != null && ProjectileCardLevelSystem.Instance != null)
        {
            int newVariantIndex = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card);
            if (newVariantIndex != enhancedVariantIndex)
            {
                enhancedVariantIndex = newVariantIndex;
                OrbitalStarManager manager2 = FindObjectOfType<OrbitalStarManager>();
                if (manager2 != null)
                {
                    Variant3ScaleMultiplier = manager2.NewScale;
                    Variant3ExtraSlowChancePercent = manager2.BonusStatusChance;
                }

                bool hasVariant3Stack = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 3);
                variantScaleMultiplier = hasVariant3Stack ? Variant3ScaleMultiplier : 1f;

                if (enhancedVariantIndex == 3 && !variant3BonusApplied)
                {
                    SlowEffect slow = GetComponent<SlowEffect>();
                    if (slow == null)
                    {
                        slow = gameObject.AddComponent<SlowEffect>();
                    }
                    slow.slowChance = Mathf.Clamp(slow.slowChance + Variant3ExtraSlowChancePercent, 0f, 100f);
                    variant3BonusApplied = true;
                }

                baseDamageRadius = 2f * Mathf.Abs(baseScale.x * variantScaleMultiplier);
                transform.localScale = baseScale * (sizeMultiplier * variantScaleMultiplier);
                radiusOffsetY = transform.localScale.y;
                UpdateRadiusCollider();
                UpdateRadiusIndicatorScale();
            }
        }

        Debug.Log($"<color=lime>╔═══ DWARFSTAR INSTANT MODIFIERS ═══╗</color>");

        float newRadius = baseDamageRadius * modifiers.sizeMultiplier + modifiers.damageRadiusIncrease;
        if (newRadius != currentRadius)
        {
            currentRadius = newRadius;
            UpdateRadiusCollider();
            UpdateRadiusIndicatorScale();
            Debug.Log($"<color=lime>  Damage Radius: {baseDamageRadius:F2} * {modifiers.sizeMultiplier:F2}x + {modifiers.damageRadiusIncrease:F2} = {currentRadius:F2}</color>");
        }

        float newBaseSpeed = baseSpeed + modifiers.speedIncrease;
        if (newBaseSpeed != baseOrbitSpeed)
        {
            baseOrbitSpeed = newBaseSpeed;

            if (useSynchronizedSpeed)
            {
                float level1ExtraExt = 30f;
                float level1ArcAngle = (180f + 2f * (orbitBelowCameraExtension + level1ExtraExt)) * Mathf.Deg2Rad;
                float level1ArcLength = level1Radius * level1ArcAngle;

                float thisLevelExtraExt = (currentLevel == 1) ? level1ExtraExt : 0f;
                float thisArcAngleDeg = 180f + 2f * (orbitBelowCameraExtension + thisLevelExtraExt);
                float thisArcAngleRad = thisArcAngleDeg * Mathf.Deg2Rad;
                float thisArcLength = GetCurrentLevelRadius() * thisArcAngleRad;

                float speedRatio = (level1ArcLength > 0f) ? (thisArcLength / level1ArcLength) : 1f;
                currentSpeed = baseOrbitSpeed * speedRatio;
            }
            else if (useAdaptiveSpeed)
            {
                float level1ExtraExt = (currentLevel == 1) ? 30f : 0f;
                float arcAngleDeg = 180f + 2f * (orbitBelowCameraExtension + level1ExtraExt);
                float arcAngleRad = arcAngleDeg * Mathf.Deg2Rad;
                float arcLength = GetCurrentLevelRadius() * arcAngleRad;

                if (targetOrbitDuration > 0f)
                {
                    currentSpeed = arcLength / targetOrbitDuration;
                }
            }
            else
            {
                currentSpeed = baseOrbitSpeed * Mathf.Pow(speedScalingPerLevel, currentLevel - 1);
            }

            SyncSpeed = currentSpeed;

            Debug.Log($"<color=lime>  Speed: {baseSpeed:F2} + {modifiers.speedIncrease:F2} = {baseOrbitSpeed:F2} (current: {currentSpeed:F2})</color>");
        }

        if (modifiers.sizeMultiplier != sizeMultiplier)
        {
            sizeMultiplier = modifiers.sizeMultiplier;
            transform.localScale = baseScale * (sizeMultiplier * variantScaleMultiplier);
            radiusOffsetY = transform.localScale.y;
            UpdateRadiusCollider();
            UpdateRadiusIndicatorScale();
            Debug.Log($"<color=lime>  Size: {baseScale} * {sizeMultiplier:F2}x = {transform.localScale}</color>");
        }

        float newBaseDamage = baseBaseDamage + modifiers.damageFlat;
        if (Mathf.Abs(newBaseDamage - baseDamage) > 0.001f)
        {
            baseDamage = newBaseDamage;
            currentDamage = baseDamage * Mathf.Pow(damageScalingPerLevel, currentLevel - 1);
            Debug.Log($"<color=lime>  Damage: {baseBaseDamage:F2} + {modifiers.damageFlat:F2} = {baseDamage:F2} (current: {currentDamage:F2})</color>");
        }

        Debug.Log($"<color=lime>╚═══════════════════════════════════╝</color>");
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 damageCenter = transform.position + new Vector3(radiusOffsetX, radiusOffsetY, 0f);

        Gizmos.color = damageRadiusGizmoColor;
        Gizmos.DrawWireSphere(damageCenter, currentRadius > 0f ? currentRadius : damageRadius);

        if (playerTransform != null)
        {
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
            float radius = GetCurrentLevelRadius();

            for (float angle = 0f; angle <= 180f; angle += 5f)
            {
                float angleRad = angle * Mathf.Deg2Rad;
                Vector3 pos = playerTransform.position + new Vector3(
                    Mathf.Cos(angleRad) * radius,
                    Mathf.Sin(angleRad) * radius,
                    0f
                );
                Gizmos.DrawSphere(pos, 0.1f);
            }
        }
    }
}