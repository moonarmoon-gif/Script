using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PolygonCollider2D))]
[RequireComponent(typeof(EnemyHealth))]
public class NorcthexEnemy : MonoBehaviour
{
    [Header("Core Settings")]
    [Tooltip("Boss attack damage used for scaling summons and projectiles")]
    [SerializeField] private float attackDamage = 20f;

    [Tooltip("Movement speed used only for knockback or minor adjustments")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("Teleport Settings")]
    [Tooltip("Duration of teleport-in animation")]
    public float teleportInDuration = 1.0f;

    [Tooltip("Duration of teleport-out animation")]
    public float teleportOutDuration = 1.0f;

    [Tooltip("Offset from camera center for initial spawn")]
    public Vector2 initialSpawnOffset = Vector2.zero;

    [Header("Sprite Settings")]
    [Tooltip("Invert sprite flip direction (if Norcthex sprite is backwards)")]
    [SerializeField] private bool invertFlip = false;

    [Tooltip("Minimum radius around player that NO enemy can be summoned")]
    public float minSummonDistanceFromPlayer = 2f;

    [Tooltip("Radius around player where FAR summons are forbidden")]
    public float farRestrictiveRadius = 9f;

    [Tooltip("Radius around player where Norcthex will try NOT to teleport. If no valid position exists, this is ignored.")]
    public float bossRestrictiveRadius = 9f;

    [Tooltip("Delay after first teleport-in before initial summon")]
    public float initialSummonDelay = 1f;

    [Tooltip("Idle time after Norcthex's initial summon before starting normal behaviour")]
    public float PostInitialSummonIdleTime = 1f;

    [Header("Idle Timings")]
    [Tooltip("Min idle time after a summon before next decision")]
    public float postSummonIdleTimeMin = 0.5f;

    [Tooltip("Max idle time after a summon before next decision")]
    public float postSummonIdleTimeMax = 1.5f;

    [Tooltip("Idle time after completing full attack animation")]
    public float postAttackIdleDuration = 2f;

    [Tooltip("Idle time after completing a teleport-in/out cycle before next decision")]
    public float postTeleportIdleDuration = 1f;

    [Tooltip("Time Norcthex stays in specialattack animation when summoning")]
    public float specialAttackDuration = 1f;

    public int maxConsecutiveAttacks = 3;
    public float attackFavourPerSummon = 1f;

    [Header("Summon Settings")]
    [Tooltip("Two far enemy prefabs (e.g., flying/archer)")]
    public GameObject[] farSummonPrefabs;

    [Tooltip("Single near enemy prefab (e.g., melee skeleton)")]
    private GameObject nearSummonPrefab;

    public GameObject[] nearSummonPrefabs;

    [Tooltip("Starting number of enemies to summon (first summon)")]
    [SerializeField] private int minSummonCount = 3;

    [Tooltip("Maximum number of enemies to summon per wave")]
    [SerializeField] private int maxSummonCount = 10;

    [Tooltip("Increase in summon count per summon wave")]
    [SerializeField] private int summonCountIncrease = 1;

    [Tooltip("Extra min summon count added per boss spawn index (element 1 = +1x, element 2 = +2x, etc.)")]
    [SerializeField] private int extraMinSummonCountPerBoss = 3;

    [Tooltip("Extra max summon count added per boss spawn index (element 1 = +1x, element 2 = +2x, etc.)")]
    [SerializeField] private int extraMaxSummonCountPerBoss = 10;

    [Tooltip("Extra amount added to summonCountIncrease per boss spawn index (element 1 = +1x, element 2 = +2x, etc.)")]
    public int ExtraSummonCountIncrease = 2;

    [Tooltip("Minimum distance between summoned enemies")]
    [SerializeField] private float minDistanceBetweenSummons = 1f;

    [Tooltip("Cooldown between summon waves (seconds)")]
    public float summonCooldown = 15f;

    [Tooltip("Additional seconds added to summonCooldown after each summon wave")]
    public float summonCooldownIncreasePerWave = 0.5f;

    [Header("Summon Scaling")]
    [Tooltip("Health increase percentage per summon wave for summoned enemies (10 = +10% per wave, multiplicative)")]
    [SerializeField] private float summonHealthIncreasePercent = 10f;

    [Tooltip("Damage increase percentage per summon wave for summoned enemies (1 = +1% per wave, multiplicative)")]
    [SerializeField] private float summonDamageIncreasePercent = 1f;

    [Tooltip("When enabled, SummonHealth/DamageIncreasePercent scales multiplicatively per wave. When disabled, it scales additively (+% each wave).")]
    public bool useMultiplicativeSummonScaling = false;

    [Header("Summon Base Percentages")]
    [Tooltip("Base health percent of Norcthex's max health given to each summoned enemy (0.05 = 5%).")]
    public float summonBaseHealthPercent = 0.05f;

    [Tooltip("Base attack damage percent of Norcthex's attackDamage given to each summoned enemy (1 = 100%).")]
    public float summonBaseAttackPercent = 1f;

    [System.Serializable]
    private class SummonPrefabOverrides
    {
        public GameObject prefab;
        [Range(0f, 1f)] public float summonBaseHealthPercent = 0f;
        public float summonBaseAttackPercent = 0f;
    }

    [SerializeField] private SummonPrefabOverrides[] farSummonOverrides;
    [SerializeField] private SummonPrefabOverrides[] nearSummonOverrides;

    private int currentSummonCount = 0;
    private int totalSummonWaves = 0;
    private int consecutiveAttacks = 0;
    private float summonHealthMultiplier = 1f;
    private float summonDamageMultiplier = 1f;
    private float nextSummonTime = -999f;

    public void ApplyBossSpawnIndex(int bossIndex)
    {
        if (bossIndex < 0)
        {
            bossIndex = 0;
        }

        int scaledMin = minSummonCount + bossIndex * extraMinSummonCountPerBoss;
        int scaledMax = maxSummonCount + bossIndex * extraMaxSummonCountPerBoss;

        int scaledIncrease = summonCountIncrease + bossIndex * ExtraSummonCountIncrease;

        if (scaledMin < 0)
        {
            scaledMin = 0;
        }

        if (scaledMax < scaledMin)
        {
            scaledMax = scaledMin;
        }

        if (scaledIncrease < 0)
        {
            scaledIncrease = 0;
        }

        minSummonCount = scaledMin;
        maxSummonCount = scaledMax;
        summonCountIncrease = scaledIncrease;
    }

    // Track all enemies summoned by Norcthex so we can instantly kill them
    // if the boss dies (even mid-summon).
    private readonly List<EnemyHealth> activeSummons = new List<EnemyHealth>();

    [Header("Summon Area Tag (4-point system)")]
    [Tooltip("Tag for point A (top-left): determines minX and minY")]
    [SerializeField] private string pointATag = "NorcthexSummonArea_A";

    [Tooltip("Tag for point B (top-right): determines maxX and minY")]
    [SerializeField] private string pointBTag = "NorcthexSummonArea_B";

    [Tooltip("Tag for point C (bottom-right): determines maxX and maxY")]
    [SerializeField] private string pointCTag = "NorcthexSummonArea_C";

    [Tooltip("Tag for point D (bottom-left): determines minX and maxY")]
    [SerializeField] private string pointDTag = "NorcthexSummonArea_D";

    private Transform pointA;
    private Transform pointB;
    private Transform pointC;
    private Transform pointD;

    [Header("Projectile Settings (Long-Range Attack)")]
    [Tooltip("Projectile prefab to spawn for ranged attack")]
    public GameObject projectilePrefab;

    [Tooltip("Transform point where projectiles spawn (child object)")]
    public Transform firePoint;

    [Tooltip("Additional offset for firepoint when sprite is flipped (facing right)")]
    public Vector2 flippedFirePointOffset = Vector2.zero;

    [Tooltip("Time Norcthex stays in attackstart animation")]
    public float attackStartDuration = 0.5f;

    [Tooltip("Time Norcthex stays in main attack animation")]
    public float attackDuration = 0.5f;

    [Tooltip("Time Norcthex stays in attackend animation")]
    public float attackEndDuration = 0.5f;

    [Tooltip("Delay from start of main attack to projectile spawn time")]
    public float projectileSpawnTiming = 0.25f;

    [Tooltip("Damage dealt by Norcthex's projectile")]
    public float projectileDamage = 20f;

    [Header("Death Settings")]
    [Tooltip("Delay before cleanup after death")]
    public float deathCleanupDelay = 2f;
    [Tooltip("Duration of visual fade-out after death before cleanup.")]
    public float DeathFadeOutDuration = 0.25f;

    [Header("Knockback Settings")]
    [Tooltip("Knockback force when hit by projectiles")]
    public float knockbackIntensity = 5f;

    [Tooltip("How long knockback lasts")]
    public float knockbackDuration = 0.2f;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;
    [SerializeField] private PolygonCollider2D capsuleCollider;

    private EnemyHealth health;
    private StatusController statusController;
    private IDamageable playerDamageable;
    private bool isDead;
    private bool behaviorStarted;
    private Coroutine behaviorRoutine;

    private Vector2 knockbackVelocity = Vector2.zero;
    private float knockbackEndTime = 0f;

    private SpriteFlipOffset spriteFlipOffset;

    // Store the original collider shape so we can mirror it when flipping
    private Vector2[] baseColliderPath;

    // FirePoint management
    private Vector3 firePointBaseLocalPosition;
    private bool firePointCached = false;

    // NEW: prevent teleporting to the same position repeatedly
    [Header("Teleport Anti-Repeat")]
    [SerializeField, Tooltip("Minimum distance required between consecutive teleports (world units).")]
    private float minTeleportDistanceFromLast = 1.0f;

    [SerializeField, Tooltip("How many alternative attempts to find a different teleport position before allowing same position.")]
    private int maxSamePositionAvoidAttempts = 120;

    private Vector3 lastTeleportPosition = new Vector3(999999f, 999999f, 0f);
    private bool hasTeleportedAtLeastOnce;

    private StaticStatus cachedStaticStatus;

    private void ResetAnimatorBools()
    {
        if (animator == null) return;

        animator.SetBool("idle", false);
        animator.SetBool("teleportin", false);
        animator.SetBool("teleportout", false);
        animator.SetBool("attackstart", false);
        animator.SetBool("attack", false);
        animator.SetBool("attackend", false);
        animator.SetBool("specialattack", false);
    }

    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<Animator>();
        if (capsuleCollider == null) capsuleCollider = GetComponent<PolygonCollider2D>();
        health = GetComponent<EnemyHealth>();
        statusController = GetComponent<StatusController>();

        projectileDamage = attackDamage;

        if (capsuleCollider != null)
        {
            capsuleCollider.isTrigger = false;

            if (capsuleCollider.pathCount > 0)
            {
                baseColliderPath = capsuleCollider.GetPath(0);
            }
        }
        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Default"), true);
        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Enemy"), true);

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        if (capsuleCollider != null)
        {
            capsuleCollider.enabled = false;
        }

        if (AdvancedPlayerController.Instance != null)
        {
            playerDamageable = AdvancedPlayerController.Instance.GetComponent<IDamageable>();
            AdvancedPlayerController.Instance.GetComponent<PlayerHealth>().OnDeath += OnPlayerDeath;
        }

        spriteFlipOffset = GetComponent<SpriteFlipOffset>();

        if (firePoint != null)
        {
            firePointBaseLocalPosition = firePoint.localPosition;
            firePointCached = true;
        }

        if (!string.IsNullOrEmpty(pointATag))
        {
            GameObject obj = GameObject.FindGameObjectWithTag(pointATag);
            if (obj != null) pointA = obj.transform;
        }
        if (!string.IsNullOrEmpty(pointBTag))
        {
            GameObject obj = GameObject.FindGameObjectWithTag(pointBTag);
            if (obj != null) pointB = obj.transform;
        }
        if (!string.IsNullOrEmpty(pointCTag))
        {
            GameObject obj = GameObject.FindGameObjectWithTag(pointCTag);
            if (obj != null) pointC = obj.transform;
        }
        if (!string.IsNullOrEmpty(pointDTag))
        {
            GameObject obj = GameObject.FindGameObjectWithTag(pointDTag);
            if (obj != null) pointD = obj.transform;
        }
    }

    void OnEnable()
    {
        if (health != null)
        {
            health.OnDeath += HandleDeath;
        }
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.OnDeath -= HandleDeath;
        }

        if (AdvancedPlayerController.Instance != null)
        {
            var playerHealth = AdvancedPlayerController.Instance.GetComponent<PlayerHealth>();
            if (playerHealth != null)
                playerHealth.OnDeath -= OnPlayerDeath;
        }
    }

    void Update()
    {
        if (isDead) return;

        if (IsStaticFrozen())
        {
            return;
        }

        bool oldFlipX = spriteRenderer != null && spriteRenderer.flipX;
        if (AdvancedPlayerController.Instance != null)
        {
            Vector3 toPlayer = AdvancedPlayerController.Instance.transform.position - transform.position;
            bool shouldFlip = toPlayer.x <= 0;
            spriteRenderer.flipX = invertFlip ? !shouldFlip : shouldFlip;
        }

        if (capsuleCollider != null && baseColliderPath != null && spriteRenderer != null)
        {
            bool newFlipX = spriteRenderer.flipX;
            if (newFlipX != oldFlipX)
            {
                Vector2[] path = new Vector2[baseColliderPath.Length];
                for (int i = 0; i < baseColliderPath.Length; i++)
                {
                    path[i] = new Vector2(-baseColliderPath[i].x, baseColliderPath[i].y);
                }

                capsuleCollider.SetPath(0, newFlipX ? path : baseColliderPath);
            }
        }

        if (firePoint != null && firePointCached)
        {
            if (spriteRenderer.flipX)
            {
                firePoint.localPosition = new Vector3(
                    -firePointBaseLocalPosition.x + flippedFirePointOffset.x,
                    firePointBaseLocalPosition.y + flippedFirePointOffset.y,
                    firePointBaseLocalPosition.z
                );
            }
            else
            {
                firePoint.localPosition = firePointBaseLocalPosition;
            }
        }

        if (animator != null)
        {
            bool teleOut = animator.GetBool("teleportout");
            bool teleIn = animator.GetBool("teleportin");
            if ((teleOut || teleIn) && animator.GetBool("idle"))
            {
                // No-op: rely on the teleport coroutines.
            }
        }
    }

    void LateUpdate()
    {
        if (spriteFlipOffset != null)
        {
            bool isDying = animator.GetBool("dead") || animator.GetBool("deadflip");
            if (isDying)
            {
                spriteFlipOffset.SetColliderOffsetEnabled(false);
                spriteFlipOffset.SetShadowOffsetEnabled(false);
            }
            else
            {
                spriteFlipOffset.SetColliderOffsetEnabled(true);
                spriteFlipOffset.SetShadowOffsetEnabled(true);
            }
        }
    }

    void FixedUpdate()
    {
        if (isDead)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        if (IsStaticFrozen())
        {
            rb.velocity = Vector2.zero;

            float dt = Time.fixedDeltaTime;
            if (dt > 0f)
            {
                knockbackEndTime += dt;
            }
            return;
        }

        if (Time.time < knockbackEndTime)
        {
            rb.velocity = knockbackVelocity;
            return;
        }
        else if (knockbackVelocity != Vector2.zero)
        {
            knockbackVelocity = Vector2.zero;
        }

        rb.velocity = Vector2.zero;
    }

    public void StartBossBehavior()
    {
        if (behaviorStarted || isDead) return;
        behaviorStarted = true;
        behaviorRoutine = StartCoroutine(BossBehaviorRoutine());
    }

    IEnumerator BossBehaviorRoutine()
    {
        yield return StartCoroutine(InitialTeleportInAtCameraCenter());

        float initialDelay = Mathf.Max(0f, initialSummonDelay);
        if (statusController != null)
        {
            initialDelay += statusController.GetLethargyAttackCooldownBonus();
        }
        if (initialDelay < 0f)
        {
            initialDelay = 0f;
        }
        if (initialDelay > 0f)
        {
            yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
                initialDelay,
                () => isDead,
                () => IsStaticFrozen());
        }
        if (!isDead)
        {
            yield return StaticPauseHelper.WaitWhileStatic(
                () => isDead,
                () => IsStaticFrozen());

            yield return StartCoroutine(PerformSummon());

            if (PostInitialSummonIdleTime > 0f)
            {
                float firstIdle = PostInitialSummonIdleTime;
                if (statusController != null)
                {
                    firstIdle += statusController.GetLethargyAttackCooldownBonus();
                }
                if (firstIdle < 0f)
                {
                    firstIdle = 0f;
                }
                if (firstIdle > 0f)
                {
                    yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
                        firstIdle,
                        () => isDead,
                        () => IsStaticFrozen());
                }
            }
        }

        while (!isDead && AdvancedPlayerController.Instance != null && AdvancedPlayerController.Instance.enabled)
        {
            if (IsStaticFrozen())
            {
                float dt = GameStateManager.GetPauseSafeDeltaTime();
                if (dt > 0f)
                {
                    nextSummonTime += dt;
                }

                yield return null;
                continue;
            }

            bool canSummon = Time.time >= nextSummonTime;

            if (canSummon)
            {
                yield return StartCoroutine(PerformSummon());

                float idleTime = Random.Range(postSummonIdleTimeMin, postSummonIdleTimeMax);
                if (statusController != null)
                {
                    idleTime += statusController.GetLethargyAttackCooldownBonus();
                }
                if (idleTime < 0f)
                {
                    idleTime = 0f;
                }
                if (idleTime > 0f)
                {
                    yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
                        idleTime,
                        () => isDead,
                        () => IsStaticFrozen());
                }
                continue;
            }

            float teleportChance = 0.5f;
            if (attackFavourPerSummon != 0f && totalSummonWaves > 1)
            {
                int biasWaves = totalSummonWaves - 1;
                if (biasWaves < 0) biasWaves = 0;
                float bias = attackFavourPerSummon * 0.01f * biasWaves;
                teleportChance = Mathf.Clamp01(0.5f - bias);
            }
            bool chooseTeleport = Random.value < teleportChance;

            if (!chooseTeleport && maxConsecutiveAttacks > 0 && consecutiveAttacks >= maxConsecutiveAttacks)
            {
                chooseTeleport = true;
            }

            if (chooseTeleport)
            {
                consecutiveAttacks = 0;

                yield return StartCoroutine(PerformTeleportCycle());

                if (isDead) yield break;

                float teleIdle = postTeleportIdleDuration;
                if (statusController != null)
                {
                    teleIdle += statusController.GetLethargyAttackCooldownBonus();
                }
                if (teleIdle < 0f)
                {
                    teleIdle = 0f;
                }
                if (teleIdle > 0f)
                {
                    yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
                        teleIdle,
                        () => isDead,
                        () => IsStaticFrozen());
                }
            }
            else
            {
                consecutiveAttacks++;

                yield return StartCoroutine(PerformAttackSequence());

                if (isDead) yield break;

                float postAttackIdle = postAttackIdleDuration;
                if (statusController != null)
                {
                    postAttackIdle += statusController.GetLethargyAttackCooldownBonus();
                }
                if (postAttackIdle < 0f)
                {
                    postAttackIdle = 0f;
                }
                if (postAttackIdle > 0f)
                {
                    yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
                        postAttackIdle,
                        () => isDead,
                        () => IsStaticFrozen());
                }
            }
        }
    }

    IEnumerator InitialTeleportInAtCameraCenter()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            yield break;
        }

        Vector3 center = mainCam.transform.position;
        Vector3 spawnPos = new Vector3(center.x + initialSpawnOffset.x, center.y + initialSpawnOffset.y, transform.position.z);
        spawnPos = ClampPositionToSummonArea(spawnPos);
        transform.position = spawnPos;

        // NEW: record so first teleport cycle cannot pick the same spot.
        lastTeleportPosition = transform.position;
        hasTeleportedAtLeastOnce = true;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        if (capsuleCollider != null)
        {
            capsuleCollider.enabled = true;
        }

        ResetAnimatorBools();
        animator.SetBool("idle", false);
        animator.SetBool("teleportin", true);

        yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
            teleportInDuration,
            () => isDead,
            () => IsStaticFrozen());

        ResetAnimatorBools();
        animator.SetBool("idle", true);
    }

    IEnumerator PerformTeleportCycle()
    {
        if (isDead)
        {
            yield break;
        }

        Vector3 oldPos = transform.position;

        ResetAnimatorBools();
        animator.SetBool("idle", false);
        animator.SetBool("teleportout", true);

        yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
            teleportOutDuration,
            () => isDead,
            () => IsStaticFrozen());

        if (isDead)
        {
            yield break;
        }

        animator.SetBool("teleportout", false);

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        if (capsuleCollider != null)
        {
            capsuleCollider.enabled = false;
        }

        Vector3 newPos;
        bool found = TryGetRandomValidBossPosition(out newPos);

        if (!found)
        {
            if (AdvancedPlayerController.Instance != null)
            {
                Vector2 playerPos = AdvancedPlayerController.Instance.transform.position;
                float minRadius = Mathf.Max(minSummonDistanceFromPlayer, bossRestrictiveRadius + 0.5f);

                bool placedInsideArea = false;

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

                    int dirAttempts = 40;
                    for (int i = 0; i < dirAttempts; i++)
                    {
                        Vector2 dir = Random.insideUnitCircle.normalized;
                        if (dir.sqrMagnitude < 0.0001f)
                        {
                            dir = Vector2.right;
                        }

                        Vector2 candidate = playerPos + dir * minRadius;

                        if (candidate.x < finalMinX || candidate.x > finalMaxX ||
                            candidate.y < finalMinY || candidate.y > finalMaxY)
                        {
                            continue;
                        }

                        newPos = new Vector3(candidate.x, candidate.y, oldPos.z);
                        placedInsideArea = true;
                        break;
                    }
                }

                if (!placedInsideArea)
                {
                    Vector2 dir = Random.insideUnitCircle.normalized;
                    if (dir.sqrMagnitude < 0.0001f)
                    {
                        dir = Vector2.right;
                    }

                    Vector2 candidate = playerPos + dir * minRadius;
                    newPos = new Vector3(candidate.x, candidate.y, oldPos.z);
                }
            }
            else
            {
                newPos = oldPos;
            }
        }

        if (isDead)
        {
            yield break;
        }

        // Clamp into area
        newPos = ClampPositionToSummonArea(newPos);

        // NEW: enforce "cannot teleport in same position"
        newPos = EnsureNotSameTeleportPosition(newPos, oldPos);

        transform.position = newPos;
        lastTeleportPosition = newPos;
        hasTeleportedAtLeastOnce = true;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        if (capsuleCollider != null)
        {
            capsuleCollider.enabled = true;
        }

        ResetAnimatorBools();

        animator.SetBool("idle", false);
        animator.SetBool("teleportin", true);

        yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
            teleportInDuration,
            () => isDead,
            () => IsStaticFrozen());

        if (isDead)
        {
            yield break;
        }

        ResetAnimatorBools();
        animator.SetBool("idle", true);
    }

    IEnumerator PerformAttackSequence()
    {
        if (!IsWithinCameraBounds())
        {
            yield break;
        }

        ResetAnimatorBools();
        animator.SetBool("attackstart", true);
        yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
            attackStartDuration,
            () => isDead,
            () => IsStaticFrozen());
        animator.SetBool("attackstart", false);

        animator.SetBool("attack", true);

        float elapsed = 0f;
        bool projectileSpawned = false;

        while (elapsed < attackDuration)
        {
            if (isDead)
            {
                yield break;
            }

            if (IsStaticFrozen())
            {
                yield return null;
                continue;
            }

            if (!projectileSpawned && elapsed >= projectileSpawnTiming)
            {
                yield return StaticPauseHelper.WaitWhileStatic(
                    () => isDead,
                    () => IsStaticFrozen());

                SpawnProjectile();
                projectileSpawned = true;
            }

            float dt = GameStateManager.GetPauseSafeDeltaTime();
            if (dt > 0f)
            {
                elapsed += dt;
            }
            yield return null;
        }

        animator.SetBool("attack", false);

        animator.SetBool("attackend", true);
        yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
            attackEndDuration,
            () => isDead,
            () => IsStaticFrozen());
        animator.SetBool("attackend", false);

        ResetAnimatorBools();
        animator.SetBool("idle", true);
    }

    IEnumerator PerformSummon()
    {
        if (isDead) yield break;

        ResetAnimatorBools();
        animator.SetBool("specialattack", true);

        float castDuration = Mathf.Max(0.1f, specialAttackDuration);
        yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
            castDuration,
            () => isDead,
            () => IsStaticFrozen());

        animator.SetBool("specialattack", false);

        ResetAnimatorBools();
        animator.SetBool("idle", true);

        yield return StaticPauseHelper.WaitWhileStatic(
            () => isDead,
            () => IsStaticFrozen());

        SummonEnemiesWithNearFarLogic();

        float cooldown = summonCooldown;
        if (statusController != null)
        {
            cooldown += statusController.GetLethargyAttackCooldownBonus();
        }
        if (cooldown < 0f)
        {
            cooldown = 0f;
        }
        nextSummonTime = Time.time + cooldown;
        if (summonCooldownIncreasePerWave != 0f)
        {
            summonCooldown += summonCooldownIncreasePerWave;
        }
    }

    void SummonEnemiesWithNearFarLogic()
    {
        if (currentSummonCount <= 0)
        {
            currentSummonCount = minSummonCount;
        }

        totalSummonWaves++;

        float healthStep = Mathf.Max(0f, summonHealthIncreasePercent) / 100f;
        float damageStep = Mathf.Max(0f, summonDamageIncreasePercent) / 100f;
        int extraWaves = Mathf.Max(0, totalSummonWaves - 1);

        if (useMultiplicativeSummonScaling)
        {
            summonHealthMultiplier = Mathf.Pow(1f + healthStep, extraWaves);
            summonDamageMultiplier = Mathf.Pow(1f + damageStep, extraWaves);
        }
        else
        {
            summonHealthMultiplier = 1f + healthStep * extraWaves;
            summonDamageMultiplier = 1f + damageStep * extraWaves;
        }

        List<Vector2> spawnedPositions = new List<Vector2>();
        int successfulSummons = 0;
        int maxAttempts = currentSummonCount * 15;
        int attempts = 0;

        Debug.Log($"<color=magenta>Norcthex Summon Wave {totalSummonWaves}: target {currentSummonCount} enemies (health x{summonHealthMultiplier:F2}, damage x{summonDamageMultiplier:F2})</color>");

        while (successfulSummons < currentSummonCount && attempts < maxAttempts)
        {
            if (isDead)
            {
                break;
            }

            attempts++;

            GameObject prefabToSpawn = ChooseRandomSummonPrefab();
            if (prefabToSpawn == null)
            {
                break;
            }

            bool isFarType = IsFarPrefab(prefabToSpawn);

            bool gotSpawnPos = TryGetRandomSummonPosition(isFarType, out Vector2 spawnPos);

            if (!gotSpawnPos)
            {
                gotSpawnPos = TryGetRadialSummonFallback(isFarType, out spawnPos);

                if (!gotSpawnPos && isFarType)
                {
                    GameObject nearPrefab = ChooseRandomNearSummonPrefab();
                    if (nearPrefab != null)
                    {
                        prefabToSpawn = nearPrefab;
                        isFarType = false;

                        gotSpawnPos = TryGetRandomSummonPosition(false, out spawnPos) ||
                                     TryGetRadialSummonFallback(false, out spawnPos);
                    }
                }

                if (!gotSpawnPos)
                {
                    continue;
                }
            }

            bool validPosition = true;
            foreach (Vector2 existingPos in spawnedPositions)
            {
                if (Vector2.Distance(spawnPos, existingPos) < minDistanceBetweenSummons)
                {
                    validPosition = false;
                    break;
                }
            }

            if (!validPosition)
            {
                continue;
            }

            Vector3 clampedSpawnWorld = ClampPositionToSummonArea(new Vector3(spawnPos.x, spawnPos.y, transform.position.z));
            spawnPos = new Vector2(clampedSpawnWorld.x, clampedSpawnWorld.y);

            GameObject summonedEnemy = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

            Debug.Log($"<color=magenta>Norcthex summoned {summonedEnemy.name} at {spawnPos} (#{successfulSummons + 1}/{currentSummonCount}, farType={isFarType})</color>");

            EnemyHealth summonedHealth = summonedEnemy.GetComponent<EnemyHealth>();
            if (summonedHealth != null)
            {
                activeSummons.Add(summonedHealth);

                summonedHealth.ignoreScalingFromEnemyScalingSystem = true;

                if (health != null)
                {
                    float bossMax = health.MaxHealth;
                    float baseSummonMax = summonedHealth.MaxHealth;
                    float baseHealthPercent = GetSummonBaseHealthPercent(prefabToSpawn, isFarType);
                    float clampedBaseHealthPercent = Mathf.Clamp01(baseHealthPercent);
                    if (bossMax > 0f && baseSummonMax > 0f && clampedBaseHealthPercent > 0f)
                    {
                        float targetSummonMax = bossMax * clampedBaseHealthPercent;
                        float ratio = targetSummonMax / baseSummonMax;
                        summonedHealth.MultiplyMaxHealth(ratio);
                    }
                }

                if (summonHealthMultiplier > 1f)
                {
                    summonedHealth.MultiplyMaxHealth(summonHealthMultiplier);
                }
            }

            var skeleton = summonedEnemy.GetComponent<SkeletonEnemy>();
            if (skeleton != null)
            {
                float baseAttackPercent = GetSummonBaseAttackPercent(prefabToSpawn, isFarType);
                float clampedBaseAttackPercent = Mathf.Clamp01(baseAttackPercent);
                if (clampedBaseAttackPercent > 0f)
                {
                    skeleton.SetBaseDamageFromBoss(attackDamage, clampedBaseAttackPercent);
                }

                if (summonDamageMultiplier > 1f)
                {
                    skeleton.MultiplyAttackDamage(summonDamageMultiplier);
                }
            }
            else
            {
                var flyingDemon = summonedEnemy.GetComponent<FlyingDemonEnemy>();
                if (flyingDemon != null)
                {
                    float baseAttackPercent = GetSummonBaseAttackPercent(prefabToSpawn, isFarType);
                    float clampedBaseAttackPercent = Mathf.Clamp01(baseAttackPercent);
                    if (clampedBaseAttackPercent > 0f)
                    {
                        flyingDemon.SetBaseDamageFromBoss(attackDamage, clampedBaseAttackPercent);
                    }

                    if (summonDamageMultiplier > 1f)
                    {
                        flyingDemon.MultiplyAttackDamage(summonDamageMultiplier);
                    }
                }
            }

            spawnedPositions.Add(spawnPos);
            successfulSummons++;
        }

        Debug.Log($"<color=magenta>Norcthex Summon Wave {totalSummonWaves} result: spawned {successfulSummons}/{currentSummonCount} enemies after {attempts} attempts</color>");

        currentSummonCount = Mathf.Min(currentSummonCount + summonCountIncrease, maxSummonCount);
    }

    private void KillAllSummons()
    {
        if (activeSummons == null || activeSummons.Count == 0)
        {
            return;
        }

        EnemyDamagePopupScope.BeginSuppressPopups();
        try
        {
            for (int i = 0; i < activeSummons.Count; i++)
            {
                EnemyHealth summoned = activeSummons[i];
                if (summoned == null) continue;

                if (summoned.IsAlive)
                {
                    Vector3 hitPoint = summoned.transform.position;
                    Vector3 hitNormal = Vector3.up;
                    float lethalDamage = summoned.MaxHealth + 999f;
                    summoned.TakeDamage(lethalDamage, hitPoint, hitNormal);
                }
            }
        }
        finally
        {
            EnemyDamagePopupScope.EndSuppressPopups();
        }

        activeSummons.Clear();
    }

    GameObject ChooseRandomSummonPrefab()
    {
        int farCount = farSummonPrefabs != null ? farSummonPrefabs.Length : 0;
        int nearCount = nearSummonPrefabs != null ? nearSummonPrefabs.Length : 0;
        bool hasLegacyNear = nearSummonPrefab != null;

        int totalOptions = farCount + nearCount + (hasLegacyNear ? 1 : 0);
        if (totalOptions == 0) return null;

        int index = Random.Range(0, totalOptions);

        if (index < farCount)
        {
            return farSummonPrefabs[index];
        }

        index -= farCount;

        if (index < nearCount)
        {
            return nearSummonPrefabs[index];
        }

        return nearSummonPrefab;
    }

    GameObject ChooseRandomNearSummonPrefab()
    {
        int nearCount = nearSummonPrefabs != null ? nearSummonPrefabs.Length : 0;
        bool hasLegacyNear = nearSummonPrefab != null;

        int totalOptions = nearCount + (hasLegacyNear ? 1 : 0);
        if (totalOptions == 0) return null;

        int index = Random.Range(0, totalOptions);
        if (index < nearCount)
        {
            return nearSummonPrefabs[index];
        }

        return nearSummonPrefab;
    }

    bool IsFarPrefab(GameObject prefab)
    {
        if (prefab == null || farSummonPrefabs == null) return false;
        for (int i = 0; i < farSummonPrefabs.Length; i++)
        {
            if (farSummonPrefabs[i] == prefab) return true;
        }
        return false;
    }

    float GetSummonBaseHealthPercent(GameObject prefab, bool isFarType)
    {
        float result = -1f;
        SummonPrefabOverrides[] source = isFarType ? farSummonOverrides : nearSummonOverrides;

        if (source != null && prefab != null)
        {
            for (int i = 0; i < source.Length; i++)
            {
                SummonPrefabOverrides entry = source[i];
                if (entry != null && entry.prefab == prefab && entry.summonBaseHealthPercent > 0f)
                {
                    result = entry.summonBaseHealthPercent;
                    break;
                }
            }
        }

        if (result <= 0f)
        {
            result = summonBaseHealthPercent;
        }

        return result;
    }

    float GetSummonBaseAttackPercent(GameObject prefab, bool isFarType)
    {
        float result = -1f;
        SummonPrefabOverrides[] source = isFarType ? farSummonOverrides : nearSummonOverrides;

        if (source != null && prefab != null)
        {
            for (int i = 0; i < source.Length; i++)
            {
                SummonPrefabOverrides entry = source[i];
                if (entry != null && entry.prefab == prefab && entry.summonBaseAttackPercent > 0f)
                {
                    result = entry.summonBaseAttackPercent;
                    break;
                }
            }
        }

        if (result <= 0f)
        {
            result = summonBaseAttackPercent;
        }

        return result;
    }

    bool TryGetRadialSummonFallback(bool isFarType, out Vector2 spawnPos)
    {
        spawnPos = transform.position;

        if (AdvancedPlayerController.Instance == null)
        {
            return false;
        }

        Vector2 playerPos = AdvancedPlayerController.Instance.transform.position;
        Vector2 dir = (Random.insideUnitCircle.normalized);
        spawnPos = playerPos + dir * minSummonDistanceFromPlayer;
        return true;
    }

    bool TryGetRandomSummonPosition(bool isFarType, out Vector2 spawnPos)
    {
        spawnPos = transform.position;

        if (pointA == null || pointB == null || pointC == null || pointD == null)
        {
            if (AdvancedPlayerController.Instance == null)
            {
                return false;
            }

            Vector2 playerPos = AdvancedPlayerController.Instance.transform.position;
            Vector2 dir = (Random.insideUnitCircle.normalized);
            spawnPos = playerPos + dir * minSummonDistanceFromPlayer;
            return true;
        }

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

        int maxAttempts = 30;
        for (int i = 0; i < maxAttempts; i++)
        {
            float x = Random.Range(finalMinX, finalMaxX);
            float y = Random.Range(finalMinY, finalMaxY);
            Vector2 candidate = new Vector2(x, y);

            if (!IsValidSummonPosition(candidate, isFarType))
            {
                continue;
            }

            spawnPos = candidate;
            return true;
        }

        return false;
    }

    bool IsValidSummonPosition(Vector2 pos, bool isFarType)
    {
        if (AdvancedPlayerController.Instance == null)
        {
            return true;
        }

        Vector2 playerPos = AdvancedPlayerController.Instance.transform.position;
        float distToPlayer = Vector2.Distance(pos, playerPos);

        if (distToPlayer < minSummonDistanceFromPlayer)
        {
            return false;
        }

        if (isFarType && distToPlayer < farRestrictiveRadius)
        {
            return false;
        }

        return true;
    }

    bool TryGetRandomValidBossPosition(out Vector3 newPos)
    {
        newPos = transform.position;

        if (pointA == null || pointB == null || pointC == null || pointD == null || AdvancedPlayerController.Instance == null)
        {
            return false;
        }

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

        Vector2 playerPos = AdvancedPlayerController.Instance.transform.position;
        int maxAttempts = Mathf.Max(1, maxSamePositionAvoidAttempts);
        float minDistSq = minSummonDistanceFromPlayer * minSummonDistanceFromPlayer;
        float bossRestrictiveSq = bossRestrictiveRadius * bossRestrictiveRadius;
        float minTeleportDistSq = Mathf.Max(0f, minTeleportDistanceFromLast) * Mathf.Max(0f, minTeleportDistanceFromLast);

        for (int i = 0; i < maxAttempts; i++)
        {
            float x = Random.Range(finalMinX, finalMaxX);
            float y = Random.Range(finalMinY, finalMaxY);
            Vector2 candidate = new Vector2(x, y);

            Vector2 toPlayer = candidate - playerPos;
            float distSq = toPlayer.sqrMagnitude;

            if (distSq < minDistSq || distSq < bossRestrictiveSq)
            {
                continue;
            }

            Vector3 candidate3 = new Vector3(candidate.x, candidate.y, transform.position.z);

            // NEW: reject positions too close to the last teleport position
            if (hasTeleportedAtLeastOnce && (candidate3 - lastTeleportPosition).sqrMagnitude < minTeleportDistSq)
            {
                continue;
            }

            newPos = candidate3;
            return true;
        }

        return false;
    }

    Vector3 ClampPositionToSummonArea(Vector3 position)
    {
        if (pointA == null || pointB == null || pointC == null || pointD == null)
        {
            return position;
        }

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

        float clampedX = Mathf.Clamp(position.x, finalMinX, finalMaxX);
        float clampedY = Mathf.Clamp(position.y, finalMinY, finalMaxY);
        return new Vector3(clampedX, clampedY, position.z);
    }

    bool IsWithinCameraBounds()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return false;

        Vector3 viewportPos = mainCam.WorldToViewportPoint(transform.position);
        return viewportPos.x >= 0f && viewportPos.x <= 1f && viewportPos.y >= 0f && viewportPos.y <= 1f;
    }

    void SpawnProjectile()
    {
        if (isDead)
        {
            return;
        }

        if (projectilePrefab == null || firePoint == null || AdvancedPlayerController.Instance == null)
        {
            return;
        }

        Vector3 spawnPos = firePoint.position;
        Transform playerTransform = AdvancedPlayerController.Instance.transform;
        Vector3 targetPos = playerTransform.position;
        Collider2D playerCol = AdvancedPlayerController.Instance.GetComponent<Collider2D>();
        if (playerCol == null)
        {
            playerCol = AdvancedPlayerController.Instance.GetComponentInChildren<Collider2D>();
        }
        if (playerCol != null)
        {
            targetPos = playerCol.bounds.center;
        }

        Vector2 dir = ((Vector2)targetPos - (Vector2)spawnPos).normalized;

        GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

        SpriteRenderer[] projSprites = proj.GetComponentsInChildren<SpriteRenderer>(true);
        if (projSprites != null && projSprites.Length > 0 && spriteRenderer != null)
        {
            bool norcthexFlipped = spriteRenderer.flipX;
            for (int i = 0; i < projSprites.Length; i++)
            {
                if (projSprites[i] != null)
                {
                    projSprites[i].flipX = true;
                    projSprites[i].flipY = !norcthexFlipped;
                }
            }
        }

        if (proj.TryGetComponent<NecromancerProjectile>(out var necroProj))
        {
            necroProj.Initialize(projectileDamage, dir, capsuleCollider);
        }
    }

    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (isDead) return;

        knockbackVelocity = direction.normalized * force * knockbackIntensity;
        knockbackEndTime = Time.time + knockbackDuration;
    }

    void OnPlayerDeath()
    {
        rb.velocity = Vector2.zero;
        if (behaviorRoutine != null)
        {
            StopCoroutine(behaviorRoutine);
            behaviorRoutine = null;
        }
        animator.SetBool("attackstart", false);
        animator.SetBool("attack", false);
        animator.SetBool("attackend", false);
        animator.SetBool("specialattack", false);
        animator.SetBool("teleportin", false);
        animator.SetBool("teleportout", false);
        animator.SetBool("idle", true);
    }

    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        KillAllSummons();

        if (behaviorRoutine != null)
        {
            StopCoroutine(behaviorRoutine);
        }

        bool isFlipped = spriteRenderer != null && spriteRenderer.flipX;
        animator.SetBool("dead", !isFlipped);
        animator.SetBool("deadflip", isFlipped);

        animator.SetBool("attackstart", false);
        animator.SetBool("attack", false);
        animator.SetBool("attackend", false);
        animator.SetBool("specialattack", false);
        animator.SetBool("teleportin", false);
        animator.SetBool("teleportout", false);
        animator.SetBool("idle", false);

        rb.velocity = Vector2.zero;
        knockbackVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static;
        capsuleCollider.enabled = false;

        StartCoroutine(DeathCleanupRoutine());
    }

    private IEnumerator DeathCleanupRoutine()
    {
        float cleanupDelay = Mathf.Max(0f, deathCleanupDelay);
        if (cleanupDelay > 0f)
        {
            yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
                cleanupDelay,
                () => false,
                IsStaticFrozen);
        }

        float fadeDuration = Mathf.Max(0f, DeathFadeOutDuration);
        SpriteRenderer shadowRenderer = null;
        if (spriteFlipOffset != null && spriteFlipOffset.shadowTransform != null)
        {
            shadowRenderer = spriteFlipOffset.shadowTransform.GetComponent<SpriteRenderer>();
        }

        if (fadeDuration > 0f && (spriteRenderer != null || shadowRenderer != null))
        {
            Color startColor = spriteRenderer != null ? spriteRenderer.color : default;
            Color shadowStartColor = shadowRenderer != null ? shadowRenderer.color : default;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                float dt = GameStateManager.GetPauseSafeDeltaTime();
                if (dt > 0f)
                {
                    elapsed += dt;
                }
                float t = Mathf.Clamp01(elapsed / fadeDuration);

                if (spriteRenderer != null)
                {
                    Color c = startColor;
                    c.a = Mathf.Lerp(startColor.a, 0f, t);
                    spriteRenderer.color = c;
                }

                if (shadowRenderer != null)
                {
                    Color c = shadowStartColor;
                    c.a = Mathf.Lerp(shadowStartColor.a, 0f, t);
                    shadowRenderer.color = c;
                }
                yield return null;
            }
        }

        Destroy(gameObject);
    }

    private bool IsStaticFrozen()
    {
        return StaticPauseHelper.IsStaticFrozen(this, ref cachedStaticStatus);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;

        if (collision.gameObject.CompareTag("Projectile"))
        {
            // Projectiles handle damage via EnemyHealth component
        }
        else if (collision.gameObject.CompareTag("Player"))
        {
            // Physical contact with player
        }
    }

    public void MultiplyAttackDamage(float multiplier)
    {
        if (multiplier <= 0f) return;
        attackDamage *= multiplier;
        projectileDamage = attackDamage;
    }

    private Vector3 EnsureNotSameTeleportPosition(Vector3 newPos, Vector3 fallbackReference)
    {
        float minDistance = Mathf.Max(0.1f, minTeleportDistanceFromLast);
        float minDistanceSq = minDistance * minDistance;

        Vector3 reference = hasTeleportedAtLeastOnce ? lastTeleportPosition : fallbackReference;

        if ((newPos - reference).sqrMagnitude >= minDistanceSq)
        {
            return newPos;
        }

        Vector2 dir = Random.insideUnitCircle.normalized;
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = Vector2.right;
        }

        Vector3 nudged = newPos + new Vector3(dir.x, dir.y, 0f) * minDistance;
        return ClampPositionToSummonArea(nudged);
    }

    private void OnDrawGizmosSelected()
    {
        // Lazily resolve summon area corner points by tag if references are missing
        if (pointA == null && !string.IsNullOrEmpty(pointATag))
        {
            GameObject obj = GameObject.FindGameObjectWithTag(pointATag);
            if (obj != null) pointA = obj.transform;
        }

        if (pointB == null && !string.IsNullOrEmpty(pointBTag))
        {
            GameObject obj = GameObject.FindGameObjectWithTag(pointBTag);
            if (obj != null) pointB = obj.transform;
        }

        if (pointC == null && !string.IsNullOrEmpty(pointCTag))
        {
            GameObject obj = GameObject.FindGameObjectWithTag(pointCTag);
            if (obj != null) pointC = obj.transform;
        }

        if (pointD == null && !string.IsNullOrEmpty(pointDTag))
        {
            GameObject obj = GameObject.FindGameObjectWithTag(pointDTag);
            if (obj != null) pointD = obj.transform;
        }

        if (pointA == null || pointB == null || pointC == null || pointD == null)
        {
            return;
        }

        Vector3 a = pointA.position;
        Vector3 b = pointB.position;
        Vector3 c = pointC.position;
        Vector3 d = pointD.position;

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(a, 0.3f);
        Gizmos.DrawSphere(b, 0.3f);
        Gizmos.DrawSphere(c, 0.3f);
        Gizmos.DrawSphere(d, 0.3f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);

        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawLine(a, c);
        Gizmos.DrawLine(b, d);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(a + Vector3.up * 0.5f, "A (Top-Left)");
        UnityEditor.Handles.Label(b + Vector3.up * 0.5f, "B (Top-Right)");
        UnityEditor.Handles.Label(c + Vector3.down * 0.5f, "C (Bottom-Right)");
        UnityEditor.Handles.Label(d + Vector3.down * 0.5f, "D (Bottom-Left)");
#endif
    }
}