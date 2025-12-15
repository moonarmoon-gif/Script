using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(EnemyHealth))]
public class ShadowEnemy : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 1.8f;
    public float offCameraMoveSpeed = 3.0f; // Speed when off-camera
    public float bounceSpeed = 3.0f;
    public float bounceSpeedMultiplier = 0.5f; // Speed increase per interval
    public float bounceSpeedInterval = 1.0f; // Time interval for speed increase (seconds)
    public float maxBounceSpeed = 8.0f; // Maximum bounce speed

    [Tooltip("Only increase bounce speed when within camera bounds")]
    public bool onlyScaleSpeedInView = true;
    [Tooltip("Distance at which Shadow switches from bouncing to walking")]
    public float walkRadius = 5.0f;
    public float attackRange = 1.2f;
    public float bounceAttackCastRadius = 2.5f;
    [Tooltip("Stop distance - enemy won't move closer than this")]
    public float stopDistance = 0.5f;
    public float attackAnimSpeed = 1.35f;
    public float attackCooldown = 1.0f;
    public float attackDuration = 0.4f;
    [Tooltip("Duration of death animation before enemy is destroyed")]
    public float deathCleanupDelay = 0.7f;
    [Tooltip("Duration of fade out effect on death (seconds)")]
    [SerializeField] private float deathFadeOutDuration = 0.5f;

    [Header("Damage Settings")]
    public float attackDamage = 15f;
    public float bounceAttackDamage = 20f;
    public float bounceAttackDamageMultiplier = 0.1f;
    [Tooltip("Delay in normal attack animation before damage is dealt (seconds)")]
    public float attackDamageDelay = 0.2f;

    [SerializeField] private float attackDamageV2 = -1f;
    [SerializeField] private float attackDamageDelayV2 = -1f;
    [SerializeField] private float bounceAttackDamageV2 = -1f;
    [SerializeField] private float bounceAttackDamageDelayV2 = -1f;

    [Header("Bounce Attack Settings")]
    [Tooltip("Duration of bounce attack animation")]
    public float bounceAttackAnimationDuration = 0.4f;

    [Tooltip("Delay before damage is applied during bounce attack (in seconds)")]
    public float bounceAttackDamageDelay = 0.5f;

    [Tooltip("Time before bounce interruption animation finishes")]
    public float bounceInterruptionDuration = 0.6f;

    [Tooltip("Slide distance multiplier during bounce attack based on speed")]
    public float bounceAttackSlideMultiplier = 0.3f;

    [Tooltip("Duration of bounce attack slide (0 = use bounceAttackAnimationDuration)")]
    public float bounceAttackSlideDuration = 0f;

    [Tooltip("Idle delay after bounce attack before resuming normal behavior")]
    public float postBounceAttackDelay = 0.3f;

    [Tooltip("Interruption window at start of bounce attack (seconds)")]
    public float bounceAttackInterruptionWindow = 0.3f;

    [Header("Collider Animation")]
    [Tooltip("Enable dynamic collider adjustment based on animation state")]
    public bool useAnimatedCollider = false;

    [Tooltip("Collider size when bouncing")]
    public Vector2 bounceColliderSize = new Vector2(1f, 1f);

    [Tooltip("Collider offset when bouncing (facing left)")]
    public Vector2 bounceColliderOffsetLeft = Vector2.zero;

    [Tooltip("Collider offset when bouncing (facing right)")]
    public Vector2 bounceColliderOffsetRight = Vector2.zero;

    [Tooltip("Collider offset when sprite is flipped (facing right)")]
    public Vector2 flippedColliderOffsetRight = Vector2.zero;

    [Tooltip("Collider offset when sprite is NOT flipped (facing left)")]
    public Vector2 flippedColliderOffsetLeft = Vector2.zero;

    [Header("AI Behavior")]
    public float neighborAvoidRadius = 0.8f;
    public float aheadBlockCheck = 0.6f;
    public float sidestepDuration = 0.35f;
    public float sidestepCooldown = 0.35f;
    public float sidestepSpeedMultiplier = 0.95f;
    public float minDotForAhead = 0.45f;

    [Header("Collision Phasing")]
    [Tooltip("Time colliding with other shadows before phasing through them")]
    public float phaseCollisionTime = 1.0f;

    [Tooltip("Enable collision phasing with other shadow enemies")]
    public bool enableCollisionPhasing = true;

    // Component references
    [Header("Visual Components")]
    [Tooltip("Drag the SpriteRenderer here (can be on child GameObject)")]
    public SpriteRenderer spriteRenderer;

    Rigidbody2D rb;
    Animator anim;
    Collider2D col;
    EnemyHealth health;
    StatusController statusController;
    SpriteFlipOffset spriteFlipOffset;

    // Player references
    Transform player;
    IDamageable playerDamageable;

    // State flags
    bool isDead;
    bool isPlayerDead;
    bool isAttacking;
    bool isBouncing;
    bool isWalking; // Walking mode after interruption within walk radius
    bool isBounceInterrupted;
    bool isBounceAttacking;
    bool attackOnCooldown;
    bool canBeInterrupted = true; // First bounce can be interrupted

    int attackActionToken = 0;
    int bounceAttackActionToken = 0;

    // Bounce speed tracking
    float currentBounceSpeed;
    float bounceSpeedBonus;
    float lastSpeedIncreaseTime;

    // Collision phasing
    Dictionary<Collider2D, float> shadowCollisionTimes = new Dictionary<Collider2D, float>();
    HashSet<Collider2D> phasingColliders = new HashSet<Collider2D>();

    // Slide tracking
    bool isSliding;
    Vector2 slideDirection;
    float slideSpeed;
    float slideEndTime;

    // Coroutine handles
    Coroutine bounceRoutine;
    Coroutine attackRoutine;
    Coroutine bounceAttackRoutine;
    Coroutine bounceInterruptRoutine;

    // Collision buffers
    static readonly Collider2D[] buf = new Collider2D[24];
    int shadowLayer;
    float sidestepUntil;
    float sidestepBlock;
    Vector2 sidestepDir;

    // Collider animation
    BoxCollider2D boxCollider;
    CapsuleCollider2D capsuleCollider;
    CircleCollider2D circleCollider;
    bool lastBouncingState = false;
    bool lastBounceAttackingState = false;
    Vector2 originalColliderSize;
    Vector2 originalColliderOffset;
    bool colliderCached = false;

    void Awake()
    {
        // Get sprite renderer from assigned field or try to find it
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogError("ShadowEnemy: No SpriteRenderer found! Please assign it in the Inspector.");
            }
        }

        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
        health = GetComponent<EnemyHealth>();
        statusController = GetComponent<StatusController>();

        if (AdvancedPlayerController.Instance != null)
        {
            player = AdvancedPlayerController.Instance.transform;
            playerDamageable = player.GetComponent<IDamageable>();

            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.OnDeath += OnPlayerDeath;
            }
        }

        shadowLayer = gameObject.layer;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.gravityScale = 0f;

        if (bounceAttackCastRadius < attackRange)
            bounceAttackCastRadius = attackRange;

        // Get collider references for animation and cache original values
        boxCollider = GetComponent<BoxCollider2D>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();
        circleCollider = GetComponent<CircleCollider2D>();

        spriteFlipOffset = GetComponent<SpriteFlipOffset>();

        // Cache original collider size and offset
        if (boxCollider != null)
        {
            originalColliderSize = boxCollider.size;
            originalColliderOffset = boxCollider.offset;
            colliderCached = true;
        }
        else if (capsuleCollider != null)
        {
            originalColliderSize = capsuleCollider.size;
            originalColliderOffset = capsuleCollider.offset;
            colliderCached = true;
        }
        else if (circleCollider != null)
        {
            originalColliderSize = new Vector2(circleCollider.radius * 2f, circleCollider.radius * 2f);
            originalColliderOffset = circleCollider.offset;
            colliderCached = true;
        }

        if (attackDamageV2 < 0f)
        {
            attackDamageV2 = attackDamage;
        }
        if (attackDamageDelayV2 < 0f)
        {
            attackDamageDelayV2 = attackDamageDelay;
        }
        if (bounceAttackDamageV2 < 0f)
        {
            bounceAttackDamageV2 = bounceAttackDamage;
        }
        if (bounceAttackDamageDelayV2 < 0f)
        {
            bounceAttackDamageDelayV2 = bounceAttackDamageDelay;
        }
    }

    int BeginAttackAction()
    {
        attackActionToken++;
        return attackActionToken;
    }

    void CancelAttackAction()
    {
        attackActionToken++;
    }

    int BeginBounceAttackAction()
    {
        bounceAttackActionToken++;
        return bounceAttackActionToken;
    }

    void CancelBounceAttackAction()
    {
        bounceAttackActionToken++;
    }

    void OnEnable()
    {
        if (health != null)
        {
            health.OnDeath += HandleDeath;
            health.OnDamageTaken += OnDamageTaken;
        }
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.OnDeath -= HandleDeath;
            health.OnDamageTaken -= OnDamageTaken;
        }

        if (player != null && player.TryGetComponent<PlayerHealth>(out var playerHealth))
        {
            playerHealth.OnDeath -= OnPlayerDeath;
        }
    }

    void OnDestroy()
    {
        if (player != null && player.TryGetComponent<PlayerHealth>(out var playerHealth))
        {
            playerHealth.OnDeath -= OnPlayerDeath;
        }
    }

    private bool hasInitialized = false;

    void Start()
    {
        isBouncing = true;
        isWalking = false;
        currentBounceSpeed = bounceSpeed;
        bounceSpeedBonus = 0f;
        lastSpeedIncreaseTime = Time.time;

        // Set initial sprite direction immediately based on spawn position
        if (player != null && spriteRenderer != null)
        {
            bool shouldFlip = !(player.position.x > transform.position.x);
            spriteRenderer.flipX = shouldFlip;
        }

        if (!isDead && bounceRoutine == null)
        {
            bounceRoutine = StartCoroutine(BounceRoutine());
        }

        // Mark as initialized after first frame
        StartCoroutine(InitializeAfterFrame());
    }

    private System.Collections.IEnumerator InitializeAfterFrame()
    {
        yield return null; // Wait one frame
        hasInitialized = true;
    }

    void Update()
    {
        bool playerDead = isPlayerDead || player == null ||
                         (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled);

        // Update animator booleans - DON'T set IsDead here (HandleDeath sets it based on flip state)
        // Play idle when on attack cooldown, bounce when not in attack range
        bool shouldPlayIdle = attackOnCooldown || isAttacking || isBounceAttacking;
        anim.SetBool("IsIdle", !isDead && !playerDead && shouldPlayIdle);
        anim.SetBool("IsBouncing", !isDead && !playerDead && isBouncing && !shouldPlayIdle);
        anim.SetBool("IsBounceInterrupted", !isDead && !playerDead && isBounceInterrupted);
        anim.SetBool("IsBounceAttacking", !isDead && !playerDead && isBounceAttacking);
        anim.SetBool("IsAttacking", !isDead && !playerDead && isAttacking);

        // Face player (sprite flip handled by child SpriteFlipOffset script if present)
        // Only update after initialization to prevent flicker
        if (hasInitialized && !isDead && !playerDead && player != null && spriteRenderer != null)
        {
            bool shouldFlip = !(player.position.x > transform.position.x);
            spriteRenderer.flipX = shouldFlip;

            // Apply flipped collider offset based on direction
            if (col != null)
            {
                Vector2 offsetToUse = shouldFlip ? flippedColliderOffsetRight : flippedColliderOffsetLeft;

                if (boxCollider != null)
                {
                    boxCollider.offset = offsetToUse;
                }
                else if (capsuleCollider != null)
                {
                    capsuleCollider.offset = offsetToUse;
                }
                else if (circleCollider != null)
                {
                    circleCollider.offset = offsetToUse;
                }
            }
        }

        // Off-camera bounce reset removed - was causing spawn flickering

        // Update collider based on animation state (check both bouncing and bounce attacking)
        if (useAnimatedCollider && (isBouncing != lastBouncingState || isBounceAttacking != lastBounceAttackingState))
        {
            UpdateColliderForAnimation();
            lastBouncingState = isBouncing;
            lastBounceAttackingState = isBounceAttacking;
        }
    }

    void FixedUpdate()
    {
        bool playerDead = isPlayerDead || player == null ||
                         (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled);

        if (isDead || playerDead)
        {
            rb.velocity = Vector2.zero;
            StopAllActions();
            return;
        }

        // Handle sliding during bounce attack (highest priority)
        if (isSliding && isBounceAttacking)
        {
            if (Time.time >= slideEndTime)
            {
                isSliding = false;
                slideSpeed = 0f;
            }
            else
            {
                rb.velocity = slideDirection * slideSpeed;
                return;
            }
        }

        // Don't move during interruption or attacks
        if (isBounceInterrupted || isAttacking || isBounceAttacking)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        float dist = Vector2.Distance(transform.position, player.position);
        Vector2 toPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;

        // CRITICAL: Check if within walk radius and NOT bouncing - switch to walking
        if (!isBouncing && !isWalking && dist <= walkRadius)
        {
            isWalking = true;
            Debug.Log($"<color=cyan>Shadow: Switching to walk mode (distance: {dist:F2}, walkRadius: {walkRadius})</color>");
        }
        // If outside walk radius and walking - switch to bouncing
        else if (isWalking && dist > walkRadius)
        {
            isWalking = false;
            isBouncing = true;
            currentBounceSpeed = bounceSpeed;
            bounceSpeedBonus = 0f;
            lastSpeedIncreaseTime = Time.time;
            canBeInterrupted = true;

            if (bounceRoutine == null)
            {
                bounceRoutine = StartCoroutine(BounceRoutine());
            }
            Debug.Log($"<color=cyan>Shadow: Switching to bounce mode (distance: {dist:F2}, walkRadius: {walkRadius})</color>");
        }

        if (isBouncing && dist <= bounceAttackCastRadius && !isBounceAttacking && !attackOnCooldown && bounceAttackRoutine == null)
        {
            isBouncing = false;
            bounceAttackRoutine = StartCoroutine(BounceAttackRoutine());
            return;
        }

        if (!isBouncing && !isBounceAttacking && dist <= attackRange && !isAttacking && !attackOnCooldown && attackRoutine == null)
        {
            // CRITICAL: Disable walking when entering attack range
            isWalking = false;
            attackRoutine = StartCoroutine(AttackRoutine());
            return;
        }

        if (isBouncing)
        {
            return;
        }

        if (dist <= attackRange)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        // Priority 3: Move towards player with sidestep avoidance (walking mode or not bouncing)
        Vector2 forward = toPlayer;

        bool blocked = IsBlockedAhead(forward);
        if (Time.time >= sidestepBlock && blocked && Time.time >= sidestepUntil)
        {
            sidestepDir = ChooseSidestep(forward);
            sidestepUntil = Time.time + sidestepDuration;
            sidestepBlock = Time.time + sidestepDuration + sidestepCooldown;
        }

        float speedMult = 1f;
        if (statusController != null)
        {
            speedMult = statusController.GetEnemyMoveSpeedMultiplier();
        }

        if (Time.time < sidestepUntil)
        {
            rb.velocity = sidestepDir * (moveSpeed * speedMult) * sidestepSpeedMultiplier;
            return;
        }

        rb.velocity = forward * (moveSpeed * speedMult);
    }

    IEnumerator BounceRoutine()
    {
        // Continuous bouncing - no cooldown, no duration limit
        // Speed increases at intervals ONLY when bouncing AND moving
        while (!isDead && isBouncing)
        {
            // Check distance to player for walk radius
            float distToPlayer = player != null ? Vector2.Distance(transform.position, player.position) : float.MaxValue;

            // Determine if in walk radius (affects speed, not state)
            bool inWalkRadius = distToPlayer <= walkRadius;

            Vector2 bounceDirection = player != null ?
                ((Vector2)player.position - (Vector2)transform.position).normalized :
                Vector2.right;

            // Check if within camera view for speed scaling
            bool inView = true;
            if (onlyScaleSpeedInView && Camera.main != null)
            {
                Vector3 viewportPos = Camera.main.WorldToViewportPoint(transform.position);
                // Use on-camera offset from AdvancedPlayerController if available
                float offset = AdvancedPlayerController.Instance != null ? AdvancedPlayerController.Instance.onCameraOffset : 0.1f;
                inView = viewportPos.x >= -offset && viewportPos.x <= 1f + offset &&
                         viewportPos.y >= -offset && viewportPos.y <= 1f + offset;
            }

            // Check if actually moving (velocity magnitude check)
            bool isActuallyMoving = rb.velocity.sqrMagnitude > 0.01f;

            // Increase speed at intervals ONLY if:
            // 1. In view (or setting disabled)
            // 2. Actually moving (not stuck)
            // 3. Currently bouncing
            if (inView && isActuallyMoving && isBouncing && Time.time - lastSpeedIncreaseTime >= bounceSpeedInterval)
            {
                currentBounceSpeed = Mathf.Min(currentBounceSpeed + bounceSpeedMultiplier, maxBounceSpeed);
                bounceSpeedBonus = currentBounceSpeed - bounceSpeed;
                lastSpeedIncreaseTime = Time.time;
            }

            // Use current bounce speed regardless of walk radius
            // Walk radius no longer affects movement speed
            float speedToUse;
            if (inView)
            {
                // In view - use current bounce speed (with multiplier)
                speedToUse = currentBounceSpeed;
            }
            else
            {
                // Off camera - use off-camera speed
                speedToUse = offCameraMoveSpeed;

                if (spriteRenderer != null && !spriteRenderer.enabled)
                {
                    spriteRenderer.enabled = true;
                }
            }

            float speedMult = 1f;
            if (statusController != null)
            {
                speedMult = statusController.GetEnemyMoveSpeedMultiplier();
            }

            rb.velocity = bounceDirection * (speedToUse * speedMult);

            yield return new WaitForFixedUpdate();
        }

        rb.velocity = Vector2.zero;
        bounceRoutine = null;
    }

    IEnumerator BounceAttackRoutine()
    {
        int myToken = BeginBounceAttackAction();
        isBounceAttacking = true;
        canBeInterrupted = true;

        if (bounceRoutine != null)
        {
            StopCoroutine(bounceRoutine);
            bounceRoutine = null;
        }

        Vector2 attackDirection = player != null ?
            ((Vector2)player.position - (Vector2)transform.position).normalized :
            Vector2.right;

        float clampedBounceSpeed = Mathf.Min(currentBounceSpeed, maxBounceSpeed * 0.7f);
        slideSpeed = clampedBounceSpeed * bounceAttackSlideMultiplier;
        slideDirection = attackDirection;
        isSliding = true;

        float slideDuration = bounceAttackSlideDuration > 0 ? bounceAttackSlideDuration : bounceAttackAnimationDuration;
        slideEndTime = Time.time + slideDuration;

        float prevSpeed = anim.speed;
        anim.speed = attackAnimSpeed;
        anim.SetBool("IsBounceAttacking", true);

        // CRITICAL: Allow interruption for specified duration at start of animation
        yield return new WaitForSeconds(bounceAttackInterruptionWindow);
        canBeInterrupted = false;

        if (isDead || isPlayerDead || myToken != bounceAttackActionToken)
        {
            anim.SetBool("IsBounceAttacking", false);
            anim.speed = prevSpeed;
            isBounceAttacking = false;
            bounceAttackRoutine = null;
            yield break;
        }

        // Wait for remaining damage delay (already waited for interruption window)
        float remainingDamageDelay = Mathf.Max(0f, bounceAttackDamageDelayV2 - bounceAttackInterruptionWindow);
        if (remainingDamageDelay > 0)
        {
            yield return new WaitForSeconds(remainingDamageDelay);
        }

        if (isDead || isPlayerDead || myToken != bounceAttackActionToken)
        {
            anim.SetBool("IsBounceAttacking", false);
            anim.speed = prevSpeed;
            isBounceAttacking = false;
            bounceAttackRoutine = null;
            yield break;
        }

        if (player != null && playerDamageable != null && playerDamageable.IsAlive)
        {
            float finalDamage = bounceAttackDamageV2 + (bounceSpeedBonus * bounceAttackDamageMultiplier);
            Vector3 hitPoint = player.position;
            Vector3 hitNormal = (transform.position - hitPoint).normalized;
            PlayerHealth.RegisterPendingAttacker(gameObject);
            playerDamageable.TakeDamage(finalDamage, hitPoint, hitNormal);
        }

        // Wait for remaining animation duration (account for interruption window)
        float totalWaitedTime = bounceAttackInterruptionWindow + remainingDamageDelay;
        float remainingDuration = bounceAttackAnimationDuration - totalWaitedTime;
        if (remainingDuration > 0)
        {
            yield return new WaitForSeconds(remainingDuration);
        }

        isSliding = false;
        slideSpeed = 0f;
        rb.velocity = Vector2.zero;

        anim.SetBool("IsBounceAttacking", false);
        anim.speed = prevSpeed;

        isBounceAttacking = false;
        bounceAttackRoutine = null;

        attackOnCooldown = true;
        float cooldown = attackCooldown;
        if (statusController != null)
        {
            cooldown += statusController.GetLethargyAttackCooldownBonus();
        }
        if (cooldown < 0f)
        {
            cooldown = 0f;
        }
        if (cooldown > 0f)
        {
            yield return new WaitForSeconds(cooldown);
        }
        attackOnCooldown = false;

        yield return new WaitForSeconds(postBounceAttackDelay);
    }

    IEnumerator AttackRoutine()
    {
        int myToken = BeginAttackAction();
        isAttacking = true;

        float prevSpeed = anim.speed;
        anim.speed = attackAnimSpeed;
        anim.SetBool("IsAttacking", true);

        // Wait for damage delay in animation
        if (attackDamageDelayV2 > 0f)
        {
            yield return new WaitForSeconds(attackDamageDelayV2);
        }

        if (isDead || isPlayerDead || myToken != attackActionToken)
        {
            anim.SetBool("IsAttacking", false);
            anim.speed = prevSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        // Deal normal attack damage at specified timing
        if (playerDamageable != null && playerDamageable.IsAlive)
        {
            Vector3 hitPoint = transform.position;
            Vector3 hitNormal = (player.position - transform.position).normalized;
            PlayerHealth.RegisterPendingAttacker(gameObject);
            playerDamageable.TakeDamage(attackDamageV2, hitPoint, hitNormal);
            Debug.Log($"<color=purple>Shadow dealt {attackDamageV2} damage at {attackDamageDelayV2}s into attack</color>");
        }

        // Wait for rest of attack duration
        float remainingAttackTime = (attackDuration / Mathf.Max(0.01f, attackAnimSpeed)) - Mathf.Max(0f, attackDamageDelayV2);
        if (remainingAttackTime > 0f)
        {
            yield return new WaitForSeconds(remainingAttackTime);
        }

        anim.SetBool("IsAttacking", false);
        anim.speed = prevSpeed;

        isAttacking = false;
        attackOnCooldown = true;
        attackRoutine = null;

        float cooldown = attackCooldown;
        if (statusController != null)
        {
            cooldown += statusController.GetLethargyAttackCooldownBonus();
        }
        if (cooldown < 0f)
        {
            cooldown = 0f;
        }
        if (cooldown > 0f)
        {
            yield return new WaitForSeconds(cooldown);
        }
        attackOnCooldown = false;
    }
    IEnumerator BounceInterruptionRoutine(bool withinWalkRadius)
    {
        isBounceInterrupted = true;
        isBouncing = false;
        isWalking = false;

        if (bounceRoutine != null)
        {
            StopCoroutine(bounceRoutine);
            bounceRoutine = null;
        }

        rb.velocity = Vector2.zero;
        anim.SetBool("IsBounceInterrupted", true);

        yield return new WaitForSeconds(bounceInterruptionDuration);

        anim.SetBool("IsBounceInterrupted", false);
        isBounceInterrupted = false;

        // Re-check distance after interruption (enemy might have been knocked back further)
        float currentDistToPlayer = player != null ? Vector2.Distance(transform.position, player.position) : float.MaxValue;
        bool stillWithinWalkRadius = currentDistToPlayer <= walkRadius;

        if (stillWithinWalkRadius)
        {
            // Stay in walk mode
            isWalking = true;
            isBouncing = false;
            Debug.Log($"<color=cyan>Shadow: Staying in walk mode (distance: {currentDistToPlayer:F2}, walkRadius: {walkRadius})</color>");
        }
        else
        {
            // Switch to bounce mode (knocked outside walk radius)
            isBouncing = true;
            isWalking = false;
            currentBounceSpeed = bounceSpeed;
            bounceSpeedBonus = 0f;
            lastSpeedIncreaseTime = Time.time;
            canBeInterrupted = true;

            Debug.Log($"<color=cyan>Shadow: Switching to bounce mode (distance: {currentDistToPlayer:F2}, walkRadius: {walkRadius})</color>");

            if (!isDead && bounceRoutine == null)
            {
                bounceRoutine = StartCoroutine(BounceRoutine());
            }
        }

        bounceInterruptRoutine = null;
    }

    void OnDamageTaken(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (isBouncing && canBeInterrupted && !isBounceInterrupted && bounceInterruptRoutine == null)
        {
            CancelBounceAttackAction();
            if (bounceRoutine != null)
            {
                StopCoroutine(bounceRoutine);
                bounceRoutine = null;
            }

            if (bounceAttackRoutine != null)
            {
                StopCoroutine(bounceAttackRoutine);
                bounceAttackRoutine = null;
                isBounceAttacking = false;
                isSliding = false;
            }

            float distToPlayer = player != null ? Vector2.Distance(transform.position, player.position) : float.MaxValue;
            bool withinWalkRadius = distToPlayer <= walkRadius;

            bounceInterruptRoutine = StartCoroutine(BounceInterruptionRoutine(withinWalkRadius));
        }
    }

    void OnPlayerDeath()
    {
        if (isDead || isPlayerDead) return;

        isPlayerDead = true;
        CancelAttackAction();
        CancelBounceAttackAction();
        rb.velocity = Vector2.zero;
        StopAllActions();
    }

    void UpdateColliderForAnimation()
    {
        if (!useAnimatedCollider || !colliderCached) return;

        Vector2 targetSize = (isBouncing || isBounceAttacking) ? bounceColliderSize : originalColliderSize;

        // Determine offset based on bouncing state and facing direction
        Vector2 targetOffset;
        if (isBouncing || isBounceAttacking)
        {
            // Use direction-specific bounce offset
            bool facingRight = spriteRenderer != null && spriteRenderer.flipX;
            targetOffset = facingRight ? bounceColliderOffsetRight : bounceColliderOffsetLeft;
            Debug.Log($"<color=cyan>Shadow: Applying bounce offset - FacingRight: {facingRight}, Offset: {targetOffset}</color>");
        }
        else
        {
            targetOffset = originalColliderOffset;
        }

        if (boxCollider != null)
        {
            boxCollider.size = targetSize;
            boxCollider.offset = targetOffset;
        }
        else if (capsuleCollider != null)
        {
            capsuleCollider.size = targetSize;
            capsuleCollider.offset = targetOffset;
        }
        else if (circleCollider != null)
        {
            // For circle collider, use the average of width and height
            circleCollider.radius = (targetSize.x + targetSize.y) * 0.25f;
            circleCollider.offset = targetOffset;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!enableCollisionPhasing) return;

        // Check if colliding with another shadow enemy
        if (collision.gameObject.layer == shadowLayer && collision.collider != col)
        {
            // Start tracking collision time
            if (!shadowCollisionTimes.ContainsKey(collision.collider))
            {
                shadowCollisionTimes[collision.collider] = Time.time;
            }
        }
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (!enableCollisionPhasing) return;

        // Check if colliding with another shadow enemy
        if (collision.gameObject.layer == shadowLayer && collision.collider != col)
        {
            // Check if collision has lasted long enough
            if (shadowCollisionTimes.ContainsKey(collision.collider))
            {
                float collisionDuration = Time.time - shadowCollisionTimes[collision.collider];

                if (collisionDuration >= phaseCollisionTime && !phasingColliders.Contains(collision.collider))
                {
                    // Start phasing through this collider
                    Physics2D.IgnoreCollision(col, collision.collider, true);
                    phasingColliders.Add(collision.collider);

                    // Schedule re-enable after some distance
                    StartCoroutine(ReEnableCollisionAfterDistance(collision.collider));
                }
            }
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (!enableCollisionPhasing) return;

        // Remove from tracking when no longer colliding
        if (shadowCollisionTimes.ContainsKey(collision.collider))
        {
            shadowCollisionTimes.Remove(collision.collider);
        }
    }

    IEnumerator ReEnableCollisionAfterDistance(Collider2D otherCollider)
    {
        // Wait until enemies are far enough apart
        float minDistance = 3.0f; // Distance before re-enabling collision

        while (otherCollider != null && Vector2.Distance(transform.position, otherCollider.transform.position) < minDistance)
        {
            yield return new WaitForSeconds(0.2f);
        }

        // Re-enable collision
        if (otherCollider != null && col != null)
        {
            Physics2D.IgnoreCollision(col, otherCollider, false);
        }

        // Remove from phasing set
        if (phasingColliders.Contains(otherCollider))
        {
            phasingColliders.Remove(otherCollider);
        }
    }

    void StopAllActions()
    {
        CancelAttackAction();
        CancelBounceAttackAction();

        if (isBouncing)
        {
            isBouncing = false;
            if (bounceRoutine != null)
            {
                StopCoroutine(bounceRoutine);
                bounceRoutine = null;
            }
        }

        isWalking = false;
        isSliding = false;
        slideSpeed = 0f;

        if (isAttacking)
        {
            isAttacking = false;
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }
            anim.SetBool("IsAttacking", false);
            anim.speed = 1f;
        }

        if (isBounceAttacking)
        {
            isBounceAttacking = false;
            if (bounceAttackRoutine != null)
            {
                StopCoroutine(bounceAttackRoutine);
                bounceAttackRoutine = null;
            }
            anim.SetBool("IsBounceAttacking", false);
            anim.speed = 1f;
        }

        if (isBounceInterrupted)
        {
            isBounceInterrupted = false;
            if (bounceInterruptRoutine != null)
            {
                StopCoroutine(bounceInterruptRoutine);
                bounceInterruptRoutine = null;
            }
            anim.SetBool("IsBounceInterrupted", false);
        }

        attackOnCooldown = false;
    }

    void LateUpdate()
    {
        if (spriteFlipOffset == null) return;

        // Check animation states
        bool isDying = anim.GetBool("IsDead") || anim.GetBool("deadflip");

        // Disable SpriteFlipOffset during death
        if (isDying)
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
    }

    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        CancelAttackAction();
        CancelBounceAttackAction();

        StopAllActions();
        rb.velocity = Vector2.zero;

        // CRITICAL: Set death animation based on flip state
        bool isFlipped = spriteRenderer != null && spriteRenderer.flipX;
        if (isFlipped)
        {
            anim.SetBool("deadflip", true);
            anim.SetBool("IsDead", false);
        }
        else
        {
            anim.SetBool("IsDead", true);
            anim.SetBool("deadflip", false);
        }

        // Disable all other animation states
        anim.SetBool("IsIdle", false);
        anim.SetBool("IsWalking", false);
        anim.SetBool("IsBouncing", false);
        anim.SetBool("IsBounceInterrupted", false);
        anim.SetBool("IsBounceAttacking", false);
        anim.SetBool("IsAttacking", false);

        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        if (col != null) col.enabled = false;

        // Start fade out effect
        StartCoroutine(FadeOutAndDestroy());
    }

    IEnumerator FadeOutAndDestroy()
    {
        // Wait for death animation to play a bit
        float animationDelay = Mathf.Max(0f, deathCleanupDelay - deathFadeOutDuration);
        if (animationDelay > 0)
        {
            yield return new WaitForSeconds(animationDelay);
        }

        // Fade out
        if (spriteRenderer != null)
        {
            float elapsed = 0f;
            Color startColor = spriteRenderer.color;

            while (elapsed < deathFadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / deathFadeOutDuration);
                spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }
        }

        Destroy(gameObject);
    }

    bool IsBlockedAhead(Vector2 forward)
    {
        int n = Physics2D.OverlapCircleNonAlloc(transform.position, neighborAvoidRadius, buf, 1 << shadowLayer);
        if (n <= 1) return false;

        Vector2 pos = transform.position;
        for (int i = 0; i < n; i++)
        {
            var c = buf[i];
            if (c == null || c.attachedRigidbody == rb) continue;

            Vector2 toOther = ((Vector2)c.transform.position - pos);
            float d = toOther.magnitude;
            if (d < 0.0001f) continue;

            Vector2 dir = toOther / d;
            float dot = Vector2.Dot(forward, dir);
            if (dot >= minDotForAhead && d <= aheadBlockCheck) return true;
        }
        return false;
    }

    Vector2 ChooseSidestep(Vector2 forward)
    {
        Vector2 left = new Vector2(-forward.y, forward.x).normalized;
        Vector2 right = new Vector2(forward.y, -forward.x).normalized;
        return (Random.value < 0.5f ? left : right);
    }
}
