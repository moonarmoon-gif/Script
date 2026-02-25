using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ElectroBall : MonoBehaviour, IInstantModifiable
{
    [Header("Motion")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float lifetimeSeconds = 5f;

    [Header("Rendering")]
    public int NewOrderInLayer = 1;

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

    [Header("Enhanced Variant - Dual Shot Angles")]
    public float LeftMinAngle = 90f;
    public float LeftMaxAngle = 270f;
    public float RightMinAngle = -90f;
    public float RightMaxAngle = 90f;

    public float MinSpawnRange = 0f;
    public float MaxSpawnRange = 0f;

    [Header("Damage Settings")]
    [SerializeField] private float damage = 20f;
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private ProjectileType projectileType = ProjectileType.Thunder;

    [Header("Explosion")]
    public float ExplosionRadius = 1f;
    public Vector2 ExplosionRadiusOffset = Vector2.zero;
    public GameObject ExplosionEffectPrefab;
    public Vector2 ExplosionEffectOffset = Vector2.zero;
    public bool EnableExplosionEffectPrefab = true;
    [FormerlySerializedAs("ExplosionDamageDelay")]
    public float ExplosionDelay = 0.2f;
    public float DetonateAnimationTime = 1f;

    [Header("Enhanced Variant 2 - ThunderBurst")]
    [Range(0f, 100f)]
    public float ThunderBurstDamage = 3f;
    public float ThunderBurstInterval = 0.2f;
    public float ThunderBurstSize = 1f;
    public float ThunderFadeDuration = 0.2f;
    public bool EnableThunderBurstRotation = true;
    public float ThunderBurstRotationDegreesPerSecond = 180f;

    [Header("Enhanced Variant 3 - Static Chance")]
    [Tooltip("Additional static chance (0-100%) granted by Enhanced Variant 3. This is ADDED on top of any existing static chance.")]
    [Range(0f, 100f)]
    public float StaticChanceIncrease = 25f;

    [Header("Collider Scaling")]
    [Tooltip("Offset for collider size relative to visual size (0 = same as visual, -0.2 = 20% smaller, 0.2 = 20% larger)")]
    [SerializeField] private float colliderSizeOffset = 0f;

    public enum SpriteFacing2D { Right = 0, Up = 90, Left = 180, Down = 270 }

    [Header("Rotation")]
    [SerializeField] private SpriteFacing2D spriteFacing = SpriteFacing2D.Right;
    [SerializeField] private float additionalRotationOffsetDeg = 0f;
    [SerializeField] private bool rotateToVelocity = true;
    [SerializeField] private float maxRotationDegreesPerSecond = 1080f;
    [SerializeField] private float minRotateVelocity = 0.01f;
    [SerializeField] private bool keepInitialRotation = false;

    private Rigidbody2D _rigidbody2D;
    private Collider2D _collider2D;
    private Animator _animator;

    private StaticEffect cachedStaticEffect;

    private Vector2 initialDirection;
    private bool directionSet = false;
    private bool hasLaunched = false;
    private bool isDetonating = false;
    private bool hasAppliedNewOrderInLayer = false;
    private bool thunderBurstActive = false;

    private Camera mainCamera;

    private float baseSpeed;
    private float baseLifetime;
    private float baseDamage;
    private float baseExplosionRadius;
    private Vector2 baseExplosionOffset;
    private Vector2 baseExplosionEffectOffset;
    private Vector3 baseScale;

    private float explosionEffectScale = 1f;

    private int enhancedVariantIndex = 0;

    private float baseThunderBurstDamage;
    private float thunderBurstDamageAfterCards;
    private float baseThunderBurstSize;
    private float thunderBurstSizeAfterCards = 1f;
    private float thunderBurstRotationSign = 1f;

    private Transform[] thunderBurstTransforms;
    private Vector3[] thunderBurstBaseLocalScales;
    private Collider2D[] thunderBurstColliders;
    private HashSet<Collider2D> thunderBurstColliderSet;
    private Coroutine thunderBurstRoutine;
    private ContactFilter2D thunderBurstContactFilter;
    private readonly Dictionary<int, float> thunderBurstNextDamageTimeByEnemyId = new Dictionary<int, float>();
    private readonly Collider2D[] thunderBurstHits = new Collider2D[24];

    private PlayerStats cachedPlayerStats;
    private float baseDamageAfterCards;

    private Vector2 cachedDetonationEffectWorldPosition;
    private Vector2 cachedDetonationCenterWorldPosition;
    private bool hasDetonationWorldSnapshot;

    private static Dictionary<string, float> lastFireTimes = new Dictionary<string, float>();
    private string prefabKey;

    public ProjectileType ProjectileElement => projectileType;

    public float GetCurrentDamage()
    {
        return baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
    }

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _collider2D = GetComponent<Collider2D>();
        _animator = GetComponentInChildren<Animator>();

        cachedStaticEffect = GetComponent<StaticEffect>();

        baseSpeed = speed;
        baseLifetime = lifetimeSeconds;
        baseDamage = damage;
        baseExplosionRadius = ExplosionRadius;
        baseExplosionOffset = ExplosionRadiusOffset;
        baseExplosionEffectOffset = ExplosionEffectOffset;
        baseScale = transform.localScale;

        explosionEffectScale = 1f;

        baseThunderBurstDamage = ThunderBurstDamage;
        thunderBurstDamageAfterCards = ThunderBurstDamage;
        baseThunderBurstSize = ThunderBurstSize;
        thunderBurstSizeAfterCards = ThunderBurstSize;

        CacheThunderBurstChildren();
        SetThunderBurstActive(false);

        Collider2D[] allColliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < allColliders.Length; i++)
        {
            if (allColliders[i] != null)
            {
                allColliders[i].isTrigger = true;
            }
        }

        if (_rigidbody2D != null)
        {
            _rigidbody2D.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rigidbody2D.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rigidbody2D.gravityScale = 0f;
        }

        thunderBurstContactFilter = new ContactFilter2D();
        thunderBurstContactFilter.useLayerMask = true;
        thunderBurstContactFilter.useTriggers = true;
        thunderBurstContactFilter.SetLayerMask(enemyLayer);
    }

    public void SetDirection(Vector2 direction)
    {
        initialDirection = direction;
        directionSet = true;
    }

    private void Update()
    {
        if (hasLaunched && !isDetonating && IsOutsideCameraBounds())
        {
            Destroy(gameObject);
            return;
        }

        if (hasLaunched && !isDetonating)
        {
            Vector3 euler = transform.eulerAngles;
            if (!Mathf.Approximately(euler.z, 0f) || !Mathf.Approximately(euler.x, 0f) || !Mathf.Approximately(euler.y, 0f))
            {
                transform.rotation = Quaternion.identity;
            }
        }

        if (thunderBurstActive && !isDetonating && thunderBurstTransforms != null && EnableThunderBurstRotation)
        {
            float dt = GameStateManager.GetPauseSafeDeltaTime();
            if (dt > 0f)
            {
                float delta = ThunderBurstRotationDegreesPerSecond * thunderBurstRotationSign * dt;
                for (int i = 0; i < thunderBurstTransforms.Length; i++)
                {
                    Transform t = thunderBurstTransforms[i];
                    if (t != null)
                    {
                        t.Rotate(0f, 0f, delta);
                    }
                }
            }
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

    private void CacheThunderBurstChildren()
    {
        List<Transform> found = new List<Transform>();
        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null)
            {
                continue;
            }

            if (t.name == "ThunderBurst" || t.name.StartsWith("ThunderBurst"))
            {
                found.Add(t);
            }
        }

        if (found.Count == 0)
        {
            thunderBurstTransforms = null;
            thunderBurstBaseLocalScales = null;
            thunderBurstColliders = null;
            thunderBurstColliderSet = null;
            return;
        }

        thunderBurstTransforms = found.ToArray();
        thunderBurstBaseLocalScales = new Vector3[thunderBurstTransforms.Length];
        for (int i = 0; i < thunderBurstTransforms.Length; i++)
        {
            thunderBurstBaseLocalScales[i] = thunderBurstTransforms[i] != null ? thunderBurstTransforms[i].localScale : Vector3.one;
        }

        List<Collider2D> cols = new List<Collider2D>();
        for (int i = 0; i < thunderBurstTransforms.Length; i++)
        {
            Transform t = thunderBurstTransforms[i];
            if (t == null)
            {
                continue;
            }

            Collider2D[] childCols = t.GetComponentsInChildren<Collider2D>(true);
            for (int c = 0; c < childCols.Length; c++)
            {
                Collider2D col = childCols[c];
                if (col != null)
                {
                    cols.Add(col);
                }
            }
        }

        thunderBurstColliders = cols.Count > 0 ? cols.ToArray() : null;
        thunderBurstColliderSet = thunderBurstColliders != null ? new HashSet<Collider2D>(thunderBurstColliders) : null;
    }

    private void SetThunderBurstActive(bool active)
    {
        if (thunderBurstTransforms == null)
        {
            return;
        }

        for (int i = 0; i < thunderBurstTransforms.Length; i++)
        {
            Transform t = thunderBurstTransforms[i];
            if (t != null)
            {
                t.gameObject.SetActive(active);
            }
        }

        if (thunderBurstColliders != null)
        {
            for (int i = 0; i < thunderBurstColliders.Length; i++)
            {
                Collider2D col = thunderBurstColliders[i];
                if (col != null)
                {
                    col.enabled = active;
                }
            }
        }

        if (active)
        {
            ApplyThunderBurstSize();
        }
    }

    private void BeginThunderBurstFadeAndDestroy()
    {
        if (thunderBurstTransforms == null || thunderBurstTransforms.Length == 0)
        {
            return;
        }

        float duration = Mathf.Max(0f, ThunderFadeDuration);
        if (duration <= 0f)
        {
            for (int i = 0; i < thunderBurstTransforms.Length; i++)
            {
                Transform t = thunderBurstTransforms[i];
                if (t != null)
                {
                    Destroy(t.gameObject);
                }
            }
            return;
        }

        StartCoroutine(ThunderBurstFadeAndDestroyRoutine(duration));
    }

    private IEnumerator ThunderBurstFadeAndDestroyRoutine(float duration)
    {
        List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        List<Color> startColors = new List<Color>();
        List<GameObject> roots = new List<GameObject>();

        for (int i = 0; i < thunderBurstTransforms.Length; i++)
        {
            Transform t = thunderBurstTransforms[i];
            if (t == null)
            {
                continue;
            }

            roots.Add(t.gameObject);

            SpriteRenderer[] rs = t.GetComponentsInChildren<SpriteRenderer>(true);
            for (int r = 0; r < rs.Length; r++)
            {
                SpriteRenderer sr = rs[r];
                if (sr == null)
                {
                    continue;
                }

                renderers.Add(sr);
                startColors.Add(sr.color);
            }
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float dt = GameStateManager.GetPauseSafeDeltaTime();
            elapsed += dt;
            float t = duration > 0.0001f ? Mathf.Clamp01(elapsed / duration) : 1f;
            float mul = 1f - t;

            for (int i = 0; i < renderers.Count; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr == null)
                {
                    continue;
                }

                Color c = startColors[i];
                c.a *= mul;
                sr.color = c;
            }

            yield return null;
        }

        for (int i = 0; i < roots.Count; i++)
        {
            if (roots[i] != null)
            {
                Destroy(roots[i]);
            }
        }
    }

    private void ApplyThunderBurstSize()
    {
        if (thunderBurstTransforms == null || thunderBurstBaseLocalScales == null)
        {
            return;
        }

        float size = Mathf.Max(0f, thunderBurstSizeAfterCards);
        for (int i = 0; i < thunderBurstTransforms.Length; i++)
        {
            Transform t = thunderBurstTransforms[i];
            if (t == null)
            {
                continue;
            }

            Vector3 baseScaleLocal = thunderBurstBaseLocalScales[i];
            t.localScale = new Vector3(baseScaleLocal.x, baseScaleLocal.y * size, baseScaleLocal.z);
        }
    }

    private IEnumerator ThunderBurstDamageRoutine()
    {
        float interval = Mathf.Max(0.01f, ThunderBurstInterval);

        while (!isDetonating)
        {
            float now = GameStateManager.PauseSafeTime;

            if (thunderBurstColliders != null)
            {
                for (int c = 0; c < thunderBurstColliders.Length; c++)
                {
                    Collider2D col = thunderBurstColliders[c];
                    if (col == null || !col.enabled)
                    {
                        continue;
                    }

                    int hitCount = col.OverlapCollider(thunderBurstContactFilter, thunderBurstHits);
                    for (int i = 0; i < hitCount; i++)
                    {
                        Collider2D hit = thunderBurstHits[i];
                        if (hit == null)
                        {
                            continue;
                        }

                        EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>() ?? hit.GetComponentInParent<EnemyHealth>();
                        if (enemyHealth == null || !enemyHealth.IsAlive)
                        {
                            continue;
                        }

                        int id = enemyHealth.gameObject.GetInstanceID();
                        if (thunderBurstNextDamageTimeByEnemyId.TryGetValue(id, out float nextTime) && now < nextTime)
                        {
                            continue;
                        }

                        thunderBurstNextDamageTimeByEnemyId[id] = now + interval;

                        if (!OffscreenDamageChecker.CanTakeDamage(enemyHealth.transform.position))
                        {
                            continue;
                        }

                        float baseDamageForEnemy = GetCurrentDamage() * (Mathf.Clamp(thunderBurstDamageAfterCards, 0f, 100f) / 100f);
                        float finalDamage = baseDamageForEnemy;
                        if (cachedPlayerStats != null)
                        {
                            finalDamage = PlayerDamageHelper.ComputeProjectileDamage(cachedPlayerStats, enemyHealth.gameObject, baseDamageForEnemy, gameObject);
                        }

                        enemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Thunder);

                        Vector3 hitPoint = hit.ClosestPoint(col.bounds.center);
                        Vector3 hitNormal = ((Vector2)col.bounds.center - (Vector2)hitPoint).normalized;
                        enemyHealth.TakeDamage(finalDamage, hitPoint, hitNormal);

                        if (cachedStaticEffect != null)
                        {
                            cachedStaticEffect.TryApplyStatic(enemyHealth.gameObject, hitPoint);
                        }
                    }
                }
            }

            yield return null;
        }
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

    private Vector2 GetExplosionCenterWorld(Vector2 radiusOffset)
    {
        Vector3 localOffset = new Vector3(radiusOffset.x, radiusOffset.y, 0f);
        Vector3 worldOffset = transform.TransformVector(localOffset);
        return (Vector2)transform.position + (Vector2)worldOffset;
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

    public void Launch(Vector2 direction, Collider2D colliderToIgnore, PlayerMana playerMana = null, bool skipCooldownCheck = false)
    {
        hasLaunched = true;

        if (_rigidbody2D == null)
        {
            Destroy(gameObject);
            return;
        }

        ProjectileCards card = ProjectileCardModifiers.Instance != null
            ? ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject)
            : null;

        enhancedVariantIndex = 0;
        bool variant3Active = false;
        if (ProjectileCardLevelSystem.Instance != null && card != null)
        {
            int selected = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card);
            bool unlocked = ProjectileCardLevelSystem.Instance.IsEnhancedUnlocked(card);

            bool hasVariant2History = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 2);
            bool hasVariant3History = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 3);

            enhancedVariantIndex = selected;
            if (enhancedVariantIndex == 0 && unlocked)
            {
                enhancedVariantIndex = 1;
            }

            thunderBurstActive = hasVariant2History || selected == 2;
            variant3Active = hasVariant3History || selected == 3;
        }

        if (variant3Active)
        {
            ProjectileStatusChanceAdditiveBonus additive = GetComponent<ProjectileStatusChanceAdditiveBonus>();
            if (additive == null)
            {
                additive = gameObject.AddComponent<ProjectileStatusChanceAdditiveBonus>();
            }
            additive.staticBonusPercent = Mathf.Max(0f, StaticChanceIncrease);
        }

        CardModifierStats modifiers = new CardModifierStats();
        if (card != null && ProjectileCardModifiers.Instance != null)
        {
            modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
        }

        float baseCooldown = cooldown;
        if (card != null && card.runtimeSpawnInterval > 0f)
        {
            baseCooldown = card.runtimeSpawnInterval;
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

        float finalSpeed = speed + modifiers.speedIncrease;
        float finalLifetime = lifetimeSeconds + modifiers.lifetimeIncrease;
        float finalDamage = damage + modifiers.damageFlat;

        float totalSizeMultiplier = modifiers.sizeMultiplier;
        float explosionRadiusBaseScaled = Mathf.Max(0f, baseExplosionRadius * totalSizeMultiplier);
        if (thunderBurstActive)
        {
            thunderBurstSizeAfterCards = Mathf.Max(0f, baseThunderBurstSize * totalSizeMultiplier);
            ApplyThunderBurstSize();
        }
        else if (totalSizeMultiplier != 1f)
        {
            transform.localScale *= totalSizeMultiplier;
            ColliderScaler.ScaleCollider(_collider2D, totalSizeMultiplier, colliderSizeOffset);
        }

        ExplosionRadius = Mathf.Max(0f, (explosionRadiusBaseScaled + modifiers.explosionRadiusBonus) * modifiers.explosionRadiusMultiplier);

        float denom = Mathf.Abs(baseExplosionRadius) > 0.0001f ? baseExplosionRadius : 1f;
        explosionEffectScale = ExplosionRadius / denom;

        thunderBurstDamageAfterCards = Mathf.Clamp(baseThunderBurstDamage, 0f, 100f);

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

        prefabKey = "ElectroBall_" + projectileType;

        if (!skipCooldownCheck)
        {
            if (card == null)
            {
                if (lastFireTimes.ContainsKey(prefabKey))
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

        if (_collider2D != null && colliderToIgnore != null)
        {
            Physics2D.IgnoreCollision(_collider2D, colliderToIgnore, true);
        }

        Vector2 chosenDir = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : (directionSet && initialDirection.sqrMagnitude > 0.0001f ? initialDirection.normalized : Vector2.right);

        thunderBurstRotationSign = chosenDir.x < 0f ? -1f : 1f;

        if (thunderBurstActive)
        {
            SetThunderBurstActive(true);
            thunderBurstNextDamageTimeByEnemyId.Clear();
            if (thunderBurstRoutine != null)
            {
                StopCoroutine(thunderBurstRoutine);
                thunderBurstRoutine = null;
            }
            thunderBurstRoutine = StartCoroutine(ThunderBurstDamageRoutine());
        }
        else
        {
            SetThunderBurstActive(false);
            thunderBurstNextDamageTimeByEnemyId.Clear();
            if (thunderBurstRoutine != null)
            {
                StopCoroutine(thunderBurstRoutine);
                thunderBurstRoutine = null;
            }
        }

        _rigidbody2D.velocity = chosenDir * finalSpeed;

        rotateToVelocity = false;
        transform.rotation = Quaternion.identity;

        PauseSafeSelfDestruct.Schedule(gameObject, finalLifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Transform t = other != null ? other.transform : null;
        while (t != null)
        {
            if (t.name == "ClickHitbox")
            {
                return;
            }
            t = t.parent;
        }

        if (isDetonating)
        {
            return;
        }

        if (((1 << other.gameObject.layer) & enemyLayer) == 0)
        {
            return;
        }

        if (_collider2D != null && !other.IsTouching(_collider2D))
        {
            return;
        }

        IDamageable damageable = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
        if (damageable == null || !damageable.IsAlive)
        {
            return;
        }

        if (!hasAppliedNewOrderInLayer)
        {
            hasAppliedNewOrderInLayer = true;
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].sortingOrder = NewOrderInLayer;
                }
            }
        }

        cachedDetonationEffectWorldPosition = transform.position;
        cachedDetonationCenterWorldPosition = GetExplosionCenterWorld(baseExplosionOffset);
        hasDetonationWorldSnapshot = true;

        isDetonating = true;

        if (thunderBurstRoutine != null)
        {
            StopCoroutine(thunderBurstRoutine);
            thunderBurstRoutine = null;
        }

        if (_rigidbody2D != null)
        {
            _rigidbody2D.velocity = Vector2.zero;
            _rigidbody2D.simulated = false;
        }

        Collider2D[] cols = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
            {
                cols[i].enabled = false;
            }
        }

        if (thunderBurstActive)
        {
            BeginThunderBurstFadeAndDestroy();
        }

        float denom = Mathf.Abs(baseExplosionRadius) > 0.0001f ? baseExplosionRadius : 1f;
        float detonationScale = ExplosionRadius / denom;
        if (detonationScale > 0.0001f && baseScale != Vector3.zero)
        {
            transform.localScale = baseScale * detonationScale;
        }

        if (!EnableExplosionEffectPrefab)
        {
            if (_animator != null)
            {
                _animator.SetBool("detonate", true);
            }

            float destroyDelay = Mathf.Max(0.05f, Mathf.Max(DetonateAnimationTime, ExplosionDelay));
            PauseSafeSelfDestruct.Schedule(gameObject, destroyDelay);
        }

        StartCoroutine(DetonateRoutine());
    }

    private IEnumerator DetonateRoutine()
    {
        float delay = Mathf.Max(0f, ExplosionDelay);
        if (delay > 0f)
        {
            yield return GameStateManager.WaitForPauseSafeSeconds(delay);
        }

        if (EnableExplosionEffectPrefab && ExplosionEffectPrefab != null)
        {
            Vector2 effectPosition = hasDetonationWorldSnapshot
                ? cachedDetonationEffectWorldPosition
                : (Vector2)transform.position;
            GameObject effect = Instantiate(ExplosionEffectPrefab, effectPosition, Quaternion.identity);
            if (effect != null)
            {
                effect.transform.localScale = effect.transform.localScale * Mathf.Max(0.0001f, explosionEffectScale);
                PauseSafeSelfDestruct.Schedule(effect, Mathf.Max(0.05f, DetonateAnimationTime));
            }
        }

        if (EnableExplosionEffectPrefab)
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    if (thunderBurstActive && thunderBurstTransforms != null)
                    {
                        Transform rt = renderers[i].transform;
                        bool isThunderBurstRenderer = false;
                        for (int t = 0; t < thunderBurstTransforms.Length; t++)
                        {
                            Transform thunderRoot = thunderBurstTransforms[t];
                            if (thunderRoot != null && rt != null && rt.IsChildOf(thunderRoot))
                            {
                                isThunderBurstRenderer = true;
                                break;
                            }
                        }

                        if (isThunderBurstRenderer)
                        {
                            continue;
                        }
                    }

                    renderers[i].enabled = false;
                }
            }
        }

        Vector2 explosionCenter = hasDetonationWorldSnapshot
            ? cachedDetonationCenterWorldPosition
            : GetExplosionCenterWorld(baseExplosionOffset);
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(explosionCenter, ExplosionRadius, enemyLayer);

        SlowEffect slowEffect = GetComponent<SlowEffect>();
        StaticEffect staticEffect = cachedStaticEffect != null ? cachedStaticEffect : GetComponent<StaticEffect>();

        for (int i = 0; i < hitColliders.Length; i++)
        {
            Collider2D hit = hitColliders[i];
            if (hit == null)
            {
                continue;
            }

            if (!OffscreenDamageChecker.CanTakeDamage(hit.transform.position))
            {
                continue;
            }

            IDamageable damageable = hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive)
            {
                continue;
            }

            Vector3 hitPoint = hit.ClosestPoint(explosionCenter);
            Vector3 hitNormal = ((Vector2)explosionCenter - (Vector2)hitPoint).normalized;

            float baseDamageForEnemy = baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
            float finalDamage = baseDamageForEnemy;

            Component damageableComponent = damageable as Component;
            GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : hit.gameObject;

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

            DamageAoeScope.BeginAoeDamage();
            damageable.TakeDamage(finalDamage, hitPoint, hitNormal);
            DamageAoeScope.EndAoeDamage();

            StatusController.TryApplyBurnFromProjectile(gameObject, hit.gameObject, hitPoint, finalDamage);
            if (slowEffect != null)
            {
                slowEffect.TryApplySlow(hit.gameObject, hitPoint);
            }
            if (staticEffect != null)
            {
                staticEffect.TryApplyStatic(hit.gameObject, hitPoint);
            }
        }

        if (EnableExplosionEffectPrefab)
        {
            Destroy(gameObject);
        }

        yield break;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 explosionCenter = GetExplosionCenterWorld(ExplosionRadiusOffset);

        Gizmos.color = new Color(1f, 1f, 0.6f, 0.25f);
        Gizmos.DrawSphere(explosionCenter, ExplosionRadius);

        Gizmos.color = new Color(1f, 1f, 0.6f, 0.85f);
        Gizmos.DrawWireSphere(explosionCenter, ExplosionRadius);
    }

    public void ApplyInstantModifiers(CardModifierStats mods)
    {
        float ns = baseSpeed + mods.speedIncrease;
        if (ns != speed)
        {
            speed = ns;
            if (_rigidbody2D != null)
            {
                Vector2 v = _rigidbody2D.velocity;
                if (v.sqrMagnitude > 0.0001f)
                {
                    _rigidbody2D.velocity = v.normalized * speed;
                }
            }
        }

        float nl = baseLifetime + mods.lifetimeIncrease;
        if (nl != lifetimeSeconds)
        {
            lifetimeSeconds = nl;
        }

        float nd = baseDamage + mods.damageFlat;
        if (nd != damage)
        {
            damage = nd;
            baseDamageAfterCards = nd;
        }

        ExplosionRadius = baseExplosionRadius;

        float totalSizeMultiplier = mods.sizeMultiplier;
        float explosionRadiusBaseScaled = Mathf.Max(0f, baseExplosionRadius * totalSizeMultiplier);
        if (thunderBurstActive)
        {
            thunderBurstSizeAfterCards = Mathf.Max(0f, baseThunderBurstSize * totalSizeMultiplier);
            ApplyThunderBurstSize();
        }
        else if (totalSizeMultiplier != 1f)
        {
            transform.localScale = baseScale * totalSizeMultiplier;
            ColliderScaler.ScaleCollider(_collider2D, totalSizeMultiplier, colliderSizeOffset);
        }

        ExplosionRadius = Mathf.Max(0f, (explosionRadiusBaseScaled + mods.explosionRadiusBonus) * mods.explosionRadiusMultiplier);

        float denom = Mathf.Abs(baseExplosionRadius) > 0.0001f ? baseExplosionRadius : 1f;
        explosionEffectScale = ExplosionRadius / denom;

        thunderBurstDamageAfterCards = Mathf.Clamp(baseThunderBurstDamage, 0f, 100f);
    }
}
