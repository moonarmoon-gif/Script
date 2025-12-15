using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(EnemyHealth))]
public class FlyingDemonEnemy : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private float stopDistance = 1.5f;
    [SerializeField] private float attackAnimSpeed = 1.35f;
    [SerializeField] private float attackCooldown = 1.0f;
    [SerializeField] private float attackDuration = 0.4f;
    [SerializeField] private float deathCleanupDelay = 0.7f;

    [Header("Summon Settings")]
    [Tooltip("Duration of summon animation (enemy is immobile and invulnerable)")]
    [SerializeField] private float summonAnimationDuration = 1f;

    [Header("Sprite Settings")]
    [Tooltip("Invert sprite flip direction (if sprite is backwards)")]
    [SerializeField] private bool invertFlip = false;

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

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;
    [SerializeField] private CapsuleCollider2D capsuleCollider;

    private EnemyHealth health;
    private StatusController statusController;
    private IDamageable playerDamageable;
    private bool isDead;
    private bool isAttacking;
    private bool attackOnCooldown;
    private bool isSummoning = true; // Start in summon state
    private bool hasDealtDamageThisAttack = false;
    private Coroutine attackRoutine;
    private Vector2 knockbackVelocity = Vector2.zero;
    private float knockbackEndTime = 0f;

    private int attackActionToken = 0;

    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<Animator>();
        if (capsuleCollider == null) capsuleCollider = GetComponent<CapsuleCollider2D>();
        health = GetComponent<EnemyHealth>();
        statusController = GetComponent<StatusController>();

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

    void Start()
    {
        // Start summon animation
        StartCoroutine(SummonRoutine());
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

    IEnumerator SummonRoutine()
    {
        isSummoning = true;
        animator.SetBool("summon", true);
        
        // Invulnerable during summon
        health.enabled = false;
        
        Debug.Log($"<color=orange>FlyingDemon summoning for {summonAnimationDuration}s (invulnerable)</color>");
        
        yield return new WaitForSeconds(summonAnimationDuration);
        
        animator.SetBool("summon", false);
        isSummoning = false;
        
        // Enable damage after summon
        health.enabled = true;
        
        Debug.Log("<color=orange>FlyingDemon summon complete, now active</color>");
    }

    void Update()
    {
        if (isDead || isSummoning) return;

        bool isMoving = rb.velocity.sqrMagnitude > 0.0001f && !isAttacking;
        animator.SetBool("moving", isMoving);
        
        animator.SetBool("idle", !isMoving && !isAttacking && attackOnCooldown);

        if (!isAttacking && !attackOnCooldown && AdvancedPlayerController.Instance != null)
        {
            float distance = Vector2.Distance(transform.position, AdvancedPlayerController.Instance.transform.position);
            if (distance <= attackRange)
            {
                attackRoutine = StartCoroutine(AttackRoutine());
            }
        }
    }

    void OnPlayerDeath()
    {
        rb.velocity = Vector2.zero;
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        isAttacking = false;
        attackOnCooldown = false;
        animator.SetBool("attack", false);
        animator.SetBool("moving", false);
        animator.SetBool("idle", true);

        CancelAttackAction();
    }

    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (isDead || isSummoning) return;
        
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
        if (isDead || isSummoning)
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

        bool shouldFlipLeft = toPlayer.x <= 0;
        spriteRenderer.flipX = invertFlip ? !shouldFlipLeft : shouldFlipLeft;

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
        isAttacking = true;
        hasDealtDamageThisAttack = false;
        animator.SetBool("attack", true);
        float originalSpeed = animator.speed;
        animator.speed = attackAnimSpeed;

        if (attackDamageDelayV2 > 0f)
        {
            yield return new WaitForSeconds(attackDamageDelayV2);
        }

        if (isDead || isSummoning || myToken != attackActionToken)
        {
            animator.SetBool("attack", false);
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        if (!hasDealtDamageThisAttack && playerDamageable != null && playerDamageable.IsAlive && 
            AdvancedPlayerController.Instance != null && AdvancedPlayerController.Instance.enabled)
        {
            Vector3 hitPoint = AdvancedPlayerController.Instance.transform.position;
            Vector3 hitNormal = (AdvancedPlayerController.Instance.transform.position - transform.position).normalized;
            PlayerHealth.RegisterPendingAttacker(gameObject);
            playerDamageable.TakeDamage(attackDamageV2, hitPoint, hitNormal);
            hasDealtDamageThisAttack = true;
            Debug.Log($"<color=orange>FlyingDemon dealt {attackDamageV2} damage</color>");
        }
        else
        {
            animator.SetBool("attack", false);
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        float remainingAttackTime = attackDuration - attackDamageDelayV2;
        if (remainingAttackTime > 0f)
        {
            yield return new WaitForSeconds(remainingAttackTime);
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
            yield return new WaitForSeconds(cooldown);
        }
        
        attackOnCooldown = false;
        animator.SetBool("idle", false);
        attackRoutine = null;
    }

    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        CancelAttackAction();

        if (attackRoutine != null) StopCoroutine(attackRoutine);

        animator.SetBool("dead", true);
        animator.SetBool("moving", false);
        animator.SetBool("idle", false);
        animator.SetBool("attack", false);
        animator.SetBool("summon", false);
        
        rb.velocity = Vector2.zero;
        knockbackVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static;
        
        capsuleCollider.enabled = false;

        Destroy(gameObject, deathCleanupDelay);
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

    public void SetBaseDamageFromBoss(float bossAttackDamage, float percent)
    {
        if (bossAttackDamage <= 0f || percent <= 0f) return;
        float newDamage = bossAttackDamage * percent;
        attackDamage = newDamage;
        attackDamageV2 = newDamage;
        Debug.Log($"<color=green>FlyingDemonEnemy base attackDamage set from boss: {bossAttackDamage:F1} * {percent:P0} = {newDamage:F1}</color>");
    }

    public void MultiplyAttackDamage(float multiplier)
    {
        if (multiplier <= 0f) return;
        attackDamage *= multiplier;
        attackDamageV2 *= multiplier;
        Debug.Log($"<color=green>FlyingDemonEnemy attackDamage scaled by x{multiplier:F2} (new: {attackDamageV2:F1})</color>");
    }
}
