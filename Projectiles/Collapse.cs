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

    [Header("Spawn Area - 6 Point System")]
    [Tooltip("Tag for point A (top-left): determines minX and minY")]
    [SerializeField] private string pointATag = "Collapse_PointA";
    [Tooltip("Tag for point B (top-right): determines maxX and minY")]
    [SerializeField] private string pointBTag = "Collapse_PointB";
    [Tooltip("Tag for point C (bottom-right): determines maxX and maxY")]
    [SerializeField] private string pointCTag = "Collapse_PointC";
    [Tooltip("Tag for point D (bottom-left): determines minX and maxY")]
    [SerializeField] private string pointDTag = "Collapse_PointD";
    [Tooltip("Tag for point E")]
    [SerializeField] private string pointETag = "Collapse_PointE";
    [Tooltip("Tag for point F")]
    [SerializeField] private string pointFTag = "Collapse_PointF";

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

    [Header("Fade Away Settings")]
    [Tooltip("Duration of visual fade-out just before Collapse is destroyed.")]
    public float FadeAwayDuration = 0.2f;

    [Tooltip("Duration of visual fade-in when Collapse first appears.")]
    public float FadeInDuration = 0.5f;

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
    private Transform pointE;
    private Transform pointF;

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

        if (_rigidbody2D != null)
        {
            _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody2D.velocity = Vector2.zero;
            _rigidbody2D.gravityScale = 0f;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        // Optional initial fade-in for all sprites under this Collapse instance.
        if (Application.isPlaying && FadeInDuration > 0f)
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers != null && renderers.Length > 0)
            {
                StartCoroutine(FadeInVisuals(renderers, FadeInDuration));
            }
        }
    }

    private void DealAoeDamage(IDamageable target, float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (target == null || !target.IsAlive || damage <= 0f) return;

        DamageAoeScope.BeginAoeDamage();
        target.TakeDamage(damage, hitPoint, hitNormal);
        DamageAoeScope.EndAoeDamage();
    }

    private IEnumerator Variant3DamageRoutine(float chargeDelay, float interval)
    {
        if (chargeDelay > 0f)
        {
            yield return GameStateManager.WaitForPauseSafeSeconds(chargeDelay);
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

                float finalDamage = baseTickDamage;

                Component damageableComponent = damageable as Component;
                GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : hit.gameObject;

                if (playerStats != null)
                {
                    finalDamage = PlayerDamageHelper.ComputeProjectileDamage(playerStats, enemyObject, baseTickDamage, gameObject);
                }

                if (enemyObject != null)
                {
                    EnemyHealth enemyHealth = enemyObject.GetComponent<EnemyHealth>() ?? enemyObject.GetComponentInParent<EnemyHealth>();
                    if (enemyHealth != null)
                    {
                        enemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Thunder);
                    }
                }

                DealAoeDamage(damageable, finalDamage, hitPoint, hitNormal);

                if (staticEffect != null)
                {
                    staticEffect.TryApplyStatic(hit.gameObject, hitPoint);
                }
            }

            yield return GameStateManager.WaitForPauseSafeSeconds(interval);
        }
    }

    public void Initialize(Vector3 spawnPosition, Collider2D playerCollider, bool skipCooldownCheck = false)
    {
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

        ProjectileCards card = ProjectileCardModifiers.Instance != null
            ? ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject)
            : null;

        UpdateEnhancedVariantAndStackState(card);

        if (fireChakram != null)
        {
            fireChakram.SetActive(false);
        }

        CardModifierStats modifiers = new CardModifierStats();
        if (card != null && ProjectileCardModifiers.Instance != null)
        {
            modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
        }

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
            pullRadius += variant2RadiusBonus;
            pullStrength += variant2PullStrengthBonus;
        }

        UpdateVariant3DamageFromModifiers(modifiers);
        UpdatePullRadiusCircleScale();

        Vector3 desiredSpawn = spawnPosition;

        // Determine whether this Collapse instance should SKIP bosses when enforcing the spawn rule.
        // Per request:
        // - Base (variant 0) and Variant 2 should EXCLUDE bosses from the rule (boss-only -> random).
        // - Variant 1, Variant 3, and Variant 2+3 stacking should INCLUDE bosses in the rule.
        bool shouldIncludeBossesInSpawnRule = (enhancedVariant == 1) || (enhancedVariant == 3) || isStackedVariant23;

        if (pointA != null && pointB != null && pointC != null && pointD != null)
        {
            System.Collections.Generic.List<Vector2> spawnPolygon = new System.Collections.Generic.List<Vector2>(6);
            spawnPolygon.Add(pointA.position);
            spawnPolygon.Add(pointB.position);
            spawnPolygon.Add(pointC.position);
            spawnPolygon.Add(pointD.position);
            if (pointE != null) spawnPolygon.Add(pointE.position);
            if (pointF != null) spawnPolygon.Add(pointF.position);

            spawnPolygon = BuildOrderedPolygon(spawnPolygon);

            float finalMinX = spawnPolygon[0].x;
            float finalMaxX = spawnPolygon[0].x;
            float finalMinY = spawnPolygon[0].y;
            float finalMaxY = spawnPolygon[0].y;

            for (int i = 1; i < spawnPolygon.Count; i++)
            {
                Vector2 v = spawnPolygon[i];
                if (v.x < finalMinX) finalMinX = v.x;
                if (v.x > finalMaxX) finalMaxX = v.x;
                if (v.y < finalMinY) finalMinY = v.y;
                if (v.y > finalMaxY) finalMaxY = v.y;
            }

            int fallbackAttempts = 64;
            for (int attempt = 0; attempt < fallbackAttempts; attempt++)
            {
                float fallbackX = Random.Range(finalMinX, finalMaxX);
                float fallbackY = Random.Range(finalMinY, finalMaxY);
                Vector2 test2D = new Vector2(fallbackX, fallbackY);
                if (!IsPointInsidePolygon(test2D, spawnPolygon))
                {
                    continue;
                }
                desiredSpawn = new Vector3(fallbackX, fallbackY, spawnPosition.z);
                break;
            }

            Vector2 min = new Vector2(finalMinX, finalMinY);
            Vector2 max = new Vector2(finalMaxX, finalMaxY);
            int enemyCount = Physics2D.OverlapAreaNonAlloc(min, max, cachedSpawnAreaEnemies, enemyLayer);

            int filteredCount = 0;
            for (int i = 0; i < enemyCount; i++)
            {
                Collider2D col = cachedSpawnAreaEnemies[i];
                if (col == null) continue;
                if (!IsPointInsidePolygon(col.transform.position, spawnPolygon))
                {
                    continue;
                }
                cachedSpawnAreaEnemies[filteredCount++] = col;
            }

            enemyCount = filteredCount;

            // Boss-only exception should ONLY apply when we are excluding bosses.
            bool onlyBosses = false;
            if (!shouldIncludeBossesInSpawnRule && enemyCount > 0)
            {
                int aliveCount = 0;
                int bossCount = 0;

                for (int i = 0; i < enemyCount; i++)
                {
                    Collider2D col = cachedSpawnAreaEnemies[i];
                    if (col == null) continue;

                    EnemyHealth eh = col.GetComponent<EnemyHealth>() ?? col.GetComponentInParent<EnemyHealth>();
                    if (eh == null || !eh.IsAlive) continue;

                    aliveCount++;

                    EnemyCardTag tag = col.GetComponent<EnemyCardTag>() ?? col.GetComponentInParent<EnemyCardTag>();
                    if (tag != null && tag.rarity == CardRarity.Boss)
                    {
                        bossCount++;
                    }
                }

                onlyBosses = aliveCount > 0 && bossCount == aliveCount;
            }

            // Enforce the spawn rule unless we're in the "boss-only => random" case.
            if (enemyCount > 0 && !onlyBosses)
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
                    Vector2 preferred = enemyPos - pullOffset;

                    Vector2 candidate = preferred + (Random.insideUnitCircle * pullRadius);
                    candidate.x = Mathf.Clamp(candidate.x, finalMinX, finalMaxX);
                    candidate.y = Mathf.Clamp(candidate.y, finalMinY, finalMaxY);

                    if (!IsPointInsidePolygon(candidate, spawnPolygon))
                    {
                        continue;
                    }

                    Vector2 center = candidate + pullOffset;

                    bool hasEnemyInRadius = false;
                    for (int i = 0; i < enemyCount; i++)
                    {
                        Collider2D enemyCol = cachedSpawnAreaEnemies[i];
                        if (enemyCol == null)
                        {
                            continue;
                        }

                        if (!shouldIncludeBossesInSpawnRule)
                        {
                            // Skip bosses for the "must contain enemy" rule.
                            EnemyCardTag tag = enemyCol.GetComponent<EnemyCardTag>() ?? enemyCol.GetComponentInParent<EnemyCardTag>();
                            if (tag != null && tag.rarity == CardRarity.Boss)
                            {
                                continue;
                            }
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

        float baseCooldown = cooldown;
        if (card != null && card.runtimeSpawnInterval > 0f)
        {
            baseCooldown = card.runtimeSpawnInterval;
        }

        float finalCooldown = baseCooldown * (1f - modifiers.cooldownReductionPercent / 100f);
        if (MinCooldownManager.Instance != null)
        {
            finalCooldown = MinCooldownManager.Instance.ClampCooldown(card, finalCooldown);
        }
        else
        {
            finalCooldown = Mathf.Max(0.1f, finalCooldown);
        }

        float effectiveCooldown = finalCooldown;
        if (playerStats != null && playerStats.projectileCooldownReduction > 0f)
        {
            float totalCdr = Mathf.Max(0f, playerStats.projectileCooldownReduction);

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

        prefabKey = "Collapse";

        bool bypassEnhancedFirstSpawnCooldown = false;
        if (!skipCooldownCheck && card != null && card.applyEnhancedFirstSpawnReduction && card.pendingEnhancedFirstSpawn)
        {
            if (card.projectileSystem == ProjectileCards.ProjectileSystemType.Passive)
            {
                bypassEnhancedFirstSpawnCooldown = true;
                card.pendingEnhancedFirstSpawn = false;
            }
        }

        if (!skipCooldownCheck)
        {
            if (!bypassEnhancedFirstSpawnCooldown && lastFireTimes.ContainsKey(prefabKey))
            {
                if (GameStateManager.PauseSafeTime - lastFireTimes[prefabKey] < effectiveCooldown)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            lastFireTimes[prefabKey] = GameStateManager.PauseSafeTime;
        }

        StartCoroutine(LifetimeRoutine(lifetimeSeconds));

        if ((enhancedVariant == 3 || isStackedVariant23) && currentVariant3DamagePerTick > 0f && variant3DamageInterval > 0f)
        {
            StartCoroutine(Variant3DamageRoutine(variant3ChargeDamageDelay, variant3DamageInterval));
        }
    }

    private IEnumerator LifetimeRoutine(float lifetime)
    {
        float fadeTime = Mathf.Max(0f, FadeAwayDuration);
        float waitTime = Mathf.Max(0f, lifetime - fadeTime);

        if (waitTime > 0f)
        {
            yield return GameStateManager.WaitForPauseSafeSeconds(waitTime);
        }

        if (fadeTime > 0f)
        {
            yield return StartCoroutine(FadeOutVisuals(fadeTime));
        }

        if (enhancedVariant == 1)
        {
            PerformVariant1Explosion();
        }

        Destroy(gameObject);
    }

    private IEnumerator FadeOutVisuals(float duration)
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers == null || renderers.Length == 0 || duration <= 0f)
        {
            yield break;
        }

        // Cache starting colors
        Color[] startColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            startColors[i] = renderers[i] != null ? renderers[i].color : Color.white;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GameStateManager.GetPauseSafeDeltaTime();
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = 1f - t;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = startColors[i];
                c.a = alpha * startColors[i].a;
                renderers[i].color = c;
            }

            yield return null;
        }
    }

    private IEnumerator FadeInVisuals(SpriteRenderer[] renderers, float duration)
    {
        if (renderers == null || renderers.Length == 0 || duration <= 0f)
        {
            yield break;
        }

        // Cache starting colors and force alpha to 0 at the beginning.
        Color[] targetColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                targetColors[i] = renderers[i].color;
                Color c = targetColors[i];
                c.a = 0f;
                renderers[i].color = c;
            }
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GameStateManager.GetPauseSafeDeltaTime();
            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color target = targetColors[i];
                Color c = target;
                c.a = target.a * t;
                renderers[i].color = c;
            }

            yield return null;
        }

        // Ensure final colors are restored exactly.
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].color = targetColors[i];
            }
        }
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

            if (enemyObject != null)
            {
                EnemyHealth enemyHealth = enemyObject.GetComponent<EnemyHealth>() ?? enemyObject.GetComponentInParent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Fire);
                }
            }

            DealAoeDamage(damageable, finalDamage, hitPoint, hitNormal);
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

            // Keep the "bosses aren't pulled" behaviour to prevent teleport sliding.
            EnemyCardTag tag = hit.GetComponent<EnemyCardTag>() ?? hit.GetComponentInParent<EnemyCardTag>();
            if (tag != null && tag.rarity == CardRarity.Boss)
            {
                continue;
            }

            float mass = Mathf.Max(0.01f, enemyRb.mass);
            StatusController status = enemyRb.GetComponent<StatusController>() ?? enemyRb.GetComponentInParent<StatusController>();
            if (status != null)
            {
                mass = status.GetEnemyEffectiveMass(mass);
            }

            float denominator = 1f + Mathf.Max(0f, resistancePerMass) * mass;
            float forceMagnitude = pullStrength / denominator;
            if (forceMagnitude <= 0f) continue;

            Vector2 toCenter = center - enemyRb.position;
            if (toCenter.sqrMagnitude < 0.0001f) continue;
            Vector2 dir = toCenter.normalized;

            enemyRb.AddForce(dir * forceMagnitude, ForceMode2D.Force);

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

    public void ApplyInstantModifiers(CardModifierStats modifiers)
    {
        ProjectileCards card = ProjectileCardModifiers.Instance != null
            ? ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject)
            : null;
        UpdateEnhancedVariantAndStackState(card);

        float lifetimeBase = baseLifetimeSeconds;
        if (enhancedVariant == 1)
        {
            lifetimeBase = variant1Lifetime;
        }
        float newLifetime = lifetimeBase + modifiers.lifetimeIncrease;
        lifetimeSeconds = newLifetime;

        float radiusAfterSize = basePullRadius;
        if (modifiers.sizeMultiplier != 1f)
        {
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

        UpdateVariant3DamageFromModifiers(modifiers);
        UpdatePullRadiusCircleScale();
    }

    private void UpdatePullRadiusCircleScale()
    {
        Vector3 rootScale = transform.localScale;
        float scaleFactor = collapseScalePerPullRadius > 0f ? collapseScalePerPullRadius : 0.5f;
        rootScale.x = pullRadius * scaleFactor;
        rootScale.y = pullRadius * scaleFactor;
        transform.localScale = rootScale;

        pullOffset = new Vector2(pullOffset.x, rootScale.y);

        if (pullRadiusCircle == null) return;

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

        if ((enhancedVariant == 3 || isStackedVariant23) && modifiers != null)
        {
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

        int storedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card);
        enhancedVariant = storedVariant;

        bool hasVariant2 = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 2);
        bool hasVariant3 = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 3);

        if (hasVariant2 && hasVariant3)
        {
            isStackedVariant23 = true;
            enhancedVariant = 2;
        }
    }

    private System.Collections.Generic.List<Vector2> BuildOrderedPolygon(System.Collections.Generic.List<Vector2> points)
    {
        if (points == null)
        {
            return null;
        }

        if (points.Count < 3)
        {
            return points;
        }

        System.Collections.Generic.List<Vector2> unique = new System.Collections.Generic.List<Vector2>(points.Count);
        const float epsSqr = 0.0001f * 0.0001f;

        for (int i = 0; i < points.Count; i++)
        {
            Vector2 p = points[i];
            bool dup = false;
            for (int j = 0; j < unique.Count; j++)
            {
                if ((unique[j] - p).sqrMagnitude <= epsSqr)
                {
                    dup = true;
                    break;
                }
            }
            if (!dup)
            {
                unique.Add(p);
            }
        }

        if (unique.Count < 3)
        {
            return unique;
        }

        Vector2 centroid = Vector2.zero;
        for (int i = 0; i < unique.Count; i++)
        {
            centroid += unique[i];
        }
        centroid /= unique.Count;

        unique.Sort((p1, p2) =>
        {
            float a1 = Mathf.Atan2(p1.y - centroid.y, p1.x - centroid.x);
            float a2 = Mathf.Atan2(p2.y - centroid.y, p2.x - centroid.x);
            return a1.CompareTo(a2);
        });

        return unique;
    }

    private bool IsPointInsidePolygon(Vector2 point, System.Collections.Generic.List<Vector2> polygon)
    {
        int count = polygon != null ? polygon.Count : 0;
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
        Vector3 center = transform.position + (Vector3)pullOffset;
        Gizmos.color = new Color(0.4f, 0f, 1f, 0.25f);
        Gizmos.DrawSphere(center, pullRadius);

        // Draw spawn area points A-F using the same color + label style as FireMine.
        GameObject pointAObj = string.IsNullOrEmpty(pointATag) ? null : GameObject.FindGameObjectWithTag(pointATag);
        GameObject pointBObj = string.IsNullOrEmpty(pointBTag) ? null : GameObject.FindGameObjectWithTag(pointBTag);
        GameObject pointCObj = string.IsNullOrEmpty(pointCTag) ? null : GameObject.FindGameObjectWithTag(pointCTag);
        GameObject pointDObj = string.IsNullOrEmpty(pointDTag) ? null : GameObject.FindGameObjectWithTag(pointDTag);
        GameObject pointEObj = string.IsNullOrEmpty(pointETag) ? null : GameObject.FindGameObjectWithTag(pointETag);
        GameObject pointFObj = string.IsNullOrEmpty(pointFTag) ? null : GameObject.FindGameObjectWithTag(pointFTag);

        System.Collections.Generic.List<Vector2> spawnPoly2D = new System.Collections.Generic.List<Vector2>(6);
        Vector3 posA = Vector3.zero;
        Vector3 posB = Vector3.zero;
        Vector3 posC = Vector3.zero;
        Vector3 posD = Vector3.zero;
        Vector3 posE = Vector3.zero;
        Vector3 posF = Vector3.zero;

        if (pointAObj != null)
        {
            posA = pointAObj.transform.position;
            spawnPoly2D.Add(posA);
        }
        if (pointBObj != null)
        {
            posB = pointBObj.transform.position;
            spawnPoly2D.Add(posB);
        }
        if (pointCObj != null)
        {
            posC = pointCObj.transform.position;
            spawnPoly2D.Add(posC);
        }
        if (pointDObj != null)
        {
            posD = pointDObj.transform.position;
            spawnPoly2D.Add(posD);
        }
        if (pointEObj != null)
        {
            posE = pointEObj.transform.position;
            spawnPoly2D.Add(posE);
        }
        if (pointFObj != null)
        {
            posF = pointFObj.transform.position;
            spawnPoly2D.Add(posF);
        }

        if (spawnPoly2D.Count >= 3)
        {
            spawnPoly2D = BuildOrderedPolygon(spawnPoly2D);

            Gizmos.color = Color.green;
            for (int i = 0; i < spawnPoly2D.Count; i++)
            {
                Gizmos.DrawSphere(spawnPoly2D[i], 0.3f);
            }

            Gizmos.color = Color.cyan;
            for (int i = 0; i < spawnPoly2D.Count; i++)
            {
                Vector3 from = spawnPoly2D[i];
                Vector3 to = spawnPoly2D[(i + 1) % spawnPoly2D.Count];
                Gizmos.DrawLine(from, to);
            }

            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            if (pointAObj != null && pointBObj != null && pointCObj != null && pointDObj != null)
            {
                Gizmos.DrawLine(posA, posC);
                Gizmos.DrawLine(posB, posD);
            }

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
                UnityEditor.Handles.Label(posE, "E");
            }
            if (pointFObj != null)
            {
                UnityEditor.Handles.Label(posF, "F");
            }
#endif
        }

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
    }
}