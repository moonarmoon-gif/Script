using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ThunderDisc : MonoBehaviour
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
    [SerializeField] private float manaCost = 10f;
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private ProjectileType projectileType = ProjectileType.ThunderDisc;
    public ProjectileType ProjectileElement => projectileType;

    [Header("Bounce")]
    public float BounceChance = 100f;
    [Range(0f, 100f)] public float ReducedBounceChance = 50f;

    // Instance-based cooldown tracking (per prefab type)
    private static System.Collections.Generic.Dictionary<string, float> lastFireTimes = new System.Collections.Generic.Dictionary<string, float>();
    private string prefabKey;

    public enum SpriteFacing2D { Right = 0, Up = 90, Left = 180, Down = 270 }
    [Header("Rotation")]
    [SerializeField] private SpriteFacing2D spriteFacing = SpriteFacing2D.Right;
    [SerializeField] private float additionalRotationOffsetDeg = 0f;
    [SerializeField] private bool rotateToVelocity = true;
    [SerializeField] private float maxRotationDegreesPerSecond = 1080f;
    [SerializeField] private float minRotateVelocity = 0.01f;
    [SerializeField] private bool keepInitialRotation = false;

    [Header("Impact VFX")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float hitEffectDuration = 1.0f;
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

    [Header("ElectroBall")]
    public bool IsElectroBall = false;

    [Header("Impact Orientation")]
    [SerializeField] private PlayerProjectiles.ImpactOrientationMode impactOrientation = PlayerProjectiles.ImpactOrientationMode.SurfaceNormal;
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

    [Header("Collider Scaling")]
    [Tooltip("Offset for collider size relative to visual size (0 = same as visual, -0.2 = 20% smaller, 0.2 = 20% larger)")]
    [SerializeField] private float colliderSizeOffset = 0f;

    [Header("Damage Cooldown")]
    [Tooltip("Minimum time between damage instances to prevent AOE spam")]
    [SerializeField] private float damageCooldown = 0.1f;

    private Rigidbody2D _rigidbody2D;
    private Collider2D _collider2D;
    private AudioSource _trailSource;
    private Coroutine _fadeOutRoutine;
    private float lastDamageTime = -999f;
    private bool hasHitEnemy = false;
    private bool hasLaunched = false;
    private Camera mainCamera;
    private GameObject player;
    private FavourEffectManager favourEffectManager;
    private PlayerStats cachedPlayerStats;
    private float baseDamageAfterCards;

    private float bounceChanceCurrent = 0f;
    private float bounceChanceMultiplierPerBounce = 0.5f;
    private float currentMoveSpeed = 15f;
    private readonly HashSet<GameObject> bouncedHitEnemies = new HashSet<GameObject>();

    private static readonly Collider2D[] bounceOverlaps = new Collider2D[48];

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _collider2D = GetComponent<Collider2D>();

        if (_rigidbody2D != null)
        {
            _rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        if (_collider2D != null && _collider2D.sharedMaterial != null)
        {
            if (_collider2D.sharedMaterial.bounciness > 0f)
            {
                Debug.LogWarning($"<color=yellow>Projectile {gameObject.name} has bouncy physics material! This will interfere with piercing.</color>");
            }
        }

        EnsureTrailAudioSource();
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
        if (hasLaunched)
        {
            Debug.LogError("<color=red>GetSpawnOffset() called AFTER Launch()! This causes mid-flight repositioning! Offset should be calculated BEFORE Instantiate()!</color>");
            return Vector2.zero;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        if ((angle >= 0f && angle <= 90f) || (angle >= 270f && angle <= 360f))
        {
            if (angle >= 0f && angle <= 90f)
            {
                float relativeAngle = angle;
                return relativeAngle > 45f ? offsetRightAbove45 : offsetRightBelow45;
            }
            else
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

    public void Launch(Vector2 direction, Collider2D colliderToIgnore, PlayerMana playerMana = null, bool skipCooldownCheck = false)
    {
        hasLaunched = true;

        if (_rigidbody2D == null)
        {
            Debug.LogWarning("FireBolt missing Rigidbody2D.");
            Destroy(gameObject);
            return;
        }

        ProjectileCards card = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
        CardModifierStats modifiers = new CardModifierStats();

        if (card != null)
        {
            modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
            Debug.Log($"<color=cyan>PlayerProjectiles using modifiers from {card.cardName}</color>");
        }

        float finalSpeed = speed + modifiers.speedIncrease;
        float finalLifetime = lifetimeSeconds + modifiers.lifetimeIncrease;
        float finalCooldown = Mathf.Max(0.1f, cooldown * (1f - modifiers.cooldownReductionPercent / 100f));
        int finalManaCost = Mathf.Max(0, Mathf.CeilToInt(manaCost * (1f - modifiers.manaCostReduction)));
        float finalDamage = damage + modifiers.damageFlat;

        if (modifiers.sizeMultiplier != 1f)
        {
            transform.localScale *= modifiers.sizeMultiplier;
            ColliderScaler.ScaleCollider(_collider2D, modifiers.sizeMultiplier, colliderSizeOffset);
        }

        Debug.Log($"<color=cyan>PlayerProjectile Modifiers Applied: Speed=+{modifiers.speedIncrease:F2}, Size={modifiers.sizeMultiplier:F2}x, DamageFlat=+{modifiers.damageFlat:F1}, Lifetime=+{modifiers.lifetimeIncrease:F2}s</color>");

        cachedPlayerStats = null;
        if (colliderToIgnore != null)
        {
            player = colliderToIgnore.gameObject;
            if (player != null)
            {
                favourEffectManager = player.GetComponent<FavourEffectManager>();
            }
            cachedPlayerStats = colliderToIgnore.GetComponent<PlayerStats>();
        }

        damage = finalDamage;
        baseDamageAfterCards = damage;

        prefabKey = $"PlayerProjectile_{projectileType}";
        bool useInternalCooldown = (card == null);

        if (!skipCooldownCheck)
        {
            if (useInternalCooldown)
            {
                if (lastFireTimes.ContainsKey(prefabKey))
                {
                    if (GameStateManager.PauseSafeTime - lastFireTimes[prefabKey] < finalCooldown)
                    {
                        Debug.Log($"FireBolt ({prefabKey}) on cooldown");
                        Destroy(gameObject);
                        return;
                    }
                }
            }

            if (playerMana != null && !playerMana.Spend(finalManaCost))
            {
                Debug.Log($"Not enough mana for FireBolt (cost: {finalManaCost})");
                Destroy(gameObject);
                return;
            }

            if (useInternalCooldown)
            {
                lastFireTimes[prefabKey] = GameStateManager.PauseSafeTime;
            }
        }
        else
        {
            Debug.Log($"<color=gold>PlayerProjectiles: Skipping cooldown/mana check (multi-projectile spawn)</color>");
        }

        PredeterminedStatusRoll pre = GetComponent<PredeterminedStatusRoll>();
        if (pre == null)
        {
            pre = gameObject.AddComponent<PredeterminedStatusRoll>();
        }
        pre.EnsureRolled();

        float effectiveBounceChance = Mathf.Max(0f, BounceChance);
        effectiveBounceChance += Mathf.Max(0f, modifiers.specialChanceBonusPercent);
        bounceChanceCurrent = Mathf.Clamp(effectiveBounceChance, 0f, 100f);

        float mult = 1f - Mathf.Clamp01(ReducedBounceChance / 100f);
        bounceChanceMultiplierPerBounce = Mathf.Clamp01(mult);

        bouncedHitEnemies.Clear();
        hasHitEnemy = false;

        if (_collider2D != null && colliderToIgnore != null)
        {
            Physics2D.IgnoreCollision(_collider2D, colliderToIgnore, true);
        }

        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        currentMoveSpeed = finalSpeed;
        _rigidbody2D.velocity = dir * finalSpeed;

        if (!keepInitialRotation)
        {
            float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float facingCorrection = (int)spriteFacing;
            float finalAngle = baseAngle + facingCorrection + additionalRotationOffsetDeg;
            transform.rotation = Quaternion.Euler(0f, 0f, finalAngle);
        }

        PauseSafeSelfDestruct.Schedule(gameObject, finalLifetime);
        StartTrailSfx();
    }

    public float GetProjectileSpeed()
    {
        return speed;
    }

    private bool IsClickHitboxCollider(Collider2D col)
    {
        if (col == null)
        {
            return false;
        }

        Transform t = col.transform;
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

    private bool TryBounce(GameObject excludeEnemy)
    {
        if (bounceChanceCurrent <= 0f)
        {
            return false;
        }

        float roll = Random.Range(0f, 100f);
        if (roll > bounceChanceCurrent)
        {
            return false;
        }

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
        Vector3 camPos3 = mainCamera.transform.position;

        Vector2 overlapCenter = new Vector2(camPos3.x, camPos3.y);
        Vector2 overlapSize = new Vector2(halfWidth * 2f, halfHeight * 2f);

        int count = Physics2D.OverlapBoxNonAlloc(overlapCenter, overlapSize, 0f, bounceOverlaps, enemyLayer);
        if (count <= 0)
        {
            return false;
        }

        float bestDistSq = float.MaxValue;
        Vector2 bestDir = Vector2.zero;
        bool found = false;

        Vector2 origin = transform.position;

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = bounceOverlaps[i];
            if (hit == null) continue;

            IDamageable damageable = hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive) continue;

            Component damageableComponent = damageable as Component;
            GameObject enemyObj = damageableComponent != null ? damageableComponent.gameObject : hit.gameObject;
            if (enemyObj == null) continue;

            if (excludeEnemy != null && enemyObj == excludeEnemy) continue;
            if (bouncedHitEnemies.Contains(enemyObj)) continue;

            Vector2 pos = hit.bounds.center;

            Vector3 viewportPos = mainCamera.WorldToViewportPoint(pos);
            if (viewportPos.z <= 0f) continue;
            if (viewportPos.x < 0f || viewportPos.x > 1f) continue;
            if (viewportPos.y < 0f || viewportPos.y > 1f) continue;

            Vector2 delta = pos - origin;
            float distSq = delta.sqrMagnitude;
            if (distSq < 0.0001f) continue;

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestDir = delta.normalized;
                found = true;
            }
        }

        for (int i = 0; i < count; i++)
        {
            bounceOverlaps[i] = null;
        }

        if (!found)
        {
            return false;
        }

        if (_rigidbody2D != null)
        {
            _rigidbody2D.velocity = bestDir * currentMoveSpeed;
        }

        if (!keepInitialRotation)
        {
            float baseAngle = Mathf.Atan2(bestDir.y, bestDir.x) * Mathf.Rad2Deg;
            float facingCorrection = (int)spriteFacing;
            float finalAngle = baseAngle + facingCorrection + additionalRotationOffsetDeg;
            transform.rotation = Quaternion.Euler(0f, 0f, finalAngle);
        }

        bounceChanceCurrent *= bounceChanceMultiplierPerBounce;
        bounceChanceCurrent = Mathf.Clamp(bounceChanceCurrent, 0f, 100f);

        hasHitEnemy = false;
        lastDamageTime = GameStateManager.PauseSafeTime;

        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsClickHitboxCollider(other))
        {
            return;
        }

        ProjectilePiercing piercing = GetComponent<ProjectilePiercing>();
        bool allowPierce = piercing != null && piercing.pierceCount > 0;

        if (((1 << other.gameObject.layer) & enemyLayer) != 0)
        {
            if (!allowPierce && hasHitEnemy)
            {
                return;
            }

            if (allowPierce && piercing.HasHitEnemy(other.gameObject))
            {
                return;
            }

            IDamageable damageable = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();

            if (damageable != null && damageable.IsAlive)
            {
                if (GameStateManager.PauseSafeTime - lastDamageTime < damageCooldown)
                {
                    return;
                }

                if (!OffscreenDamageChecker.CanTakeDamage(other.transform.position))
                {
                    Debug.Log($"<color=yellow>PlayerProjectile: Enemy {other.gameObject.name} too far offscreen, no damage dealt</color>");
                    return;
                }

                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Vector3 hitNormal = (transform.position - other.transform.position).normalized;
                Vector3 effectBasePosition = hitPoint;
                Collider2D enemyCollider = other;
                if (enemyCollider != null)
                {
                    effectBasePosition = enemyCollider.bounds.center;
                }

                float baseForHit = baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
                float finalDamage = baseForHit;

                Component damageableComponent = damageable as Component;
                GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : other.gameObject;

                if (enemyObject != null && bouncedHitEnemies.Contains(enemyObject))
                {
                    return;
                }

                if (cachedPlayerStats != null)
                {
                    finalDamage = PlayerDamageHelper.ComputeProjectileDamage(cachedPlayerStats, enemyObject, baseForHit, gameObject);
                }

                EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>() ?? other.GetComponentInParent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    DamageNumberManager.DamageType damageType = projectileType == ProjectileType.Fire
                        ? DamageNumberManager.DamageType.Fire
                        : (projectileType == ProjectileType.Thunder || projectileType == ProjectileType.ThunderDisc
                            ? DamageNumberManager.DamageType.Thunder
                            : DamageNumberManager.DamageType.Ice);
                    enemyHealth.SetLastIncomingDamageType(damageType);
                }

                damageable.TakeDamage(finalDamage, hitPoint, hitNormal);

                StatusController.TryApplyBurnFromProjectile(gameObject, other.gameObject, hitPoint, finalDamage);

                SlowEffect slowEffect = GetComponent<SlowEffect>();
                if (slowEffect != null)
                {
                    slowEffect.TryApplySlow(other.gameObject, hitPoint);
                }

                StaticEffect staticEffect = GetComponent<StaticEffect>();
                if (staticEffect != null)
                {
                    staticEffect.TryApplyStatic(other.gameObject, hitPoint);
                }

                TryPlayHitEffect(effectBasePosition, enemyObject);

                if (enemyObject != null)
                {
                    bouncedHitEnemies.Add(enemyObject);
                }

                if (allowPierce)
                {
                    bool shouldContinue = piercing.OnEnemyHit(other.gameObject);
                    if (shouldContinue)
                    {
                        Debug.Log($"<color=green>Projectile piercing through enemy. Remaining: {piercing.GetRemainingPierces()}</color>");
                        return;
                    }
                }

                if (TryBounce(enemyObject))
                {
                    return;
                }

                hasHitEnemy = true;
                lastDamageTime = GameStateManager.PauseSafeTime;
                HandleImpact(hitPoint, hitNormal, other.transform);
                Destroy(gameObject);
                return;
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        ProjectilePiercing piercing = GetComponent<ProjectilePiercing>();
        if (piercing != null)
        {
            return;
        }

        if (((1 << collision.gameObject.layer) & enemyLayer) != 0)
        {
            if (hasHitEnemy)
            {
                Debug.Log($"<color=yellow>Projectile already hit an enemy, ignoring {collision.gameObject.name}</color>");
                return;
            }

            IDamageable damageable = collision.gameObject.GetComponent<IDamageable>() ??
                                     collision.gameObject.GetComponentInParent<IDamageable>();

            if (damageable != null && damageable.IsAlive)
            {
                if (GameStateManager.PauseSafeTime - lastDamageTime < damageCooldown)
                {
                    return;
                }

                Vector3 hitPoint = collision.GetContact(0).point;
                Vector3 hitNormal = collision.GetContact(0).normal;

                float baseForHit = baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
                float finalDamage = baseForHit;

                Component damageableComponent = damageable as Component;
                GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : collision.gameObject;

                if (enemyObject != null && bouncedHitEnemies.Contains(enemyObject))
                {
                    return;
                }

                if (cachedPlayerStats != null)
                {
                    finalDamage = PlayerDamageHelper.ComputeProjectileDamage(cachedPlayerStats, enemyObject, baseForHit, gameObject);
                }

                EnemyHealth enemyHealth = collision.gameObject.GetComponent<EnemyHealth>() ?? collision.gameObject.GetComponentInParent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    DamageNumberManager.DamageType damageType = projectileType == ProjectileType.Fire
                        ? DamageNumberManager.DamageType.Fire
                        : (projectileType == ProjectileType.Thunder || projectileType == ProjectileType.ThunderDisc
                            ? DamageNumberManager.DamageType.Thunder
                            : DamageNumberManager.DamageType.Ice);
                    enemyHealth.SetLastIncomingDamageType(damageType);
                }

                damageable.TakeDamage(finalDamage, hitPoint, hitNormal);
                lastDamageTime = GameStateManager.PauseSafeTime;

                StatusController.TryApplyBurnFromProjectile(gameObject, collision.gameObject, hitPoint, finalDamage);

                SlowEffect slowEffect = GetComponent<SlowEffect>();
                if (slowEffect != null)
                {
                    slowEffect.TryApplySlow(collision.gameObject, hitPoint);
                }

                StaticEffect staticEffect = GetComponent<StaticEffect>();
                if (staticEffect != null)
                {
                    staticEffect.TryApplyStatic(collision.gameObject, hitPoint);
                }

                TryPlayHitEffect(hitPoint, enemyObject);

                if (enemyObject != null)
                {
                    bouncedHitEnemies.Add(enemyObject);
                }

                if (TryBounce(enemyObject))
                {
                    return;
                }

                hasHitEnemy = true;
                HandleImpact(hitPoint, hitNormal, collision.collider.transform);
                Destroy(gameObject);
                return;
            }
            else if (damageable != null && !damageable.IsAlive)
            {
                Debug.Log($"<color=yellow>FireBolt hit dead enemy {collision.gameObject.name}, destroying</color>");
                Destroy(gameObject);
                return;
            }
        }

        if (collision.contacts.Length > 0)
        {
            Vector3 hitPoint = collision.contacts[0].point;
            Vector3 hitNormal = collision.contacts[0].normal;

            TryPlayHitEffect(hitPoint, null);
            HandleImpact(hitPoint, hitNormal, collision.collider.transform);
        }
        Destroy(gameObject);
    }

    private void HandleImpact(Vector3 point, Vector3 normal, Transform hitParent)
    {
        if (impactClip != null)
        {
            AudioSource.PlayClipAtPoint(impactClip, point, impactVolume);
        }

        if (hitEffectPrefab == null)
        {
            return;
        }

        if (hitEffectTimingAdjustment > 0f)
        {
            return;
        }

        Quaternion rotation = ComputeImpactRotation(point, normal);

        Vector3 spawnPos = point;
        spawnPos += (Vector3)GetHitEffectOffsetForImpact(normal);
        spawnPos.z += impactZOffset;

        GameObject impactFx = Instantiate(hitEffectPrefab, spawnPos, rotation);

        if (impactFx != null)
        {
            if (!Mathf.Approximately(hitEffectSizeMultiplier, 1f))
            {
                impactFx.transform.localScale *= hitEffectSizeMultiplier;
            }

            if (parentImpactToHit && hitParent != null)
            {
                impactFx.transform.SetParent(hitParent, true);
            }

            Destroy(impactFx, hitEffectDuration);
        }
    }

    private Quaternion ComputeImpactRotation(Vector3 point, Vector3 normal)
    {
        Vector3 forward = Vector3.forward;

        Vector2 v = _rigidbody2D != null ? _rigidbody2D.velocity : Vector2.right;
        Vector2 dir = v.sqrMagnitude > 0.0001f ? v.normalized : Vector2.right;

        Vector3 up = Vector3.up;

        if (impactOrientation == PlayerProjectiles.ImpactOrientationMode.SurfaceNormal)
        {
            up = normal;
        }
        else if (impactOrientation == PlayerProjectiles.ImpactOrientationMode.Opposite)
        {
            up = -normal;
        }
        else if (impactOrientation == PlayerProjectiles.ImpactOrientationMode.ProjectileVelocity)
        {
            up = dir;
        }
        else
        {
            up = Vector3.up;
        }

        float angle = Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg;
        float facingCorrection = (int)hitEffectSpriteFacing;
        float desired = angle + facingCorrection + hitEffectAdditionalRotationOffsetDeg;

        return Quaternion.Euler(0f, 0f, desired);
    }

    private Vector2 GetHitEffectOffsetForImpact(Vector3 normal)
    {
        Vector2 velocity = _rigidbody2D != null ? _rigidbody2D.velocity : Vector2.right;
        Vector2 dir = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : Vector2.right;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        if ((angle >= 0f && angle <= 90f) || (angle >= 270f && angle <= 360f))
        {
            if (angle >= 0f && angle <= 90f)
            {
                float relativeAngle = angle;
                return relativeAngle > 45f ? hitEffectOffsetRightAbove45 : hitEffectOffsetRightBelow45;
            }
            else
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
            else if (angle >= 180f && angle <= 270f)
            {
                float relativeAngle = angle - 180f;
                return relativeAngle > 45f ? hitEffectOffsetLeftAbove45 : hitEffectOffsetLeftBelow45;
            }
        }

        return hitEffectOffset;
    }

    private void TryPlayHitEffect(Vector3 basePosition, GameObject enemyObject)
    {
        if (hitEffectPrefab == null)
        {
            return;
        }

        if (hitEffectTimingAdjustment > 0f)
        {
            StartCoroutine(SpawnDelayedHitEffect(basePosition, enemyObject, hitEffectTimingAdjustment));
            return;
        }

        Vector2 velocity = _rigidbody2D != null ? _rigidbody2D.velocity : Vector2.right;
        Vector2 dir = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : Vector2.right;

        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float facingCorrection = (int)hitEffectSpriteFacing;
        float desired = baseAngle + facingCorrection + hitEffectAdditionalRotationOffsetDeg;
        Quaternion rotation = Quaternion.Euler(0f, 0f, desired);

        Vector3 spawnPos = basePosition;
        spawnPos += (Vector3)GetHitEffectOffsetForImpact(Vector3.up);
        spawnPos.z += impactZOffset;

        GameObject effect = Instantiate(hitEffectPrefab, spawnPos, rotation);
        if (effect != null)
        {
            if (!Mathf.Approximately(hitEffectSizeMultiplier, 1f))
            {
                effect.transform.localScale *= hitEffectSizeMultiplier;
            }

            Destroy(effect, hitEffectDuration);
        }
    }

    private IEnumerator SpawnDelayedHitEffect(Vector3 basePosition, GameObject enemyObject, float delay)
    {
        yield return new WaitForSeconds(delay);
        TryPlayHitEffect(basePosition, enemyObject);
    }

    private void EnsureTrailAudioSource()
    {
        _trailSource = GetComponent<AudioSource>();
        if (_trailSource == null)
        {
            _trailSource = gameObject.AddComponent<AudioSource>();
        }

        _trailSource.playOnAwake = false;
        _trailSource.loop = trailLoop;
        _trailSource.spatialBlend = trailSpatialBlend;
        _trailSource.dopplerLevel = trailDopplerLevel;
    }

    private void StartTrailSfx()
    {
        if (_trailSource == null)
        {
            EnsureTrailAudioSource();
        }

        if (_trailSource == null)
        {
            return;
        }

        if (trailClip == null)
        {
            return;
        }

        _trailSource.clip = trailClip;
        _trailSource.volume = trailVolume;
        _trailSource.pitch = trailPitch;
        _trailSource.loop = trailLoop;
        _trailSource.spatialBlend = trailSpatialBlend;
        _trailSource.dopplerLevel = trailDopplerLevel;

        _trailSource.Play();
    }

    private void StopTrailSfx(bool immediate)
    {
        if (_trailSource == null)
        {
            return;
        }

        if (immediate)
        {
            _trailSource.Stop();
            return;
        }

        if (_fadeOutRoutine != null)
        {
            StopCoroutine(_fadeOutRoutine);
        }

        _fadeOutRoutine = StartCoroutine(FadeOutTrailAudio());
    }

    private IEnumerator FadeOutTrailAudio()
    {
        if (_trailSource == null)
        {
            yield break;
        }

        float startVolume = _trailSource.volume;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, trailFadeOutSeconds);

        while (elapsed < duration)
        {
            elapsed += GameStateManager.GetPauseSafeDeltaTime();
            float t = Mathf.Clamp01(elapsed / duration);
            _trailSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        _trailSource.Stop();
        _trailSource.volume = startVolume;
    }
}
