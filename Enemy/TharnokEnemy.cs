using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(EnemyHealth))]
public class TharnokEnemy : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float stopDistance = 1.4f;
    public float attackZone = 1f;
    [SerializeField] private float attackAnimSpeed = 1.0f;
    [SerializeField] private float attackCooldown = 1.0f;
    [SerializeField] private float deathCleanupDelay = 0.7f;
    [Tooltip("Duration of fade out effect on death (seconds)")]
    [SerializeField] private float deathFadeOutDuration = 0.5f;

    [Header("Melee Attack Settings")]
    [Tooltip("Total duration of the melee attack animation (seconds)")]
    [SerializeField] private float attackDuration = 0.7f;
    [SerializeField] private float firstAttackDamage = 15f;
    [Tooltip("Delay before FIRST melee damage (seconds)")]
    [SerializeField] private float firstAttackDamageDelay = 0.2f;
    [SerializeField] private float secondAttackDamage = 20f;
    [Tooltip("Time from attack animation START until SECOND melee damage (seconds)")]
    [SerializeField] private float secondAttackDamageDelay = 0.45f;

    [SerializeField] private float firstAttackDamageV2 = -1f;
    [SerializeField] private float firstAttackDamageDelayV2 = -1f;
    [SerializeField] private float secondAttackDamageV2 = -1f;
    [SerializeField] private float secondAttackDamageDelayV2 = -1f;

    [Header("Ranged Attack Settings")]
    [Tooltip("Distance at which Tharnok starts using ranged attacks")]
    public float shootingRange = 6.0f;
    [Tooltip("Projectile prefab to spawn")]
    public GameObject projectilePrefab;
    [Tooltip("Transform point where projectiles spawn (child object)")]
    public Transform firePoint;
    [Tooltip("Additional offset for firepoint when sprite is flipped (facing right)")]
    public Vector2 flippedFirePointOffset = Vector2.zero;
    [Tooltip("Delay before starting ranged attack animation")]
    public float preRangedAttackDelay = 0.5f;
    [Tooltip("Ranged attack animation duration")]
    public float rangedAttackAnimationTime = 0.5f;
    [Tooltip("Projectile spawn timing offset. 0 = spawn when attack animation ends, +1 = 1s after animation, -1 = 1s before animation ends")]
    public float projectileSpawnTiming = 0f;
    [Tooltip("Idle duration after each projectile is fired (Tharnok plays idle and does not move)")]
    [FormerlySerializedAs("postRangedAttackCooldown")]
    public float postRangedAttackIdleDuration = 1.0f;
    [Tooltip("Damage dealt by projectiles")]
    public float projectileDamage = 15f;

    [SerializeField] private float projectileDamageV2 = -1f;
    [SerializeField] private float projectileSpawnTimingV2 = 999999f;
    [SerializeField] private float preRangedAttackDelayV2 = -1f;
    [Tooltip("Minimum number of ranged projectiles Tharnok will fire per volley before switching to melee.")]
    public int minRangedShots = 1;
    [Tooltip("How many ranged projectiles Tharnok will fire before switching to melee only. 0 = unlimited.")]
    public int maxRangedShots = 1;
    [Tooltip("Cooldown duration after firing maxRangedShots before Tharnok can start a new ranged volley (seconds). 0 = no global projectile cooldown.")]
    public float projectileCooldown = 0f;

    [Header("Knockback Settings")]
    [Tooltip("Knockback force when hit by projectiles")]
    public float knockbackIntensity = 5f;
    [Tooltip("How long knockback lasts")]
    public float knockbackDuration = 0.2f;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;
    [SerializeField] private CapsuleCollider2D capsuleCollider;

    private EnemyHealth health;
    private StatusController statusController;
    private IDamageable playerDamageable;
    private Transform player;
    private SpriteFlipOffset spriteFlipOffset;

    private Vector3 firePointBaseLocalPosition;
    private bool firePointCached;

    private bool isDead;
    private bool isPlayerDead;
    private bool isAttacking;
    private bool attackOnCooldown;
    private Coroutine attackRoutine;

    // Ranged state
    private bool isShootingProjectile;
    private bool canShootRanged = true;
    private bool isInPreRangedDelay;
    private Coroutine rangedRoutine;
    private int rangedShotsFired;
    private bool isInPostRangedCooldown;
    private float projectileCooldownTimer;
    private int currentRangedVolleyTarget;
    private bool hasRolledRangedVolleyTargetThisLife;

    private int meleeActionToken = 0;
    private int rangedActionToken = 0;

    // Movement / knockback
    private bool isMoving;
    private Vector2 knockbackVelocity = Vector2.zero;
    private float knockbackEndTime = 0f;

    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<Animator>();
        if (capsuleCollider == null) capsuleCollider = GetComponent<CapsuleCollider2D>();
        health = GetComponent<EnemyHealth>();
        statusController = GetComponent<StatusController>();
        spriteFlipOffset = GetComponent<SpriteFlipOffset>();

        if (firePoint != null)
        {
            firePointBaseLocalPosition = firePoint.localPosition;
            firePointCached = true;
        }

        capsuleCollider.isTrigger = false;
        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Default"), true);
        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Enemy"), true);

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

        RollRangedVolleyTargetOnceForLife();

        if (firstAttackDamageV2 < 0f)
        {
            firstAttackDamageV2 = firstAttackDamage;
        }
        if (firstAttackDamageDelayV2 < 0f)
        {
            firstAttackDamageDelayV2 = firstAttackDamageDelay;
        }
        if (secondAttackDamageV2 < 0f)
        {
            secondAttackDamageV2 = secondAttackDamage;
        }
        if (secondAttackDamageDelayV2 < 0f)
        {
            secondAttackDamageDelayV2 = secondAttackDamageDelay;
        }
        if (projectileDamageV2 < 0f)
        {
            projectileDamageV2 = projectileDamage;
        }
        if (projectileSpawnTimingV2 > 900000f)
        {
            projectileSpawnTimingV2 = projectileSpawnTiming;
        }
        if (preRangedAttackDelayV2 < 0f)
        {
            preRangedAttackDelayV2 = preRangedAttackDelay;
        }
    }

    private int BeginMeleeAction()
    {
        meleeActionToken++;
        return meleeActionToken;
    }

    private void CancelMeleeAction()
    {
        meleeActionToken++;
    }

    private int BeginRangedAction()
    {
        rangedActionToken++;
        return rangedActionToken;
    }

    private void CancelRangedAction()
    {
        rangedActionToken++;
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
        if (projectileCooldownTimer > 0f)
        {
            projectileCooldownTimer -= Time.deltaTime;
            if (projectileCooldownTimer < 0f)
            {
                projectileCooldownTimer = 0f;
            }
        }

        if (isDead) return;

        bool playerDead = player == null ||
                          (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled);

        if (playerDead)
        {
            animator.SetBool("idle", true);
            animator.SetBool("moving", false);
            animator.SetBool("movingflip", false);
            animator.SetBool("attack", false);
            animator.SetBool("attackflip", false);
            animator.SetBool("attackfar", false);
            animator.SetBool("attackfarflip", false);
            return;
        }

        bool isChasing = isMoving && !isAttacking && !isShootingProjectile;

        // Tharnok: always use "moving" bool for walking, never set "movingflip" true.
        if (isChasing)
        {
            animator.SetBool("moving", true);
        }
        else
        {
            animator.SetBool("moving", false);
        }
        animator.SetBool("movingflip", false);

        bool showIdle = (!isChasing && !isAttacking && !isShootingProjectile && attackOnCooldown)
                        || isInPreRangedDelay
                        || isInPostRangedCooldown;
        animator.SetBool("idle", showIdle);

        if (!isAttacking && !attackOnCooldown && !isShootingProjectile && !isInPostRangedCooldown && player != null)
        {
            float distance = Vector2.Distance(transform.position, player.position);
            if (distance <= attackRange)
            {
                attackRoutine = StartCoroutine(MeleeAttackRoutine());
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

        if (player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled))
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

        if (isAttacking || isShootingProjectile || isInPostRangedCooldown)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        Vector3 toPlayer = player.position - transform.position;
        float distance = toPlayer.magnitude;

        spriteRenderer.flipX = toPlayer.x <= 0;

        bool hasRangedQuota = maxRangedShots <= 0 || rangedShotsFired < maxRangedShots;
        bool hasRolledNonZeroVolley = maxRangedShots <= 0 || currentRangedVolleyTarget > 0;

        bool canTryRanged = canShootRanged &&
                            projectileCooldownTimer <= 0f &&
                            !isInPostRangedCooldown &&
                            hasRangedQuota &&
                            hasRolledNonZeroVolley &&
                            distance > (attackRange + attackZone) &&
                            distance <= shootingRange;

        if (canTryRanged)
        {
            rb.velocity = Vector2.zero;
            isMoving = false;

            if (!isShootingProjectile && rangedRoutine == null)
            {
                rangedRoutine = StartCoroutine(RangedAttackRoutine());
            }

            return;
        }

        if (distance <= stopDistance)
        {
            rb.velocity = Vector2.zero;
            isMoving = false;
            return;
        }

        float speedMult = 1f;
        if (statusController != null)
        {
            speedMult = statusController.GetEnemyMoveSpeedMultiplier();
        }

        rb.velocity = toPlayer.normalized * (moveSpeed * speedMult);
        isMoving = true;
    }

    void RollRangedVolleyTargetOnceForLife()
    {
        if (hasRolledRangedVolleyTargetThisLife)
        {
            return;
        }

        hasRolledRangedVolleyTargetThisLife = true;

        if (maxRangedShots > 0)
        {
            int minShots = Mathf.Clamp(minRangedShots, 0, maxRangedShots);
            currentRangedVolleyTarget = Random.Range(minShots, maxRangedShots + 1);

            if (currentRangedVolleyTarget <= 0)
            {
                canShootRanged = false;
            }
        }
    }

    IEnumerator MeleeAttackRoutine()
    {
        int myToken = BeginMeleeAction();
        isAttacking = true;
        attackOnCooldown = false;

        float originalSpeed = animator.speed;
        animator.speed = attackAnimSpeed;

        bool isFlipped = spriteRenderer.flipX;
        animator.SetBool("attack", !isFlipped);
        animator.SetBool("attackflip", isFlipped);

        float firstDelay = Mathf.Max(0f, firstAttackDamageDelayV2);
        if (firstDelay > 0f)
        {
            yield return new WaitForSeconds(firstDelay);
        }

        if (isDead || isPlayerDead || myToken != meleeActionToken)
        {
            animator.SetBool("attack", false);
            animator.SetBool("attackflip", false);
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        if (!DealMeleeDamage(firstAttackDamageV2))
        {
            animator.SetBool("attack", false);
            animator.SetBool("attackflip", false);
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        float deltaSecond = Mathf.Max(0f, secondAttackDamageDelayV2 - firstAttackDamageDelayV2);
        if (deltaSecond > 0f)
        {
            yield return new WaitForSeconds(deltaSecond);
        }

        if (!isDead && !isPlayerDead && myToken == meleeActionToken)
        {
            DealMeleeDamage(secondAttackDamageV2);
        }

        float totalDamageTime = Mathf.Max(firstAttackDamageDelayV2, secondAttackDamageDelayV2);
        float remainingTime = attackDuration - totalDamageTime;
        if (remainingTime > 0f)
        {
            yield return new WaitForSeconds(remainingTime);
        }

        animator.SetBool("attack", false);
        animator.SetBool("attackflip", false);
        animator.speed = originalSpeed;
        isAttacking = false;

        if (attackCooldown > 0f)
        {
            attackOnCooldown = true;
            animator.SetBool("idle", true);

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
            animator.SetBool("idle", false);
        }

        attackRoutine = null;
    }

    bool DealMeleeDamage(float damage)
    {
        if (playerDamageable != null && playerDamageable.IsAlive &&
            player != null && AdvancedPlayerController.Instance != null && AdvancedPlayerController.Instance.enabled)
        {
            Vector3 hitPoint = player.position;
            Vector3 hitNormal = (player.position - transform.position).normalized;
            PlayerHealth.RegisterPendingAttacker(gameObject);
            playerDamageable.TakeDamage(damage, hitPoint, hitNormal);
            return true;
        }

        return false;
    }

    IEnumerator RangedAttackRoutine()
    {
        int myToken = BeginRangedAction();
        if (maxRangedShots > 0 && currentRangedVolleyTarget <= 0)
        {
            isShootingProjectile = false;
            rangedRoutine = null;
            yield break;
        }

        isShootingProjectile = true;

        if (preRangedAttackDelayV2 > 0f)
        {
            float delay = preRangedAttackDelayV2;
            if (statusController != null)
            {
                delay += statusController.GetLethargyAttackCooldownBonus();
            }
            if (delay < 0f)
            {
                delay = 0f;
            }

            isInPreRangedDelay = true;
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }
            isInPreRangedDelay = false;
        }

        if (isDead || isPlayerDead || myToken != rangedActionToken || projectilePrefab == null || player == null)
        {
            animator.SetBool("attackfar", false);
            animator.SetBool("attackfarflip", false);
            isShootingProjectile = false;
            rangedRoutine = null;
            yield break;
        }

        bool isFlipped = spriteRenderer.flipX;
        animator.SetBool("attackfar", !isFlipped);
        animator.SetBool("attackfarflip", isFlipped);

        float spawnTime = rangedAttackAnimationTime + projectileSpawnTimingV2;
        if (spawnTime > 0f)
        {
            yield return new WaitForSeconds(spawnTime);
        }

        if (isDead || isPlayerDead || myToken != rangedActionToken || projectilePrefab == null || player == null)
        {
            animator.SetBool("attackfar", false);
            animator.SetBool("attackfarflip", false);
            isShootingProjectile = false;
            rangedRoutine = null;
            yield break;
        }

        if (!isDead && projectilePrefab != null && player != null)
        {
            UpdateFirePointPosition();

            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            Vector2 dir = ((Vector2)player.position - (Vector2)spawnPos).normalized;

            GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

            if (proj.TryGetComponent<NecromancerProjectile>(out var necroProj))
            {
                necroProj.Initialize(projectileDamageV2, dir, capsuleCollider);
            }
        }

        float remainingAnimTime = rangedAttackAnimationTime - spawnTime;
        if (remainingAnimTime > 0f)
        {
            yield return new WaitForSeconds(remainingAnimTime);
        }

        animator.SetBool("attackfar", false);
        animator.SetBool("attackfarflip", false);

        isShootingProjectile = false;
        rangedShotsFired++;

        bool hasInfiniteRanged = maxRangedShots <= 0;
        int volleyTarget = currentRangedVolleyTarget;
        if (!hasInfiniteRanged && volleyTarget <= 0)
        {
            volleyTarget = maxRangedShots;
        }

        bool hasRangedQuota = hasInfiniteRanged || rangedShotsFired < volleyTarget;

        if (postRangedAttackIdleDuration > 0f)
        {
            float idle = postRangedAttackIdleDuration;
            if (statusController != null)
            {
                idle += statusController.GetLethargyAttackCooldownBonus();
            }
            if (idle < 0f)
            {
                idle = 0f;
            }

            isInPostRangedCooldown = true;
            if (idle > 0f)
            {
                yield return new WaitForSeconds(idle);
            }
            isInPostRangedCooldown = false;
        }

        if (!hasRangedQuota && maxRangedShots > 0)
        {
            if (projectileCooldown > 0f)
            {
                projectileCooldownTimer = projectileCooldown;
            }

            rangedShotsFired = 0;
        }

        rangedRoutine = null;
    }

    void UpdateFirePointPosition()
    {
        if (firePoint == null) return;
        Vector3 baseLocal = firePointCached ? firePointBaseLocalPosition : firePoint.localPosition;

        if (spriteRenderer.flipX)
        {
            firePoint.localPosition = new Vector3(
                -Mathf.Abs(baseLocal.x) + flippedFirePointOffset.x,
                baseLocal.y + flippedFirePointOffset.y,
                baseLocal.z
            );
        }
        else
        {
            firePoint.localPosition = new Vector3(
                Mathf.Abs(baseLocal.x),
                baseLocal.y,
                baseLocal.z
            );
        }
    }

    void LateUpdate()
    {
        if (spriteFlipOffset != null)
        {
            bool isWalking = animator.GetBool("moving");
            bool isMeleeAttacking = animator.GetBool("attack") || animator.GetBool("attackflip");
            bool isRangedAttacking = animator.GetBool("attackfar") || animator.GetBool("attackfarflip");
            bool isDying = animator.GetBool("dead") || animator.GetBool("deadflip");

            // Tharnok: keep collider/shadow offsets enabled while walking; only
            // disable them during attacks or death.
            bool disableOffsets = isMeleeAttacking || isRangedAttacking || isDying;
            bool enableOffsets = !disableOffsets;

            spriteFlipOffset.SetColliderOffsetEnabled(enableOffsets);
            spriteFlipOffset.SetShadowOffsetEnabled(enableOffsets);
        }
    }

    void OnPlayerDeath()
    {
        if (isDead || isPlayerDead) return;

        isPlayerDead = true;
        rb.velocity = Vector2.zero;

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (rangedRoutine != null)
        {
            StopCoroutine(rangedRoutine);
            rangedRoutine = null;
        }

        CancelMeleeAction();
        CancelRangedAction();

        isAttacking = false;
        isShootingProjectile = false;
        attackOnCooldown = false;
        canShootRanged = false;

        animator.SetBool("attack", false);
        animator.SetBool("attackflip", false);
        animator.SetBool("attackfar", false);
        animator.SetBool("attackfarflip", false);
        animator.SetBool("moving", false);
        animator.SetBool("movingflip", false);
        animator.SetBool("idle", true);
    }

    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (isDead) return;

        knockbackVelocity = direction.normalized * force * knockbackIntensity;
        knockbackEndTime = Time.time + knockbackDuration;

        CancelMeleeAction();
        CancelRangedAction();

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (rangedRoutine != null)
        {
            StopCoroutine(rangedRoutine);
            rangedRoutine = null;
        }

        isAttacking = false;
        isShootingProjectile = false;
        attackOnCooldown = false;
        animator.SetBool("attack", false);
        animator.SetBool("attackflip", false);
        animator.SetBool("attackfar", false);
        animator.SetBool("attackfarflip", false);
    }

    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        CancelMeleeAction();
        CancelRangedAction();

        if (attackRoutine != null) StopCoroutine(attackRoutine);
        if (rangedRoutine != null) StopCoroutine(rangedRoutine);

        bool isFlipped = spriteRenderer != null && spriteRenderer.flipX;
        animator.SetBool("dead", !isFlipped);
        animator.SetBool("deadflip", isFlipped);

        animator.SetBool("moving", false);
        animator.SetBool("movingflip", false);
        animator.SetBool("idle", false);
        animator.SetBool("attack", false);
        animator.SetBool("attackflip", false);
        animator.SetBool("attackfar", false);
        animator.SetBool("attackfarflip", false);

        rb.velocity = Vector2.zero;
        knockbackVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static;

        capsuleCollider.enabled = false;

        StartCoroutine(FadeOutAndDestroy());
    }

    IEnumerator FadeOutAndDestroy()
    {
        float animationDelay = Mathf.Max(0f, deathCleanupDelay - deathFadeOutDuration);
        if (animationDelay > 0f)
        {
            yield return new WaitForSeconds(animationDelay);
        }

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
