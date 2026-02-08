using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(EnemyHealth))]
public class FireWizardEnemy : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float stopDistance = 1.4f;
    [SerializeField] private float attackAnimSpeed = 1.0f;
    [SerializeField] private float attackCooldown = 1.0f;
    [SerializeField] private float attackDuration = 0.5f;
    [SerializeField] private float deathCleanupDelay = 0.7f;
    [Tooltip("Duration of fade out effect on death (seconds)")]
    [SerializeField] private float deathFadeOutDuration = 0.5f;

    [Header("Damage Settings")]
    [SerializeField] private float attackDamage = 15f;
    [Tooltip("Delay before FIRST damage instance (seconds)")]
    [SerializeField] private float firstAttackDamageDelay = 0.2f;
    [Tooltip("Delay between subsequent damage instances (seconds)")]
    [SerializeField] private float restAttackDamageDelay = 0.15f;
    [Tooltip("Number of damage instances per attack (1 = normal, 2+ = multiple hits)")]
    [SerializeField] private int damageInstancesPerAttack = 1;

    [SerializeField] private float attackDamageV2 = -1f;
    [SerializeField] private float firstAttackDamageDelayV2 = -1f;
    [SerializeField] private float restAttackDamageDelayV2 = -1f;
    [SerializeField] private int damageInstancesPerAttackV2 = -1;
    
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
    private SpriteFlipOffset spriteFlipOffset;
    private bool isDead;
    private bool isAttacking;
    private bool attackOnCooldown;
    private bool hasDealtDamageThisAttack = false;
    private Coroutine attackRoutine;
    private Vector2 knockbackVelocity = Vector2.zero;
    private float knockbackEndTime = 0f;
    private bool wasMoving = false;

    private bool isPlayerDead;
    private int attackActionToken = 0;

    private StaticStatus cachedStaticStatus;

    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<Animator>();
        if (capsuleCollider == null) capsuleCollider = GetComponent<CapsuleCollider2D>();
        health = GetComponent<EnemyHealth>();
        statusController = GetComponent<StatusController>();
        spriteFlipOffset = GetComponent<SpriteFlipOffset>();

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
        if (firstAttackDamageDelayV2 < 0f)
        {
            firstAttackDamageDelayV2 = firstAttackDamageDelay;
        }
        if (restAttackDamageDelayV2 < 0f)
        {
            restAttackDamageDelayV2 = restAttackDamageDelay;
        }
        if (damageInstancesPerAttackV2 < 0)
        {
            damageInstancesPerAttackV2 = damageInstancesPerAttack;
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

    void Update()
    {
        if (isDead || isPlayerDead) return;

        if (IsStaticFrozen())
        {
            return;
        }

        if (!isAttacking && !attackOnCooldown && AdvancedPlayerController.Instance != null)
        {
            float distance = Vector2.Distance(transform.position, AdvancedPlayerController.Instance.transform.position);
            if (distance <= attackRange)
            {
                attackRoutine = StartCoroutine(AttackRoutine());
            }
        }
    }
    
    void LateUpdate()
    {
        // SpriteFlipOffset control (must run even when dead)
        if (spriteFlipOffset != null)
        {
            // Check animation states
            bool isWalking = animator.GetBool("moving") || animator.GetBool("movingflip");
            bool isDying = animator.GetBool("dead") || animator.GetBool("deadflip");

            // Disable SpriteFlipOffset during walking or death
            if (isWalking || isDying)
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
        
        if (isDead) return;

        if (IsStaticFrozen())
        {
            return;
        }

        // CRITICAL: Use different movement booleans based on flip state
        bool ismoving = rb.velocity.sqrMagnitude > 0.0001f && !isAttacking;
        
        if (spriteRenderer.flipX) // Flipped (facing left)
        {
            animator.SetBool("movingflip", ismoving);
            animator.SetBool("moving", false);
        }
        else // Not flipped (facing right)
        {
            animator.SetBool("moving", ismoving);
            animator.SetBool("movingflip", false);
        }
        
        animator.SetBool("idle", !ismoving && !isAttacking && attackOnCooldown);
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
        isAttacking = false;
        attackOnCooldown = false;

        CancelAttackAction();

        animator.SetBool("attack", false);
        animator.SetBool("moving", false);
        animator.SetBool("movingflip", false);
        animator.SetBool("idle", true);
    }

    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (isDead) return;
        
        knockbackVelocity = direction.normalized * force * knockbackIntensity;
        knockbackEndTime = Time.time + knockbackDuration;

        CancelAttackAction();
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        isAttacking = false;
        attackOnCooldown = false;
        animator.SetBool("attack", false);
    }

    void FixedUpdate()
    {
        if (isDead || isPlayerDead)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        if (IsStaticFrozen())
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

        if (isAttacking || AdvancedPlayerController.Instance == null || !AdvancedPlayerController.Instance.enabled)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        Vector3 toPlayer = AdvancedPlayerController.Instance.transform.position - transform.position;
        float distance = toPlayer.magnitude;

        if (distance <= stopDistance)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        spriteRenderer.flipX = toPlayer.x <= 0;

        float speedMult = 1f;
        if (statusController != null)
        {
            speedMult = statusController.GetEnemyMoveSpeedMultiplier();
        }

        rb.velocity = toPlayer.normalized * (moveSpeed * speedMult);
    }

    IEnumerator AttackRoutine()
    {
        int myToken = BeginAttackAction();

        yield return StaticPauseHelper.WaitWhileStatic(
            () => isDead || isPlayerDead || myToken != attackActionToken,
            () => IsStaticFrozen());

        isAttacking = true;
        hasDealtDamageThisAttack = false;
        animator.SetBool("attack", true);
        float originalSpeed = animator.speed;
        animator.speed = attackAnimSpeed;

        if (firstAttackDamageDelayV2 > 0f)
        {
            yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
                firstAttackDamageDelayV2,
                () => isDead || isPlayerDead || myToken != attackActionToken,
                () => IsStaticFrozen());
        }

        if (isDead || isPlayerDead || myToken != attackActionToken)
        {
            animator.SetBool("attack", false);
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        // Check range before dealing damage
        if (AdvancedPlayerController.Instance != null)
        {
            float distance = Vector2.Distance(transform.position, AdvancedPlayerController.Instance.transform.position);
            if (distance > attackRange)
            {
                animator.SetBool("attack", false);
                animator.speed = originalSpeed;
                isAttacking = false;
                yield break;
            }
        }

        // Deal damage instances
        int instances = Mathf.Max(1, damageInstancesPerAttackV2);
        for (int i = 0; i < instances; i++)
        {
            if (isDead || isPlayerDead || myToken != attackActionToken)
            {
                animator.SetBool("attack", false);
                animator.speed = originalSpeed;
                isAttacking = false;
                attackRoutine = null;
                yield break;
            }

            // Check range before EACH damage instance
            if (AdvancedPlayerController.Instance != null)
            {
                float distance = Vector2.Distance(transform.position, AdvancedPlayerController.Instance.transform.position);
                if (distance > attackRange)
                {
                    Debug.Log($"<color=yellow>FireWizard: Out of range during attack (distance: {distance:F2} > {attackRange}), canceling attack</color>");
                    animator.SetBool("attack", false);
                    animator.speed = originalSpeed;
                    isAttacking = false;
                    attackRoutine = null;
                    yield break;
                }
            }
            
            if (playerDamageable != null && playerDamageable.IsAlive && AdvancedPlayerController.Instance != null && AdvancedPlayerController.Instance.enabled)
            {
                yield return StaticPauseHelper.WaitWhileStatic(
                    () => isDead || isPlayerDead || myToken != attackActionToken,
                    () => IsStaticFrozen());

                Vector3 hitPoint = AdvancedPlayerController.Instance.transform.position;
                Vector3 hitNormal = (AdvancedPlayerController.Instance.transform.position - transform.position).normalized;
                PlayerHealth.RegisterPendingAttacker(gameObject);
                playerDamageable.TakeDamage(attackDamageV2, hitPoint, hitNormal);
                Debug.Log($"<color=orange>FireWizard dealt {attackDamageV2} damage (instance {i + 1}/{instances})</color>");
                
                if (i < instances - 1)
                {
                    if (restAttackDamageDelayV2 > 0f)
                    {
                        yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
                            restAttackDamageDelayV2,
                            () => isDead || isPlayerDead || myToken != attackActionToken,
                            () => IsStaticFrozen());
                    }
                }
            }
            else
            {
                animator.SetBool("attack", false);
                animator.speed = originalSpeed;
                isAttacking = false;
                attackRoutine = null;
                yield break;
            }
        }

        float totalDamageTime = firstAttackDamageDelayV2 + (restAttackDamageDelayV2 * (instances - 1));
        float remainingTime = attackDuration - totalDamageTime;
        if (remainingTime > 0)
        {
            yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
                remainingTime,
                () => isDead || isPlayerDead || myToken != attackActionToken,
                () => IsStaticFrozen());
        }

        animator.SetBool("attack", false);
        animator.speed = originalSpeed;
        isAttacking = false;
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
            yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
                cooldown,
                () => isDead || isPlayerDead || myToken != attackActionToken,
                () => IsStaticFrozen());
        }

        attackOnCooldown = false;
        animator.SetBool("idle", false);
        attackRoutine = null;
    }

    private bool IsStaticFrozen()
    {
        return StaticPauseHelper.IsStaticFrozen(this, ref cachedStaticStatus);
    }

    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        if (attackRoutine != null) StopCoroutine(attackRoutine);

        // CRITICAL: Set death animation based on flip state
        bool isFlipped = spriteRenderer.flipX;
        animator.SetBool("dead", !isFlipped);      // Normal death when NOT flipped
        animator.SetBool("deadflip", isFlipped);   // Flipped death when flipped
        
        // Disable all other animation states
        animator.SetBool("moving", false);
        animator.SetBool("movingflip", false);
        animator.SetBool("idle", false);
        animator.SetBool("attack", false);
        
        rb.velocity = Vector2.zero;
        knockbackVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static;
        
        capsuleCollider.enabled = false;

        StartCoroutine(FadeOutAndDestroy());
    }
    
    IEnumerator FadeOutAndDestroy()
    {
        float animationDelay = Mathf.Max(0f, deathCleanupDelay - deathFadeOutDuration);
        if (animationDelay > 0)
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
}
