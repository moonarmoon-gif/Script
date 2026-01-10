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

    [Header("Mana & Cooldown")]
    [SerializeField] private int manaCost = 20;
    [SerializeField] private float cooldown = 2f;

    [Header("Collider Scaling")]
    [Tooltip("Offset for collider size relative to visual size (0 = same as visual, -0.2 = 20% smaller, 0.2 = 20% larger)")]
    [SerializeField] private float colliderSizeOffset = 0f;

    [Header("Enhanced Variant 1 - Dual Thunder")]
    [Tooltip("Strike radius increase for Enhanced Variant 1 (0.25 = +25%)")]
    [SerializeField] private float enhancedStrikeRadiusIncrease = 0.25f;

    [Tooltip("Speed bonus for Enhanced Variant 1")]
    public float variant1SpeedBonus = 10f;

    [Tooltip("Cooldown reduction for Enhanced Variant 1 (0.25 = 25% reduction)")]
    public float variant1CooldownReduction = 0.25f;

    [Tooltip("Base cooldown for Enhanced Variant 1 (seconds). If 0, falls back to ProjectileCards.runtimeSpawnInterval or script cooldown.")]
    public float variant1BaseCooldown = 0f;

    [Tooltip("Size multiplier for Enhanced Variant 1 (Dual Thunder). Applies on top of normal size modifiers.")]
    public float variant1SizeMultiplier = 1f;

    [Header("Enhanced Variant 2 - Slow Striker")]
    [Tooltip("Movement speed for Variant 2 (slow horizontal flight)")]
    public float variant2Speed = 3f;

    [Tooltip("Strike interval for Variant 2 (periodic strikes in seconds)")]
    public float variant2StrikeInterval = 3f;

    [Tooltip("Base cooldown for Variant 2 (in seconds)")]
    public float variant2BaseCooldown = 30f;
    public float variant12BaseCooldown = 0f;

    public float variant2MinStrikeInterval = 1f;
    public float variant2MinSpeed = 1f;

    [Tooltip("Size increase for Variant 2 (1.25 = 25% larger)")]
    public float variant2SizeMultiplier = 1.25f;

    [Tooltip("Animation speed decrease for Variant 2 (0.8 = 20% slower)")]
    public float variant2AnimationSpeed = 0.8f;

    [Tooltip("Unique strike effect size multiplier for Variant 2 (1.5 = 50% larger)")]
    public float variant2StrikeEffectSizeMultiplier = 1.5f;

    [Tooltip("Camera offset for 'on camera' detection (positive = bird needs to be further in, negative = bird can be further out)")]
    public float variant2CameraOffset = 0f;

    [Header("Enhanced Variant 2 - Modifier Exchange Rates")]
    [Tooltip("Exchange rate: strikeZoneRadius → strikeInterval reduction (e.g., 1.0 = each +1 radius reduces interval by 1 second)")]
    public float variant2StrikeZoneToIntervalRate = 1f;

    [Tooltip("Exchange rate: speed modifier → speed reduction for Variant 2 (e.g., 0.1 = each +1 speed reduces Variant 2 speed by 0.1)")]
    public float variant2SpeedReductionRate = 0.1f;

    [SerializeField] private int variant2MaxEffectsPerStrike = 999;
    [SerializeField] private bool debugVariant2Logging = false;

    // Variant 2 runtime variables
    private float nextStrikeTime = 0f;
    private bool isVariant2Active = false;
    private bool isVariant12Active = false;
    private bool isVariant12TopBird = false;
    private bool hasPerformedFirstStrike = false;
    private int damageNumbersShownThisStrike = 0;
    private int effectsSpawnedThisStrike = 0;

    private Rigidbody2D _rigidbody2D;
    private Collider2D _collider2D;
    private AudioSource _audioSource;
    private Animator _animator;
    private bool isMovingRight = true;
    private Camera mainCamera;
    private float spawnTime;

    private HashSet<GameObject> damagedEnemies = new HashSet<GameObject>();
    private Dictionary<GameObject, float> pendingDamageEnemies = new Dictionary<GameObject, float>();

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

    // Track if bird spawned from left (moving right) or right (moving left)
    private bool spawnedFromLeft = false;

    // Base values for instant modifier recalculation
    private float baseFlySpeed;
    private float baseStrikeZoneRadius;
    private float baseLifetimeSeconds;
    private float baseDamage;
    private Vector3 baseScale;
    private PlayerStats cachedPlayerStats;
    private float baseDamageAfterCards;

    [Header("Offscreen Destruction Grace Periods")]
    public float baseGracePeriod = 10f;
    public float variant1GracePeriod = 10f;
    public float variant2GracePeriod = 10f;

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

    // Static tracking for Variant 2 per-frame strike throttling
    private static int lastVariant2StrikeFrame = -1;
    private static int variant2StrikesThisFrame = 0;

    private Coroutine variant2StrikeRoutine;
    private bool pendingVariant2StrikeRequest;

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

        isVariant12Active = hasVariant1History && hasVariant2History;

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

        isVariant2Active = (enhancedVariant == 2) || isVariant12Active;

        float enhancedStrikeRadiusMult = 1f;
        if (enhancedVariant == 1 && !isVariant2Active)
        {
            enhancedStrikeRadiusMult = 1f + enhancedStrikeRadiusIncrease;
        }

        float baseCooldown = cooldown;
        if (isVariant12Active && variant12BaseCooldown > 0f)
        {
            baseCooldown = variant12BaseCooldown;
            if (card != null) card.runtimeSpawnInterval = variant12BaseCooldown;
        }
        else if (isVariant2Active)
        {
            baseCooldown = variant2BaseCooldown;
            if (card != null) card.runtimeSpawnInterval = variant2BaseCooldown;
        }
        else if (card != null && card.runtimeSpawnInterval > 0f)
        {
            baseCooldown = card.runtimeSpawnInterval;
        }

        if (enhancedVariant == 1 && variant1BaseCooldown > 0f && !isVariant12Active)
        {
            baseCooldown = variant1BaseCooldown;
            if (card != null) card.runtimeSpawnInterval = Mathf.Max(0.1f, variant1BaseCooldown);
        }

        if (enhancedVariant == 1)
        {
            baseCooldown *= (1f - variant1CooldownReduction);
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
        int finalManaCost = Mathf.Max(0, Mathf.CeilToInt(manaCost * (1f - modifiers.manaCostReduction)));
        damage += modifiers.damageFlat;

        float baseVersionSpeed = baseFlySpeed + modifiers.speedIncrease;
        float currentStrikeZoneRadius = strikeZoneRadius;
        float baseRadiusForStrike = baseStrikeZoneRadius;

        if (enhancedVariant == 1)
        {
            baseRadiusForStrike = baseStrikeZoneRadius * enhancedStrikeRadiusMult;
            if (variant1SpeedBonus != 0f) baseVersionSpeed += variant1SpeedBonus;
        }

        float baseVersionStrikeZone = (baseRadiusForStrike + modifiers.strikeZoneRadiusBonus) * modifiers.strikeZoneRadiusMultiplier;

        if (isVariant2Active)
        {
            float speedIncrease = baseVersionSpeed - baseFlySpeed;
            float speedReduction = speedIncrease * variant2SpeedReductionRate;
            float targetSpeed = variant2Speed - speedReduction;
            flySpeed = Mathf.Max(variant2MinSpeed, targetSpeed);

            float strikeZoneIncrease = baseVersionStrikeZone - currentStrikeZoneRadius;
            float intervalReduction = strikeZoneIncrease * variant2StrikeZoneToIntervalRate;
            variant2StrikeInterval = Mathf.Max(variant2MinStrikeInterval, variant2StrikeInterval - intervalReduction);

            nextStrikeTime = Time.time;
        }
        else
        {
            strikeZoneRadius = baseVersionStrikeZone * Mathf.Sqrt(modifiers.sizeMultiplier);
            flySpeed = baseVersionSpeed;
        }

        float variantSizeMultiplier = 1f;
        if (enhancedVariant == 1 && variant1SizeMultiplier != 1f) variantSizeMultiplier = variant1SizeMultiplier;
        else if (isVariant2Active && variant2SizeMultiplier != 1f) variantSizeMultiplier = variant2SizeMultiplier;

        float finalSizeMultiplier = variantSizeMultiplier * modifiers.sizeMultiplier;

        if (!Mathf.Approximately(finalSizeMultiplier, 1f))
        {
            transform.localScale = baseScale * finalSizeMultiplier;
            ColliderScaler.ScaleCollider(_collider2D, finalSizeMultiplier, colliderSizeOffset);
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

        bool bypassEnhancedFirstSpawnCooldown = false;
        if (!skipCooldownCheck && card != null && card.applyEnhancedFirstSpawnReduction && card.pendingEnhancedFirstSpawn)
        {
            if (card.projectileSystem == ProjectileCards.ProjectileSystemType.Passive)
            {
                // Thunderbird exception: if Variant 2 was already chosen and the
                // player later chooses Variant 1 (stacked V1+V2), do NOT apply
                // the enhanced first-spawn cooldown bypass.
                if (!isVariant12Active)
                {
                    bypassEnhancedFirstSpawnCooldown = true;
                    card.pendingEnhancedFirstSpawn = false;
                }
            }
        }

        prefabKey = "ThunderBird";

        if (!skipCooldownCheck)
        {
            if (!bypassEnhancedFirstSpawnCooldown && lastFireTimes.ContainsKey(prefabKey))
            {
                if (Time.time - lastFireTimes[prefabKey] < finalCooldown)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            PlayerMana playerMana = FindObjectOfType<PlayerMana>();
            if (playerMana != null && !playerMana.Spend(finalManaCost))
            {
                Destroy(gameObject);
                return;
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
            if (isVariant2Active && !isVariant12Active)
            {
                float minY = minPos.position.y;
                float maxY = maxPos.position.y;
                float centerY = (minY + maxY) / 2f;

                bool spawnLeft = Random.value < 0.5f;
                float spawnX = spawnLeft ? minPos.position.x : maxPos.position.x;
                isMovingRight = spawnLeft;
                spawnedFromLeft = spawnLeft;

                transform.position = new Vector3(spawnX, centerY, spawnPosition.z);

                if (_animator != null) _animator.speed = variant2AnimationSpeed;
                if (spriteRenderer != null) spriteRenderer.flipX = !isMovingRight;
            }
            else if (enhancedVariant == 1 || isVariant12Active)
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

                if (isVariant12Active)
                {
                    if (_animator != null) _animator.speed = variant2AnimationSpeed;
                    isVariant12TopBird = spawnInTopZone;
                }

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

        if (isVariant2Active)
        {
            nextStrikeTime = Time.time + variant2StrikeInterval;
        }

        StartCoroutine(FlyRoutine(finalLifetime));
        StartCoroutine(DamageDetectionRoutine());
    }

    private IEnumerator FlyRoutine(float lifetime)
    {
        float elapsedTime = 0f;

        float gracePeriod = baseGracePeriod;
        if (isVariant2Active) gracePeriod = variant2GracePeriod;
        else if (enhancedVariant == 1) gracePeriod = variant1GracePeriod;

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

            if (isVariant2Active)
            {
                if (!birdCanDamageHere)
                {
                    yield return new WaitForSeconds(0.1f);
                    continue;
                }

                bool shouldStrike = false;

                if (!hasPerformedFirstStrike && !IsOffScreenForVariant2())
                {
                    if (isVariant12Active && !isVariant12TopBird)
                    {
                        hasPerformedFirstStrike = true;
                        nextStrikeTime = Time.time + (variant2StrikeInterval * 0.5f);
                    }
                    else
                    {
                        shouldStrike = true;
                        hasPerformedFirstStrike = true;
                        nextStrikeTime = Time.time + variant2StrikeInterval;
                    }
                }
                else if (Time.time >= nextStrikeTime)
                {
                    shouldStrike = true;
                    nextStrikeTime = Time.time + variant2StrikeInterval;
                }

                if (shouldStrike && !IsOffScreenForVariant2())
                {
                    PerformVariant2Strike();
                }

                // Apply pending damage for V2
                List<GameObject> ready = new List<GameObject>();
                foreach (var kvp in pendingDamageEnemies)
                {
                    if (Time.time >= kvp.Value) ready.Add(kvp.Key);
                }

                foreach (GameObject enemy in ready)
                {
                    if (enemy == null)
                    {
                        pendingDamageEnemies.Remove(enemy);
                        continue;
                    }

                    IDamageable damageable;
                    EnemyHealth enemyHealth;
                    GameObject enemyObject;

                    if (!TryGetCachedEnemyData(enemy, out damageable, out enemyHealth, out enemyObject) || damageable == null || !damageable.IsAlive)
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
                    Vector3 hitNormal = (transform.position - enemyPosition).normalized;

                    float baseDamageForEnemy = baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
                    float finalDamage = baseDamageForEnemy;

                    GameObject damageTarget = enemyObject != null ? enemyObject : enemy;

                    if (cachedPlayerStats != null)
                    {
                        finalDamage = PlayerDamageHelper.ComputeContinuousProjectileDamage(cachedPlayerStats, damageTarget, baseDamageForEnemy, gameObject);
                    }

                    if (enemyHealth != null)
                    {
                        enemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Thunder);
                    }

                    damageable.TakeDamage(finalDamage, enemyPosition, hitNormal);
                    pendingDamageEnemies.Remove(enemy);
                }

                yield return new WaitForSeconds(0.1f);
                continue;
            }

            if (!birdCanDamageHere)
            {
                yield return new WaitForFixedUpdate();
                continue;
            }

            Vector2 strikeCenter = (Vector2)transform.position + strikeZoneOffset;
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(strikeCenter, strikeZoneRadius, enemyLayer);

            foreach (Collider2D hitCollider in hitColliders)
            {
                if (hitCollider == null) continue;

                if (damagedEnemies.Contains(hitCollider.gameObject)) continue;
                if (pendingDamageEnemies.ContainsKey(hitCollider.gameObject)) continue;

                IDamageable damageable = hitCollider.GetComponent<IDamageable>() ?? hitCollider.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                if (!OffscreenDamageChecker.CanTakeDamage(hitCollider.transform.position)) continue;

                pendingDamageEnemies[hitCollider.gameObject] = Time.time + damageDelay;

                // Early strike VFX (follows collider, preserves per-enemy offsets, freezes on death)
                if (strikeEffectTimingAdjustment > 0f && strikeEffectPrefab != null)
                {
                    Vector3 anchorPosition = GetStrikeEffectAnchorPosition(hitCollider.gameObject, hitCollider);
                    Vector2 effectOffset = GetStrikeEffectOffset(hitCollider.gameObject);
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

                Component damageableComponent = damageable as Component;
                GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : enemy;

                if (cachedPlayerStats != null)
                {
                    finalDamage = PlayerDamageHelper.ComputeContinuousProjectileDamage(cachedPlayerStats, enemyObject, baseDamageForEnemy, gameObject);
                }

                EnemyHealth enemyHealth1 = enemy.GetComponent<EnemyHealth>() ?? enemy.GetComponentInParent<EnemyHealth>();
                if (enemyHealth1 != null)
                {
                    enemyHealth1.SetLastIncomingDamageType(DamageNumberManager.DamageType.Thunder);
                }

                damageable.TakeDamage(finalDamage, enemyPosition, hitNormal);

                if (!isVariant2Active)
                {
                    damagedEnemies.Add(enemy);
                }

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

    private IEnumerator SpawnDelayedVariant2EffectFollowCollider(Collider2D followCollider, Vector3 positionAtSchedule, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (strikeEffectPrefab == null || followCollider == null) yield break;
        SpawnStrikeEffectFollowing(followCollider, positionAtSchedule, variant2StrikeEffectSizeMultiplier);
    }

    private void PerformVariant2Strike()
    {
        if (mainCamera == null) return;

        if (variant2StrikeRoutine != null)
        {
            pendingVariant2StrikeRequest = true;
            return;
        }

        int maxStrikesPerFrame = 0;
        if (DamageNumberManager.Instance != null)
        {
            maxStrikesPerFrame = DamageNumberManager.Instance.ThunderbirdV2MaxStrikesPerFrame;
        }

        Collider2D[] allEnemies = Physics2D.OverlapBoxAll(
            mainCamera.transform.position,
            new Vector2(mainCamera.orthographicSize * mainCamera.aspect, mainCamera.orthographicSize),
            0f,
            enemyLayer
        );

        variant2StrikeRoutine = StartCoroutine(PerformVariant2StrikeRoutine(allEnemies, maxStrikesPerFrame));
    }

    private IEnumerator PerformVariant2StrikeRoutine(Collider2D[] allEnemies, int maxStrikesPerFrame)
    {
        if (mainCamera == null)
        {
            variant2StrikeRoutine = null;
            yield break;
        }

        int hitCount = 0;
        effectsSpawnedThisStrike = 0;
        damageNumbersShownThisStrike = 0;
        bool playedStrikeSound = false;

        for (int i = 0; i < allEnemies.Length; i++)
        {
            Collider2D enemyCollider = allEnemies[i];
            if (enemyCollider == null)
            {
                continue;
            }

            // If we hit the cap for THIS frame, delay by exactly 1 frame and
            // continue processing remaining enemies next frame.
            while (maxStrikesPerFrame > 0 && variant2StrikesThisFrame >= maxStrikesPerFrame)
            {
                if (debugVariant2Logging)
                {
                    Debug.Log($"<color=cyan>ThunderBird V2: hit cap {maxStrikesPerFrame} reached for frame {Time.frameCount}, deferring remaining enemies by 1 frame</color>");
                }

                yield return null;

                int frame = Time.frameCount;
                if (frame != lastVariant2StrikeFrame)
                {
                    lastVariant2StrikeFrame = frame;
                    variant2StrikesThisFrame = 0;
                }
            }

            Vector3 enemyViewport = mainCamera.WorldToViewportPoint(enemyCollider.transform.position);
            bool enemyOnScreen = enemyViewport.x >= 0f && enemyViewport.x <= 1f &&
                                 enemyViewport.y >= 0f && enemyViewport.y <= 1f &&
                                 enemyViewport.z > 0f;

            if (!enemyOnScreen)
            {
                continue;
            }

            GameObject enemyRoot = enemyCollider.gameObject;

            IDamageable damageable;
            EnemyHealth enemyHealth;
            GameObject damageObject;

            if (!TryGetCachedEnemyData(enemyRoot, out damageable, out enemyHealth, out damageObject) || damageable == null) continue;
            if (!damageable.IsAlive) continue;

            Vector3 enemyPosition = enemyCollider.transform.position;

            bool registeredStrikeThisEnemy = false;

            if (damageDelay > 0f)
            {
                if (!OffscreenDamageChecker.CanTakeDamage(enemyPosition))
                {
                    continue;
                }

                if (!pendingDamageEnemies.ContainsKey(enemyRoot))
                {
                    pendingDamageEnemies[enemyRoot] = Time.time + damageDelay;

                    if (strikeEffectTimingAdjustment > 0f && strikeEffectPrefab != null && effectsSpawnedThisStrike < variant2MaxEffectsPerStrike)
                    {
                        Vector3 anchorPosition = GetStrikeEffectAnchorPosition(enemyRoot, enemyCollider);
                        Vector2 effectOffset = GetStrikeEffectOffset(enemyRoot);
                        Vector3 effectPosition = anchorPosition + (Vector3)effectOffset;

                        SpawnStrikeEffectFollowing(enemyCollider, effectPosition, variant2StrikeEffectSizeMultiplier);
                        effectsSpawnedThisStrike++;
                    }

                    variant2StrikesThisFrame++;
                    registeredStrikeThisEnemy = true;
                }
            }
            else
            {
                Vector3 hitNormal = (transform.position - enemyPosition).normalized;

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
                hitCount++;
                variant2StrikesThisFrame++;
                registeredStrikeThisEnemy = true;

                if (strikeEffectPrefab != null && effectsSpawnedThisStrike < variant2MaxEffectsPerStrike)
                {
                    Vector3 anchorPosition = GetStrikeEffectAnchorPosition(enemyRoot, enemyCollider);
                    Vector2 effectOffset = GetStrikeEffectOffset(enemyRoot);
                    Vector3 effectPosition = anchorPosition + (Vector3)effectOffset;

                    if (strikeEffectTimingAdjustment < 0f)
                    {
                        StartCoroutine(SpawnDelayedVariant2EffectFollowCollider(enemyCollider, effectPosition, Mathf.Abs(strikeEffectTimingAdjustment)));
                    }
                    else
                    {
                        SpawnStrikeEffectFollowing(enemyCollider, effectPosition, variant2StrikeEffectSizeMultiplier);
                        effectsSpawnedThisStrike++;
                    }
                }
            }

            if (registeredStrikeThisEnemy && !playedStrikeSound)
            {
                playedStrikeSound = true;
                if (strikeClip != null && _audioSource != null)
                {
                    _audioSource.PlayOneShot(strikeClip, strikeVolume);
                }
            }
        }

        variant2StrikeRoutine = null;

        if (pendingVariant2StrikeRequest)
        {
            pendingVariant2StrikeRequest = false;
            PerformVariant2Strike();
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

    private bool IsOffScreenForVariant2()
    {
        if (mainCamera == null) return false;

        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;
        Vector3 camPos = mainCamera.transform.position;
        Vector3 pos = transform.position;

        float offset = variant2CameraOffset;

        float left = camPos.x - halfWidth + offset;
        float right = camPos.x + halfWidth - offset;
        float bottom = camPos.y - halfHeight + offset;
        float top = camPos.y + halfHeight - offset;

        if (left > right)
        {
            left = camPos.x - halfWidth;
            right = camPos.x + halfWidth;
        }
        if (bottom > top)
        {
            bottom = camPos.y - halfHeight;
            top = camPos.y + halfHeight;
        }

        return pos.x < left || pos.x > right || pos.y < bottom || pos.y > top;
    }

    private bool CheckBirdOverlap(Vector3 testPosition)
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

    public void ApplyInstantModifiers(CardModifierStats modifiers)
    {
        float newSpeed;

        if (isVariant2Active)
        {
            float baseVersionSpeed = baseFlySpeed + modifiers.speedIncrease;
            float speedIncrease = baseVersionSpeed - baseFlySpeed;
            float speedReduction = speedIncrease * variant2SpeedReductionRate;
            float targetSpeed = variant2Speed - speedReduction;
            newSpeed = Mathf.Max(variant2MinSpeed, targetSpeed);
        }
        else if (enhancedVariant == 1)
        {
            float baseVersionSpeed = baseFlySpeed + modifiers.speedIncrease;
            newSpeed = baseVersionSpeed + variant1SpeedBonus;
        }
        else
        {
            newSpeed = baseFlySpeed + modifiers.speedIncrease;
        }

        if (!Mathf.Approximately(newSpeed, flySpeed))
        {
            float minSpeed = isVariant2Active ? variant2MinSpeed : 1f;
            flySpeed = Mathf.Max(minSpeed, newSpeed);
            if (_rigidbody2D != null)
            {
                Vector2 direction = isMovingRight ? Vector2.right : Vector2.left;
                _rigidbody2D.velocity = direction * flySpeed;
            }
        }

        float newStrikeZone = (baseStrikeZoneRadius + modifiers.strikeZoneRadiusBonus) * modifiers.strikeZoneRadiusMultiplier;
        strikeZoneRadius = newStrikeZone;

        float instantVariantSizeMultiplier = 1f;
        if (enhancedVariant == 1 && variant1SizeMultiplier != 1f) instantVariantSizeMultiplier = variant1SizeMultiplier;
        else if (isVariant2Active && variant2SizeMultiplier != 1f) instantVariantSizeMultiplier = variant2SizeMultiplier;

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