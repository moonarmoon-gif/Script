using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NovaStar - Fire orbital projectile that moves clockwise (left to right) around the player
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class NovaStar : MonoBehaviour, IInstantModifiable
{
    [Header("Star Type")]
    [Tooltip("Always NovaStar (fire, clockwise)")]
    public ProjectileType starType = ProjectileType.NovaStar;
    
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
    
    [Tooltip("Extra margin beyond camera bounds before Level 1 stars start fading in (in world units)")]
    public float level1FadeInMargin = 3f;
    
    // Calculated speed based on level
    private float currentSpeed;
    private bool isOnCamera = false;
    
    // Orbit extension below camera (set by manager per level)
    private float orbitBelowCameraExtension = 30f;
    
    [Tooltip("Duration to pause at center (in seconds)")]
    public float centerPauseDuration = 5f;
    
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
    
    // Status effects are now handled by BurnEffect component
    // Add BurnEffect component to the prefab in Inspector
    
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
    public Color damageRadiusGizmoColor = Color.red;
    
    // Runtime reference to radius indicator child
    private Transform radiusIndicator;
    
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
    
    // NovaStar is always clockwise (left to right)
    private bool isClockwise => true;
    
    // Projectile modifiers support
    private List<ProjectileModifierData> activeModifiers = new List<ProjectileModifierData>();
    
    public bool useSharedModifiers = true;
    
    // Base values for instant modifier recalculation
    private float baseDamageRadius;
    private float baseSpeed;
    private Vector3 baseScale;
    private float baseBaseDamage; // Base of baseDamage (for damage multiplier)
    
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
        
        // NovaStar: Start from bottom-LEFT, move clockwise to bottom-RIGHT
        // Clockwise means decreasing angles.
        // orbitBelowCameraExtension is the CENTER extension; orbitTiltDegrees
        // tilts the path so that positive values push the RIGHT side lower and
        // the LEFT side higher (and negative does the opposite).
        float baseExtension = orbitBelowCameraExtension;
        float halfTilt = orbitTiltDegrees * 0.5f;
        float leftExtension = Mathf.Max(0f, baseExtension - halfTilt);
        float level1ExtraExtension = (currentLevel == 1) ? 30f : 0f;
        float startAngle = 180f + leftExtension + level1ExtraExtension;
        currentAngle = startAngle + angleOffset;
        
        // Calculate level-based stats
        if (useSynchronizedSpeed)
        {
            // Synchronized speed calculation
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
            
            Debug.Log($"<color=gold>NovaStar Synchronized Speed: Level {currentLevel}, Radius={GetCurrentLevelRadius():F2}, Arc={thisArcAngleDeg}°, Speed={currentSpeed:F2}</color>");
        }
        else if (useAdaptiveSpeed)
        {
            // Adaptive speed calculation
            float level1ExtraExt = (currentLevel == 1) ? 30f : 0f;
            float arcAngleDeg = 180f + 2f * (orbitBelowCameraExtension + level1ExtraExt);
            float arcAngleRad = arcAngleDeg * Mathf.Deg2Rad;
            float arcLength = GetCurrentLevelRadius() * arcAngleRad;
            currentSpeed = arcLength / targetOrbitDuration;
            SyncSpeed = currentSpeed;
            Debug.Log($"<color=gold>NovaStar Adaptive Speed: Level {currentLevel}, Speed={currentSpeed:F2}</color>");
        }
        else
        {
            // Normal speed scaling
            currentSpeed = baseOrbitSpeed * Mathf.Pow(speedScalingPerLevel, currentLevel - 1);
            SyncSpeed = currentSpeed;
        }
        
        currentDamage = baseDamage * Mathf.Pow(damageScalingPerLevel, currentLevel - 1);
        
        // Calculate damage interval
        damageInterval = 1f / damageInstancesPerSecond;
        nextDamageTime = Time.time + damageInterval;
        
        // Store base values for instant modifier recalculation
        baseDamageRadius = damageRadius;
        if (baseSpeed <= 0f)
        {
            baseSpeed = baseOrbitSpeed;
        }
        baseScale = transform.localScale;
        baseBaseDamage = baseDamage;
        
        // Get size modifier from card
        ProjectileCards card = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
        if (card != null)
        {
            CardModifierStats modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);

            // When synchronizeSpawns is enabled and both stars are enhanced, use the
            // shared averaged modifiers from OrbitalStarManager so NovaStar and
            // DwarfStar always share the same effective stats.
            OrbitalStarManager manager = FindObjectOfType<OrbitalStarManager>();
            if (manager != null && useSharedModifiers)
            {
                modifiers = manager.GetEffectiveStarModifiers(card);
            }

            sizeMultiplier = modifiers.sizeMultiplier;
            
            // CRITICAL: Apply damage radius modifier (RAW value added)
            float damageRadiusBonus = modifiers.damageRadiusIncrease;
            currentRadius = (damageRadius + damageRadiusBonus) * sizeMultiplier;
            
            Debug.Log($"<color=yellow>NovaStar Initialize: Card={card.cardName}, BaseDamageRadius={damageRadius:F2}, Modifier=+{damageRadiusBonus:F2}, Size={sizeMultiplier:F2}x, FinalRadius={currentRadius:F2}</color>");
        }
        else
        {
            // No modifiers - use base radius with size multiplier
            currentRadius = damageRadius * sizeMultiplier;
            Debug.Log($"<color=red>NovaStar Initialize: NO CARD FOUND! Using base radius={damageRadius:F2}</color>");
        }
        
        UpdateRadiusCollider();
        
        // Position at starting point (off-camera)
        UpdatePosition();
        
        // Start fade-in
        if (spriteRenderer != null)
        {
            StartCoroutine(FadeIn());
        }
        
        // Start orbital movement
        StartCoroutine(OrbitalMovement());
        
        Debug.Log($"<color=cyan>NovaStar initialized: Level {currentLevel}, Start Angle: {currentAngle:F1}°, Speed: {currentSpeed:F1}, Damage: {currentDamage:F1}</color>");
        
        // Setup visual effects
        SetupVisualEffects();
        
        // Find and setup RadiusIndicator child
        FindRadiusIndicator();
    }
    
    private IEnumerator FadeIn()
    {
        // Start invisible
        Color color = spriteRenderer.color;
        color.a = 0f;
        spriteRenderer.color = color;
        
        // Only Level 1 needs delayed fade-in
        if (currentLevel == 1)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                float cameraHeight = mainCam.orthographicSize * 2f;
                float cameraWidth = cameraHeight * mainCam.aspect;
                Vector3 camPos = mainCam.transform.position;
                float margin = level1FadeInMargin;
                
                // Wait until star is approaching screen
                while (true)
                {
                    float distX = Mathf.Abs(transform.position.x - camPos.x);
                    float distY = Mathf.Abs(transform.position.y - camPos.y);
                    
                    bool isApproachingScreen = distX < (cameraWidth / 2f + margin) && distY < (cameraHeight / 2f + margin);
                    
                    if (isApproachingScreen) break;
                    
                    yield return null;
                }
            }
        }
        
        // Now fade in
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
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
                // Check if star is on camera
                UpdateOnCameraStatus();
                
                float effectiveSpeed = currentSpeed;
                
                // Move along the orbit
                float angleStep = (effectiveSpeed / GetCurrentLevelRadius()) * Mathf.Rad2Deg * Time.deltaTime;
                
                // NovaStar: Move clockwise (decreasing angles)
                currentAngle -= angleStep;
                
                // Check if reached center (90° = top)
                if (currentAngle <= 90f && currentAngle + angleStep > 90f)
                {
                    currentAngle = 90f;
                    yield return StartCoroutine(PauseAtCenter());
                }
                
                // Check if completed orbit
                float level1ExtraExtension = (currentLevel == 1) ? 30f : 0f;
                // Use RIGHT-side extension for the completion angle so tilt is
                // applied correctly (right side lower when orbitTiltDegrees > 0).
                float baseExtension = orbitBelowCameraExtension;
                float halfTilt = orbitTiltDegrees * 0.5f;
                float rightExtension = Mathf.Max(0f, baseExtension + halfTilt);
                float endAngle = 0f - rightExtension - level1ExtraExtension;
                if (currentAngle <= endAngle)
                {
                    yield return StartCoroutine(CompleteOrbit());
                    yield break;
                }
                
                // Update position
                UpdatePosition();
            }
            
            yield return null;
        }
    }
    
    private IEnumerator PauseAtCenter()
    {
        isPaused = true;
        Debug.Log($"<color=yellow>NovaStar paused at center for {centerPauseDuration}s</color>");
        
        TriggerCenterBurst();
        
        yield return new WaitForSeconds(centerPauseDuration);
        
        isPaused = false;
        Debug.Log($"<color=green>NovaStar resuming movement</color>");
    }
    
    private IEnumerator CompleteOrbit()
    {
        Debug.Log($"<color=lime>NovaStar completed Level {currentLevel} orbit</color>");
        
        // Only Level 1 needs off-screen detection
        if (currentLevel == 1)
        {
            Debug.Log($"<color=yellow>NovaStar Level 1: Waiting to go off-screen before destruction</color>");
            
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                float cameraHeight = mainCam.orthographicSize * 2f;
                float cameraWidth = cameraHeight * mainCam.aspect;
                Vector3 camPos = mainCam.transform.position;
                float margin = level1OffScreenMargin;
                
                // Continue moving until off-screen
                while (true)
                {
                    float distX = Mathf.Abs(transform.position.x - camPos.x);
                    float distY = Mathf.Abs(transform.position.y - camPos.y);
                    
                    bool isOffScreen = distX > (cameraWidth / 2f + margin) || distY > (cameraHeight / 2f + margin);
                    
                    if (isOffScreen)
                    {
                        Debug.Log($"<color=yellow>NovaStar Level 1: Now off-screen, destroying</color>");
                        
                        // Notify manager
                        OrbitalStarManager manager = FindObjectOfType<OrbitalStarManager>();
                        if (manager != null)
                        {
                            manager.OnOrbitComplete(starType, currentLevel);
                        }
                        
                        break;
                    }
                    
                    // Continue moving
                    float angleChange = currentSpeed * Time.deltaTime;
                    currentAngle -= angleChange;
                    
                    float angleRad = currentAngle * Mathf.Deg2Rad;
                    float radius = GetCurrentLevelRadius();
                    Vector3 offset = new Vector3(Mathf.Cos(angleRad) * radius, Mathf.Sin(angleRad) * radius, 0f);
                    transform.position = playerTransform.position + offset;
                    
                    yield return null;
                }
            }
        }
        
        // Wait for additional delay if set
        if (additionalOrbitDelay > 0f)
        {
            Debug.Log($"<color=magenta>NovaStar: Waiting additional {additionalOrbitDelay:F2}s before completion</color>");
            yield return new WaitForSeconds(additionalOrbitDelay);
        }
        
        // For levels 2-6, notify manager
        if (currentLevel > 1)
        {
            OrbitalStarManager manager = FindObjectOfType<OrbitalStarManager>();
            if (manager != null)
            {
                manager.OnOrbitComplete(starType, currentLevel);
            }
        }
        
        // Destroy this star
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
    }
    
    private void UpdateRadiusCollider()
    {
        if (radiusCollider != null)
        {
            radiusCollider.radius = currentRadius;
        }
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
        // Deal damage to enemies in radius
        if (Time.time >= nextDamageTime)
        {
            DealDamageToEnemiesInRadius();
            nextDamageTime = Time.time + damageInterval;
        }
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

                // Apply modifiers
                foreach (var modifier in activeModifiers)
                {
                    if (modifier != null)
                    {
                        finalDamage = modifier.ApplyDamageModifier(finalDamage);
                    }
                }

                // Apply PlayerStats damage calculation so flatDamage is applied
                // directly per hit (1:1) using the global projectile damage
                // pipeline.
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
                
                // Tag EnemyHealth so the nova hit uses the fire damage color
                // in the central EnemyHealth damage-number pipeline.
                EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>() ?? hit.GetComponentInParent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Fire);
                }

                // Use the enemy's own position as the hit point so damage
                // numbers spawn on the enemy sprite instead of on the orbiting
                // NovaStar projectile.
                Vector3 enemyPos = hit.bounds.center;
                damageable.TakeDamage(finalDamage, enemyPos, Vector3.zero);
                damageCount++;
                
                // Apply burn effect using BurnEffect component
                BurnEffect burnEffect = GetComponent<BurnEffect>();
                if (burnEffect != null)
                {
                    burnEffect.Initialize(finalDamage, ProjectileType.Fire);
                    burnEffect.TryApplyBurn(hit.gameObject, hit.transform.position);
                }

                SlowEffect slowEffect = GetComponent<SlowEffect>();
                if (slowEffect != null)
                {
                    slowEffect.TryApplySlow(hit.gameObject, hit.transform.position);
                }

                StaticEffect staticEffect = GetComponent<StaticEffect>();
                if (staticEffect != null)
                {
                    staticEffect.TryApplyStatic(hit.gameObject, hit.transform.position);
                }
            }
        }
        
        if (damageCount > 0)
        {
            Debug.Log($"<color=lime>NovaStar dealt {currentDamage:F1} damage to {damageCount} enemies</color>");
        }
    }
    
    public void ApplyModifiers(List<ProjectileModifierData> modifiers)
    {
        if (modifiers != null)
        {
            activeModifiers.AddRange(modifiers);
            Debug.Log($"<color=yellow>NovaStar applied {modifiers.Count} modifiers</color>");
        }
    }
    
    private void SetupVisualEffects()
    {
        if (trailRenderer != null)
        {
            trailRenderer.enabled = true;
            Color trailColor = new Color(1f, 0.5f, 0f, 0.8f); // Orange for fire
            trailRenderer.startColor = trailColor;
            trailRenderer.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
            trailRenderer.time = 0.5f;
            trailRenderer.startWidth = 0.3f;
            trailRenderer.endWidth = 0.05f;
        }
        
        if (ambientParticles != null)
        {
            var main = ambientParticles.main;
            main.startColor = new Color(1f, 0.6f, 0f, 0.8f); // Orange for fire
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
            Debug.Log($"<color=magenta>NovaStar center burst effect triggered!</color>");
        }
    }
    
    private void FindRadiusIndicator()
    {
        if (string.IsNullOrEmpty(radiusIndicatorName)) return;
        
        radiusIndicator = transform.Find(radiusIndicatorName);
        
        if (radiusIndicator != null)
        {
            Debug.Log($"<color=cyan>NovaStar: Found RadiusIndicator child '{radiusIndicatorName}'</color>");
            UpdateRadiusIndicatorScale();
        }
    }
    
    private void UpdateRadiusIndicatorScale()
    {
        if (radiusIndicator == null) return;
        
        float scale = currentRadius * 2f;
        radiusIndicator.localScale = new Vector3(scale, scale, 1f);
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
    
    /// <summary>
    /// Apply modifiers instantly (IInstantModifiable interface)
    /// Called when a new modifier card is selected
    /// </summary>
    public void ApplyInstantModifiers(CardModifierStats modifiers)
    {
        // Use shared Nova/Dwarf modifiers when synchronized spawns are active so
        // that any new modifiers applied later are also shared between both stars.
        ProjectileCards card = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
        if (card != null && useSharedModifiers)
        {
            OrbitalStarManager manager = FindObjectOfType<OrbitalStarManager>();
            if (manager != null)
            {
                modifiers = manager.GetEffectiveStarModifiers(card);
            }
        }

        Debug.Log($"<color=lime>╔═══ NOVASTAR INSTANT MODIFIERS ═══╗</color>");
        
        // Recalculate damage radius
        float newRadius = (baseDamageRadius + modifiers.damageRadiusIncrease) * modifiers.sizeMultiplier;
        if (newRadius != currentRadius)
        {
            currentRadius = newRadius;
            UpdateRadiusCollider();
            Debug.Log($"<color=lime>  Damage Radius: {baseDamageRadius:F2} + {modifiers.damageRadiusIncrease:F2} * {modifiers.sizeMultiplier:F2}x = {currentRadius:F2}</color>");
        }
        
        // Recalculate speed (affects baseOrbitSpeed)
        float newBaseSpeed = baseSpeed + modifiers.speedIncrease;
        if (newBaseSpeed != baseOrbitSpeed)
        {
            baseOrbitSpeed = newBaseSpeed;

            // Recalculate currentSpeed according to the active speed mode, mirroring
            // the logic used in Initialize so that synchronized/adaptive orbits
            // keep their intended timing when modifiers change.
            if (useSynchronizedSpeed)
            {
                // Synchronized speed: all levels complete in the same time window,
                // scaled by arc length relative to Level 1.
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
                // Adaptive speed: adjust so each level's orbit length completes in
                // targetOrbitDuration seconds.
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
                // Default: level-based speed scaling
                currentSpeed = baseOrbitSpeed * Mathf.Pow(speedScalingPerLevel, currentLevel - 1);
            }

            SyncSpeed = currentSpeed;

            Debug.Log($"<color=lime>  Speed: {baseSpeed:F2} + {modifiers.speedIncrease:F2} = {baseOrbitSpeed:F2} (current: {currentSpeed:F2})</color>");
        }
        
        // Recalculate size
        if (modifiers.sizeMultiplier != sizeMultiplier)
        {
            sizeMultiplier = modifiers.sizeMultiplier;
            transform.localScale = baseScale * sizeMultiplier;
            Debug.Log($"<color=lime>  Size: {baseScale} * {sizeMultiplier:F2}x = {transform.localScale}</color>");
        }
        
        // Recalculate damage: apply FLAT bonus first, then level-based scaling
        float newBaseDamage = baseBaseDamage + modifiers.damageFlat;
        if (Mathf.Abs(newBaseDamage - baseDamage) > 0.001f)
        {
            baseDamage = newBaseDamage;
            // Recalculate current damage based on level
            currentDamage = baseDamage * Mathf.Pow(damageScalingPerLevel, currentLevel - 1);
            Debug.Log($"<color=lime>  Damage: {baseBaseDamage:F2} + {modifiers.damageFlat:F2} = {baseDamage:F2} (current: {currentDamage:F2})</color>");
        }
        
        Debug.Log($"<color=lime>╚═══════════════════════════════════╝</color>");
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw current damage radius
        Vector3 damageCenter = transform.position + new Vector3(radiusOffsetX, radiusOffsetY, 0f);
        
        Gizmos.color = damageRadiusGizmoColor;
        Gizmos.DrawWireSphere(damageCenter, currentRadius > 0f ? currentRadius : damageRadius);
        
        // Draw orbital path
        if (playerTransform != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange for fire
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
