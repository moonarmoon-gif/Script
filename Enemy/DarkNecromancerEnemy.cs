using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(EnemyHealth))]
public class DarkNecromancerEnemy : MonoBehaviour
{
    [Header("Mini-Boss Movement Settings")]
    [Tooltip("Duration of initial forward movement (seconds)")]
    [SerializeField] private float initialMoveDuration = 2f;

    [Tooltip("Duration of subsequent forward movements (seconds)")]
    [SerializeField] private float subsequentMoveDuration = 1f;

    [Tooltip("Movement speed")]
    public float moveSpeed = 2f;

    [Tooltip("Stop distance from player")]
    [SerializeField] private float stopDistance = 3f;

    [Header("Attack Pattern Settings")]
    [Tooltip("Minimum idle duration when far from player (seconds)")]
    [SerializeField] private float idleDurationMin = 1.5f;

    [Tooltip("Maximum idle duration when far from player (seconds)")]
    [SerializeField] private float idleDurationMax = 3f;

    [Tooltip("Fixed idle duration for the very first FAR idle phase (seconds)")]
    [SerializeField] private float firstIdleDuration = 1f;

    [Tooltip("Minimum idle duration when close to player (seconds)")]
    [SerializeField] private float idleDurationCloseMin = 0.5f;

    [Tooltip("Maximum idle duration when close to player (seconds)")]
    [SerializeField] private float idleDurationCloseMax = 1.5f;

    [Tooltip("Attack duration (seconds)")]
    [SerializeField] private float attackDuration = 1f;

    [Tooltip("Special idle duration after attack (seconds)")]
    [SerializeField] private float specialIdleDuration = 1f;

    [Tooltip("Chance to play special idle after attack (0-1, 0.33 = 33%)")]
    [SerializeField] private float specialIdleChance = 0.33f;

    [Header("Damage Settings")]
    [SerializeField] private float attackDamage = 15f;
    [Tooltip("Delay in attack animation before damage is dealt (seconds)")]
    [SerializeField] private float attackDamageDelay = 0.2f;

    [SerializeField] private float attackDamageV2 = -1f;
    [SerializeField] private float attackDamageDelayV2 = -1f;

    [Header("Knockback Settings")]
    [Tooltip("Knockback force when hit by projectiles")]
    public float knockbackIntensity = 5f;
    [Tooltip("How long knockback lasts")]
    public float knockbackDuration = 0.2f;

    [Header("Death Settings")]
    [Tooltip("Duration of death animation before cleanup (seconds)")]
    public float deathAnimationDuration = 2f;

    [Header("Spawn Settings")]
    [Tooltip("Spawn at center top outside camera view")]
    [SerializeField] private bool spawnAtTopOutsideView = true;

    [Tooltip("Reference to EnemySpawner to get maxPos Y value")]
    [SerializeField] private EnemySpawner enemySpawner;

    [Tooltip("Additional Y-offset added to EnemySpawner.maxPos.y when spawning at top (positive = higher, negative = lower)")]
    [SerializeField] public float topSpawnYOffset = 0f;

    [Header("Enemy Summoning")]
    [Tooltip("Enemy to summon when far from player (before stop distance)")]
    [SerializeField] private GameObject farEnemyType; // FlyingDemon

    [Tooltip("Enemy to summon when close to player (within stop distance)")]
    [SerializeField] private GameObject closeEnemyType; // Skeleton

    [Tooltip("Starting number of enemies to summon (first summon)")]
    [SerializeField] private int minSummonCount = 3;

    [Tooltip("Increase in summon count per attack")]
    [SerializeField] private int summonCountIncrease = 1;

    [Tooltip("Maximum number of enemies to summon per attack")]
    [SerializeField] private int maxSummonCount = 10;

    private int currentSummonCount = 0; // Tracks current summon count

    [Header("Summon Scaling")]
    [Tooltip("Health increase percentage per summon wave for summoned enemies (10 = +10% per wave, multiplicative)")]
    [SerializeField] private float summonHealthIncreasePercent = 10f;

    [Tooltip("Damage increase percentage per summon wave for summoned enemies (1 = +1% per wave, multiplicative)")]
    [SerializeField] private float summonDamageIncreasePercent = 1f;

    [Header("Summon Base Percentages")]
    [Tooltip("Base health percent of DarkNecromancer's max health given to each summoned enemy (0.05 = 5%).")]
    public float summonBaseHealthPercent = 0.05f;

    [Tooltip("Base attack damage percent of DarkNecromancer's attackDamage given to each summoned enemy (1 = 100%).")]
    public float summonBaseAttackPercent = 1f;

    // Runtime summon scaling state
    private int totalSummonWaves = 0;
    private float summonHealthMultiplier = 1f;
    private float summonDamageMultiplier = 1f;

    [Tooltip("Four corner points defining the summon area (relative to necromancer position)")]
    [SerializeField] private Vector2 summonAreaPoint1 = new Vector2(-3f, 3f);
    [SerializeField] private Vector2 summonAreaPoint2 = new Vector2(3f, 3f);
    [SerializeField] private Vector2 summonAreaPoint3 = new Vector2(3f, -3f);
    [SerializeField] private Vector2 summonAreaPoint4 = new Vector2(-3f, -3f);

    [Tooltip("Minimum distance between summoned enemies")]
    [SerializeField] private float minDistanceBetweenSummons = 1f;

    [Header("Shadow Offset")]
    [Tooltip("Shadow transform to offset when walking")]
    [SerializeField] private Transform shadowTransform;

    [Tooltip("Shadow offset when walking AND sprite is flipped")]
    [SerializeField] private Vector2 walkingFlippedShadowOffset = Vector2.zero;

    [Tooltip("Shadow offset when walking BUT NOT flipped")]
    [SerializeField] private Vector2 walkingNotFlippedShadowOffset = Vector2.zero;

    [Tooltip("Shadow offset when DEAD AND sprite is flipped")]
    [SerializeField] private Vector2 deathFlippedShadowOffset = Vector2.zero;

    [Tooltip("Shadow offset when DEAD BUT NOT flipped")]
    [SerializeField] private Vector2 deathNotFlippedShadowOffset = Vector2.zero;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;
    [SerializeField] private CapsuleCollider2D capsuleCollider;

    private EnemyHealth health;
    private StatusController statusController;
    private IDamageable playerDamageable;
    private bool isDead;
    private bool isPlayerDead;
    private bool isInPattern = false;
    private Coroutine patternRoutine;
    private Vector2 knockbackVelocity = Vector2.zero;
    private float knockbackEndTime = 0f;

    private int attackActionToken = 0;

    // Pattern state tracking
    private bool isFirstMove = true;
    private bool isMoving = false;
    private bool isAttacking = false;
    private bool isIdle = false;
    private bool isSpecialIdle = false;
    private bool hasPlayedFirstIdle = false;
    private bool hasEnteredCameraView = false;
    private Vector3 baseShadowPosition;
    private bool hasShadow = false;
    private SpriteFlipOffset spriteFlipOffset;
    private bool wasMovingForShadow = false;

    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<Animator>();
        if (capsuleCollider == null) capsuleCollider = GetComponent<CapsuleCollider2D>();
        health = GetComponent<EnemyHealth>();
        statusController = GetComponent<StatusController>();

        // Initialize shadow
        if (shadowTransform != null)
        {
            hasShadow = true;
            baseShadowPosition = shadowTransform.localPosition;
        }

        spriteFlipOffset = GetComponent<SpriteFlipOffset>();

        // Phantom settings - only collide with Projectiles and Player
        capsuleCollider.isTrigger = false;
        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Default"), true);
        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Enemy"), true);

        if (AdvancedPlayerController.Instance != null)
        {
            playerDamageable = AdvancedPlayerController.Instance.GetComponent<IDamageable>();
            AdvancedPlayerController.Instance.GetComponent<PlayerHealth>().OnDeath += OnPlayerDeath;
        }

        if (attackDamageV2 < 0f)
        {
            attackDamageV2 = attackDamage;
        }
        if (attackDamageDelayV2 < 0f)
        {
            attackDamageDelayV2 = attackDamageDelay;
        }

        // Spawn position logic
        if (Camera.main != null)
        {
            if (spawnAtTopOutsideView)
            {
                // CHECKED: Spawn at center top outside camera view
                float centerX = Camera.main.transform.position.x;

                float spawnY;

                if (enemySpawner != null && enemySpawner.maxPos != null)
                {
                    // Use EnemySpawner maxPos Y plus configured offset
                    spawnY = enemySpawner.maxPos.position.y + topSpawnYOffset;
                    Debug.Log($"<color=purple>DarkNecromancer using EnemySpawner maxPos Y: {enemySpawner.maxPos.position.y} with offset {topSpawnYOffset} => {spawnY}</color>");
                }
                else
                {
                    // Fallback: use camera top plus offset
                    float baseY = Camera.main.transform.position.y + Camera.main.orthographicSize + 2f; // Default: above camera
                    spawnY = baseY + topSpawnYOffset;
                    Debug.LogWarning("<color=orange>DarkNecromancer: EnemySpawner or maxPos not assigned, using camera top spawn with offset</color>");
                }

                Vector3 spawnPos = new Vector3(centerX, spawnY, transform.position.z);
                transform.position = spawnPos;
                Debug.Log($"<color=purple>DarkNecromancer spawned at CENTER TOP: {spawnPos}</color>");
            }
            else
            {
                // UNCHECKED: Spawn at random position like other enemies (using EnemySpawner bounds)
                if (enemySpawner != null && enemySpawner.minPos != null && enemySpawner.maxPos != null)
                {
                    float randomX = Random.Range(enemySpawner.minPos.position.x, enemySpawner.maxPos.position.x);
                    float randomY = Random.Range(enemySpawner.minPos.position.y, enemySpawner.maxPos.position.y);
                    Vector3 spawnPos = new Vector3(randomX, randomY, transform.position.z);
                    transform.position = spawnPos;
                    Debug.Log($"<color=purple>DarkNecromancer spawned at RANDOM position: {spawnPos}</color>");
                }
                else
                {
                    Debug.LogWarning("<color=orange>DarkNecromancer: EnemySpawner or min/maxPos not assigned, using default position</color>");
                }
            }
        }
    }

    private int BeginAttackAction()
    {
        attackActionToken++;
        return attackActionToken;
    }

    private void CancelAttackAction()
    {
        attackActionToken++;
    }

    void OnEnable() => health.OnDeath += HandleDeath;

    void OnDisable()
    {
        health.OnDeath -= HandleDeath;
        if (AdvancedPlayerController.Instance != null)
        {
            var playerHealth = AdvancedPlayerController.Instance.GetComponent<PlayerHealth>();
            if (playerHealth != null)
                playerHealth.OnDeath -= OnPlayerDeath;
        }
    }

    void Start()
    {
        // Initialize summon count
        currentSummonCount = minSummonCount;

        // Start the attack pattern
        if (!isDead && AdvancedPlayerController.Instance != null)
        {
            patternRoutine = StartCoroutine(AttackPatternRoutine());
        }
    }

    void Update()
    {
        if (isDead || isPlayerDead) return;

        // Face player
        if (AdvancedPlayerController.Instance != null)
        {
            Vector3 toPlayer = AdvancedPlayerController.Instance.transform.position - transform.position;
            spriteRenderer.flipX = toPlayer.x <= 0;
        }

        // Update animator states
        // CRITICAL: Force idle when player is dead
        bool playerDead = AdvancedPlayerController.Instance == null || !AdvancedPlayerController.Instance.enabled;
        if (playerDead)
        {
            animator.SetBool("idle", true);
            animator.SetBool("specialidle", false);
            animator.SetBool("attack", false);
            animator.SetBool("moving", false);
            animator.SetBool("movingflip", false);
        }
        else
        {
            animator.SetBool("idle", isIdle);
            animator.SetBool("specialidle", isSpecialIdle);
        }
    }

    void LateUpdate()
    {
        if (spriteFlipOffset == null) return;

        // Check if moving/movingflip animation is playing
        bool isMovingAnim = animator.GetBool("moving") || animator.GetBool("movingflip");
        bool isDying = animator.GetBool("dead") || animator.GetBool("deadflip");

        // Disable SpriteFlipOffset during walking or death
        if (isMovingAnim || isDying)
        {
            spriteFlipOffset.SetColliderOffsetEnabled(false);
            spriteFlipOffset.SetShadowOffsetEnabled(false);
        }
        else
        {
            // Enable SpriteFlipOffset for all other states
            spriteFlipOffset.SetColliderOffsetEnabled(true);
            spriteFlipOffset.SetShadowOffsetEnabled(true);
        }

        if (isDead) return;

        // Only update if state changed
        if (isMovingAnim != wasMovingForShadow)
        {
            wasMovingForShadow = isMovingAnim;
        }

        // When moving, apply custom shadow offset based on flip state
        if (isMovingAnim && hasShadow && shadowTransform != null)
        {
            bool isFlipped = spriteRenderer.flipX;
            Vector2 offset = isFlipped ? walkingFlippedShadowOffset : walkingNotFlippedShadowOffset;
            shadowTransform.localPosition = baseShadowPosition + (Vector3)offset;
        }
        animator.SetBool("attack", isAttacking);

        // Use different movement booleans based on flip state
        if (spriteRenderer.flipX) // Flipped (right side)
        {
            animator.SetBool("movingflip", isMoving);
            animator.SetBool("moving", false);
        }
        else // Not flipped (left side)
        {
            animator.SetBool("moving", isMoving);
            animator.SetBool("movingflip", false);
        }

        // Update shadow offset based on walking and flip state
        if (hasShadow && shadowTransform != null)
        {
            // Check if actually moving (velocity check)
            bool isActuallyMoving = rb != null && rb.velocity.sqrMagnitude > 0.01f;

            Vector3 targetPosition = baseShadowPosition;

            if (isActuallyMoving && spriteRenderer.flipX) // Walking AND flipped
            {
                targetPosition = new Vector3(
                    baseShadowPosition.x + walkingFlippedShadowOffset.x,
                    baseShadowPosition.y + walkingFlippedShadowOffset.y,
                    baseShadowPosition.z
                );
            }
            else if (isActuallyMoving && !spriteRenderer.flipX) // Walking BUT NOT flipped
            {
                targetPosition = new Vector3(
                    baseShadowPosition.x + walkingNotFlippedShadowOffset.x,
                    baseShadowPosition.y + walkingNotFlippedShadowOffset.y,
                    baseShadowPosition.z
                );
            }

            shadowTransform.localPosition = targetPosition;
        }
    }

    void OnPlayerDeath()
    {
        if (isDead || isPlayerDead) return;

        isPlayerDead = true;
        rb.velocity = Vector2.zero;
        if (patternRoutine != null)
        {
            StopCoroutine(patternRoutine);
            patternRoutine = null;
        }

        CancelAttackAction();

        isInPattern = false;
        isMoving = false;
        isAttacking = false;
        isIdle = true;
        isSpecialIdle = false;
        animator.SetBool("attack", false);
        animator.SetBool("moving", false);
        animator.SetBool("movingflip", false);
        animator.SetBool("idle", true);
        animator.SetBool("specialidle", false);
    }

    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (isDead) return;

        knockbackVelocity = direction.normalized * force * knockbackIntensity;
        knockbackEndTime = Time.time + knockbackDuration;

        CancelAttackAction();
        isAttacking = false;
        animator.SetBool("attack", false);
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

        // Movement is handled by pattern routine
        if (!isMoving)
        {
            rb.velocity = Vector2.zero;
        }
    }

    IEnumerator AttackPatternRoutine()
    {
        isInPattern = true;

        // First, walk into camera view if spawned outside
        if (!hasEnteredCameraView)
        {
            yield return StartCoroutine(WalkIntoCameraView());
        }

        while (!isDead && !isPlayerDead && AdvancedPlayerController.Instance != null)
        {
            // Check if close enough to player
            float distanceToPlayer = Vector2.Distance(transform.position, AdvancedPlayerController.Instance.transform.position);

            if (distanceToPlayer <= stopDistance)
            {
                // Close enough - just do attack pattern without moving
                Debug.Log("<color=purple>DarkNecromancer: Close enough, entering attack pattern only</color>");

                // Idle (use close range duration)
                yield return StartCoroutine(IdlePhase(true));

                // Attack
                yield return StartCoroutine(AttackPhase());

                // Special Idle (33% chance)
                yield return StartCoroutine(SpecialIdlePhase());

                continue; // Repeat pattern
            }

            // MOVE PHASE
            float moveDuration = isFirstMove ? initialMoveDuration : subsequentMoveDuration;
            yield return StartCoroutine(MovePhase(moveDuration));
            isFirstMove = false; // After first move, use subsequent duration

            // IDLE PHASE (use far range duration)
            yield return StartCoroutine(IdlePhase(false));

            // ATTACK PHASE
            yield return StartCoroutine(AttackPhase());

            // SPECIAL IDLE PHASE (33% chance)
            yield return StartCoroutine(SpecialIdlePhase());

            // Pattern repeats
        }

        isInPattern = false;
    }

    IEnumerator WalkIntoCameraView()
    {
        Debug.Log("<color=purple>DarkNecromancer: Walking into camera view...</color>");
        isMoving = true;
        isIdle = false;
        isSpecialIdle = false;
        isAttacking = false;

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            hasEnteredCameraView = true;
            isMoving = false;
            yield break;
        }

        // Walk down until in camera view
        while (!isDead && AdvancedPlayerController.Instance != null)
        {
            // Check if in camera view
            Vector3 viewportPos = mainCam.WorldToViewportPoint(transform.position);

            if (viewportPos.y < 0.95f) // Entered camera view (with small margin)
            {
                hasEnteredCameraView = true;
                rb.velocity = Vector2.zero;
                isMoving = false;
                Debug.Log("<color=purple>DarkNecromancer: Entered camera view, starting pattern</color>");
                break;
            }

            // Move down toward player
            Vector3 toPlayer = AdvancedPlayerController.Instance.transform.position - transform.position;

            float speedMult = 1f;
            if (statusController != null)
            {
                speedMult = statusController.GetEnemyMoveSpeedMultiplier();
            }

            rb.velocity = toPlayer.normalized * (moveSpeed * speedMult);

            yield return new WaitForFixedUpdate();
        }

        rb.velocity = Vector2.zero;
        isMoving = false;
    }

    IEnumerator MovePhase(float duration)
    {
        Debug.Log($"<color=purple>DarkNecromancer: MOVE phase ({duration}s)</color>");
        isMoving = true;
        isIdle = false;
        isSpecialIdle = false;
        isAttacking = false;

        float elapsed = 0f;

        while (elapsed < duration && !isDead && AdvancedPlayerController.Instance != null)
        {
            // Move toward player
            Vector3 toPlayer = AdvancedPlayerController.Instance.transform.position - transform.position;
            float distance = toPlayer.magnitude;

            // Stop if reached stop distance
            if (distance <= stopDistance)
            {
                rb.velocity = Vector2.zero;
                break;
            }

            float speedMult = 1f;
            if (statusController != null)
            {
                speedMult = statusController.GetEnemyMoveSpeedMultiplier();
            }

            rb.velocity = toPlayer.normalized * (moveSpeed * speedMult);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.velocity = Vector2.zero;
        isMoving = false;

        Debug.Log("<color=purple>DarkNecromancer: MOVE phase complete</color>");
    }

    IEnumerator IdlePhase(bool isCloseToPlayer)
    {
        // Choose idle duration based on distance, with a FIXED first FAR idle
        float idleDuration;
        if (!hasPlayedFirstIdle && !isCloseToPlayer)
        {
            idleDuration = firstIdleDuration;
            hasPlayedFirstIdle = true;
            Debug.Log($"<color=purple>DarkNecromancer: FIRST IDLE phase (FAR - {idleDuration:F2}s)</color>");
        }
        else if (isCloseToPlayer)
        {
            idleDuration = Random.Range(idleDurationCloseMin, idleDurationCloseMax);
            Debug.Log($"<color=purple>DarkNecromancer: IDLE phase (CLOSE - {idleDuration:F2}s)</color>");
        }
        else
        {
            idleDuration = Random.Range(idleDurationMin, idleDurationMax);
            Debug.Log($"<color=purple>DarkNecromancer: IDLE phase (FAR - {idleDuration:F2}s)</color>");
        }

        isMoving = false;
        isIdle = true;
        isSpecialIdle = false;
        isAttacking = false;

        yield return new WaitForSeconds(idleDuration);

        isIdle = false;
        Debug.Log("<color=purple>DarkNecromancer: IDLE phase complete</color>");
    }

    IEnumerator AttackPhase()
    {
        int myToken = BeginAttackAction();
        Debug.Log($"<color=purple>DarkNecromancer: ATTACK phase ({attackDuration}s)</color>");
        isMoving = false;
        isIdle = false;
        isSpecialIdle = false;
        isAttacking = true;

        // Wait for attack animation to complete
        yield return new WaitForSeconds(attackDuration);

        if (isDead || isPlayerDead || myToken != attackActionToken || AdvancedPlayerController.Instance == null)
        {
            isAttacking = false;
            Debug.Log("<color=purple>DarkNecromancer: ATTACK phase cancelled before summon resolved</color>");
            yield break;
        }

        // Check distance to player for enemy type selection
        float distanceToPlayer = Vector2.Distance(transform.position, AdvancedPlayerController.Instance.transform.position);
        bool isWithinStopDistance = distanceToPlayer <= stopDistance;

        // Summon enemies after attack completes
        SummonEnemies(isWithinStopDistance);

        isAttacking = false;

        // Go idle after attack
        isIdle = true;
        float idleDuration = Random.Range(idleDurationMin, idleDurationMax);
        if (statusController != null)
        {
            idleDuration += statusController.GetLethargyAttackCooldownBonus();
        }
        if (idleDuration < 0f)
        {
            idleDuration = 0f;
        }
        if (idleDuration > 0f)
        {
            yield return new WaitForSeconds(idleDuration);
        }
        isIdle = false;

        Debug.Log("<color=purple>DarkNecromancer: ATTACK phase complete</color>");
    }

    void SummonEnemies(bool isWithinStopDistance)
    {
        if (isDead || isPlayerDead) return;

        // Select enemy type based on distance
        GameObject enemyToSummon = isWithinStopDistance ? closeEnemyType : farEnemyType;

        if (enemyToSummon == null)
        {
            Debug.LogWarning($"<color=orange>DarkNecromancer: No enemy assigned for {(isWithinStopDistance ? "close" : "far")} range!</color>");
            return;
        }

        // Update summon wave scaling: linear from base, only after the first wave
        totalSummonWaves++;

        float healthStep = Mathf.Max(0f, summonHealthIncreasePercent) / 100f;
        float damageStep = Mathf.Max(0f, summonDamageIncreasePercent) / 100f;
        int extraWaves = Mathf.Max(0, totalSummonWaves - 1);
        summonHealthMultiplier = 1f + healthStep * extraWaves;
        summonDamageMultiplier = 1f + damageStep * extraWaves;

        Debug.Log($"<color=purple>DarkNecromancer Summon Wave {totalSummonWaves}: Health x{summonHealthMultiplier:F2}, Damage x{summonDamageMultiplier:F2}</color>");

        List<Vector2> spawnedPositions = new List<Vector2>();
        int successfulSummons = 0;
        int maxAttempts = currentSummonCount * 10; // Prevent infinite loop
        int attempts = 0;

        string enemyTypeName = isWithinStopDistance ? "close-range" : "far-range";
        Debug.Log($"<color=purple>DarkNecromancer summoning {currentSummonCount}x {enemyToSummon.name} ({enemyTypeName})</color>");

        while (successfulSummons < currentSummonCount && attempts < maxAttempts)
        {
            attempts++;

            // Random position within 4-point area
            Vector2 spawnPos = GetRandomPointInQuad();

            // Check if position is valid (not too close to other spawns)
            bool validPosition = true;
            foreach (Vector2 existingPos in spawnedPositions)
            {
                if (Vector2.Distance(spawnPos, existingPos) < minDistanceBetweenSummons)
                {
                    validPosition = false;
                    break;
                }
            }

            if (validPosition)
            {
                GameObject summonedEnemy = Instantiate(enemyToSummon, spawnPos, Quaternion.identity);

                EnemyHealth summonedHealth = summonedEnemy.GetComponent<EnemyHealth>();
                if (summonedHealth != null)
                {
                    summonedHealth.ignoreScalingFromEnemyScalingSystem = true;

                    if (health != null)
                    {
                        float bossMax = health.MaxHealth;
                        float baseSummonMax = summonedHealth.MaxHealth;
                        // Clamp base percent so summons can never exceed 100% of boss HP from base scaling
                        float clampedBaseHealthPercent = Mathf.Clamp01(summonBaseHealthPercent);
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

                // Apply base attack percent from DarkNecromancer, then wave-based damage multiplier
                SkeletonEnemy skeleton = summonedEnemy.GetComponent<SkeletonEnemy>();
                if (skeleton != null)
                {
                    // Clamp base attack percent so summons can never exceed 100% of boss attack from base scaling
                    float clampedBaseAttackPercent = Mathf.Clamp01(summonBaseAttackPercent);
                    if (clampedBaseAttackPercent > 0f)
                    {
                        skeleton.SetBaseDamageFromBoss(attackDamageV2, clampedBaseAttackPercent);
                    }

                    if (summonDamageMultiplier > 1f)
                    {
                        skeleton.MultiplyAttackDamage(summonDamageMultiplier);
                    }
                }
                else
                {
                    FlyingDemonEnemy flyingDemon = summonedEnemy.GetComponent<FlyingDemonEnemy>();
                    if (flyingDemon != null)
                    {
                        float clampedBaseAttackPercent = Mathf.Clamp01(summonBaseAttackPercent);
                        if (clampedBaseAttackPercent > 0f)
                        {
                            flyingDemon.SetBaseDamageFromBoss(attackDamageV2, clampedBaseAttackPercent);
                        }

                        if (summonDamageMultiplier > 1f)
                        {
                            flyingDemon.MultiplyAttackDamage(summonDamageMultiplier);
                        }
                    }
                }

                spawnedPositions.Add(spawnPos);
                successfulSummons++;
                Debug.Log($"<color=purple>DarkNecromancer summoned {enemyToSummon.name} at {spawnPos}</color>");
            }
        }

        if (successfulSummons < currentSummonCount)
        {
            Debug.LogWarning($"<color=orange>DarkNecromancer: Only summoned {successfulSummons}/{currentSummonCount} enemies after {attempts} attempts</color>");
        }

        // Increase summon count for next time (progressive)
        currentSummonCount = Mathf.Min(currentSummonCount + summonCountIncrease, maxSummonCount);
        Debug.Log($"<color=purple>DarkNecromancer: Next summon will spawn {currentSummonCount} enemies</color>");
    }

    IEnumerator SpecialIdlePhase()
    {
        // 33% chance to play special idle
        float roll = Random.value;

        if (roll <= specialIdleChance)
        {
            Debug.Log($"<color=purple>DarkNecromancer: SPECIAL IDLE phase ({specialIdleDuration}s) - Rolled {roll:F2}</color>");
            isMoving = false;
            isIdle = false;
            isSpecialIdle = true;
            isAttacking = false;

            yield return new WaitForSeconds(specialIdleDuration);

            isSpecialIdle = false;
            Debug.Log("<color=purple>DarkNecromancer: SPECIAL IDLE phase complete</color>");
        }
        else
        {
            Debug.Log($"<color=purple>DarkNecromancer: SPECIAL IDLE skipped - Rolled {roll:F2} (need ≤{specialIdleChance:F2})</color>");
            // Skip special idle
            yield return null;
        }
    }

    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        CancelAttackAction();

        if (patternRoutine != null) StopCoroutine(patternRoutine);

        // CRITICAL: Set death animation based on flip state
        bool isFlipped = spriteRenderer.flipX;
        animator.SetBool("dead", !isFlipped);      // Normal death when NOT flipped
        animator.SetBool("deadflip", isFlipped);   // Flipped death when flipped
        
        // Disable all other animation states
        animator.SetBool("moving", false);
        animator.SetBool("movingflip", false);
        animator.SetBool("idle", false);
        animator.SetBool("specialidle", false);
        animator.SetBool("attack", false);
        
        Debug.Log($"<color=purple>DarkNecromancer: Death animation - flipped={isFlipped}, dead={!isFlipped}, deadflip={isFlipped}</color>");
        
        rb.velocity = Vector2.zero;
        knockbackVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static;
        
        capsuleCollider.enabled = false;
        
        // Apply death shadow offset based on flip state
        if (hasShadow && shadowTransform != null)
        {
            Vector2 offset = isFlipped ? deathFlippedShadowOffset : deathNotFlippedShadowOffset;
            shadowTransform.localPosition = baseShadowPosition + (Vector3)offset;
            Debug.Log($"<color=purple>DarkNecromancer: Death - Applied shadow offset {offset} (flipped={isFlipped})</color>");
        }
        
        // Disable shadow offset control during death
        if (spriteFlipOffset != null)
        {
            spriteFlipOffset.SetShadowOffsetEnabled(false);
        }

        Destroy(gameObject, deathAnimationDuration);
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
        if (attackDamageV2 >= 0f)
        {
            attackDamageV2 *= multiplier;
        }
        Debug.Log($"<color=purple>DarkNecromancerEnemy attackDamage scaled by x{multiplier:F2} (new: {attackDamage:F1})</color>");
    }

    Vector2 GetRandomPointInQuad()
    {
        // Random point using barycentric coordinates in quad
        float r1 = Random.value;
        float r2 = Random.value;

        // Interpolate between the 4 points
        Vector2 p1 = (Vector2)transform.position + summonAreaPoint1;
        Vector2 p2 = (Vector2)transform.position + summonAreaPoint2;
        Vector2 p3 = (Vector2)transform.position + summonAreaPoint3;
        Vector2 p4 = (Vector2)transform.position + summonAreaPoint4;

        // Bilinear interpolation
        Vector2 top = Vector2.Lerp(p1, p2, r1);
        Vector2 bottom = Vector2.Lerp(p4, p3, r1);
        Vector2 result = Vector2.Lerp(top, bottom, r2);

        return result;
    }

    void OnDrawGizmosSelected()
    {
        // Draw summon area quad (magenta)
        Gizmos.color = Color.magenta;
        Vector3 p1 = transform.position + (Vector3)summonAreaPoint1;
        Vector3 p2 = transform.position + (Vector3)summonAreaPoint2;
        Vector3 p3 = transform.position + (Vector3)summonAreaPoint3;
        Vector3 p4 = transform.position + (Vector3)summonAreaPoint4;

        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p4);
        Gizmos.DrawLine(p4, p1);

        // Draw stop distance (yellow)
        Gizmos.color = Color.yellow;
        DrawCircle(transform.position, stopDistance, 32);
    }

    void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}
