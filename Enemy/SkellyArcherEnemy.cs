using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(EnemyHealth))]
public class SkellyArcherEnemy : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Walking speed towards player")]
    public float walkSpeed = 1.2f;
    
    [Tooltip("Distance at which archer stops and starts shooting")]
    public float shootingRange = 6.0f;
    
    [Range(0, 0.3f)]
    [Tooltip("Movement smoothing factor")]
    public float movementSmoothing = 0.05f;
    
    [Tooltip("Minimum movement threshold to consider moving")]
    public float minMoveThreshold = 0.1f;

    [Header("Summon Settings")]
    [Tooltip("Duration of summon animation (enemy is immobile and invulnerable)")]
    [SerializeField] private float summonAnimationDuration = 1f;
    
    [Tooltip("Minimum idle time after summon animation")]
    [SerializeField] private float postSummonIdleTimeMin = 0.5f;
    
    [Tooltip("Maximum idle time after summon animation")]
    [SerializeField] private float postSummonIdleTimeMax = 1.5f;

    [Header("Projectile Settings")]
    [Tooltip("Projectile prefab to spawn")]
    public GameObject projectilePrefab;
    
    [Tooltip("Transform point where projectiles spawn (child object)")]
    public Transform firePoint;
    
    [Tooltip("Additional offset for firepoint when sprite is flipped (facing right)")]
    public Vector2 flippedFirePointOffset = Vector2.zero;
    
    [Tooltip("Time before attack animation starts (idle during this)")]
    public float preAttackDelay = 0.5f;
    
    [Tooltip("Attack animation duration")]
    public float attackAnimationTime = 0.5f;
    
    [Tooltip("Projectile spawn timing offset. 0 = spawn when attack animation ends, +1 = 1s after animation, -1 = 1s before animation ends")]
    public float projectileSpawnTiming = 0f;
    
    [Tooltip("Delay before dealing attack damage (0 = default timing)")]
    public float attackDamageDelay = 0f;
    
    [Tooltip("Cooldown after projectile is fired (idle during this)")]
    public float postAttackCooldown = 1.0f;
    
    [Tooltip("Damage dealt by projectiles")]
    public float projectileDamage = 15f;

    [SerializeField] private float projectileDamageV2 = -1f;
    [SerializeField] private float projectileSpawnTimingV2 = 999999f;

    [Header("Reload Settings")]
    [Tooltip("Duration of reload animation after each shot")]
    public float reloadAnimationDuration = 0.7f;

    [Header("Knockback Settings")]
    [Tooltip("Knockback force multiplier")]
    public float knockbackIntensity = 5f;
    
    [Tooltip("Duration of knockback effect")]
    public float knockbackDuration = 0.2f;

    [Header("Death Settings")]
    [Tooltip("Delay before cleanup after death")]
    public float deathCleanupDelay = 1.0f;
    [Tooltip("Duration of fade-out effect on death (seconds)")]
    public float deathFadeOutDuration = 0.5f;

    // Component references
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private Animator animator;
    private CapsuleCollider2D capsuleCollider;
    private EnemyHealth health;
    private StatusController statusController;
    private Transform player;
    private IDamageable playerDamageable;
    private Vector2 currentVelocity;
    private SpriteFlipOffset spriteFlipOffset;
    private CollapsePullController collapsePullController;

    // State flags
    private bool isDead;
    private bool isPlayerDead;
    private bool isSummoning = false;
    private bool freezeOnPlayerDeathPending = false;
    private bool isShootingProjectile;
    private bool isMoving;
    private bool canShoot = true;
    private bool isInPreAttackDelay = false;
    private bool isReloading = false;

    private int shootActionToken = 0;

    // Knockback
    private Vector2 knockbackVelocity = Vector2.zero;
    private float knockbackEndTime = 0f;

    // FirePoint management
    private Vector3 firePointBaseLocalPosition;
    private bool firePointCached = false;

    // Coroutine handles
    private Coroutine shootRoutine;

    private StaticStatus cachedStaticStatus;

    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<Animator>();
        if (capsuleCollider == null) capsuleCollider = GetComponent<CapsuleCollider2D>();
        health = GetComponent<EnemyHealth>();
        statusController = GetComponent<StatusController>();

        // Find player
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

        // Configure rigidbody
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        
        capsuleCollider.isTrigger = false;
        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Default"), true);
        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Enemy"), true);

        // Validate projectile setup
        if (projectilePrefab == null)
        {
            Debug.LogWarning($"{name}: No projectile prefab assigned!");
        }
        
        if (firePoint == null)
        {
            Debug.LogWarning($"{name}: No fire point assigned! Projectiles will spawn at enemy center.");
        }
        else
        {
            firePointBaseLocalPosition = firePoint.localPosition;
            firePointCached = true;
        }
        
        spriteFlipOffset = GetComponent<SpriteFlipOffset>();
        collapsePullController = GetComponent<CollapsePullController>();

        if (projectileDamageV2 < 0f)
        {
            projectileDamageV2 = projectileDamage;
        }
        if (projectileSpawnTimingV2 > 900000f)
        {
            projectileSpawnTimingV2 = projectileSpawnTiming;
        }
    }

    private int BeginShootAction()
    {
        shootActionToken++;
        return shootActionToken;
    }

    private void CancelShootAction()
    {
        shootActionToken++;
    }

    void Start()
    {
        // Start summon animation
        StartCoroutine(SummonRoutine());
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
        
        if (player != null && player.TryGetComponent<PlayerHealth>(out var playerHealth))
        {
            playerHealth.OnDeath -= OnPlayerDeath;
        }
    }

    IEnumerator SummonRoutine()
    {
        isSummoning = true;
        animator.SetBool("summon", true);
        
        // Choose appropriate digout animation based on current flip state
        bool isFlippedAtSummon = spriteRenderer != null && spriteRenderer.flipX;

        if (player != null)
        {
            // Match the same left/right facing logic used in Update
            isFlippedAtSummon = !(player.position.x > transform.position.x);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = isFlippedAtSummon;
        }

        animator.SetBool("digout", !isFlippedAtSummon);
        animator.SetBool("digoutflip", isFlippedAtSummon);
        
        // Invulnerable during summon
        if (health != null)
        {
            health.enabled = false;
        }
        
        Debug.Log($"<color=green>SkellyArcher summoning for {summonAnimationDuration}s (invulnerable)</color>");

        yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
            summonAnimationDuration,
            () => isDead,
            () => IsStaticFrozen());
        
        animator.SetBool("summon", false);
        animator.SetBool("digout", false);
        animator.SetBool("digoutflip", false);
        
        // Enable damage after summon animation
        if (health != null)
        {
            health.enabled = true;
        }

        // If the player died during summon, freeze immediately now that
        // the digout/digoutflip animation has finished.
        if (freezeOnPlayerDeathPending)
        {
            freezeOnPlayerDeathPending = false;
            isSummoning = false;
            FreezeImmediatelyOnPlayerDeath();
            yield break;
        }
        
        // Post-summon idle time (random between min and max)
        float postSummonIdleTime = Random.Range(postSummonIdleTimeMin, postSummonIdleTimeMax);
        Debug.Log($"<color=green>SkellyArcher post-summon idle for {postSummonIdleTime:F2}s</color>");
        
        animator.SetBool("IsIdle", true);
        yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
            postSummonIdleTime,
            () => isDead,
            () => IsStaticFrozen());
        animator.SetBool("IsIdle", false);
        
        isSummoning = false;
        Debug.Log("<color=green>SkellyArcher now active</color>");
    }

    void Update()
    {
        if (isDead || isSummoning) return;

        if (IsStaticFrozen())
        {
            return;
        }

        bool playerDead = isPlayerDead || player == null || 
                         (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled);

        // CRITICAL: Force idle when player is dead
        if (playerDead)
        {
            animator.SetBool("IsIdle", true);
            animator.SetBool("IsAttacking", false);
        }
        else
        {
            if (collapsePullController == null)
            {
                collapsePullController = GetComponent<CollapsePullController>();
            }

            bool forceWalkFromCollapse = collapsePullController != null && collapsePullController.IsPulled;

            // During pre-attack delay, show idle (not attacking). Do NOT idle while reloading.
            bool shouldIdle = !isDead && !isMoving && !isReloading && (!isShootingProjectile || isInPreAttackDelay) && !forceWalkFromCollapse;
            animator.SetBool("IsIdle", shouldIdle);
            animator.SetBool("IsAttacking", !isDead && isShootingProjectile && !isInPreAttackDelay);
        }

        // Face player and update FirePoint position
        if (!isDead && !playerDead && player != null)
        {
            spriteRenderer.flipX = !(player.position.x > transform.position.x);
            UpdateFirePointPosition();
        }
    }

    void LateUpdate()
    {
        // SpriteFlipOffset control (EvilWizard-style)
        if (spriteFlipOffset != null)
        {
            bool isWalking = animator.GetBool("IsWalking") || animator.GetBool("movingflip");
            bool isDying = animator.GetBool("IsDead") || animator.GetBool("deadflip");
            bool isSpawningFlipped = animator.GetBool("digoutflip");

            if (isWalking || isDying || isSpawningFlipped)
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
        
        if (isDead) return;

        // Check if player is dead
        bool playerDead = isPlayerDead || player == null || 
                         (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled);

        // Use different movement booleans based on flip state
        if (playerDead)
        {
            animator.SetBool("movingflip", false);
            animator.SetBool("IsWalking", false);
        }
        else
        {
            if (collapsePullController == null)
            {
                collapsePullController = GetComponent<CollapsePullController>();
            }

            bool forceWalkFromCollapse = collapsePullController != null && collapsePullController.IsPulled;

            if (forceWalkFromCollapse)
            {
                if (spriteRenderer.flipX)
                {
                    animator.SetBool("movingflip", true);
                    animator.SetBool("IsWalking", false);
                }
                else
                {
                    animator.SetBool("IsWalking", true);
                    animator.SetBool("movingflip", false);
                }
            }
            else if (spriteRenderer.flipX)
            {
                animator.SetBool("movingflip", isMoving);
                animator.SetBool("IsWalking", false);
            }
            else
            {
                animator.SetBool("IsWalking", isMoving);
                animator.SetBool("movingflip", false);
            }
        }
    }

    void FixedUpdate()
    {
        if (isDead || isSummoning)
        {
            rb.velocity = Vector2.zero;
            isMoving = false;
            return;
        }

        if (IsStaticFrozen())
        {
            rb.velocity = Vector2.zero;
            isMoving = false;
            float dt = Time.fixedDeltaTime;
            if (dt > 0f)
            {
                knockbackEndTime += dt;
            }
            return;
        }

        // Check if player is valid
        if (isPlayerDead || player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled))
        {
            rb.velocity = Vector2.zero;
            StopAllActions();
            return;
        }

        // Handle knockback
        if (Time.time < knockbackEndTime)
        {
            rb.velocity = knockbackVelocity;
            isMoving = true;
            return;
        }
        else if (knockbackVelocity != Vector2.zero)
        {
            knockbackVelocity = Vector2.zero;
        }

        if (collapsePullController == null)
        {
            collapsePullController = GetComponent<CollapsePullController>();
        }

        bool isPulledByCollapse = collapsePullController != null && collapsePullController.IsPulled;

        // Halt movement while shooting
        if (isShootingProjectile)
        {
            if (!isPulledByCollapse)
            {
                rb.velocity = Vector2.zero;
            }
            isMoving = false;
            return;
        }

        float dist = Vector2.Distance(transform.position, player.position);

        // Shoot projectiles if in range
        if (dist <= shootingRange)
        {
            if (!isPulledByCollapse)
            {
                rb.velocity = Vector2.zero;
            }
            isMoving = false;
            
            if (canShoot && !isShootingProjectile && shootRoutine == null)
            {
                shootRoutine = StartCoroutine(ShootProjectileRoutine());
            }
            return;
        }

        // Move towards player using the global enemy move-speed multiplier so
        // HASTE/BURDEN stacks affect this enemy as well.
        float speedMult = 1f;
        if (statusController != null)
        {
            speedMult = statusController.GetEnemyMoveSpeedMultiplier();
        }

        Vector2 targetVelocity = (player.position - transform.position).normalized * (walkSpeed * speedMult);
        rb.velocity = Vector2.SmoothDamp(rb.velocity, targetVelocity, ref currentVelocity, movementSmoothing);
        isMoving = true;
    }

    IEnumerator ShootProjectileRoutine()
    {
        int myToken = BeginShootAction();
        isShootingProjectile = true;
        canShoot = false;

        yield return StaticPauseHelper.WaitWhileStatic(
            () => isDead || isSummoning || isPlayerDead || myToken != shootActionToken || player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled),
            () => IsStaticFrozen());

        // Pre-attack delay (idle before attack animation starts). LETHARGY
        // increases this windup per stack.
        if (preAttackDelay > 0)
        {
            float delay = preAttackDelay;
            if (statusController != null)
            {
                delay += statusController.GetLethargyAttackCooldownBonus();
            }
            if (delay < 0f)
            {
                delay = 0f;
            }

            isInPreAttackDelay = true;
            yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
                delay,
                () => isDead || isSummoning || isPlayerDead || myToken != shootActionToken || player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled),
                () => IsStaticFrozen());
            isInPreAttackDelay = false;
        }

        if (isDead || isSummoning || isPlayerDead || myToken != shootActionToken || player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled))
        {
            isShootingProjectile = false;
            canShoot = true;
            shootRoutine = null;
            yield break;
        }

        // Attack animation starts here
        // Calculate when to spawn projectile
        float spawnTime = attackAnimationTime + projectileSpawnTimingV2;
        
        // Wait until spawn time (if positive)
        if (spawnTime > 0)
        {
            yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
                spawnTime,
                () => isDead || isSummoning || isPlayerDead || myToken != shootActionToken || player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled),
                () => IsStaticFrozen());
        }

        if (isDead || isSummoning || isPlayerDead || myToken != shootActionToken || player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled))
        {
            isShootingProjectile = false;
            canShoot = true;
            shootRoutine = null;
            yield break;
        }

        // Spawn projectile
        if (projectilePrefab != null && player != null)
        {
            yield return StaticPauseHelper.WaitWhileStatic(
                () => isDead || isSummoning || isPlayerDead || myToken != shootActionToken || player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled),
                () => IsStaticFrozen());

            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;

            Vector3 targetPos = player.position;
            Collider2D playerCol = player.GetComponent<Collider2D>();
            if (playerCol == null)
            {
                playerCol = player.GetComponentInChildren<Collider2D>();
            }
            if (playerCol != null)
            {
                targetPos = playerCol.bounds.center;
            }

            Vector2 direction = ((Vector2)targetPos - (Vector2)spawnPos).normalized;
            
            GameObject proj = Object.Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

            // Initialize projectile (reuses NecromancerProjectile behaviour)
            if (proj.TryGetComponent<NecromancerProjectile>(out var necroProj))
            {
                necroProj.Initialize(projectileDamageV2, direction, capsuleCollider);
            }
        }

        // Wait for remaining animation time (if projectile spawned early)
        float remainingAnimTime = attackAnimationTime - spawnTime;
        if (remainingAnimTime > 0)
        {
            yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
                remainingAnimTime,
                () => isDead || isSummoning || isPlayerDead || myToken != shootActionToken || player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled),
                () => IsStaticFrozen());
        }

        if (isDead || isSummoning || isPlayerDead || myToken != shootActionToken)
        {
            isShootingProjectile = false;
            canShoot = true;
            shootRoutine = null;
            yield break;
        }

        // Attack animation finished
        isShootingProjectile = false;

        // Post-attack cooldown with reload animation
        float remainingCooldown = Mathf.Max(0f, postAttackCooldown);

        if (reloadAnimationDuration > 0f)
        {
            float reloadTime = Mathf.Min(remainingCooldown, reloadAnimationDuration);
            if (reloadTime > 0f)
            {
                isReloading = true;
                animator.SetBool("reload", true);
                animator.SetBool("IsIdle", false);
                yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
                    reloadTime,
                    () => isDead || isSummoning || isPlayerDead || myToken != shootActionToken || player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled),
                    () => IsStaticFrozen());
                animator.SetBool("reload", false);
                isReloading = false;
                remainingCooldown -= reloadTime;
            }
        }

        // If there is still cooldown remaining after reload, stay idle
        if (remainingCooldown > 0f)
        {
            animator.SetBool("IsIdle", true);
            yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
                remainingCooldown,
                () => isDead || isSummoning || isPlayerDead || myToken != shootActionToken || player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled),
                () => IsStaticFrozen());
            animator.SetBool("IsIdle", false);
        }

        canShoot = true;
        shootRoutine = null;
    }

    private bool IsStaticFrozen()
    {
        return StaticPauseHelper.IsStaticFrozen(this, ref cachedStaticStatus);
    }

    void UpdateFirePointPosition()
    {
        if (firePoint == null || !firePointCached) return;

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

    void StopAllActions()
    {
        CancelShootAction();
        if (shootRoutine != null)
        {
            StopCoroutine(shootRoutine);
            shootRoutine = null;
        }
        isShootingProjectile = false;
        canShoot = true;
    }

    void OnPlayerDeath()
    {
        if (isDead || isPlayerDead) return;
        isPlayerDead = true;
        CancelShootAction();
        rb.velocity = Vector2.zero;
        StopAllActions();

        // If currently in summon/digout, defer freeze until after summon
        if (isSummoning || (animator != null && (animator.GetBool("summon") || animator.GetBool("digout") || animator.GetBool("digoutflip"))))
        {
            freezeOnPlayerDeathPending = true;
            Debug.Log("<color=yellow>SkellyArcher will freeze after summon completes (player died during digout)</color>");
            return;
        }

        FreezeImmediatelyOnPlayerDeath();
    }

    private void FreezeImmediatelyOnPlayerDeath()
    {
        if (animator != null)
        {
            animator.SetBool("IsIdle", true);
            animator.SetBool("IsWalking", false);
            animator.SetBool("movingflip", false);
            animator.SetBool("IsAttacking", false);
            animator.SetBool("reload", false);
            animator.SetBool("summon", false);
            animator.SetBool("digout", false);
            animator.SetBool("digoutflip", false);
        }

        Debug.Log("<color=yellow>Skeleton frozen on player death</color>");
    }

    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (isDead) return;

        CancelShootAction();
        StopAllActions();
        
        knockbackVelocity = direction.normalized * force * knockbackIntensity;
        knockbackEndTime = Time.time + knockbackDuration;
    }

    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        CancelShootAction();

        StopAllActions();

        // Set death animation based on flip state
        bool isFlipped = spriteRenderer.flipX;
        animator.SetBool("IsDead", !isFlipped);      // Normal death when NOT flipped
        animator.SetBool("deadflip", isFlipped);     // Flipped death when flipped
        
        // Disable all other animation states
        animator.SetBool("IsIdle", false);
        animator.SetBool("IsWalking", false);
        animator.SetBool("movingflip", false);
        animator.SetBool("IsAttacking", false);
        animator.SetBool("reload", false);
        animator.SetBool("summon", false);
        
        rb.velocity = Vector2.zero;
        knockbackVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static;
        
        capsuleCollider.enabled = false;

        StartCoroutine(DeathCleanupRoutine());
    }

    IEnumerator DeathCleanupRoutine()
    {
        float animationDelay = Mathf.Max(0f, deathCleanupDelay - deathFadeOutDuration);
        if (animationDelay > 0f)
        {
            yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
                animationDelay,
                () => false,
                () => false);
        }

        if (spriteRenderer != null && deathFadeOutDuration > 0f)
        {
            float elapsed = 0f;
            Color startColor = spriteRenderer.color;

            while (elapsed < deathFadeOutDuration)
            {
                float dt = GameStateManager.GetPauseSafeDeltaTime();
                if (dt > 0f)
                {
                    elapsed += dt;
                }
                float alpha = Mathf.Lerp(1f, 0f, elapsed / deathFadeOutDuration);
                spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }
        }

        Destroy(gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead || isSummoning) return;

        if (collision.gameObject.CompareTag("Projectile"))
        {
            // Projectiles handle damage via EnemyHealth component
        }
        else if (collision.gameObject.CompareTag("Player"))
        {
            // Physical contact with player
        }
    }
}
