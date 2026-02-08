using System.Collections;
using UnityEngine;

/// <summary>
/// FireWorm enemy that shoots projectiles at the player
/// No AI behavior, no melee attacks, CrowEnemy-style collision phasing
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(EnemyHealth))]
public class FireWormEnemy : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Walking speed towards player")]
    public float walkSpeed = 1.2f;
    
    [Tooltip("Distance at which FireWorm stops and starts shooting")]
    public float shootingRange = 6.0f;
    
    [Range(0, 0.3f)]
    [Tooltip("Movement smoothing factor")]
    public float movementSmoothing = 0.05f;
    
    [Tooltip("Minimum movement threshold to consider moving")]
    public float minMoveThreshold = 0.1f;

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

    [Header("Knockback Settings")]
    [Tooltip("Knockback force multiplier")]
    public float knockbackIntensity = 5f;
    
    [Tooltip("Duration of knockback effect")]
    public float knockbackDuration = 0.2f;

    [Header("Death Settings")]
    [Tooltip("Delay before cleanup after death")]
    public float deathCleanupDelay = 1.0f;
    [Tooltip("Duration of fade out effect on death (seconds)")]
    [SerializeField] private float deathFadeOutDuration = 0.5f;

    // Component references
    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private Animator anim;
    private Collider2D col;
    private EnemyHealth health;
    private StatusController statusController;
    private Transform player;
    private IDamageable playerDamageable;
    private Vector2 currentVelocity;

    // State flags
    private bool isDead;
    private bool isPlayerDead;
    private bool isShootingProjectile;
    private bool isMoving;
    private bool canShoot = true;
    private bool isInPreAttackDelay = false;

    private int shootActionToken = 0;
    
    // Knockback
    private Vector2 knockbackVelocity = Vector2.zero;
    private float knockbackEndTime = 0f;

    // FirePoint management
    private Vector3 firePointBaseLocalPosition;
    private bool firePointCached = false;

    // Coroutine handles
    private Coroutine shootRoutine;
    private SpriteFlipOffset spriteFlipOffset;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
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
        
        // CrowEnemy-style collision phasing - pass through everything except projectiles and player
        col.isTrigger = false;
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

    void Update()
    {
        if (isDead) return;

        bool playerDead = isPlayerDead || player == null || 
                         (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled);

        // Update animator booleans
        anim.SetBool("IsDead", isDead);
        // During pre-attack delay, show idle (not attacking)
        anim.SetBool("IsIdle", !isDead && !playerDead && !isMoving && (!isShootingProjectile || isInPreAttackDelay));
        anim.SetBool("IsWalking", !isDead && !playerDead && isMoving && !isShootingProjectile);
        // Only show attacking AFTER pre-attack delay
        anim.SetBool("IsAttacking", !isDead && !playerDead && isShootingProjectile && !isInPreAttackDelay);

        // Face player and update FirePoint position
        if (!isDead && !playerDead && player != null)
        {
            sr.flipX = !(player.position.x > transform.position.x);
            UpdateFirePointPosition();
        }
    }

    void FixedUpdate()
    {
        if (isDead)
        {
            rb.velocity = Vector2.zero;
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

        // Halt movement while shooting
        if (isShootingProjectile)
        {
            rb.velocity = Vector2.zero;
            isMoving = false;
            return;
        }

        float dist = Vector2.Distance(transform.position, player.position);

        // Shoot projectiles if in range
        if (dist <= shootingRange)
        {
            rb.velocity = Vector2.zero;
            isMoving = false;
            
            if (canShoot && !isShootingProjectile && shootRoutine == null)
            {
                shootRoutine = StartCoroutine(ShootProjectileRoutine());
            }
            return;
        }

        // Move towards player, applying enemy-wide HASTE/BURDEN multipliers
        // via the StatusController so movement-speed buffs/debuffs affect
        // this enemy consistently with others.
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

        // Pre-attack delay (idle before attack animation starts). LETHARGY
        // increases this windup just like attack cooldowns.
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
            yield return new WaitForSeconds(delay);
            isInPreAttackDelay = false;

            if (isDead || isPlayerDead || myToken != shootActionToken || player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled))
            {
                isShootingProjectile = false;
                canShoot = true;
                shootRoutine = null;
                yield break;
            }
            
            // Check if player moved out of range during pre-attack delay
            if (player != null)
            {
                float dist = Vector2.Distance(transform.position, player.position);
                if (dist > shootingRange)
                {
                    Debug.Log("<color=orange>FireWorm: Player out of range, cancelling attack</color>");
                    isShootingProjectile = false;
                    canShoot = true;
                    shootRoutine = null;
                    yield break;
                }
            }
        }

        // Attack animation starts here
        // Calculate when to spawn projectile
        // projectileSpawnTiming: 0 = end of animation, +1 = 1s after, -1 = 1s before end
        float spawnTime = attackAnimationTime + projectileSpawnTimingV2;
        
        // Wait until spawn time (if positive), checking range periodically
        if (spawnTime > 0)
        {
            float elapsed = 0f;
            float checkInterval = 0.1f; // Check every 0.1 seconds
            
            while (elapsed < spawnTime)
            {
                if (isDead || isPlayerDead || myToken != shootActionToken || player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled))
                {
                    isShootingProjectile = false;
                    canShoot = true;
                    shootRoutine = null;
                    yield break;
                }

                // Check if player moved out of range
                if (player != null)
                {
                    float dist = Vector2.Distance(transform.position, player.position);
                    if (dist > shootingRange)
                    {
                        Debug.Log("<color=orange>FireWorm: Player out of range during attack, cancelling</color>");
                        isShootingProjectile = false;
                        canShoot = true;
                        shootRoutine = null;
                        yield break;
                    }
                }
                
                float waitTime = Mathf.Min(checkInterval, spawnTime - elapsed);
                yield return new WaitForSeconds(waitTime);
                elapsed += waitTime;
            }
        }

        if (isDead || isPlayerDead || myToken != shootActionToken || player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled))
        {
            isShootingProjectile = false;
            canShoot = true;
            shootRoutine = null;
            yield break;
        }

        // Spawn projectile
        if (projectilePrefab != null && player != null)
        {
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
            
            GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

            // Initialize projectile
            if (proj.TryGetComponent<NecromancerProjectile>(out var necroProj))
            {
                necroProj.Initialize(projectileDamageV2, direction, col);
            }
            
            Debug.Log($"<color=orange>FireWorm fired projectile at {spawnTime:F2}s into attack animation</color>");
        }

        // Wait for remaining animation time (if projectile spawned early)
        float remainingAnimTime = attackAnimationTime - spawnTime;
        if (remainingAnimTime > 0)
        {
            yield return new WaitForSeconds(remainingAnimTime);
            Debug.Log($"<color=orange>FireWorm waited {remainingAnimTime:F2}s for animation to complete</color>");
        }

        if (isDead || isPlayerDead || myToken != shootActionToken)
        {
            isShootingProjectile = false;
            canShoot = true;
            shootRoutine = null;
            yield break;
        }

        // Attack animation finished - set to idle
        isShootingProjectile = false;

        // Post-attack cooldown (idle after attack finishes). LETHARGY stacks
        // extend this downtime.
        if (postAttackCooldown > 0)
        {
            float cooldown = postAttackCooldown;
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
        }

        canShoot = true;
        shootRoutine = null;
    }

    void UpdateFirePointPosition()
    {
        if (firePoint == null || !firePointCached) return;

        if (sr.flipX)
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

    void OnPlayerDeath()
    {
        if (isDead || isPlayerDead) return;
        isPlayerDead = true;
        CancelShootAction();
        rb.velocity = Vector2.zero;
        StopAllActions();
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

        // CRITICAL: Set death animation based on flip state
        bool isFlipped = sr.flipX;
        anim.SetBool("IsDead", !isFlipped);      // Normal death when NOT flipped
        anim.SetBool("deadflip", isFlipped);     // Flipped death when flipped
        
        // Disable all other animation states
        anim.SetBool("IsIdle", false);
        anim.SetBool("IsWalking", false);
        anim.SetBool("IsAttacking", false);
        
        rb.velocity = Vector2.zero;
        knockbackVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static;
        
        col.enabled = false;

        StartCoroutine(DeathCleanupRoutine());
    }

    IEnumerator DeathCleanupRoutine()
    {
        // Wait for death animation to play a bit
        float animationDelay = Mathf.Max(0f, deathCleanupDelay - deathFadeOutDuration);
        if (animationDelay > 0)
        {
            yield return new WaitForSeconds(animationDelay);
        }

        // Fade out
        if (sr != null)
        {
            float elapsed = 0f;
            Color startColor = sr.color;

            while (elapsed < deathFadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / deathFadeOutDuration);
                sr.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }
        }

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
}
