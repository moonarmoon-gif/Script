using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(EnemyHealth))]
public class OrcArcherEnemy : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 1.2f;
    public float shootingRange = 6.0f;
    [Range(0, 0.3f)]
    public float movementSmoothing = 0.05f;

    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public Transform firePoint;
    public Vector2 flippedFirePointOffset = Vector2.zero;
    public float postAttackCooldown = 1.0f;
    public float projectileDamage = 15f;

    public float AttackStartAnimationTime = 0.5f;
    public float AttackShotAnimationTime = 0.35f;
    public float ReloadAnimationTime = 0.7f;
    public float AttackToIdleAnimationTime = 0.4f;

    public int ComboAttackAmount = 5;

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
    private bool firePointCached;

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
            firePointCached = true;
        }

        if (ComboAttackAmount < 1) ComboAttackAmount = 1;
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
            animator.SetBool("movingflip", false);
            animator.SetBool("attackstart", false);
            animator.SetBool("attackshot", false);
            animator.SetBool("attackstatic", false);
            animator.SetBool("attacktoidle", false);
            animator.SetBool("reload", false);
            return;
        }

        animator.SetBool("idle", !isMoving && !isAttacking);

        if (player != null)
        {
            spriteRenderer.flipX = !(player.position.x > transform.position.x);
            UpdateFirePointPosition();
        }
    }

    void LateUpdate()
    {
        if (spriteFlipOffset != null)
        {
            bool offsetsDisabled = animator.GetBool("moving") || animator.GetBool("movingflip")
                || animator.GetBool("dead") || animator.GetBool("deadflip");

            if (offsetsDisabled != lastOffsetsDisabled)
            {
                spriteFlipOffset.SetColliderOffsetEnabled(!offsetsDisabled);
                spriteFlipOffset.SetShadowOffsetEnabled(!offsetsDisabled);
                lastOffsetsDisabled = offsetsDisabled;
            }
        }

        if (isDead) return;

        bool playerDead = isPlayerDead || player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled);
        if (playerDead)
        {
            animator.SetBool("moving", false);
            animator.SetBool("movingflip", false);
            return;
        }

        bool flipped = spriteRenderer != null && spriteRenderer.flipX;
        bool shouldMove = isMoving && !isAttacking;

        if (flipped)
        {
            animator.SetBool("movingflip", shouldMove);
            animator.SetBool("moving", false);
        }
        else
        {
            animator.SetBool("moving", shouldMove);
            animator.SetBool("movingflip", false);
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
                attackRoutine = StartCoroutine(ComboAttackRoutine());
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

    private IEnumerator ComboAttackRoutine()
    {
        int myToken = BeginAttackAction();

        isAttacking = true;
        canAttack = false;

        animator.SetBool("attackstart", true);
        animator.SetBool("attackshot", false);
        animator.SetBool("attackstatic", false);
        animator.SetBool("attacktoidle", false);
        animator.SetBool("reload", false);

        float startTime = Mathf.Max(0f, AttackStartAnimationTime);
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

        int shots = Mathf.Max(1, ComboAttackAmount);
        for (int i = 0; i < shots; i++)
        {
            if (isDead || isPlayerDead || myToken != attackActionToken || player == null)
            {
                EndAttackEarly();
                yield break;
            }

            animator.SetBool("attackshot", true);
            FireOneProjectile();

            float shotTime = Mathf.Max(0f, AttackShotAnimationTime);
            if (shotTime > 0f)
            {
                yield return new WaitForSeconds(shotTime);
            }

            animator.SetBool("attackshot", false);

            if (i < shots - 1)
            {
                animator.SetBool("reload", true);

                float reloadTime = Mathf.Max(0f, ReloadAnimationTime);
                if (reloadTime > 0f)
                {
                    yield return new WaitForSeconds(reloadTime);
                }

                animator.SetBool("reload", false);
            }
        }

        animator.SetBool("attacktoidle", true);

        float toIdleTime = Mathf.Max(0f, AttackToIdleAnimationTime);
        if (toIdleTime > 0f)
        {
            yield return new WaitForSeconds(toIdleTime);
        }

        animator.SetBool("attacktoidle", false);

        isAttacking = false;

        float cooldown = Mathf.Max(0f, postAttackCooldown);
        if (statusController != null)
        {
            cooldown += statusController.GetLethargyAttackCooldownBonus();
        }
        if (cooldown > 0f)
        {
            animator.SetBool("idle", true);
            yield return new WaitForSeconds(cooldown);
        }

        animator.SetBool("idle", false);

        canAttack = true;
        attackRoutine = null;
    }

    private void EndAttackEarly()
    {
        animator.SetBool("attackstart", false);
        animator.SetBool("attackshot", false);
        animator.SetBool("attackstatic", false);
        animator.SetBool("attacktoidle", false);
        animator.SetBool("reload", false);
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

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        Vector2 dir = (player.position - spawnPos).normalized;

        GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        if (proj.TryGetComponent<NecromancerProjectile>(out var necroProj))
        {
            necroProj.Initialize(projectileDamage, dir, col);
        }
    }

    private void UpdateFirePointPosition()
    {
        if (firePoint == null || !firePointCached) return;

        if (spriteRenderer != null && spriteRenderer.flipX)
        {
            firePoint.localPosition = new Vector3(
                -firePointBaseLocalPosition.x + flippedFirePointOffset.x,
                firePointBaseLocalPosition.y + flippedFirePointOffset.y,
                firePointBaseLocalPosition.z);
        }
        else
        {
            firePoint.localPosition = firePointBaseLocalPosition;
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
        animator.SetBool("movingflip", false);
        animator.SetBool("attackstart", false);
        animator.SetBool("attackshot", false);
        animator.SetBool("attackstatic", false);
        animator.SetBool("attacktoidle", false);
        animator.SetBool("reload", false);

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
