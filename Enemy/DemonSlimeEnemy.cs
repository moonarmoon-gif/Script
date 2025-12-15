using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(EnemyHealth))]
public class DemonSlimeEnemy : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 1.5f;
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private float stopDistance = 1.5f; // Stops moving when this close
    [SerializeField] private float attackAnimSpeed = 1.35f;
    [SerializeField] private float attackCooldown = 1.0f;
    [SerializeField] private float attackDuration = 0.4f;
    [SerializeField] private float deathCleanupDelay = 0.7f;

    [Header("Special Walking Mechanics")]
    [Tooltip("Duration of walking movement (seconds)")]
    [SerializeField] private float walkDuration = 1.0f;
    [Tooltip("Duration of pause between walks (seconds)")]
    [SerializeField] private float walkPauseDuration = 0.5f;
    
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
    private bool hasDealtDamageThisAttack = false;
    private Coroutine attackRoutine;
    private Vector2 knockbackVelocity = Vector2.zero;
    private float knockbackEndTime = 0f;
    
    // Walking mechanics
    private float walkTimer = 0f;
    private bool isWalkingPhase = false; // Start with PAUSE first (false = paused, true = walking)

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
        if (isDead) return;

        // Update animator - moving state includes both walking and pausing (special walk cycle)
        bool isMoving = rb.velocity.sqrMagnitude > 0.0001f && !isAttacking;
        animator.SetBool("moving", isMoving);
        
        // Idle state only during attack cooldown
        animator.SetBool("idle", !isMoving && !isAttacking && attackOnCooldown);

        // Check for attack trigger
        if (!isAttacking && !attackOnCooldown && AdvancedPlayerController.Instance != null)
        {
            float distance = Vector2.Distance(transform.position, AdvancedPlayerController.Instance.transform.position);
            if (distance <= attackRange)
            {
                Debug.Log($"<color=purple>DemonSlime starting attack! Distance: {distance:F2}, AttackRange: {attackRange}</color>");
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

        if (isAttacking || AdvancedPlayerController.Instance == null || !AdvancedPlayerController.Instance.enabled)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        Vector3 toPlayer = AdvancedPlayerController.Instance.transform.position - transform.position;
        float distance = toPlayer.magnitude;

        // Stop moving when close enough (within stop distance)
        if (distance <= stopDistance)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        // Face player - with optional flip inversion
        bool shouldFlipLeft = toPlayer.x <= 0;
        spriteRenderer.flipX = invertFlip ? !shouldFlipLeft : shouldFlipLeft;

        float speedMult = 1f;
        if (statusController != null)
        {
            speedMult = statusController.GetEnemyMoveSpeedMultiplier();
        }
        
        // Special walking mechanics: walk for duration, pause for duration, repeat
        walkTimer += Time.fixedDeltaTime;
        
        if (isWalkingPhase)
        {
            // Walking phase - move toward player
            if (walkTimer >= walkDuration)
            {
                // Switch to pause phase
                isWalkingPhase = false;
                walkTimer = 0f;
                rb.velocity = Vector2.zero; // Stop during pause
                Debug.Log("<color=purple>DemonSlime: Entering PAUSE phase</color>");
            }
            else
            {
                // Continue walking (still counts as "moving" animation)
                rb.velocity = toPlayer.normalized * (moveSpeed * speedMult);
            }
        }
        else
        {
            // Pause phase - stay still but KEEP moving animation
            if (walkTimer >= walkPauseDuration)
            {
                // Switch back to walking phase
                isWalkingPhase = true;
                walkTimer = 0f;
                Debug.Log("<color=purple>DemonSlime: Entering WALK phase</color>");
            }
            else
            {
                // Stay still during pause (but animation stays "moving")
                rb.velocity = Vector2.zero;
            }
        }
    }

    IEnumerator AttackRoutine()
    {
        int myToken = BeginAttackAction();
        isAttacking = true;
        hasDealtDamageThisAttack = false;
        animator.SetBool("attack", true);
        animator.SetBool("idle", false); // Not idle during attack
        float originalSpeed = animator.speed;
        animator.speed = attackAnimSpeed;

        // Wait for damage delay in animation
        if (attackDamageDelayV2 > 0f)
        {
            yield return new WaitForSeconds(attackDamageDelayV2);
        }

        if (isDead || myToken != attackActionToken)
        {
            animator.SetBool("attack", false);
            animator.SetBool("idle", true);
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
                attackRoutine = null;
                yield break;
            }
        }

        // Deal damage ONCE per attack cycle
        if (!hasDealtDamageThisAttack && playerDamageable != null && playerDamageable.IsAlive && 
            AdvancedPlayerController.Instance != null && AdvancedPlayerController.Instance.enabled)
        {
            Vector3 hitPoint = transform.position;
            Vector3 hitNormal = (AdvancedPlayerController.Instance.transform.position - transform.position).normalized;
            playerDamageable.TakeDamage(attackDamageV2, hitPoint, hitNormal);
            hasDealtDamageThisAttack = true;
            Debug.Log($"<color=purple>DemonSlime dealt {attackDamageV2} damage at {attackDamageDelayV2}s into attack</color>");
        }
        else
        {
            // Player died during attack, stop immediately
            animator.SetBool("attack", false);
            animator.SetBool("idle", true);
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        // Wait for rest of attack duration
        float remainingAttackTime = attackDuration - attackDamageDelayV2;
        if (remainingAttackTime > 0f)
        {
            yield return new WaitForSeconds(remainingAttackTime);
        }

        animator.SetBool("attack", false);
        animator.speed = originalSpeed;
        isAttacking = false;
        attackOnCooldown = true;
        
        // Enter idle state during cooldown
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
        
        rb.velocity = Vector2.zero;
        knockbackVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static;
        
        capsuleCollider.enabled = false;

        Destroy(gameObject, deathCleanupDelay);
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
