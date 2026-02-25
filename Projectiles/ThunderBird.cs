using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ThunderBird : MonoBehaviour, IInstantModifiable
{
    [Header("Bird Settings")]
    [SerializeField] private float flySpeed = 8f;
    [Tooltip("Time before bird is destroyed after spawning")]
    [SerializeField] private float lifetimeSeconds = 10f;

    [Header("Strike Zone")]
    [Tooltip("Radius of the damage zone around the bird")]
    [SerializeField] private float strikeZoneRadius = 2f;
    [Tooltip("Offset for strike zone detection area in X and Y coordinates")]
    [SerializeField] private Vector2 strikeZoneOffset = Vector2.zero;
    [Tooltip("Delay before damage is registered when enemy enters strike zone (in seconds)")]
    [SerializeField] private float damageDelay = 0.25f;
    [SerializeField] private float damage = 30f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private ProjectileType projectileType = ProjectileType.Thunder;

    [Header("Spawn Area")]
    [Tooltip("Tag name for minimum spawn position GameObject (left side)")]
    [SerializeField] private string minPosTag = "ThunderBird_MinPos";
    [Tooltip("Tag name for maximum spawn position GameObject (right side)")]
    [SerializeField] private string maxPosTag = "ThunderBird_MaxPos";

    private Transform minPos;
    private Transform maxPos;

    public Transform MinPos => minPos;
    public Transform MaxPos => maxPos;

    [Header("Visual Effects")]
    [SerializeField] private GameObject strikeEffectPrefab;
    [SerializeField] private float strikeEffectDuration = 1f;
    [Tooltip("Size multiplier for strike effect")]
    [SerializeField] private float strikeEffectSizeMultiplier = 1f;
    [Tooltip("Effect timing adjustment: negative = delay effect, positive = play effect early (relative to damage delay)")]
    [SerializeField] private float strikeEffectTimingAdjustment = 0f;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Audio")]
    [SerializeField] private AudioClip flyClip;
    [Range(0f, 1f)][SerializeField] private float flyVolume = 0.7f;
    [SerializeField] private AudioClip strikeClip;
    [Range(0f, 1f)][SerializeField] private float strikeVolume = 0.8f;

    [Header("Cooldown")]
    [SerializeField] private float cooldown = 2f;

    [Header("Collider Scaling")]
    [Tooltip("Offset for collider size relative to visual size (0 = same as visual, -0.2 = 20% smaller, 0.2 = 20% larger)")]
    [SerializeField] private float colliderSizeOffset = 0f;

    private ThunderBirdV1 variant1Component;
    private ThunderBirdV2 variant2Component;

    private Rigidbody2D _rigidbody2D;
    private Collider2D _collider2D;
    private AudioSource _audioSource;
    private Animator _animator;
    private bool isMovingRight = true;
    private Camera mainCamera;
    private float spawnTime;

    private HashSet<GameObject> damagedEnemies = new HashSet<GameObject>();
    private Dictionary<GameObject, float> pendingDamageEnemies = new Dictionary<GameObject, float>();
    private readonly HashSet<GameObject> pendingVariant2GlobalStrikeEnemies = new HashSet<GameObject>();

    private int pendingVariant2StrikeRequests;
    private GameObject pendingVariant2TriggerEnemyRoot;

    private class CachedEnemyData
    {
        public IDamageable damageable;
        public EnemyHealth enemyHealth;
        public GameObject damageObject;
    }

    private readonly Dictionary<GameObject, CachedEnemyData> cachedEnemyData = new Dictionary<GameObject, CachedEnemyData>();

    [System.Serializable]
    public class EnemyStrikeOffset
    {
        public string enemyName;
        [Header("Left to Right (spawns at minPos)")]
        public Vector2 offsetLeftToRightLeft;
        public Vector2 offsetLeftToRightRight;
        [Header("Right to Left (spawns at maxPos)")]
        public Vector2 offsetRightToLeftLeft;
        public Vector2 offsetRightToLeftRight;
    }

    [Header("Per-Enemy Strike Offsets")]
    [SerializeField] private List<EnemyStrikeOffset> perEnemyOffsets = new List<EnemyStrikeOffset>();

    [Header("Strike Effect Placement")]
    [SerializeField] private bool usePerEnemyStrikeOffsets = true;

    private bool spawnedFromLeft = false;

    private bool isVariant2Active = false;
    private bool isVariant12Active = false;

    private static int lastVariant2StrikeFrame = -1;
    private static int variant2StrikesThisFrame = 0;

    private Coroutine variant2StrikeRoutine;

    private float baseFlySpeed;
    private float baseStrikeZoneRadius;
    private float baseLifetimeSeconds;
    private float baseDamage;
    private Vector3 baseScale;
    private PlayerStats cachedPlayerStats;
    private float baseDamageAfterCards;

    [Header("Offscreen Destruction Grace Periods")]
    public float baseGracePeriod = 10f;

    // Enhanced system
    private int enhancedVariant = 0;

    // Static tracking for dual spawn alternation (per side)
    private static bool leftBirdSpawnTop = true;
    private static bool rightBirdSpawnTop = false;
    private static bool isFirstDualSpawn = true;
    private static int variant1SpawnCounter = 0;

    // Static tracking for cross-direction collision avoidance
    private static List<Vector3> currentFrameSpawnPositions = new List<Vector3>();
    private static int lastSpawnFrame = -1;

    [Header("Collision Avoidance")]
    public static float crossDirectionMinDistance = 2f;
    public static float sameSideMinDistance = 2f;

    // Instance-based cooldown tracking
    private static System.Collections.Generic.Dictionary<string, float> lastFireTimes = new System.Collections.Generic.Dictionary<string, float>();
    private string prefabKey;

    // ==========================================================
    // VFX FOLLOW + FREEZE-ON-DEATH FIX (keeps original behavior)
    // ==========================================================
    private class StrikeEffectFollowAndFreeze : MonoBehaviour
    {
        private Transform followTarget;        // collider transform
        private Collider2D followCollider;     // freeze at bounds center
        private EnemyHealth followHealth;      // detect death
        private bool frozen;
        private Vector3 frozenWorldPos;

        // NEW: keep local offset from the collider transform so per-enemy offsets work while following
        private Vector3 localOffset;

        public void Init(Transform targetTransform, Collider2D collider, Vector3 initialWorldPos)
        {
            followTarget = targetTransform;
            followCollider = collider;

            if (followTarget != null)
            {
                followHealth = followTarget.GetComponent<EnemyHealth>() ?? followTarget.GetComponentInParent<EnemyHealth>();
            }
            else
            {
                followHealth = null;
            }

            localOffset = Vector3.zero;
            if (followTarget != null)
            {
                localOffset = initialWorldPos - followTarget.position;
            }

            frozen = false;
            frozenWorldPos = initialWorldPos;
        }

        private void LateUpdate()
        {
            if (frozen)
            {
                transform.position = frozenWorldPos;
                return;
            }

            bool missing = followTarget == null;
            bool inactive = !missing && !followTarget.gameObject.activeInHierarchy;
            bool dead = followHealth != null && !followHealth.IsAlive;

            if (missing || inactive || dead)
            {
                FreezeNow();
                return;
            }

            // Follow collider transform + preserve offset
            transform.position = followTarget.position + localOffset;
        }

        private void FreezeNow()
        {
            frozen = true;

            // Freeze at the last known "offset position" if possible.
            // (This prevents snapping to collider center and losing the per-enemy offset.)
            frozenWorldPos = transform.position;

            transform.SetParent(null, true);
            transform.position = frozenWorldPos;
        }
    }

    private GameObject SpawnStrikeEffectFollowing(Collider2D followCollider, Vector3 worldPos, float sizeMultiplier)
    {
        if (strikeEffectPrefab == null || followCollider == null)
        {
            return null;
        }

        GameObject effect = Instantiate(strikeEffectPrefab, worldPos, Quaternion.identity);

        // Parent for the original look/feel (so particle systems using local space still behave like before)
        effect.transform.SetParent(followCollider.transform, true);

        // Ensure the follower preserves the per-enemy offset while following.
        var follower = effect.AddComponent<StrikeEffectFollowAndFreeze>();
        follower.Init(followCollider.transform, followCollider, worldPos);

        if (!Mathf.Approximately(sizeMultiplier, 1f))
        {
            effect.transform.localScale *= sizeMultiplier;
        }

        Destroy(effect, strikeEffectDuration);
        return effect;
    }

    private Vector2 GetStrikeOffsetForEnemy(GameObject enemy)
    {
        string enemyName = enemy.name.Replace("(Clone)", "").Trim();

        foreach (var offsetData in perEnemyOffsets)
        {
            if (offsetData.enemyName == enemyName)
            {
                Camera mainCam = Camera.main;
                bool enemyOnLeftSide = mainCam != null && enemy.transform.position.x < mainCam.transform.position.x;

                if (spawnedFromLeft)
                {
                    return enemyOnLeftSide ? offsetData.offsetLeftToRightLeft : offsetData.offsetLeftToRightRight;
                }
                else
                {
                    return enemyOnLeftSide ? offsetData.offsetRightToLeftLeft : offsetData.offsetRightToLeftRight;
                }
            }
        }

        return Vector2.zero;
    }

    private Vector2 GetStrikeEffectOffset(GameObject enemy)
    {
        if (!usePerEnemyStrikeOffsets) return Vector2.zero;

        GameObject enemyRoot = ResolveEnemyRootForStrike(enemy);
        if (enemyRoot == null) return Vector2.zero;

        return GetStrikeOffsetForEnemy(enemyRoot);
    }

    private GameObject ResolveEnemyRootForStrike(GameObject candidate)
    {
        if (candidate == null) return null;

        EnemyHealth enemyHealth = candidate.GetComponentInParent<EnemyHealth>();
        if (enemyHealth != null) return enemyHealth.gameObject;

        IDamageable damageable = candidate.GetComponentInParent<IDamageable>();
        if (damageable is Component damageableComponent) return damageableComponent.gameObject;

        return candidate;
    }

    private Vector3 GetStrikeEffectAnchorPosition(GameObject enemy, Collider2D colliderHint)
    {
        if (colliderHint != null) return colliderHint.bounds.center;

        Collider2D enemyCollider = null;
        if (enemy != null)
        {
            enemyCollider = enemy.GetComponent<Collider2D>() ?? enemy.GetComponentInChildren<Collider2D>() ?? enemy.GetComponentInParent<Collider2D>();
        }

        if (enemyCollider != null) return enemyCollider.bounds.center;
        if (enemy != null) return enemy.transform.position;
        return Vector3.zero;
    }

    private bool TryGetCachedEnemyData(GameObject source, out IDamageable damageable, out EnemyHealth enemyHealth, out GameObject damageObject)
    {
        damageable = null;
        enemyHealth = null;
        damageObject = source;

        if (source == null) return false;

        if (cachedEnemyData.TryGetValue(source, out CachedEnemyData cached))
        {
            damageable = cached.damageable;
            enemyHealth = cached.enemyHealth;
            damageObject = cached.damageObject;
            return damageable != null;
        }

        IDamageable resolvedDamageable = source.GetComponent<IDamageable>() ?? source.GetComponentInParent<IDamageable>();
        GameObject resolvedDamageObject = source;
        EnemyHealth resolvedEnemyHealth = null;

        if (resolvedDamageable is Component damageableComponent)
        {
            resolvedDamageObject = damageableComponent.gameObject;
        }

        if (resolvedDamageObject != null)
        {
            resolvedEnemyHealth = resolvedDamageObject.GetComponent<EnemyHealth>() ?? resolvedDamageObject.GetComponentInParent<EnemyHealth>();
        }

        CachedEnemyData newEntry = new CachedEnemyData
        {
            damageable = resolvedDamageable,
            enemyHealth = resolvedEnemyHealth,
            damageObject = resolvedDamageObject
        };

        cachedEnemyData[source] = newEntry;

        damageable = resolvedDamageable;
        enemyHealth = resolvedEnemyHealth;
        damageObject = resolvedDamageObject;

        return damageable != null;
    }

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _collider2D = GetComponent<Collider2D>();
        mainCamera = Camera.main;
        spawnTime = Time.time;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();

        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        _animator = GetComponent<Animator>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();

        if (_rigidbody2D != null)
        {
            _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody2D.velocity = Vector2.zero;
        }
    }

    public void Initialize(Vector3 spawnPosition, Collider2D playerCollider, bool skipCooldownCheck = false)
    {
        ProjectileCards card = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);

        bool hasVariant1History = false;
        bool hasVariant2History = false;

        if (ProjectileCardLevelSystem.Instance != null && card != null)
        {
            enhancedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card);
            hasVariant1History = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 1);
            hasVariant2History = ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 2);
        }

        bool hasVariant1Context = (enhancedVariant == 1) || hasVariant1History;
        bool hasVariant2Context = (enhancedVariant == 2) || hasVariant2History;

        isVariant12Active = hasVariant1Context && hasVariant2Context;
        isVariant2Active = (enhancedVariant == 2) || isVariant12Active;

        pendingVariant2GlobalStrikeEnemies.Clear();
        pendingVariant2StrikeRequests = 0;
        pendingVariant2TriggerEnemyRoot = null;

        variant1Component = GetComponent<ThunderBirdV1>();
        if (variant1Component != null)
        {
            variant1Component.Configure((enhancedVariant == 1) || isVariant12Active);
        }

        variant2Component = GetComponent<ThunderBirdV2>();
        if (variant2Component != null)
        {
            variant2Component.Configure(isVariant2Active);
        }

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

        baseFlySpeed = flySpeed;
        baseStrikeZoneRadius = strikeZoneRadius;
        baseLifetimeSeconds = lifetimeSeconds;
        baseDamage = damage;
        baseScale = transform.localScale;

        CardModifierStats modifiers = new CardModifierStats();
        if (card != null) modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);

        float enhancedStrikeRadiusMult = 1f;
        bool variant1Active = (enhancedVariant == 1) || isVariant12Active;
        if (variant1Active)
        {
            enhancedStrikeRadiusMult = variant1Component != null
                ? variant1Component.GetStrikeRadiusMultiplier()
                : 1f;
        }

        float baseCooldown = cooldown;
        if (card != null && card.runtimeSpawnInterval > 0f)
        {
            baseCooldown = card.runtimeSpawnInterval;
        }

        if (variant1Active)
        {
            float v1BaseCooldownOverride;
            if (variant1Component != null && variant1Component.TryGetBaseCooldownOverride(out v1BaseCooldownOverride))
            {
                baseCooldown = v1BaseCooldownOverride;
                if (card != null) card.runtimeSpawnInterval = Mathf.Max(0.1f, v1BaseCooldownOverride);
            }
        }

        if (isVariant2Active)
        {
            float v2BaseCooldownOverride;
            if (variant2Component != null && variant2Component.TryGetBaseCooldownOverride(out v2BaseCooldownOverride))
            {
                baseCooldown = v2BaseCooldownOverride;
                if (card != null) card.runtimeSpawnInterval = Mathf.Max(0.1f, v2BaseCooldownOverride);
            }
        }

        if (variant1Active)
        {
            float v1CooldownMult = variant1Component != null
                ? variant1Component.GetCooldownMultiplier()
                : 1f;
            baseCooldown *= v1CooldownMult;
        }

        float finalLifetime = lifetimeSeconds + modifiers.lifetimeIncrease;
        float finalCooldown = baseCooldown * (1f - modifiers.cooldownReductionPercent / 100f);
        if (MinCooldownManager.Instance != null)
        {
            finalCooldown = MinCooldownManager.Instance.ClampCooldown(card, finalCooldown);
        }
        else
        {
            finalCooldown = Mathf.Max(0.1f, finalCooldown);
        }
        damage += modifiers.damageFlat;

        float baseVersionSpeed = baseFlySpeed + modifiers.speedIncrease;
        float baseRadiusForStrike = baseStrikeZoneRadius;

        if (variant1Active)
        {
            baseRadiusForStrike = baseStrikeZoneRadius * enhancedStrikeRadiusMult;
            float v1SpeedBonus = variant1Component != null ? variant1Component.GetSpeedBonus() : 0f;
            if (v1SpeedBonus != 0f) baseVersionSpeed += v1SpeedBonus;
        }

        float baseVersionStrikeZone = (baseRadiusForStrike + modifiers.strikeZoneRadiusBonus) * modifiers.strikeZoneRadiusMultiplier;

        strikeZoneRadius = baseVersionStrikeZone * Mathf.Sqrt(modifiers.sizeMultiplier);
        flySpeed = baseVersionSpeed;

        float variantSizeMultiplier = 1f;
        if (variant1Active)
        {
            float v1SizeMult = variant1Component != null ? variant1Component.GetSizeMultiplier() : 1f;
            if (!Mathf.Approximately(v1SizeMult, 1f)) variantSizeMultiplier = v1SizeMult;
        }

        float finalSizeMultiplier = variantSizeMultiplier * modifiers.sizeMultiplier;

        if (!Mathf.Approximately(finalSizeMultiplier, 1f))
        {
            transform.localScale = baseScale * finalSizeMultiplier;
        }
        else
        {
            transform.localScale = baseScale;
        }

        strikeZoneOffset = new Vector2(strikeZoneOffset.x, transform.localScale.y * 1.5f);

        cachedPlayerStats = null;
        if (playerCollider != null) cachedPlayerStats = playerCollider.GetComponent<PlayerStats>();
        if (cachedPlayerStats == null) cachedPlayerStats = FindObjectOfType<PlayerStats>();

        baseDamageAfterCards = damage;

        prefabKey = "ThunderBird";

        if (!skipCooldownCheck)
        {
            if (lastFireTimes.ContainsKey(prefabKey))
            {
                if (Time.time - lastFireTimes[prefabKey] < finalCooldown)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            lastFireTimes[prefabKey] = Time.time;
        }

        if (_collider2D != null && playerCollider != null)
        {
            Physics2D.IgnoreCollision(_collider2D, playerCollider, true);
        }

        // Spawn positioning logic unchanged
        if (minPos != null && maxPos != null)
        {
            bool usedV1ComponentSpawn = false;
            if (variant1Component != null && variant1Component.IsActive)
            {
                usedV1ComponentSpawn = variant1Component.TryApplyVariant1SpawnPosition(spawnPosition);
            }

            if (!usedV1ComponentSpawn && variant1Active)
            {
                float minY = minPos.position.y;
                float maxY = maxPos.position.y;
                float midY = (minY + maxY) / 2f;

                bool isLeftBird = (variant1SpawnCounter % 2 == 0);
                variant1SpawnCounter++;

                if (isFirstDualSpawn)
                {
                    leftBirdSpawnTop = Random.value < 0.5f;
                    rightBirdSpawnTop = !leftBirdSpawnTop;
                    isFirstDualSpawn = false;
                }

                bool spawnInTopZone;
                float spawnX;

                if (isLeftBird)
                {
                    spawnX = minPos.position.x;
                    spawnInTopZone = leftBirdSpawnTop;
                    isMovingRight = true;
                    spawnedFromLeft = true;
                    leftBirdSpawnTop = !leftBirdSpawnTop;
                }
                else
                {
                    spawnX = maxPos.position.x;
                    spawnInTopZone = rightBirdSpawnTop;
                    isMovingRight = false;
                    spawnedFromLeft = false;
                    rightBirdSpawnTop = !rightBirdSpawnTop;
                }

                float spawnY;
                int maxAttempts = 10;
                int attempt = 0;
                bool validPosition = false;

                do
                {
                    spawnY = spawnInTopZone ? Random.Range(midY, maxY) : Random.Range(minY, midY);
                    Vector3 testPos = new Vector3(spawnX, spawnY, spawnPosition.z);
                    validPosition = !CheckBirdOverlap(testPos);
                    attempt++;
                } while (!validPosition && attempt < maxAttempts);

                Vector3 finalPosition = new Vector3(spawnX, spawnY, spawnPosition.z);
                transform.position = finalPosition;

                currentFrameSpawnPositions.Add(finalPosition);
                if (spriteRenderer != null) spriteRenderer.flipX = !isMovingRight;
            }
            else
            {
                Random.InitState(System.DateTime.Now.Millisecond + GetInstanceID());

                bool spawnFromLeftLocal = Random.value < 0.5f;
                float spawnX = spawnFromLeftLocal ? minPos.position.x : maxPos.position.x;

                float spawnY;
                int maxAttempts = 10;
                int attempt = 0;
                bool validPosition = false;

                do
                {
                    spawnY = Random.Range(minPos.position.y, maxPos.position.y);
                    Vector3 testPos = new Vector3(spawnX, spawnY, spawnPosition.z);
                    validPosition = !CheckBirdOverlap(testPos);
                    attempt++;
                } while (!validPosition && attempt < maxAttempts);

                transform.position = new Vector3(spawnX, spawnY, spawnPosition.z);

                isMovingRight = spawnFromLeftLocal;
                spawnedFromLeft = spawnFromLeftLocal;

                if (spriteRenderer != null) spriteRenderer.flipX = !isMovingRight;
            }
        }
        else
        {
            transform.position = spawnPosition;
            isMovingRight = true;
        }

        if (flyClip != null && _audioSource != null)
        {
            _audioSource.clip = flyClip;
            _audioSource.volume = flyVolume;
            _audioSource.loop = true;
            _audioSource.Play();
        }

        StartCoroutine(FlyRoutine(finalLifetime));
        StartCoroutine(DamageDetectionRoutine());
    }

    private IEnumerator FlyRoutine(float lifetime)
    {
        float elapsedTime = 0f;

        float gracePeriod = baseGracePeriod;
        if (variant1Component != null && variant1Component.IsActive)
        {
            gracePeriod = variant1Component.GetGracePeriod(baseGracePeriod);
        }

        while (elapsedTime < lifetime)
        {
            Vector2 direction = isMovingRight ? Vector2.right : Vector2.left;
            _rigidbody2D.velocity = direction * flySpeed;

            if (Time.time - spawnTime > gracePeriod && IsOffScreen())
            {
                Destroy(gameObject);
                yield break;
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }

    private IEnumerator DamageDetectionRoutine()
    {
        while (true)
        {
            bool birdCanDamageHere = OffscreenDamageChecker.CanTakeDamage(transform.position);

            if (!birdCanDamageHere)
            {
                yield return new WaitForFixedUpdate();
                continue;
            }

            if (damagedEnemies.Count > 0)
            {
                damagedEnemies.RemoveWhere(e => e == null || !e.activeInHierarchy || ((e.GetComponent<EnemyHealth>() ?? e.GetComponentInParent<EnemyHealth>()) != null && !((e.GetComponent<EnemyHealth>() ?? e.GetComponentInParent<EnemyHealth>()).IsAlive)));
            }

            if (pendingVariant2GlobalStrikeEnemies.Count > 0)
            {
                pendingVariant2GlobalStrikeEnemies.RemoveWhere(e => e == null || !e.activeInHierarchy || ((e.GetComponent<EnemyHealth>() ?? e.GetComponentInParent<EnemyHealth>()) != null && !((e.GetComponent<EnemyHealth>() ?? e.GetComponentInParent<EnemyHealth>()).IsAlive)));
            }

            Vector2 strikeCenter = (Vector2)transform.position + strikeZoneOffset;
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(strikeCenter, strikeZoneRadius, enemyLayer);

            foreach (Collider2D hitCollider in hitColliders)
            {
                if (hitCollider == null) continue;

                GameObject enemyRoot = ResolveEnemyRootForStrike(hitCollider.gameObject);
                if (enemyRoot == null) enemyRoot = hitCollider.gameObject;

                if (pendingVariant2GlobalStrikeEnemies.Contains(enemyRoot)) continue;
                if (damagedEnemies.Contains(enemyRoot)) continue;
                if (pendingDamageEnemies.ContainsKey(enemyRoot)) continue;

                IDamageable damageable = enemyRoot.GetComponent<IDamageable>() ?? enemyRoot.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                if (!OffscreenDamageChecker.CanTakeDamage(hitCollider.transform.position)) continue;

                bool canAttemptGlobalRoll = isVariant2Active && variant2StrikeRoutine == null && pendingVariant2StrikeRequests == 0;
                if (canAttemptGlobalRoll)
                {
                    float chance = variant2Component != null && variant2Component.IsActive
                        ? variant2Component.GlobalStrikeChancePercent
                        : 0f;

                    float rollThreshold = Mathf.Clamp01(chance / 100f);
                    if (Random.value <= rollThreshold)
                    {
                        RequestVariant2Strike(enemyRoot);

                        break;
                    }
                }

                pendingDamageEnemies[enemyRoot] = Time.time + damageDelay;

                // Early strike VFX (follows collider, preserves per-enemy offsets, freezes on death)
                if (strikeEffectTimingAdjustment > 0f && strikeEffectPrefab != null)
                {
                    Vector3 anchorPosition = GetStrikeEffectAnchorPosition(enemyRoot, hitCollider);
                    Vector2 effectOffset = GetStrikeEffectOffset(enemyRoot);
                    Vector3 effectPosition = anchorPosition + (Vector3)effectOffset;

                    SpawnStrikeEffectFollowing(hitCollider, effectPosition, strikeEffectSizeMultiplier);
                }
            }

            // Apply pending damage
            List<GameObject> enemiesToDamage = new List<GameObject>();
            foreach (var kvp in pendingDamageEnemies)
            {
                if (Time.time >= kvp.Value) enemiesToDamage.Add(kvp.Key);
            }

            foreach (GameObject enemy in enemiesToDamage)
            {
                if (enemy == null)
                {
                    pendingDamageEnemies.Remove(enemy);
                    continue;
                }

                IDamageable damageable = enemy.GetComponent<IDamageable>() ?? enemy.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive)
                {
                    pendingDamageEnemies.Remove(enemy);
                    continue;
                }

                if (!OffscreenDamageChecker.CanTakeDamage(enemy.transform.position))
                {
                    pendingDamageEnemies.Remove(enemy);
                    continue;
                }

                Vector3 enemyPosition = enemy.transform.position;
                Vector3 hitNormal = (strikeCenter - (Vector2)enemyPosition).normalized;

                float baseDamageForEnemy = baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
                float finalDamage = baseDamageForEnemy;

                GameObject enemyObj = enemy;
                if (cachedPlayerStats != null)
                {
                    finalDamage = PlayerDamageHelper.ComputeContinuousProjectileDamage(cachedPlayerStats, enemyObj, baseDamageForEnemy, gameObject);
                }

                EnemyHealth enemyHealth1 = enemy.GetComponent<EnemyHealth>() ?? enemy.GetComponentInParent<EnemyHealth>();
                if (enemyHealth1 != null)
                {
                    enemyHealth1.SetLastIncomingDamageType(DamageNumberManager.DamageType.Thunder);
                }

                damageable.TakeDamage(finalDamage, enemyPosition, hitNormal);

                damagedEnemies.Add(enemy);

                // On-hit strike VFX (follows collider, preserves per-enemy offsets, freezes on death)
                if (strikeEffectPrefab != null && strikeEffectTimingAdjustment <= 0f)
                {
                    Collider2D followCol =
                        enemy.GetComponent<Collider2D>() ??
                        enemy.GetComponentInChildren<Collider2D>() ??
                        enemy.GetComponentInParent<Collider2D>();

                    Vector3 anchorPosition = GetStrikeEffectAnchorPosition(enemy, followCol);
                    Vector2 effectOffset = GetStrikeEffectOffset(enemy);
                    Vector3 effectPosition = anchorPosition + (Vector3)effectOffset;

                    if (strikeEffectTimingAdjustment < 0f)
                    {
                        StartCoroutine(SpawnDelayedEffectFollowCollider(followCol, effectPosition, Mathf.Abs(strikeEffectTimingAdjustment), strikeEffectSizeMultiplier));
                    }
                    else
                    {
                        if (followCol != null)
                        {
                            SpawnStrikeEffectFollowing(followCol, effectPosition, strikeEffectSizeMultiplier);
                        }
                    }
                }

                if (strikeClip != null)
                {
                    AudioSource.PlayClipAtPoint(strikeClip, enemyPosition, strikeVolume);
                }

                pendingDamageEnemies.Remove(enemy);
            }

            yield return new WaitForFixedUpdate();
        }
    }

    private IEnumerator SpawnDelayedEffectFollowCollider(Collider2D followCollider, Vector3 positionAtSchedule, float delay, float sizeMultiplier)
    {
        yield return new WaitForSeconds(delay);
        if (strikeEffectPrefab == null || followCollider == null) yield break;
        SpawnStrikeEffectFollowing(followCollider, positionAtSchedule, sizeMultiplier);
    }

    private void RequestVariant2Strike(GameObject triggerEnemyRoot)
    {
        if (!isVariant2Active) return;
        if (mainCamera == null) return;

        if (variant2StrikeRoutine != null)
        {
            pendingVariant2StrikeRequests++;
            if (pendingVariant2TriggerEnemyRoot == null) pendingVariant2TriggerEnemyRoot = triggerEnemyRoot;
            return;
        }

        int maxStrikesPerFrame = 0;
        int damageNumbersPerFrame = 0;
        if (DamageNumberManager.Instance != null)
        {
            maxStrikesPerFrame = DamageNumberManager.Instance.MaxStrikesPerFrame;
            damageNumbersPerFrame = DamageNumberManager.Instance.DamageNumbersPerFrame;
        }

        Vector2 camCenter = mainCamera.transform.position;
        Vector2 camSize = new Vector2(mainCamera.orthographicSize * 2f * mainCamera.aspect, mainCamera.orthographicSize * 2f);
        Collider2D[] allEnemies = Physics2D.OverlapBoxAll(camCenter, camSize, 0f, enemyLayer);

        List<Collider2D> targets = new List<Collider2D>(allEnemies.Length);

        for (int i = 0; i < allEnemies.Length; i++)
        {
            Collider2D enemyCollider = allEnemies[i];
            if (enemyCollider == null) continue;

            Vector3 enemyViewport = mainCamera.WorldToViewportPoint(enemyCollider.transform.position);
            bool enemyOnScreen = enemyViewport.x >= 0f && enemyViewport.x <= 1f && enemyViewport.y >= 0f && enemyViewport.y <= 1f && enemyViewport.z > 0f;
            if (!enemyOnScreen) continue;

            GameObject enemyRoot = ResolveEnemyRootForStrike(enemyCollider.gameObject);
            if (enemyRoot == null) enemyRoot = enemyCollider.gameObject;

            if (pendingVariant2GlobalStrikeEnemies.Contains(enemyRoot)) continue;

            pendingDamageEnemies.Remove(enemyRoot);
            pendingVariant2GlobalStrikeEnemies.Add(enemyRoot);

            if (strikeEffectTimingAdjustment > 0f && strikeEffectPrefab != null)
            {
                Vector3 anchorPosition = GetStrikeEffectAnchorPosition(enemyRoot, enemyCollider);
                Vector2 effectOffset = GetStrikeEffectOffset(enemyRoot);
                Vector3 effectPosition = anchorPosition + (Vector3)effectOffset;
                SpawnStrikeEffectFollowing(enemyCollider, effectPosition, strikeEffectSizeMultiplier);
            }

            targets.Add(enemyCollider);
        }

        variant2StrikeRoutine = StartCoroutine(PerformVariant2StrikeRoutine(targets.ToArray(), maxStrikesPerFrame, damageNumbersPerFrame));
    }

    private IEnumerator PerformVariant2StrikeRoutine(Collider2D[] targets, int maxStrikesPerFrame, int damageNumbersPerFrame)
    {
        try
        {
            if (targets == null || targets.Length == 0)
            {
                yield break;
            }

            if (damageDelay > 0f)
            {
                yield return new WaitForSeconds(damageDelay);
            }

            bool playedStrikeSound = false;

            for (int i = 0; i < targets.Length; i++)
            {
                Collider2D enemyCollider = targets[i];
                if (enemyCollider == null) continue;

                GameObject enemyRoot = ResolveEnemyRootForStrike(enemyCollider.gameObject);
                if (enemyRoot == null) enemyRoot = enemyCollider.gameObject;

                while (true)
                {
                    int frame = Time.frameCount;
                    if (frame != lastVariant2StrikeFrame)
                    {
                        lastVariant2StrikeFrame = frame;
                        variant2StrikesThisFrame = 0;
                    }

                    bool atMaxStrikes = maxStrikesPerFrame > 0 && variant2StrikesThisFrame >= maxStrikesPerFrame;
                    bool atMaxDamageNumbers = damageNumbersPerFrame > 0 && variant2StrikesThisFrame >= damageNumbersPerFrame;
                    if (!atMaxStrikes && !atMaxDamageNumbers) break;

                    yield return null;
                }

                if (mainCamera == null)
                {
                    pendingVariant2GlobalStrikeEnemies.Remove(enemyRoot);
                    continue;
                }

                Vector3 enemyViewport = mainCamera.WorldToViewportPoint(enemyCollider.transform.position);
                bool enemyOnScreen = enemyViewport.x >= 0f && enemyViewport.x <= 1f && enemyViewport.y >= 0f && enemyViewport.y <= 1f && enemyViewport.z > 0f;
                if (!enemyOnScreen)
                {
                    pendingVariant2GlobalStrikeEnemies.Remove(enemyRoot);
                    continue;
                }

                IDamageable damageable;
                EnemyHealth enemyHealth;
                GameObject damageObject;
                if (!TryGetCachedEnemyData(enemyRoot, out damageable, out enemyHealth, out damageObject) || damageable == null)
                {
                    pendingVariant2GlobalStrikeEnemies.Remove(enemyRoot);
                    continue;
                }
                if (!damageable.IsAlive)
                {
                    pendingVariant2GlobalStrikeEnemies.Remove(enemyRoot);
                    continue;
                }

                Vector3 enemyPosition = enemyCollider.transform.position;
                if (!OffscreenDamageChecker.CanTakeDamage(enemyPosition))
                {
                    pendingVariant2GlobalStrikeEnemies.Remove(enemyRoot);
                    continue;
                }

                Vector2 strikeCenter = (Vector2)transform.position + strikeZoneOffset;
                Vector3 hitNormal = (strikeCenter - (Vector2)enemyPosition).normalized;

                float baseDamageForEnemy = baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
                float finalDamage = baseDamageForEnemy;

                GameObject enemyObj = damageObject != null ? damageObject : enemyRoot;
                if (cachedPlayerStats != null)
                {
                    finalDamage = PlayerDamageHelper.ComputeContinuousProjectileDamage(cachedPlayerStats, enemyObj, baseDamageForEnemy, gameObject);
                }

                if (enemyHealth != null)
                {
                    enemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Thunder);
                }

                damageable.TakeDamage(finalDamage, enemyPosition, hitNormal);
                variant2StrikesThisFrame++;

                pendingVariant2GlobalStrikeEnemies.Remove(enemyRoot);
                damagedEnemies.Add(enemyRoot);

                if (strikeEffectPrefab != null && strikeEffectTimingAdjustment <= 0f)
                {
                    Collider2D followCol =
                        enemyRoot.GetComponent<Collider2D>() ??
                        enemyRoot.GetComponentInChildren<Collider2D>() ??
                        enemyRoot.GetComponentInParent<Collider2D>();

                    Vector3 anchorPosition = GetStrikeEffectAnchorPosition(enemyRoot, followCol);
                    Vector2 effectOffset = GetStrikeEffectOffset(enemyRoot);
                    Vector3 effectPosition = anchorPosition + (Vector3)effectOffset;

                    if (strikeEffectTimingAdjustment < 0f)
                    {
                        StartCoroutine(SpawnDelayedEffectFollowCollider(followCol, effectPosition, Mathf.Abs(strikeEffectTimingAdjustment), strikeEffectSizeMultiplier));
                    }
                    else
                    {
                        if (followCol != null)
                        {
                            SpawnStrikeEffectFollowing(followCol, effectPosition, strikeEffectSizeMultiplier);
                        }
                    }
                }

                if (!playedStrikeSound)
                {
                    playedStrikeSound = true;
                    if (strikeClip != null && _audioSource != null)
                    {
                        _audioSource.PlayOneShot(strikeClip, strikeVolume);
                    }
                }
            }
        }
        finally
        {
            pendingVariant2GlobalStrikeEnemies.Clear();
            variant2StrikeRoutine = null;

            if (pendingVariant2StrikeRequests > 0)
            {
                pendingVariant2StrikeRequests = 0;
                GameObject trigger = pendingVariant2TriggerEnemyRoot;
                pendingVariant2TriggerEnemyRoot = null;
                if (isActiveAndEnabled)
                {
                    RequestVariant2Strike(trigger);
                }
            }
        }
    }

    private bool IsOffScreen()
    {
        if (minPos == null || maxPos == null) return false;

        float leftBoundary = minPos.position.x;
        float rightBoundary = maxPos.position.x;
        float x = transform.position.x;

        return isMovingRight ? x > rightBoundary : x < leftBoundary;
    }

    public bool CheckBirdOverlap(Vector3 testPosition)
    {
        if (Time.frameCount != lastSpawnFrame)
        {
            currentFrameSpawnPositions.Clear();
            lastSpawnFrame = Time.frameCount;
        }

        foreach (Vector3 spawnPos in currentFrameSpawnPositions)
        {
            float distance = Vector3.Distance(testPosition, spawnPos);
            if (distance < sameSideMinDistance) return true;

            bool testIsLeft = testPosition.x < 0f;
            bool spawnIsLeft = spawnPos.x < 0f;

            if (testIsLeft != spawnIsLeft)
            {
                float yDistance = Mathf.Abs(testPosition.y - spawnPos.y);
                if (yDistance < crossDirectionMinDistance) return true;
            }
        }

        ThunderBird[] allBirds = FindObjectsOfType<ThunderBird>();
        foreach (ThunderBird bird in allBirds)
        {
            if (bird == this) continue;

            Vector3 birdPos = bird.transform.position;

            float distance = Vector3.Distance(testPosition, birdPos);
            if (distance < sameSideMinDistance) return true;

            bool testIsLeft = testPosition.x < 0f;
            bool birdIsLeft = birdPos.x < 0f;

            if (testIsLeft != birdIsLeft)
            {
                float yDistance = Mathf.Abs(testPosition.y - birdPos.y);
                if (yDistance < crossDirectionMinDistance) return true;
            }
        }

        return false;
    }

    public void RecordSpawnPosition(Vector3 spawnPosition)
    {
        if (Time.frameCount != lastSpawnFrame)
        {
            currentFrameSpawnPositions.Clear();
            lastSpawnFrame = Time.frameCount;
        }

        currentFrameSpawnPositions.Add(spawnPosition);
    }

    public void SetMovementDirection(bool movingRight, bool didSpawnFromLeft)
    {
        isMovingRight = movingRight;
        spawnedFromLeft = didSpawnFromLeft;
        if (spriteRenderer != null) spriteRenderer.flipX = !isMovingRight;
    }

    public void ApplyInstantModifiers(CardModifierStats modifiers)
    {
        float newSpeed;

        bool variant1Active = (enhancedVariant == 1) || isVariant12Active;
        if (variant1Active)
        {
            float baseVersionSpeed = baseFlySpeed + modifiers.speedIncrease;
            float v1SpeedBonus = variant1Component != null ? variant1Component.GetSpeedBonus() : 0f;
            newSpeed = baseVersionSpeed + v1SpeedBonus;
        }
        else
        {
            newSpeed = baseFlySpeed + modifiers.speedIncrease;
        }

        if (!Mathf.Approximately(newSpeed, flySpeed))
        {
            flySpeed = Mathf.Max(1f, newSpeed);
            if (_rigidbody2D != null)
            {
                Vector2 direction = isMovingRight ? Vector2.right : Vector2.left;
                _rigidbody2D.velocity = direction * flySpeed;
            }
        }

        float newStrikeZone = (baseStrikeZoneRadius + modifiers.strikeZoneRadiusBonus) * modifiers.strikeZoneRadiusMultiplier;
        strikeZoneRadius = newStrikeZone;

        float instantVariantSizeMultiplier = 1f;
        if (variant1Active)
        {
            float v1SizeMult = variant1Component != null ? variant1Component.GetSizeMultiplier() : 1f;
            if (!Mathf.Approximately(v1SizeMult, 1f)) instantVariantSizeMultiplier = v1SizeMult;
        }

        float instantFinalSizeMultiplier = instantVariantSizeMultiplier * modifiers.sizeMultiplier;

        if (!Mathf.Approximately(instantFinalSizeMultiplier, 1f))
        {
            transform.localScale = baseScale * instantFinalSizeMultiplier;
        }
        else
        {
            transform.localScale = baseScale;
        }

        strikeZoneOffset = new Vector2(strikeZoneOffset.x, transform.localScale.y * 1.5f);
        damage = baseDamage + modifiers.damageFlat;
        baseDamageAfterCards = damage;
    }

    private void OnDestroy()
    {
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;
        Vector3 offset = new Vector3(strikeZoneOffset.x, strikeZoneOffset.y, 0f);
        Vector3 strikeCenter = center + offset;

        Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
        Gizmos.DrawSphere(strikeCenter, strikeZoneRadius);

        Gizmos.color = new Color(1f, 1f, 0f, 0.9f);
        Gizmos.DrawWireSphere(strikeCenter, strikeZoneRadius);
    }
}