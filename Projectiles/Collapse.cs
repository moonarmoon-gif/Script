using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Collapse : MonoBehaviour, IInstantModifiable
{
    [Header("Core Settings")]
    [SerializeField] private float lifetimeSeconds = 6f;
    [Tooltip("Radius in which enemies will be pulled toward the Collapse center")] 
    public float pullRadius = 3f;

    [Header("Spawn Area - 4 Point System")]
    [Tooltip("Tag for point A (top-left): determines minX and minY")] 
    [SerializeField] private string pointATag = "Collapse_PointA";
    [Tooltip("Tag for point B (top-right): determines maxX and minY")] 
    [SerializeField] private string pointBTag = "Collapse_PointB";
    [Tooltip("Tag for point C (bottom-right): determines maxX and maxY")] 
    [SerializeField] private string pointCTag = "Collapse_PointC";
    [Tooltip("Tag for point D (bottom-left): determines minX and maxY")] 
    [SerializeField] private string pointDTag = "Collapse_PointD";

    [Header("Pull Settings")]
    [Tooltip("Base pull strength. Higher values pull enemies more strongly.")]
    public float pullStrength = 10f;

    [Tooltip("Resistance per unit of mass. Pull effectiveness decreases by ResistancePerMass * mass.")]
    public float resistancePerMass = 0.1f;

    [Tooltip("Offset for pull center in X and Y coordinates")] 
    [SerializeField] private Vector2 pullOffset = Vector2.zero;

    [Tooltip("Layers considered enemies for pulling")] 
    [SerializeField] private LayerMask enemyLayer;

    [Header("Pull Animation")]
    [Tooltip("Radius near the core where enemies are considered fully pulled and their walk animation is forced.")]
    [SerializeField] private float fullyPulledAnimationRadius = 0.5f;

    [Header("Visual Scaling")]
    [Tooltip("Factor used to derive Collapse root scale from pullRadius (default 0.5 = half of pullRadius).")]
    public float collapseScalePerPullRadius = 0.5f;

    [Header("Projectile Element Type (for universal modifiers)")]
    [SerializeField] private ProjectileType projectileType = ProjectileType.Nuclear;

    [Header("Mana & Cooldown")]
    [SerializeField] private int manaCost = 20;
    [SerializeField] private float cooldown = 3f;

    [Header("Enhanced Variant 1 - Gravity Burst")]
    [SerializeField] private float variant1PullStrengthBonus = 100f;
    [SerializeField] private float variant1ExplosionRadius = 4f;
    [SerializeField] private float variant1ExplosionDamage = 100f;
    [SerializeField] private Vector2 variant1ExplosionOffset = Vector2.zero;
    [SerializeField] private float variant1Lifetime = 6f;
    [SerializeField] private GameObject variant1ExplosionEffectPrefab;
    [SerializeField] private bool showVariant1ExplosionGizmo = true;

    [Header("Enhanced Variant 2 - Overcharged Core")]
    [SerializeField] private float variant2RadiusBonus = 2f;
    [SerializeField] private float variant2PullStrengthBonus = 50f;

    [Header("Enhanced Variant 3 - Static Core")]
    [Tooltip("Base damage dealt per tick for Variant 3 inside the pull radius")] 
    [SerializeField] private float variant3DamagePerTick = 5f;

    [Tooltip("Time between Variant 3 damage ticks (seconds)")] 
    [SerializeField] private float variant3DamageInterval = 0.25f;

    [Tooltip("Delay before Variant 3 begins dealing periodic damage (seconds)")] 
    [SerializeField] private float variant3ChargeDamageDelay = 1f;

    private Transform pointA;
    private Transform pointB;
    private Transform pointC;
    private Transform pointD;

    private Rigidbody2D _rigidbody2D;
    private Collider2D _collider2D;

    // Base values for instant modifier recalculation
    private float baseLifetimeSeconds;
    private float basePullRadius;
    private float basePullStrength;
    private Vector3 baseScale;
    private Transform pullRadiusCircle;
    private bool pullRadiusCircleInitialized = false;
    private GameObject fireChakram;

    // Variant 3 damage tracking
    private float baseVariant3DamagePerTick;
    private float currentVariant3DamagePerTick;

    // Player stats for applying flat damage, multipliers, crit, etc.
    private PlayerStats playerStats;

    // Instance-based cooldown tracking (shared across all Collapse instances)
    private static System.Collections.Generic.Dictionary<string, float> lastFireTimes = new System.Collections.Generic.Dictionary<string, float>();
    private string prefabKey;
    private int enhancedVariant = 0;
    private bool isStackedVariant23 = false;

    private static Collider2D[] cachedSpawnAreaEnemies = new Collider2D[256];

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _collider2D = GetComponent<Collider2D>();

        // Store base values
        baseLifetimeSeconds = lifetimeSeconds;
        basePullRadius = pullRadius;
        basePullStrength = pullStrength;
        baseScale = transform.localScale;
        baseVariant3DamagePerTick = variant3DamagePerTick;
        currentVariant3DamagePerTick = baseVariant3DamagePerTick;

        // Optional visual helper for pull radius; no special 2x logic needed now.
        pullRadiusCircle = transform.Find("PullRadiusCircle");
        if (pullRadiusCircle == null)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == "PullRadiusCircle")
                {
                    pullRadiusCircle = children[i];
                    break;
                }
            }
        }

        Transform fireChakramTransform = transform.Find("FireChakram");
        if (fireChakramTransform == null)
        {
            Transform[] childrenAll = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < childrenAll.Length; i++)
            {
                if (childrenAll[i].name == "FireChakram")
                {
                    fireChakramTransform = childrenAll[i];
                    break;
                }
            }
        }
        if (fireChakramTransform != null)
        {
            fireChakram = fireChakramTransform.gameObject;
            fireChakram.SetActive(false);
        }

        UpdatePullRadiusCircleScale();

        // Keep Collapse stationary; we only use it as a field source
        if (_rigidbody2D != null)
        {
            _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody2D.velocity = Vector2.zero;
            _rigidbody2D.gravityScale = 0f;
        }

        // Cache PlayerStats for Variant 3 damage (flat damage, multipliers, crit)
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }
    }

    private IEnumerator Variant3DamageRoutine(float chargeDelay, float interval)
    {
        if (chargeDelay > 0f)
        {
            yield return new WaitForSeconds(chargeDelay);
        }

        if (fireChakram != null)
        {
            fireChakram.SetActive(true);
        }

        StaticEffect staticEffect = GetComponent<StaticEffect>();

        while (true)
        {
            Vector2 center = (Vector2)transform.position + pullOffset;
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, pullRadius, enemyLayer);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                IDamageable damageable = hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive)
                {
                    continue;
                }

                if (!OffscreenDamageChecker.CanTakeDamage(hit.transform.position))
                {
                    continue;
                }

                Vector3 hitPoint = hit.ClosestPoint(center);
                Vector3 hitNormal = (center - (Vector2)hitPoint).normalized;

                float baseTickDamage = currentVariant3DamagePerTick;
                if (baseTickDamage <= 0f)
                {
                    continue;
                }

                // Apply PlayerStats damage calculation (flat damage + multipliers + crit)
                float finalDamage = baseTickDamage;

                Component damageableComponent = damageable as Component;
                GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : hit.gameObject;

                if (playerStats != null)
                {
                    finalDamage = PlayerDamageHelper.ComputeProjectileDamage(playerStats, enemyObject, baseTickDamage, gameObject);
                }

                // Tag EnemyHealth so Variant 3 periodic damage uses the Thunder
                // damage color in the central EnemyHealth damage-number pipeline.
                if (enemyObject != null)
                {
                    EnemyHealth enemyHealth = enemyObject.GetComponent<EnemyHealth>() ?? enemyObject.GetComponentInParent<EnemyHealth>();
                    if (enemyHealth != null)
                    {
                        enemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Thunder);
                    }
                }

                damageable.TakeDamage(finalDamage, hitPoint, hitNormal);

                if (staticEffect != null)
                {
                    staticEffect.TryApplyStatic(hit.gameObject, hitPoint);
                }
            }

            yield return new WaitForSeconds(interval);
        }
    }

    public void Initialize(Vector3 spawnPosition, Collider2D playerCollider, bool skipCooldownCheck = false)
    {
        // Find spawn area GameObjects by tag (4-point system, same pattern as FireMine)
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

        // Get card-specific modifiers
        ProjectileCards card = ProjectileCardModifiers.Instance != null
            ? ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject)
            : null;

        // Determine enhanced variant and whether V2+V3 stacking is active based on
        // variant history so stacking works regardless of which variant was
        // chosen first in the UI.
        UpdateEnhancedVariantAndStackState(card);

        // Ensure FireChakram starts disabled for all variants; Variant 3 will
        // explicitly enable it after its charge damage delay inside
        // Variant3DamageRoutine.
        if (fireChakram != null)
        {
            fireChakram.SetActive(false);
        }

        CardModifierStats modifiers = new CardModifierStats();
        if (card != null && ProjectileCardModifiers.Instance != null)
        {
            modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
        }

        // Apply modifiers: lifetime, radius, size, and dedicated pull-strength multiplier
        float lifetimeBase = baseLifetimeSeconds;
        if (enhancedVariant == 1)
        {
            lifetimeBase = variant1Lifetime;
        }
        float finalLifetime = lifetimeBase + modifiers.lifetimeIncrease;
        lifetimeSeconds = finalLifetime;

        float radiusAfterSize = basePullRadius;
        if (modifiers.sizeMultiplier != 1f)
        {
            // Keep the visual scale driven entirely from pullRadius
            radiusAfterSize *= modifiers.sizeMultiplier;
        }

        pullRadius = (radiusAfterSize + modifiers.explosionRadiusBonus) * modifiers.explosionRadiusMultiplier;

        float strengthBonus = modifiers.pullStrengthMultiplier;
        pullStrength = basePullStrength + strengthBonus;

        if (enhancedVariant == 1)
        {
            pullStrength += variant1PullStrengthBonus;
        }
        else if (enhancedVariant == 2)
        {
            // Variant 2 (and stacked Variant 2+3): apply radius and pull bonuses
            // from the Overcharged Core behaviour.
            pullRadius += variant2RadiusBonus;
            pullStrength += variant2PullStrengthBonus;
        }

        // Variant 3: scale periodic damage by card damage multiplier so it respects upgrades
        UpdateVariant3DamageFromModifiers(modifiers);

        UpdatePullRadiusCircleScale();

        Vector3 desiredSpawn = spawnPosition;

        if (pointA != null && pointB != null && pointC != null && pointD != null)
        {
            float minX1 = pointA.position.x;
            float maxX1 = pointB.position.x;
            float minX2 = pointD.position.x;
            float maxX2 = pointC.position.x;

            float minY1 = pointA.position.y;
            float maxY1 = pointD.position.y;
            float minY2 = pointB.position.y;
            float maxY2 = pointC.position.y;

            float finalMinX = Mathf.Min(minX1, maxX1, minX2, maxX2);
            float finalMaxX = Mathf.Max(minX1, maxX1, minX2, maxX2);

            float finalMinY = Mathf.Min(minY1, maxY1, minY2, maxY2);
            float finalMaxY = Mathf.Max(minY1, maxY1, minY2, maxY2);

            float fallbackX = Random.Range(finalMinX, finalMaxX);
            float fallbackY = Random.Range(finalMinY, finalMaxY);
            desiredSpawn = new Vector3(fallbackX, fallbackY, spawnPosition.z);

            Vector2 min = new Vector2(finalMinX, finalMinY);
            Vector2 max = new Vector2(finalMaxX, finalMaxY);
            int enemyCount = Physics2D.OverlapAreaNonAlloc(min, max, cachedSpawnAreaEnemies, enemyLayer);

            if (enemyCount > 0)
            {
                float radiusSqr = pullRadius * pullRadius;
                int attempts = 48;
                for (int attempt = 0; attempt < attempts; attempt++)
                {
                    Collider2D chosenEnemy = cachedSpawnAreaEnemies[Random.Range(0, enemyCount)];
                    if (chosenEnemy == null)
                    {
                        continue;
                    }

                    Vector2 enemyPos = chosenEnemy.transform.position;

                    // Ideal placement puts the pull center directly on the enemy.
                    Vector2 preferred = enemyPos - pullOffset;

                    // Add random jitter so we can still satisfy bounds near edges.
                    Vector2 candidate = preferred + (Random.insideUnitCircle * pullRadius);
                    candidate.x = Mathf.Clamp(candidate.x, finalMinX, finalMaxX);
                    candidate.y = Mathf.Clamp(candidate.y, finalMinY, finalMaxY);

                    Vector2 center = candidate + pullOffset;

                    bool hasEnemyInRadius = false;
                    for (int i = 0; i < enemyCount; i++)
                    {
                        Collider2D enemyCol = cachedSpawnAreaEnemies[i];
                        if (enemyCol == null)
                        {
                            continue;
                        }

                        Vector2 otherEnemyPos = enemyCol.transform.position;
                        if ((otherEnemyPos - center).sqrMagnitude <= radiusSqr)
                        {
                            hasEnemyInRadius = true;
                            break;
                        }
                    }

                    if (hasEnemyInRadius)
                    {
                        desiredSpawn = new Vector3(candidate.x, candidate.y, spawnPosition.z);
                        break;
                    }
                }
            }
        }

        transform.position = desiredSpawn;

        // CRITICAL: Use ProjectileCards spawnInterval if available, otherwise script cooldown
        float baseCooldown = cooldown;
        if (card != null && card.runtimeSpawnInterval > 0f)
        {
            baseCooldown = card.runtimeSpawnInterval;
        }

        float finalCooldown = Mathf.Max(0.1f, baseCooldown * (1f - modifiers.cooldownReductionPercent / 100f));
        int finalManaCost = Mathf.Max(0, Mathf.CeilToInt(manaCost * (1f - modifiers.manaCostReduction)));

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

        // Generate key based ONLY on projectile type name so all Collapse share cooldown
        prefabKey = "Collapse";

        if (!skipCooldownCheck)
        {
            // Check cooldown
            if (!bypassEnhancedFirstSpawnCooldown && lastFireTimes.ContainsKey(prefabKey))
            {
                if (Time.time - lastFireTimes[prefabKey] < finalCooldown)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            // Check mana
            PlayerMana playerMana = FindObjectOfType<PlayerMana>();
            if (playerMana != null && finalManaCost > 0 && !playerMana.Spend(finalManaCost))
            {
                Destroy(gameObject);
                return;
            }

            lastFireTimes[prefabKey] = Time.time;
        }

        // Ignore collision with player
        if (_collider2D != null && playerCollider != null)
        {
            Physics2D.IgnoreCollision(_collider2D, playerCollider, true);
        }

        // Start lifetime
        StartCoroutine(LifetimeRoutine(lifetimeSeconds));

        // Enhanced Variant 3: Begin periodic damage after a charge delay and
        // continue dealing damage inside the pull radius until Collapse expires.
        // When both Variant 2 and 3 have ever been chosen for this card, enable
        // the Variant 3 behaviour as part of the stacked V2+V3 mode regardless
        // of which variant is currently selected in the UI.
        if ((enhancedVariant == 3 || isStackedVariant23) && currentVariant3DamagePerTick > 0f && variant3DamageInterval > 0f)
        {
            StartCoroutine(Variant3DamageRoutine(variant3ChargeDamageDelay, variant3DamageInterval));
        }
    }

    private IEnumerator LifetimeRoutine(float lifetime)
    {
        yield return new WaitForSeconds(lifetime);

        if (enhancedVariant == 1)
        {
            PerformVariant1Explosion();
        }

        Destroy(gameObject);
    }

    private void PerformVariant1Explosion()
    {
        if (variant1ExplosionRadius <= 0f)
        {
            return;
        }

        Vector2 center = (Vector2)transform.position + variant1ExplosionOffset;

        if (variant1ExplosionEffectPrefab != null)
        {
            Instantiate(variant1ExplosionEffectPrefab, center, Quaternion.identity);
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, variant1ExplosionRadius, enemyLayer);

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            IDamageable damageable = hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive)
            {
                continue;
            }

            if (!OffscreenDamageChecker.CanTakeDamage(hit.transform.position))
            {
                continue;
            }

            Vector3 hitPoint = hit.ClosestPoint(center);
            Vector3 hitNormal = (center - (Vector2)hitPoint).normalized;
            float baseDamage = variant1ExplosionDamage;
            float finalDamage = baseDamage;

            Component damageableComponent = damageable as Component;
            GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : hit.gameObject;

            if (playerStats != null)
            {
                finalDamage = PlayerDamageHelper.ComputeProjectileDamage(playerStats, enemyObject, baseDamage, gameObject);
            }

            // Tag EnemyHealth so the Gravity Burst explosion uses the fire
            // damage color when EnemyHealth renders the damage number.
            if (enemyObject != null)
            {
                EnemyHealth enemyHealth = enemyObject.GetComponent<EnemyHealth>() ?? enemyObject.GetComponentInParent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Fire);
                }
            }

            damageable.TakeDamage(finalDamage, hitPoint, hitNormal);
        }
    }

    private void FixedUpdate()
    {
        if (pullStrength <= 0f || pullRadius <= 0f) return;

        Vector2 center = (Vector2)transform.position + pullOffset;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, pullRadius, enemyLayer);

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;

            Rigidbody2D enemyRb = hit.attachedRigidbody;
            if (enemyRb == null) continue;

            float mass = Mathf.Max(0.01f, enemyRb.mass);

            // Compute pull force so that heavier enemies are pulled less.
            // Formula: F = PullStrength / (1 + ResistancePerMass * mass)
            //  - When mass is small, F ≈ PullStrength
            //  - As mass increases, denominator grows and F decreases smoothly
            float denominator = 1f + Mathf.Max(0f, resistancePerMass) * mass;
            float forceMagnitude = pullStrength / denominator;
            if (forceMagnitude <= 0f) continue;

            Vector2 toCenter = center - enemyRb.position;
            if (toCenter.sqrMagnitude < 0.0001f) continue;
            Vector2 dir = toCenter.normalized;

            // Apply force; enemy AI is free to keep moving toward the player since
            // we don't override their velocity or movement logic.
            enemyRb.AddForce(dir * forceMagnitude, ForceMode2D.Force);

            // When enemies are very close to the Collapse core, keep their
            // walk/move animation playing so they do not flicker between walk
            // and idle while being held in place by the pull.
            if (fullyPulledAnimationRadius > 0f && toCenter.sqrMagnitude <= fullyPulledAnimationRadius * fullyPulledAnimationRadius)
            {
                CollapsePullController pullController = enemyRb.GetComponent<CollapsePullController>();
                if (pullController == null)
                {
                    pullController = enemyRb.gameObject.AddComponent<CollapsePullController>();
                }

                pullController.SetPulled(true, dir);
            }
        }
    }

    /// <summary>
    /// Apply modifiers instantly to this Collapse instance (IInstantModifiable).
    /// </summary>
    public void ApplyInstantModifiers(CardModifierStats modifiers)
    {
        // Re-evaluate enhanced variant and stacking state in case the player has
        // unlocked additional variants since this Collapse was spawned.
        ProjectileCards card = ProjectileCardModifiers.Instance != null
            ? ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject)
            : null;
        UpdateEnhancedVariantAndStackState(card);

        // Lifetime
        float lifetimeBase = baseLifetimeSeconds;
        if (enhancedVariant == 1)
        {
            lifetimeBase = variant1Lifetime;
        }
        float newLifetime = lifetimeBase + modifiers.lifetimeIncrease;
        lifetimeSeconds = newLifetime;

        // Radius
        float radiusAfterSize = basePullRadius;
        if (modifiers.sizeMultiplier != 1f)
        {
            // Keep the visual scale driven entirely from pullRadius
            radiusAfterSize *= modifiers.sizeMultiplier;
        }

        pullRadius = (radiusAfterSize + modifiers.explosionRadiusBonus) * modifiers.explosionRadiusMultiplier;

        float strengthMult = modifiers.pullStrengthMultiplier;
        pullStrength = basePullStrength + strengthMult;

        if (enhancedVariant == 1)
        {
            pullStrength += variant1PullStrengthBonus;
        }
        else if (enhancedVariant == 2)
        {
            pullRadius += variant2RadiusBonus;
            pullStrength += variant2PullStrengthBonus;
        }

        // Variant 3: update periodic damage so it respects current card modifiers
        UpdateVariant3DamageFromModifiers(modifiers);

        UpdatePullRadiusCircleScale();
    }

    private void UpdatePullRadiusCircleScale()
    {
        // 1) Scale the Collapse root so its X/Y scale tracks
        //    pullRadius * collapseScalePerPullRadius. This keeps the overall
        //    visual size tied to pullRadius.
        Vector3 rootScale = transform.localScale;
        float scaleFactor = collapseScalePerPullRadius > 0f ? collapseScalePerPullRadius : 0.5f;
        rootScale.x = pullRadius * scaleFactor;
        rootScale.y = pullRadius * scaleFactor;
        transform.localScale = rootScale;

        // Bind pullOffset's Y component to the current visual Y scale so the
        // Collapse pull center tracks the vertical size of the effect.
        pullOffset = new Vector2(pullOffset.x, rootScale.y);

        if (pullRadiusCircle == null) return;

        // 2) Ensure PullRadiusCircle's *world* radius is exactly 2x pullRadius,
        //    regardless of the root scale. Because the circle is a child of the
        //    root, we cancel out the parent's scale when computing its localScale.
        //    WorldRadius ≈ parentScale * localScale, so:
        //        localScale = (2 * pullRadius) / parentScale.
        Vector3 currentLossy = transform.lossyScale;
        float parentScaleX = Mathf.Approximately(currentLossy.x, 0f) ? 1f : currentLossy.x;
        float parentScaleY = Mathf.Approximately(currentLossy.y, 0f) ? 1f : currentLossy.y;

        Vector3 circleScale = pullRadiusCircle.localScale;
        circleScale.x = (pullRadius * 2f) / parentScaleX;
        circleScale.y = (pullRadius * 2f) / parentScaleY;
        pullRadiusCircle.localScale = circleScale;
    }

    private void UpdateVariant3DamageFromModifiers(CardModifierStats modifiers)
    {
        currentVariant3DamagePerTick = baseVariant3DamagePerTick;

        // When V2+V3 stacking is active, treat this as Variant 3 for periodic
        // damage scaling so upgrades affect the Static Core damage ticks.
        if ((enhancedVariant == 3 || isStackedVariant23) && modifiers != null)
        {
            // Add FLAT damage from card modifiers on top of the base tick.
            if (modifiers.damageFlat != 0f)
            {
                currentVariant3DamagePerTick += modifiers.damageFlat;
            }
        }
    }

    private void UpdateEnhancedVariantAndStackState(ProjectileCards card)
    {
        isStackedVariant23 = false;

        if (card == null || ProjectileCardLevelSystem.Instance == null)
        {
            enhancedVariant = 0;
            return;
        }

        // Start from the currently selected enhanced variant in the UI.
        int storedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card);
        enhancedVariant = storedVariant;

        // Order-independent V2+V3 stacking: as soon as BOTH Variant 2 and
        // Variant 3 have ever been chosen for this card, always run the
        // combined behaviour that includes V2's radius/pull bonuses and V3's
        // periodic damage / FireChakram logic, regardless of which variant is
        // currently selected in the UI.
        bool hasVariant2 = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 2);
        bool hasVariant3 = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 3);

        if (hasVariant2 && hasVariant3)
        {
            isStackedVariant23 = true;
            enhancedVariant = 2;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position + (Vector3)pullOffset;
        Gizmos.color = new Color(0.4f, 0f, 1f, 0.25f);
        Gizmos.DrawSphere(center, pullRadius);
        Gizmos.color = new Color(0.7f, 0.1f, 1f, 0.8f);
        Gizmos.DrawWireSphere(center, pullRadius);

        if (showVariant1ExplosionGizmo && variant1ExplosionRadius > 0f)
        {
            Vector3 explosionCenter = transform.position + (Vector3)variant1ExplosionOffset;
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
            Gizmos.DrawSphere(explosionCenter, variant1ExplosionRadius);
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.9f);
            Gizmos.DrawWireSphere(explosionCenter, variant1ExplosionRadius);
        }

        GameObject pointAObj = string.IsNullOrEmpty(pointATag) ? null : GameObject.FindGameObjectWithTag(pointATag);
        GameObject pointBObj = string.IsNullOrEmpty(pointBTag) ? null : GameObject.FindGameObjectWithTag(pointBTag);
        GameObject pointCObj = string.IsNullOrEmpty(pointCTag) ? null : GameObject.FindGameObjectWithTag(pointCTag);
        GameObject pointDObj = string.IsNullOrEmpty(pointDTag) ? null : GameObject.FindGameObjectWithTag(pointDTag);

        if (pointAObj != null && pointBObj != null && pointCObj != null && pointDObj != null)
        {
            Vector3 posA = pointAObj.transform.position;
            Vector3 posB = pointBObj.transform.position;
            Vector3 posC = pointCObj.transform.position;
            Vector3 posD = pointDObj.transform.position;

            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(posA, 0.3f);
            Gizmos.DrawSphere(posB, 0.3f);
            Gizmos.DrawSphere(posC, 0.3f);
            Gizmos.DrawSphere(posD, 0.3f);

            Gizmos.color = new Color(0f, 1f, 1f, 0.8f);
            Gizmos.DrawLine(posA, posB);
            Gizmos.DrawLine(posB, posC);
            Gizmos.DrawLine(posC, posD);
            Gizmos.DrawLine(posD, posA);

            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawLine(posA, posC);
            Gizmos.DrawLine(posB, posD);
        }
    }
}
