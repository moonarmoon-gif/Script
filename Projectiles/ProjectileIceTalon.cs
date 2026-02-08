using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ProjectileIceTalon : MonoBehaviour, IInstantModifiable
{
    [Header("Motion")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float lifetimeSeconds = 5f;

    [Header("Offscreen Destruction")]
    [Tooltip("Bonus destroy boundary size (world units). If projectile goes outside the camera bounds plus this value, it is destroyed immediately.")]
    public float DestroyCameraOffset = 0f;

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
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private ProjectileType projectileType = ProjectileType.Ice; // ICE TALON - Always Ice type

    [Header("Enhanced Variant 3 - Slow Chance")]
    [Tooltip("Slow chance for Enhanced Variant 3 (0 = 0%, 0.5 = 50%, 1 = 100%). Applied to SlowEffect.slowChance.")]
    [Range(0f, 1f)]
    [SerializeField] private float variant3SlowEffectChance = 0.5f;

    [Header("Y-Axis Enemy Ignore (Left Side)")]
    [Tooltip("IceTalon cannot damage enemies on the LEFT side of the screen while its own Y position is below this value.")]
    public float YaxisIgnoreLeftEnemies = -7f;

    [Header("Enhanced Variant 1 - Multi-shot & Pierce")]
    [Tooltip("Additional pierce count granted by Enhanced Variant 1 (multi-pierce).")]
    public int enhancedPierceBonus = 0;

    [Tooltip("Additional projectile count granted by Enhanced Variant 1 (multi-shot). Used by ProjectileSpawner.")]
    public int enhancedProjectileCountBonus = 0;

    [Header("Enhanced Variant 2 - Speed & Pierce")]
    [Tooltip("Additional pierce count granted by Enhanced Variant 2.")]
    public int enhancedVariant2PierceBonus = 0;

    [Tooltip("Additional speed granted by Enhanced Variant 2.")]
    public float enhancedVariant2SpeedBonus = 0f;

    [Tooltip("Optional override cooldown for Variant 2 when it is active. If > 0, Launch will use this instead of the card's runtimeSpawnInterval.")]
    public float variant2BaseCooldown = 0f;

    [Header("Spread Settings")]
    [Tooltip("Minimum angular separation (degrees) between Talon projectiles when using custom angles and multi-shot.")]
    public float minAngleSeparation = 0f;

    private static System.Collections.Generic.Dictionary<string, float> lastFireTimes = new System.Collections.Generic.Dictionary<string, float>();
    private string prefabKey;

    private float baseSpeed;
    private float baseLifetime;
    private float baseDamage;
    private Vector3 baseScale;

    public enum SpriteFacing2D { Right = 0, Up = 90, Left = 180, Down = 270 }

    [Header("Rotation")]
    [SerializeField] private SpriteFacing2D spriteFacing = SpriteFacing2D.Right;
    [SerializeField] private float additionalRotationOffsetDeg = 0f;
    [SerializeField] private bool rotateToVelocity = true;
    [SerializeField] private float maxRotationDegreesPerSecond = 1080f;
    [SerializeField] private float minRotateVelocity = 0.01f;
    [SerializeField] private bool keepInitialRotation = false;

    [Header("Hit Effect")]
    [Tooltip("0 = spawn at enemy collider center, 1 = spawn at actual impact point (Collider2D.ClosestPoint).")]
    [Range(0f, 1f)]
    [SerializeField] private float hitEffectSpawnBias = 1f;
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

    [Header("Trail Effect")]
    public GameObject TrailPrefab;
    public GameObject Trail1Position;
    public GameObject Trail2Position;
    public float TrailEffectTime;
    public float TrailEffectInterval;
    public float TrailSpeed = 1f;

    private Rigidbody2D _rigidbody2D;
    private Collider2D _collider2D;
    private AudioSource _trailSource;
    private Coroutine _fadeOutRoutine;

    private Coroutine _trailEffectRoutine;

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

    private bool hasLaunched = false;
    private Camera mainCamera;

    private float lastDamageTime = -999f;

    [Header("Damage Cooldown")]
    [Tooltip("Minimum time between damage instances")]
    [SerializeField] private float damageCooldown = 0.1f;

    [Header("Collider Scaling")]
    [Tooltip("Offset for collider size relative to visual size (0 = same as visual, -0.2 = 20% smaller, 0.2 = 20% larger)")]
    [SerializeField] private float colliderSizeOffset = 0f;

    private bool isAdditionalProjectile = false;
    private bool hasPlayedBreakEffect = false;

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        baseSpeed = speed; baseLifetime = lifetimeSeconds; baseDamage = damage; baseScale = transform.localScale;

        _collider2D = GetComponent<Collider2D>();
        if (_collider2D == null)
        {
            _collider2D = GetComponentInChildren<Collider2D>();
        }

        Collider2D[] allColliders = GetComponentsInChildren<Collider2D>();
        foreach (Collider2D col in allColliders)
        {
            if (!col.isTrigger)
            {
                col.isTrigger = true;
            }
        }

        if (_rigidbody2D != null)
        {
            _rigidbody2D.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rigidbody2D.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rigidbody2D.gravityScale = 0f;
        }

        EnsureTrailAudioSource();
    }

    // RESTORED: ProjectileSpawner expects this to exist.
    public void SetDirection(Vector2 direction)
    {
        initialDirection = direction;
        directionSet = true;
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
        StopTrailEffects();
        StopTrailSfx(true);
    }

    private void Update()
    {
        if (hasLaunched && IsOutsideCameraBounds())
        {
            Destroy(gameObject);
            return;
        }

        if (keepInitialRotation || _rigidbody2D == null) return;
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

    private bool IsOutsideCameraBounds()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            return false;
        }

        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;

        Vector3 camPos = mainCamera.transform.position;
        Vector3 pos = transform.position;

        float offset = Mathf.Max(0f, DestroyCameraOffset);

        float left = camPos.x - halfWidth - offset;
        float right = camPos.x + halfWidth + offset;
        float bottom = camPos.y - halfHeight - offset;
        float top = camPos.y + halfHeight + offset;

        return pos.x < left || pos.x > right || pos.y < bottom || pos.y > top;
    }

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

    // NOTE: playerMana is intentionally ignored (mana removed).
    public void Launch(Vector2 direction, Collider2D colliderToIgnore, PlayerMana playerMana = null, bool skipCooldownCheck = false)
    {
        hasLaunched = true;

        if (_rigidbody2D == null)
        {
            Debug.LogWarning("ProjectileTalon missing Rigidbody2D.");
            Destroy(gameObject);
            return;
        }

        ProjectileCards card = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
        CardModifierStats modifiers = new CardModifierStats();

        if (card != null)
        {
            modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
        }

        int enhancedVariant = 0;
        bool hasVariant1 = false;
        bool hasVariant2 = false;
        if (ProjectileCardLevelSystem.Instance != null && card != null)
        {
            enhancedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card);
            hasVariant1 = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 1);
            hasVariant2 = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 2);

            if (enhancedVariant == 3)
            {
                SlowEffect slowEffect = GetComponent<SlowEffect>();
                if (slowEffect != null)
                {
                    // Variant 3 uses an explicit slow chance; interpret the
                    // field as 0-1 and convert to the 0-100% range used by
                    // SlowEffect.
                    slowEffect.slowChance = Mathf.Clamp01(variant3SlowEffectChance) * 100f;
                }
            }
        }

        int enhancedPierceAdd = 0;
        float enhancedSpeedAdd = 0f;

        if (hasVariant1)
        {
            enhancedPierceAdd += enhancedPierceBonus;
        }

        if (hasVariant2)
        {
            enhancedPierceAdd += enhancedVariant2PierceBonus;
            enhancedSpeedAdd += enhancedVariant2SpeedBonus;
        }

        float baseSpeedLocal = speed + modifiers.speedIncrease;
        float finalSpeed = baseSpeedLocal + enhancedSpeedAdd;
        float finalLifetime = lifetimeSeconds + modifiers.lifetimeIncrease;

        float baseCooldown = cooldown;
        if (card != null && card.runtimeSpawnInterval > 0f)
        {
            baseCooldown = card.runtimeSpawnInterval;
        }

        if (hasVariant2 && variant2BaseCooldown > 0f)
        {
            baseCooldown = variant2BaseCooldown;
        }

        if (card != null)
        {
            card.runtimeSpawnInterval = Mathf.Max(0.0001f, baseCooldown);
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

        float finalDamage = (baseDamage + modifiers.damageFlat) * modifiers.damageMultiplier;

        if (modifiers.sizeMultiplier != 1f)
        {
            transform.localScale *= modifiers.sizeMultiplier;
            ColliderScaler.ScaleCollider(_collider2D, modifiers.sizeMultiplier, colliderSizeOffset);
        }

        int totalPierceCount = modifiers.pierceCount + enhancedPierceAdd;

        PlayerStats stats = null;
        if (colliderToIgnore != null)
        {
            stats = colliderToIgnore.GetComponent<PlayerStats>();
        }
        cachedPlayerStats = stats != null ? stats : FindObjectOfType<PlayerStats>();

        baseDamageAfterCards = finalDamage;

        damage = finalDamage;
        speed = finalSpeed;
        lifetimeSeconds = finalLifetime;

        bool bypassEnhancedFirstSpawnCooldown = false;
        if (!skipCooldownCheck && card != null && card.applyEnhancedFirstSpawnReduction && card.pendingEnhancedFirstSpawn)
        {
            if (card.projectileSystem == ProjectileCards.ProjectileSystemType.Passive)
            {
                bypassEnhancedFirstSpawnCooldown = true;
                card.pendingEnhancedFirstSpawn = false;
            }
        }

        prefabKey = $"ProjectileTalon_{projectileType}";

        if (!skipCooldownCheck)
        {
            if (card == null)
            {
                if (!bypassEnhancedFirstSpawnCooldown && lastFireTimes.ContainsKey(prefabKey))
                {
                    if (GameStateManager.PauseSafeTime - lastFireTimes[prefabKey] < finalCooldown)
                    {
                        Destroy(gameObject);
                        return;
                    }
                }

                lastFireTimes[prefabKey] = GameStateManager.PauseSafeTime;
            }
        }
        else
        {
            isAdditionalProjectile = true;
        }

        if (_collider2D != null && colliderToIgnore != null)
        {
            Physics2D.IgnoreCollision(_collider2D, colliderToIgnore, true);
        }

        Vector2 chosenDir = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : (directionSet && initialDirection.sqrMagnitude > 0.0001f ? initialDirection.normalized : Vector2.right);

        _rigidbody2D.velocity = chosenDir * finalSpeed;

        if (!keepInitialRotation)
        {
            float baseAngle = Mathf.Atan2(chosenDir.y, chosenDir.x) * Mathf.Rad2Deg;
            float facingCorrection = (int)spriteFacing;
            float finalAngle = baseAngle + facingCorrection + additionalRotationOffsetDeg;
            transform.rotation = Quaternion.Euler(0f, 0f, finalAngle);
        }

        ProjectilePiercing piercing = gameObject.GetComponent<ProjectilePiercing>();
        int prefabDefaultPierceCount = 0;

        if (piercing == null)
        {
            piercing = gameObject.AddComponent<ProjectilePiercing>();

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
            prefabDefaultPierceCount = piercing.pierceCount;
        }

        int finalPierceCount = totalPierceCount;
        if (prefabDefaultPierceCount > 0 && totalPierceCount == 0)
        {
            finalPierceCount = prefabDefaultPierceCount;
        }
        else if (prefabDefaultPierceCount > 0 && totalPierceCount > 0)
        {
            finalPierceCount = prefabDefaultPierceCount + totalPierceCount;
        }

        piercing.SetMaxPierces(finalPierceCount);

        PauseSafeSelfDestruct.Schedule(gameObject, finalLifetime);
        StartTrailEffects();
        StartTrailSfx();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsClickHitboxCollider(other))
        {
            return;
        }

        ProjectilePiercing piercing = GetComponentInParent<ProjectilePiercing>();
        if (piercing == null)
        {
            piercing = GetComponent<ProjectilePiercing>();
        }

        bool isEnemyLayer = ((1 << other.gameObject.layer) & enemyLayer) != 0;
        EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>() ?? other.GetComponentInParent<EnemyHealth>();

        GameObject enemyKey = null;
        Transform enemyTransform = null;

        if (enemyHealth != null)
        {
            enemyKey = enemyHealth.gameObject;
            enemyTransform = enemyHealth.transform;
        }
        else if (isEnemyLayer)
        {
            enemyKey = other.gameObject;
            enemyTransform = other.transform;
        }

        if (enemyKey != null)
        {
            if (piercing != null && piercing.HasHitEnemy(enemyKey))
            {
                return;
            }

            Camera cam = Camera.main;
            bool enemyOnLeftSide;
            if (cam != null)
            {
                enemyOnLeftSide = enemyTransform.position.x < cam.transform.position.x;
            }
            else
            {
                enemyOnLeftSide = enemyTransform.position.x < transform.position.x;
            }

            if (enemyOnLeftSide && transform.position.y < YaxisIgnoreLeftEnemies)
            {
                return;
            }

            IDamageable damageable = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
            if (damageable == null && enemyKey != null)
            {
                damageable = enemyKey.GetComponent<IDamageable>() ?? enemyKey.GetComponentInParent<IDamageable>();
            }

            if (damageable == null)
            {
                if (enemyHealth != null)
                {
                    return;
                }

                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Vector3 hitNormal = (transform.position - hitPoint).normalized;

                TryPlayHitEffect(hitPoint);

                HandleImpact(hitPoint, hitNormal, other.transform);
                Destroy(gameObject);
                return;
            }

            if (damageable != null && damageable.IsAlive)
            {
                if (GameStateManager.PauseSafeTime - lastDamageTime < damageCooldown)
                {
                    return;
                }

                if (!OffscreenDamageChecker.CanTakeDamage(enemyTransform.position))
                {
                    return;
                }

                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Vector3 hitNormal = (transform.position - hitPoint).normalized;
                Vector3 effectBasePosition = Vector3.Lerp(other.bounds.center, hitPoint, hitEffectSpawnBias);

                float baseDamageForEnemy = baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
                float finalDamage = baseDamageForEnemy;

                GameObject enemyObject = enemyKey;

                if (cachedPlayerStats != null)
                {
                    finalDamage = PlayerDamageHelper.ComputeProjectileDamage(cachedPlayerStats, enemyObject, baseDamageForEnemy, gameObject);
                }

                if (enemyObject != null)
                {
                    EnemyHealth resolvedEnemyHealth = enemyObject.GetComponent<EnemyHealth>() ?? enemyObject.GetComponentInParent<EnemyHealth>();
                    if (resolvedEnemyHealth != null)
                    {
                        resolvedEnemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Ice);
                    }
                }

                damageable.TakeDamage(finalDamage, hitPoint, hitNormal);

                SlowEffect slowEffect = GetComponent<SlowEffect>();
                if (slowEffect != null)
                {
                    slowEffect.TryApplySlow(enemyKey, hitPoint);
                }

                StaticEffect staticEffect = GetComponent<StaticEffect>();
                if (staticEffect != null)
                {
                    staticEffect.TryApplyStatic(enemyKey, hitPoint);
                }

                lastDamageTime = GameStateManager.PauseSafeTime;

                TryPlayHitEffect(effectBasePosition);

                if (piercing != null)
                {
                    bool shouldContinue = piercing.OnEnemyHit(enemyKey);
                    if (!shouldContinue)
                    {
                        HandleImpact(hitPoint, hitNormal, enemyTransform);
                        Destroy(gameObject);
                        return;
                    }
                }
                else
                {
                    HandleImpact(hitPoint, hitNormal, enemyTransform);
                    Destroy(gameObject);
                    return;
                }
            }
            else if (damageable != null && !damageable.IsAlive)
            {
                if (piercing != null && piercing.GetRemainingPierces() > 0)
                {
                    return;
                }

                Destroy(gameObject);
                return;
            }
        }
        else
        {
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 hitNormal = (transform.position - hitPoint).normalized;

            TryPlayHitEffect(hitPoint);

            HandleImpact(hitPoint, hitNormal, other.transform);
            Destroy(gameObject);
        }
    }

    private bool IsClickHitboxCollider(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        Transform t = other.transform;
        while (t != null)
        {
            if (t.name == "ClickHitbox")
            {
                return true;
            }
            t = t.parent;
        }

        return false;
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
            PauseSafeSelfDestruct.Schedule(fx, hitEffectDuration);
        }
    }

    private IEnumerator SpawnHitEffectDelayed(Vector3 position, float delay)
    {
        yield return GameStateManager.WaitForPauseSafeSeconds(delay);

        if (hitEffectPrefab != null)
        {
            SpawnHitEffectImmediate(position);
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

    private void StartTrailEffects()
    {
        if (TrailPrefab == null)
        {
            return;
        }

        if (Trail1Position == null || Trail2Position == null)
        {
            return;
        }

        if (TrailEffectTime <= 0f || TrailEffectInterval <= 0f)
        {
            return;
        }

        StopTrailEffects();
        _trailEffectRoutine = StartCoroutine(TrailEffectRoutine());
    }

    private void StopTrailEffects()
    {
        if (_trailEffectRoutine != null)
        {
            StopCoroutine(_trailEffectRoutine);
            _trailEffectRoutine = null;
        }
    }

    private IEnumerator TrailEffectRoutine()
    {
        while (true)
        {
            SpawnTrailAt(Trail1Position.transform.position);
            SpawnTrailAt(Trail2Position.transform.position);

            float elapsed = 0f;
            while (elapsed < TrailEffectInterval)
            {
                elapsed += GameStateManager.GetPauseSafeDeltaTime();
                yield return null;
            }
        }
    }

    private void SpawnTrailAt(Vector3 position)
    {
        if (TrailPrefab == null)
        {
            return;
        }

        GameObject trailObj = Instantiate(TrailPrefab, position, transform.rotation);
        if (TrailEffectTime > 0f)
        {
            PauseSafeSelfDestruct.Schedule(trailObj, TrailEffectTime);
        }

        Vector2 oppositeDir = Vector2.zero;
        if (_rigidbody2D != null && _rigidbody2D.velocity.sqrMagnitude > 0.0001f)
        {
            oppositeDir = -_rigidbody2D.velocity.normalized;
        }
        else if (directionSet && initialDirection.sqrMagnitude > 0.0001f)
        {
            oppositeDir = -initialDirection.normalized;
        }

        if (oppositeDir != Vector2.zero && TrailSpeed != 0f)
        {
            TalonTrailMover mover = trailObj.AddComponent<TalonTrailMover>();
            mover.Direction = oppositeDir;
            mover.Speed = TrailSpeed;
        }
    }

    public sealed class TalonTrailMover : MonoBehaviour
    {
        public Vector2 Direction;
        public float Speed;

        private void Update()
        {
            float dt = GameStateManager.GetPauseSafeDeltaTime();
            transform.position += (Vector3)(Direction * Speed * dt);
        }
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
            t += GameStateManager.GetPauseSafeDeltaTime();
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

    public void ApplyInstantModifiers(CardModifierStats mods)
    {
        Debug.Log($"<color=lime>╔ ICETALON ╗</color>");
        float ns = baseSpeed + mods.speedIncrease;
        if (ns != speed)
        {
            speed = ns;
            if (_rigidbody2D != null)
                _rigidbody2D.velocity = _rigidbody2D.velocity.normalized * speed;
            Debug.Log($"<color=lime>Speed:{baseSpeed:F2}+{mods.speedIncrease:F2}={speed:F2}</color>");
        }
        float nl = baseLifetime + mods.lifetimeIncrease;
        if (nl != lifetimeSeconds)
        {
            lifetimeSeconds = nl;
            Debug.Log($"<color=lime>Lifetime:{baseLifetime:F2}+{mods.lifetimeIncrease:F2}={lifetimeSeconds:F2}</color>");
        }
        float nd = (baseDamage + mods.damageFlat) * mods.damageMultiplier;
        if (nd != damage)
        {
            damage = nd;
            baseDamageAfterCards = nd;
            Debug.Log($"<color=lime>Damage:{baseDamage:F2}*{mods.damageMultiplier:F2}x={damage:F2}</color>");
        }
        if (mods.sizeMultiplier != 1f)
        {
            transform.localScale = baseScale * mods.sizeMultiplier;
            Debug.Log($"<color=lime>Size:{baseScale}*{mods.sizeMultiplier:F2}x={transform.localScale}</color>");
        }
        Debug.Log($"<color=lime>╚═══════════════════════════════════════╝</color>");
    }
}