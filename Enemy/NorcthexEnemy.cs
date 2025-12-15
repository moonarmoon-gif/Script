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
    private readonly System.Collections.Generic.List<EnemyHealth> activeSummons = new System.Collections.Generic.List<EnemyHealth>();

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

    // Centralized animator bool reset to prevent stuck animation states
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

        if (capsuleCollider != null)
        {
            capsuleCollider.isTrigger = false;

            // Cache base collider path for flip mirroring
            if (capsuleCollider.pathCount > 0)
            {
                baseColliderPath = capsuleCollider.GetPath(0);
            }
        }
        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Default"), true);
        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Enemy"), true);

        // Start completely hidden until the first teleport-in plays
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

        // Cache 4-point area transforms using tags
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

        // Face player if available (supports optional invertFlip like DeathBringer)
        bool oldFlipX = spriteRenderer != null && spriteRenderer.flipX;
        if (AdvancedPlayerController.Instance != null)
        {
            Vector3 toPlayer = AdvancedPlayerController.Instance.transform.position - transform.position;
            bool shouldFlip = toPlayer.x <= 0;
            spriteRenderer.flipX = invertFlip ? !shouldFlip : shouldFlip;
        }

        // If flip state changed, mirror collider path horizontally
        if (capsuleCollider != null && baseColliderPath != null && spriteRenderer != null)
        {
            bool newFlipX = spriteRenderer.flipX;
            if (newFlipX != oldFlipX)
            {
                Vector2[] path = new Vector2[baseColliderPath.Length];
                for (int i = 0; i < baseColliderPath.Length; i++)
                {
                    // Mirror X around local origin; Y stays the same
                    path[i] = new Vector2(-baseColliderPath[i].x, baseColliderPath[i].y);
                }

                // When flipX is false, use the original base path; when true, use mirrored path
                capsuleCollider.SetPath(0, newFlipX ? path : baseColliderPath);
            }
        }

        // Update FirePoint position for flip
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

        // SAFETY: While any teleport animation is active, idle must NOT be true.
        // This guarantees we never end up with idle=true and teleportin=false during
        // a teleport sequence, which can prevent the animator from transitioning
        // correctly from teleportout into teleportin.
        if (animator != null)
        {
            bool teleOut = animator.GetBool("teleportout");
            bool teleIn = animator.GetBool("teleportin");
            if ((teleOut || teleIn) && animator.GetBool("idle"))
            {
                // No-op: rely on the teleport coroutines to manage idle/teleport booleans.
            }
        }
    }

    void LateUpdate()
    {
        if (spriteFlipOffset != null)
        {
            // Like other bosses, disable SpriteFlipOffset only for death if needed
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

        // Handle knockback
        if (Time.time < knockbackEndTime)
        {
            rb.velocity = knockbackVelocity;
            return;
        }
        else if (knockbackVelocity != Vector2.zero)
        {
            knockbackVelocity = Vector2.zero;
        }

        // Norcthex does not walk around; movement only through knockback/teleport
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
        // Initial teleport in at camera center + offset
        yield return StartCoroutine(InitialTeleportInAtCameraCenter());

        // Initial summon after teleport in + InitialSummonDelay
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
            yield return new WaitForSeconds(initialDelay);
        }
        if (!isDead)
        {
            yield return StartCoroutine(PerformSummon());

            // Optional fixed idle duration after the very first summon
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
                    yield return new WaitForSeconds(firstIdle);
                }
            }
        }

        while (!isDead && AdvancedPlayerController.Instance != null && AdvancedPlayerController.Instance.enabled)
        {
            // Decide what to do next AFTER respecting summon cooldown
            bool canSummon = Time.time >= nextSummonTime;

            if (canSummon)
            {
                yield return StartCoroutine(PerformSummon());

                // Post-summon idle
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
                    yield return new WaitForSeconds(idleTime);
                }
                continue;
            }

            // Otherwise choose between teleport and attack, starting from 50/50
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
                    yield return new WaitForSeconds(teleIdle);
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
                    yield return new WaitForSeconds(postAttackIdle);
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

        // Move to camera center + offset immediately
        Vector3 center = mainCam.transform.position;
        Vector3 spawnPos = new Vector3(center.x + initialSpawnOffset.x, center.y + initialSpawnOffset.y, transform.position.z);
        spawnPos = ClampPositionToSummonArea(spawnPos);
        transform.position = spawnPos;

        // Enable visuals and collider only when teleporting in
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        if (capsuleCollider != null)
        {
            capsuleCollider.enabled = true;
        }

        // Clean animator state before playing teleport in
        ResetAnimatorBools();

        // While teleport-in is playing, idle must be FALSE
        animator.SetBool("idle", false);

        // Play teleport-in animation
        animator.SetBool("teleportin", true);

        yield return new WaitForSeconds(teleportInDuration);

        // Return to idle after teleport-in completes
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

        // Clear all state and start teleport-out at current position
        ResetAnimatorBools();

        // While teleport-out is playing, idle must be FALSE
        animator.SetBool("idle", false);
        animator.SetBool("teleportout", true);

        yield return new WaitForSeconds(teleportOutDuration);

        // If Norcthex died during the teleport-out animation, abort the
        // teleport immediately so he cannot "finish" the move post-mortem.
        if (isDead)
        {
            yield break;
        }

        // End teleport-out before moving
        animator.SetBool("teleportout", false);

        // Hide during the actual teleport move
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        if (capsuleCollider != null)
        {
            capsuleCollider.enabled = false;
        }

        // Choose new position within NorcthexSummonArea, respecting bossRestrictiveRadius when possible
        Vector3 newPos;
        bool found = TryGetRandomValidBossPosition(out newPos);

        if (!found)
        {
            if (AdvancedPlayerController.Instance != null)
            {
                Vector2 playerPos = AdvancedPlayerController.Instance.transform.position;
                float minRadius = Mathf.Max(minSummonDistanceFromPlayer, bossRestrictiveRadius + 0.5f);

                bool placedInsideArea = false;

                // If the 4-point area exists, first try to keep the teleport
                // inside that box while still respecting bossRestrictiveRadius.
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

                // If we couldn't keep it inside the area (or area is missing),
                // fall back to a simple radial teleport that still respects the
                // bossRestrictiveRadius via minRadius.
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

        newPos = ClampPositionToSummonArea(newPos);
        transform.position = newPos;

        // Show again and teleport in at the new location
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        if (capsuleCollider != null)
        {
            capsuleCollider.enabled = true;
        }

        ResetAnimatorBools();

        // While teleport-in is playing, idle must be FALSE
        animator.SetBool("idle", false);
        animator.SetBool("teleportin", true);

        yield return new WaitForSeconds(teleportInDuration);

        // If Norcthex died while teleporting in, do not force him back to
        // idle – let the death animation control the final state.
        if (isDead)
        {
            yield break;
        }

        // Finish teleport by returning to idle
        ResetAnimatorBools();
        animator.SetBool("idle", true);
    }

    IEnumerator PerformAttackSequence()
    {
        // Ensure Norcthex is within camera bounds before attacking
        if (!IsWithinCameraBounds())
        {
            yield break;
        }

        // Attack start
        ResetAnimatorBools();
        animator.SetBool("attackstart", true);
        yield return new WaitForSeconds(attackStartDuration);
        animator.SetBool("attackstart", false);

        // Main attack
        animator.SetBool("attack", true);

        float elapsed = 0f;
        bool projectileSpawned = false;

        while (elapsed < attackDuration)
        {
            if (!projectileSpawned && elapsed >= projectileSpawnTiming)
            {
                SpawnProjectile();
                projectileSpawned = true;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        animator.SetBool("attack", false);

        // Attack end
        animator.SetBool("attackend", true);
        yield return new WaitForSeconds(attackEndDuration);
        animator.SetBool("attackend", false);

        ResetAnimatorBools();
        animator.SetBool("idle", true);
    }

    IEnumerator PerformSummon()
    {
        if (isDead) yield break;

        // Play specialattack animation for summoning
        ResetAnimatorBools();
        animator.SetBool("specialattack", true);

        // Use dedicated specialAttackDuration for the summon cast
        float castDuration = Mathf.Max(0.1f, specialAttackDuration);
        yield return new WaitForSeconds(castDuration);

        animator.SetBool("specialattack", false);

        ResetAnimatorBools();
        animator.SetBool("idle", true);

        // Do the actual summon wave
        SummonEnemiesWithNearFarLogic();

        // Start summon cooldown AFTER this wave, then increase the base
        // cooldown for subsequent waves.
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
        // Initialize summon count on first summon
        if (currentSummonCount <= 0)
        {
            currentSummonCount = minSummonCount;
        }

        // Summon wave scaling like DarkNecromancer
        totalSummonWaves++;

        float healthStep = Mathf.Max(0f, summonHealthIncreasePercent) / 100f;
        float damageStep = Mathf.Max(0f, summonDamageIncreasePercent) / 100f;
        int extraWaves = Mathf.Max(0, totalSummonWaves - 1);
        summonHealthMultiplier = 1f + healthStep * extraWaves;
        summonDamageMultiplier = 1f + damageStep * extraWaves;

        List<Vector2> spawnedPositions = new List<Vector2>();
        int successfulSummons = 0;
        int maxAttempts = currentSummonCount * 15;
        int attempts = 0;

        Debug.Log($"<color=magenta>Norcthex Summon Wave {totalSummonWaves}: target {currentSummonCount} enemies (health x{summonHealthMultiplier:F2}, damage x{summonDamageMultiplier:F2})</color>");

        while (successfulSummons < currentSummonCount && attempts < maxAttempts)
        {
            // If Norcthex has died while this wave is being computed, abort any
            // remaining summons immediately so no new enemies appear after his
            // death.
            if (isDead)
            {
                break;
            }

            attempts++;

            // Decide enemy type (random among FAR and NEAR prefabs)
            GameObject prefabToSpawn = ChooseRandomSummonPrefab();
            if (prefabToSpawn == null)
            {
                break;
            }

            bool isFarType = IsFarPrefab(prefabToSpawn);

            // First try to find a position inside the 4-point area
            bool gotSpawnPos = TryGetRandomSummonPosition(isFarType, out Vector2 spawnPos);

            // If that fails, fall back to a radial spawn around the player while still
            // respecting minSummonDistanceFromPlayer / farRestrictiveRadius
            if (!gotSpawnPos)
            {
                gotSpawnPos = TryGetRadialSummonFallback(isFarType, out spawnPos);

                // If we still have nothing for FAR type, try downgrading to NEAR type
                if (!gotSpawnPos && isFarType)
                {
                    GameObject nearPrefab = ChooseRandomNearSummonPrefab();
                    if (nearPrefab != null)
                    {
                        prefabToSpawn = nearPrefab;
                        isFarType = false;

                        // Try area-based NEAR first, then radial NEAR
                        gotSpawnPos = TryGetRandomSummonPosition(false, out spawnPos) ||
                                     TryGetRadialSummonFallback(false, out spawnPos);
                    }
                }

                if (!gotSpawnPos)
                {
                    continue;
                }
            }

            // Check spacing between summoned enemies
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

            // Final safety: clamp the chosen spawn position to the Norcthex
            // 4-point summon area so that, even after radial fallbacks, no
            // summoned enemy can appear outside the purple gizmo.
            Vector3 clampedSpawnWorld = ClampPositionToSummonArea(new Vector3(spawnPos.x, spawnPos.y, transform.position.z));
            spawnPos = new Vector2(clampedSpawnWorld.x, clampedSpawnWorld.y);

            GameObject summonedEnemy = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

            Debug.Log($"<color=magenta>Norcthex summoned {summonedEnemy.name} at {spawnPos} (#{successfulSummons + 1}/{currentSummonCount}, farType={isFarType})</color>");

            EnemyHealth summonedHealth = summonedEnemy.GetComponent<EnemyHealth>();
            if (summonedHealth != null)
            {
                // Track this summon so it can be force-killed when Norcthex dies.
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

            // Apply base attack percent and wave multiplier
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

        // Increase summon count for next wave
        currentSummonCount = Mathf.Min(currentSummonCount + summonCountIncrease, maxSummonCount);
    }

    /// <summary>
    /// Immediately kill all enemies that were summoned by Norcthex. This is
    /// called as soon as the boss dies so no summoned minions remain alive.
    /// </summary>
    private void KillAllSummons()
    {
        if (activeSummons == null || activeSummons.Count == 0)
        {
            return;
        }

        for (int i = 0; i < activeSummons.Count; i++)
        {
            EnemyHealth summoned = activeSummons[i];
            if (summoned == null) continue;

            // If the summon is still alive, force it to die using its normal
            // EnemyHealth death pipeline so death animations and cleanup run.
            if (summoned.IsAlive)
            {
                Vector3 hitPoint = summoned.transform.position;
                Vector3 hitNormal = Vector3.up;
                float lethalDamage = summoned.MaxHealth + 999f;
                summoned.TakeDamage(lethalDamage, hitPoint, hitNormal);
            }
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

    // Radial fallback used when the 4-point area search fails to find a valid summon
    // position. Samples around the player while still respecting IsValidSummonPosition.
    bool TryGetRadialSummonFallback(bool isFarType, out Vector2 spawnPos)
    {
        spawnPos = transform.position;

        if (AdvancedPlayerController.Instance == null)
        {
            return false;
        }

        Vector2 playerPos = AdvancedPlayerController.Instance.transform.position;

        float innerRadius;
        float outerRadius;

        if (isFarType)
        {
            innerRadius = Mathf.Max(farRestrictiveRadius, minSummonDistanceFromPlayer);
            outerRadius = innerRadius + 3f;
        }
        else
        {
            innerRadius = minSummonDistanceFromPlayer;
            outerRadius = Mathf.Max(innerRadius + 3f, bossRestrictiveRadius * 0.5f);
        }

        if (outerRadius <= innerRadius)
        {
            outerRadius = innerRadius + 1f;
        }

        // Optional constraint: if the 4-point area is available, keep fallback
        // positions inside that box as well, so everything stays within the
        // purple gizmo.
        bool hasArea = pointA != null && pointB != null && pointC != null && pointD != null;
        float finalMinX = 0f, finalMaxX = 0f, finalMinY = 0f, finalMaxY = 0f;

        if (hasArea)
        {
            float minX1 = pointA.position.x;
            float maxX1 = pointB.position.x;
            float minX2 = pointD.position.x;
            float maxX2 = pointC.position.x;

            float minY1 = pointA.position.y;
            float maxY1 = pointD.position.y;
            float minY2 = pointB.position.y;
            float maxY2 = pointC.position.y;

            finalMinX = Mathf.Min(minX1, maxX1, minX2, maxX2);
            finalMaxX = Mathf.Max(minX1, maxX1, minX2, maxX2);

            finalMinY = Mathf.Min(minY1, maxY1, minY2, maxY2);
            finalMaxY = Mathf.Max(minY1, maxY1, minY2, maxY2);
        }

        int maxAttempts = 30;
        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector2.right;
            }

            float radius = Random.Range(innerRadius, outerRadius);
            Vector2 candidate = playerPos + dir * radius;

            if (hasArea)
            {
                if (candidate.x < finalMinX || candidate.x > finalMaxX ||
                    candidate.y < finalMinY || candidate.y > finalMaxY)
                {
                    continue;
                }
            }

            if (!IsValidSummonPosition(candidate, isFarType))
            {
                continue;
            }

            spawnPos = candidate;
            return true;
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize Norcthex's 4-point summon/teleport area in the Scene view
        GameObject pointAObj = !string.IsNullOrEmpty(pointATag) ? GameObject.FindGameObjectWithTag(pointATag) : null;
        GameObject pointBObj = !string.IsNullOrEmpty(pointBTag) ? GameObject.FindGameObjectWithTag(pointBTag) : null;
        GameObject pointCObj = !string.IsNullOrEmpty(pointCTag) ? GameObject.FindGameObjectWithTag(pointCTag) : null;
        GameObject pointDObj = !string.IsNullOrEmpty(pointDTag) ? GameObject.FindGameObjectWithTag(pointDTag) : null;

        if (pointAObj == null || pointBObj == null || pointCObj == null || pointDObj == null)
        {
            return;
        }

        Vector3 posA = pointAObj.transform.position;
        Vector3 posB = pointBObj.transform.position;
        Vector3 posC = pointCObj.transform.position;
        Vector3 posD = pointDObj.transform.position;

        // Corner markers
        Gizmos.color = new Color(0.6f, 0f, 1f, 0.85f); // purple for Norcthex
        float cornerRadius = 0.4f;
        Gizmos.DrawSphere(posA, cornerRadius);
        Gizmos.DrawSphere(posB, cornerRadius);
        Gizmos.DrawSphere(posC, cornerRadius);
        Gizmos.DrawSphere(posD, cornerRadius);

        // Outline of the quad
        Gizmos.color = new Color(0.6f, 0f, 1f, 0.6f);
        Gizmos.DrawLine(posA, posB);
        Gizmos.DrawLine(posB, posC);
        Gizmos.DrawLine(posC, posD);
        Gizmos.DrawLine(posD, posA);

        // Diagonals for readability
        Gizmos.color = new Color(0.6f, 0f, 1f, 0.25f);
        Gizmos.DrawLine(posA, posC);
        Gizmos.DrawLine(posB, posD);

        // Also visualize the min summon distance and boss restrictive radius around the player (if available)
        if (AdvancedPlayerController.Instance != null)
        {
            Vector3 playerPos = AdvancedPlayerController.Instance.transform.position;

            // Global "no summon" radius
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.25f);
            Gizmos.DrawWireSphere(playerPos, minSummonDistanceFromPlayer);

            // Norcthex restrictive radius
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.25f);
            Gizmos.DrawWireSphere(playerPos, bossRestrictiveRadius);
        }
    }

    bool TryGetRandomSummonPosition(bool isFarType, out Vector2 spawnPos)
    {
        spawnPos = transform.position;

        if (pointA == null || pointB == null || pointC == null || pointD == null)
        {
            // Fallback: spawn near boss but respect minSummonDistanceFromPlayer
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
        int maxAttempts = 80;
        float minDistSq = minSummonDistanceFromPlayer * minSummonDistanceFromPlayer;
        float bossRestrictiveSq = bossRestrictiveRadius * bossRestrictiveRadius;

        for (int i = 0; i < maxAttempts; i++)
        {
            float x = Random.Range(finalMinX, finalMaxX);
            float y = Random.Range(finalMinY, finalMaxY);
            Vector2 candidate = new Vector2(x, y);

            Vector2 toPlayer = candidate - playerPos;
            float distSq = toPlayer.sqrMagnitude;

            // Must be outside BOTH the global minimum radius and bossRestrictiveRadius
            if (distSq < minDistSq || distSq < bossRestrictiveSq)
            {
                continue;
            }

            newPos = new Vector3(candidate.x, candidate.y, transform.position.z);
            return true;
        }

        // Let caller fall back to a purely radial teleport that enforces bossRestrictiveRadius
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
        Vector3 targetPos = AdvancedPlayerController.Instance.transform.position;
        Vector2 dir = (targetPos - spawnPos).normalized;

        GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

        SpriteRenderer[] projSprites = proj.GetComponentsInChildren<SpriteRenderer>(true);
        if (projSprites != null && projSprites.Length > 0 && spriteRenderer != null)
        {
            bool norcthexFlipped = spriteRenderer.flipX;
            for (int i = 0; i < projSprites.Length; i++)
            {
                if (projSprites[i] != null)
                {
                    // Norcthex normal (not flipped): projectile flipped on BOTH X and Y.
                    // Norcthex flipped: projectile flipped on X but NOT Y.
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

        // Ensure every Norcthex summon dies instantly when the boss dies.
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

    IEnumerator DeathCleanupRoutine()
    {
        yield return new WaitForSeconds(deathCleanupDelay);
        Destroy(gameObject);
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
    }
}
