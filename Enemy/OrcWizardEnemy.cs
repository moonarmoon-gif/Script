using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(EnemyHealth))]
public class OrcWizardEnemy : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 1.2f;
    public float shootingRange = 6.0f;
    [Range(0, 0.3f)]
    public float movementSmoothing = 0.05f;

    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public Transform firePoint;
    public Transform firePoint2;
    public Vector2 flippedFirePointOffset = Vector2.zero;
    public Vector2 flippedFirePoint2Offset = Vector2.zero;

    public int MinProjectiles = 4;
    public int MaxProjectiles = 10;

    public float MinDamage = 1f;
    public float MaxDamage = 4f;

    public float AttackStartAnimationTime = 0.5f;
    public float AttackEndAnimationTime = 0.5f;

    public float ChannelingTimer = 0.5f;
    public float ProjectileFireInterval = 0.4f;

    public float postAttackCooldown = 1.0f;

    [Header("Knockback Settings")]
    public float knockbackIntensity = 5f;
    public float knockbackDuration = 0.2f;

    [Header("Death Settings")]
    public float deathCleanupDelay = 1.0f;
    public float deathFadeOutDuration = 0.5f;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private Animator animator;
    private Collider2D col;
    private EnemyHealth health;
    private StatusController statusController;
    private Transform player;
    private Vector2 currentVelocity;
    private SpriteFlipOffset spriteFlipOffset;

    private bool isDead;
    private bool isPlayerDead;
    private bool isAttacking;
    private bool isMoving;
    private bool canAttack = true;

    private int attackActionToken = 0;
    private Vector2 knockbackVelocity = Vector2.zero;
    private float knockbackEndTime = 0f;

    private Vector3 firePointBaseLocalPosition;
    private Vector3 firePoint2BaseLocalPosition;
    private bool firePointsCached;

    private bool lastOffsetsDisabled;
    private Coroutine attackRoutine;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
        health = GetComponent<EnemyHealth>();
        statusController = GetComponent<StatusController>();
        spriteFlipOffset = GetComponent<SpriteFlipOffset>();

        if (AdvancedPlayerController.Instance != null)
        {
            player = AdvancedPlayerController.Instance.transform;
            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.OnDeath += OnPlayerDeath;
            }
        }

        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        col.isTrigger = false;
        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Default"), true);
        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Enemy"), true);

        if (firePoint != null)
        {
            firePointBaseLocalPosition = firePoint.localPosition;
        }
        if (firePoint2 != null)
        {
            firePoint2BaseLocalPosition = firePoint2.localPosition;
        }
        firePointsCached = firePoint != null && firePoint2 != null;

        if (MinProjectiles < 1) MinProjectiles = 1;
        if (MaxProjectiles < MinProjectiles) MaxProjectiles = MinProjectiles;
        if (ProjectileFireInterval < 0f) ProjectileFireInterval = 0f;
        if (ChannelingTimer < 0f) ChannelingTimer = 0f;
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

        bool playerDead = isPlayerDead || player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled);

        if (playerDead)
        {
            animator.SetBool("idle", true);
            animator.SetBool("moving", false);
            animator.SetBool("attackstart", false);
            animator.SetBool("attackloop", false);
            animator.SetBool("attackend", false);
            return;
        }

        animator.SetBool("moving", isMoving && !isAttacking);
        animator.SetBool("idle", !isMoving && !isAttacking);

        if (player != null)
        {
            spriteRenderer.flipX = !(player.position.x > transform.position.x);
            UpdateFirePointPositions();
        }
    }

    void LateUpdate()
    {
        if (spriteFlipOffset == null) return;

        bool offsetsDisabled = animator.GetBool("dead") || animator.GetBool("deadflip");
        if (offsetsDisabled != lastOffsetsDisabled)
        {
            spriteFlipOffset.SetColliderOffsetEnabled(!offsetsDisabled);
            spriteFlipOffset.SetShadowOffsetEnabled(!offsetsDisabled);
            lastOffsetsDisabled = offsetsDisabled;
        }
    }

    void FixedUpdate()
    {
        if (isDead)
        {
            rb.velocity = Vector2.zero;
            isMoving = false;
            return;
        }

        if (isPlayerDead || player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled))
        {
            rb.velocity = Vector2.zero;
            isMoving = false;
            StopAllActions();
            return;
        }

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

        if (isAttacking)
        {
            rb.velocity = Vector2.zero;
            isMoving = false;
            return;
        }

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist <= shootingRange)
        {
            rb.velocity = Vector2.zero;
            isMoving = false;

            if (canAttack && attackRoutine == null)
            {
                attackRoutine = StartCoroutine(AttackRoutine());
            }
            return;
        }

        float speedMult = 1f;
        if (statusController != null)
        {
            speedMult = statusController.GetEnemyMoveSpeedMultiplier();
        }

        Vector2 targetVelocity = (player.position - transform.position).normalized * (walkSpeed * speedMult);
        rb.velocity = Vector2.SmoothDamp(rb.velocity, targetVelocity, ref currentVelocity, movementSmoothing);
        isMoving = true;
    }

    private IEnumerator AttackRoutine()
    {
        int myToken = BeginAttackAction();

        isAttacking = true;
        canAttack = false;

        animator.SetBool("attackstart", true);
        animator.SetBool("attackloop", false);
        animator.SetBool("attackend", false);

        float startTime = Mathf.Max(0f, AttackStartAnimationTime);
        if (statusController != null)
        {
            startTime += statusController.GetLethargyAttackCooldownBonus();
        }
        if (startTime > 0f)
        {
            yield return new WaitForSeconds(startTime);
        }

        if (isDead || isPlayerDead || myToken != attackActionToken || player == null)
        {
            EndAttackEarly();
            yield break;
        }

        animator.SetBool("attackstart", false);
        animator.SetBool("attackloop", true);

        float channelDelay = Mathf.Max(0f, ChannelingTimer);
        if (channelDelay > 0f)
        {
            yield return new WaitForSeconds(channelDelay);
        }

        if (isDead || isPlayerDead || myToken != attackActionToken || player == null)
        {
            EndAttackEarly();
            yield break;
        }

        int count = Random.Range(Mathf.Max(1, MinProjectiles), Mathf.Max(1, MaxProjectiles) + 1);

        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                if (ProjectileFireInterval > 0f)
                {
                    yield return new WaitForSeconds(ProjectileFireInterval);
                }
            }

            if (isDead || isPlayerDead || myToken != attackActionToken || player == null)
            {
                EndAttackEarly();
                yield break;
            }

            FireOneProjectile();
        }

        animator.SetBool("attackloop", false);
        animator.SetBool("attackend", true);

        float endTime = Mathf.Max(0f, AttackEndAnimationTime);
        if (endTime > 0f)
        {
            yield return new WaitForSeconds(endTime);
        }

        animator.SetBool("attackend", false);

        isAttacking = false;

        float cooldown = Mathf.Max(0f, postAttackCooldown);
        if (statusController != null)
        {
            cooldown += statusController.GetLethargyAttackCooldownBonus();
        }
        if (cooldown > 0f)
        {
            yield return new WaitForSeconds(cooldown);
        }

        canAttack = true;
        attackRoutine = null;
    }

    private void EndAttackEarly()
    {
        animator.SetBool("attackstart", false);
        animator.SetBool("attackloop", false);
        animator.SetBool("attackend", false);
        isAttacking = false;
        canAttack = true;
        attackRoutine = null;
    }

    private void FireOneProjectile()
    {
        if (projectilePrefab == null || player == null)
        {
            return;
        }

        Vector3 spawnPos = transform.position;
        if (firePoint != null && firePoint2 != null)
        {
            Vector3 a = firePoint.position;
            Vector3 b = firePoint2.position;
            float minX = Mathf.Min(a.x, b.x);
            float maxX = Mathf.Max(a.x, b.x);
            float minY = Mathf.Min(a.y, b.y);
            float maxY = Mathf.Max(a.y, b.y);
            spawnPos = new Vector3(Random.Range(minX, maxX), Random.Range(minY, maxY), a.z);
        }
        else if (firePoint != null)
        {
            spawnPos = firePoint.position;
        }

        Vector2 dir = (player.position - spawnPos).normalized;
        float dmg = Random.Range(Mathf.Min(MinDamage, MaxDamage), Mathf.Max(MinDamage, MaxDamage));

        GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        if (proj.TryGetComponent<NecromancerProjectile>(out var necroProj))
        {
            necroProj.Initialize(dmg, dir, col);
        }
    }

    private void UpdateFirePointPositions()
    {
        if (!firePointsCached) return;

        if (spriteRenderer != null && spriteRenderer.flipX)
        {
            firePoint.localPosition = new Vector3(
                -firePointBaseLocalPosition.x + flippedFirePointOffset.x,
                firePointBaseLocalPosition.y + flippedFirePointOffset.y,
                firePointBaseLocalPosition.z);

            firePoint2.localPosition = new Vector3(
                -firePoint2BaseLocalPosition.x + flippedFirePoint2Offset.x,
                firePoint2BaseLocalPosition.y + flippedFirePoint2Offset.y,
                firePoint2BaseLocalPosition.z);
        }
        else
        {
            firePoint.localPosition = firePointBaseLocalPosition;
            firePoint2.localPosition = firePoint2BaseLocalPosition;
        }
    }

    void StopAllActions()
    {
        CancelAttackAction();
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        isAttacking = false;
        canAttack = true;
    }

    void OnPlayerDeath()
    {
        if (isDead || isPlayerDead) return;
        isPlayerDead = true;
        CancelAttackAction();
        rb.velocity = Vector2.zero;
        StopAllActions();
    }

    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (isDead) return;

        CancelAttackAction();
        StopAllActions();

        knockbackVelocity = direction.normalized * force * knockbackIntensity;
        knockbackEndTime = Time.time + knockbackDuration;
    }

    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        CancelAttackAction();
        StopAllActions();

        bool isFlipped = spriteRenderer != null && spriteRenderer.flipX;
        animator.SetBool("dead", !isFlipped);
        animator.SetBool("deadflip", isFlipped);

        animator.SetBool("idle", false);
        animator.SetBool("moving", false);
        animator.SetBool("attackstart", false);
        animator.SetBool("attackloop", false);
        animator.SetBool("attackend", false);

        rb.velocity = Vector2.zero;
        knockbackVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static;

        col.enabled = false;

        StartCoroutine(DeathCleanupRoutine());
    }

    IEnumerator DeathCleanupRoutine()
    {
        float animationDelay = Mathf.Max(0f, deathCleanupDelay - deathFadeOutDuration);
        if (animationDelay > 0f)
        {
            yield return new WaitForSeconds(animationDelay);
        }

        if (spriteRenderer != null && deathFadeOutDuration > 0f)
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
}
