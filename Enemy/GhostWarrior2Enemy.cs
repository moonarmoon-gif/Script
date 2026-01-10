using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(EnemyHealth))]
public class GhostWarrior2Enemy : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float stopDistance = 1.4f;
    public float PreAttackDelay = 1f;
    [SerializeField] private float attackAnimSpeed = 1.0f;
    [SerializeField] private float attackCooldown = 1.0f;
    [SerializeField] private float deathCleanupDelay = 0.7f;
    [Tooltip("Duration of fade out effect on death (seconds)")]
    [SerializeField] private float deathFadeOutDuration = 0.5f;

    [Header("Attack Settings")]
    [SerializeField] private float attackDuration = 0.5f;
    [SerializeField] private float attackDamage = 15f;
    [Tooltip("Delay before attack damage")]
    [SerializeField] private float attackDamageDelay = 0.2f;

    [SerializeField] private float attackDamageV2 = -1f;
    [SerializeField] private float attackDamageDelayV2 = -1f;

    [Header("Run-Up Settings")]
    [Tooltip("Minimum time spent walking before transitioning to running.")]
    public float MinInitialWalkDuration = 5f;
    [Tooltip("Maximum time spent walking before transitioning to running.")]
    public float MaxInitialWalkDuration = 10f;
    [Tooltip("Interval between speed increases during the running phase.")]
    public float SpeedIncreaseInterval = 0.2f;
    [Tooltip("Amount added to moveSpeed every interval during the running phase.")]
    public float SpeedIncrementEveryInterval = 0.1f;

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
    private Coroutine attackRoutine;
    private Coroutine preAttackDelayRoutine;
    private bool preAttackDelayReady;
    private bool wasInAttackRange;
    private Vector2 knockbackVelocity = Vector2.zero;
    private float knockbackEndTime = 0f;
    private bool wasOffsetDrivenByAnim = false;

    private int attackActionToken = 0;

    // Run-up state
    private float initialWalkDuration;
    private float initialWalkElapsed;
    private float currentMoveSpeed;
    private float speedIncreaseTimer;
    private bool isRunningPhase;

    private void ResetRunSpeedBonus()
    {
        currentMoveSpeed = moveSpeed;
        speedIncreaseTimer = 0f;
    }

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

        if (attackDamageV2 < 0f) attackDamageV2 = attackDamage;
        if (attackDamageDelayV2 < 0f) attackDamageDelayV2 = attackDamageDelay;

        if (MaxInitialWalkDuration < MinInitialWalkDuration)
        {
            float tmp = MinInitialWalkDuration;
            MinInitialWalkDuration = MaxInitialWalkDuration;
            MaxInitialWalkDuration = tmp;
        }

        initialWalkDuration = Random.Range(MinInitialWalkDuration, MaxInitialWalkDuration);
        if (initialWalkDuration < 0f) initialWalkDuration = 0f;

        initialWalkElapsed = 0f;
        currentMoveSpeed = moveSpeed;
        speedIncreaseTimer = 0f;
        isRunningPhase = false;
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
        if (health == null) health = GetComponent<EnemyHealth>();
        if (health != null) health.OnDeath += HandleDeath;
    }

    void OnDisable()
    {
        if (health != null) health.OnDeath -= HandleDeath;

        if (AdvancedPlayerController.Instance != null)
        {
            var playerHealth = AdvancedPlayerController.Instance.GetComponent<PlayerHealth>();
            if (playerHealth != null)
                playerHealth.OnDeath -= OnPlayerDeath;
        }
    }

    void Update()
    {
        if (spriteFlipOffset == null || isDead) return;

        bool offsetDrivenByAnim =
            animator.GetBool("moving") || animator.GetBool("movingflip") ||
            animator.GetBool("running") || animator.GetBool("runningflip") ||
            animator.GetBool("dead") || animator.GetBool("deadflip") ||
            animator.GetBool("attack"); // removed attackflip

        if (offsetDrivenByAnim != wasOffsetDrivenByAnim)
        {
            if (offsetDrivenByAnim)
            {
                spriteFlipOffset.SetColliderOffsetEnabled(false);
                spriteFlipOffset.SetShadowOffsetEnabled(false);
            }
            else
            {
                spriteFlipOffset.SetColliderOffsetEnabled(true);
                spriteFlipOffset.SetShadowOffsetEnabled(true);
            }

            wasOffsetDrivenByAnim = offsetDrivenByAnim;
        }

        if (isDead) return;

        float dt = Time.deltaTime;
        if (dt < 0f) dt = 0f;

        if (!isRunningPhase)
        {
            initialWalkElapsed += dt;
            if (initialWalkElapsed >= initialWalkDuration)
            {
                isRunningPhase = true;
            }
        }

        bool ismoving = rb.velocity.sqrMagnitude > 0.0001f && !isAttacking;
        bool isFlipped = spriteRenderer.flipX;

        bool shouldBeRunningAnim = ismoving && isRunningPhase;

        if (!shouldBeRunningAnim || isAttacking || Time.time < knockbackEndTime || preAttackDelayRoutine != null || preAttackDelayReady)
        {
            ResetRunSpeedBonus();
        }

        if (ismoving)
        {
            if (isRunningPhase)
            {
                animator.SetBool("running", !isFlipped);
                animator.SetBool("runningflip", isFlipped);
                animator.SetBool("moving", false);
                animator.SetBool("movingflip", false);
            }
            else
            {
                animator.SetBool("moving", !isFlipped);
                animator.SetBool("movingflip", isFlipped);
                animator.SetBool("running", false);
                animator.SetBool("runningflip", false);
            }
        }
        else
        {
            animator.SetBool("moving", false);
            animator.SetBool("movingflip", false);
            animator.SetBool("running", false);
            animator.SetBool("runningflip", false);
        }

        bool shouldIdle =
            !ismoving &&
            !isAttacking &&
            (attackOnCooldown ||
             preAttackDelayRoutine != null ||
             preAttackDelayReady ||
             AdvancedPlayerController.Instance == null ||
             (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled));
        animator.SetBool("idle", shouldIdle);

        bool inRange = false;
        float distance = 999999f;

        if (AdvancedPlayerController.Instance != null && AdvancedPlayerController.Instance.enabled)
        {
            distance = Vector2.Distance(transform.position, AdvancedPlayerController.Instance.transform.position);
            inRange = distance <= attackRange;

            if (!inRange)
            {
                wasInAttackRange = false;
                preAttackDelayReady = false;
                if (preAttackDelayRoutine != null)
                {
                    StopCoroutine(preAttackDelayRoutine);
                    preAttackDelayRoutine = null;
                }

                if (isAttacking)
                {
                    CancelAttackAction();
                    if (attackRoutine != null)
                    {
                        StopCoroutine(attackRoutine);
                        attackRoutine = null;
                    }

                    isAttacking = false;
                    attackOnCooldown = false;
                    animator.speed = 1f;
                    animator.SetBool("attack", false);
                }
            }
            else if (!wasInAttackRange)
            {
                wasInAttackRange = true;
                preAttackDelayReady = PreAttackDelay <= 0f;

                if (!preAttackDelayReady)
                {
                    if (preAttackDelayRoutine != null)
                    {
                        StopCoroutine(preAttackDelayRoutine);
                        preAttackDelayRoutine = null;
                    }
                    preAttackDelayRoutine = StartCoroutine(PreAttackDelayRoutine());
                }
            }

            bool canBuildRunningBonus =
                isRunningPhase &&
                !isAttacking &&
                Time.time >= knockbackEndTime &&
                distance > attackRange &&
                preAttackDelayRoutine == null &&
                !preAttackDelayReady &&
                (animator.GetBool("running") || animator.GetBool("runningflip"));

            if (canBuildRunningBonus && SpeedIncreaseInterval > 0f)
            {
                speedIncreaseTimer += dt;
                while (speedIncreaseTimer >= SpeedIncreaseInterval)
                {
                    currentMoveSpeed += SpeedIncrementEveryInterval;
                    speedIncreaseTimer -= SpeedIncreaseInterval;
                }
            }
        }
        else
        {
            wasInAttackRange = false;
            preAttackDelayReady = false;
            if (preAttackDelayRoutine != null)
            {
                StopCoroutine(preAttackDelayRoutine);
                preAttackDelayRoutine = null;
            }

            ResetRunSpeedBonus();
        }

        if (!isAttacking && !attackOnCooldown && inRange && preAttackDelayReady && Time.time >= knockbackEndTime)
        {
            attackRoutine = StartCoroutine(AttackRoutine());
        }
    }

    private IEnumerator PreAttackDelayRoutine()
    {
        float delay = Mathf.Max(0f, PreAttackDelay);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        preAttackDelayRoutine = null;

        if (isDead || AdvancedPlayerController.Instance == null || !AdvancedPlayerController.Instance.enabled)
        {
            yield break;
        }

        if (Time.time < knockbackEndTime)
        {
            yield break;
        }

        float distance = Vector2.Distance(transform.position, AdvancedPlayerController.Instance.transform.position);
        if (distance > attackRange)
        {
            wasInAttackRange = false;
            preAttackDelayReady = false;
            yield break;
        }

        preAttackDelayReady = true;
    }

    void OnPlayerDeath()
    {
        rb.velocity = Vector2.zero;
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        if (preAttackDelayRoutine != null)
        {
            StopCoroutine(preAttackDelayRoutine);
            preAttackDelayRoutine = null;
        }
        preAttackDelayReady = false;
        wasInAttackRange = false;
        isAttacking = false;
        attackOnCooldown = false;

        animator.SetBool("attack", false);

        animator.SetBool("moving", false);
        animator.SetBool("movingflip", false);
        animator.SetBool("running", false);
        animator.SetBool("runningflip", false);
        animator.SetBool("idle", true);

        ResetRunSpeedBonus();
        CancelAttackAction();
    }

    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (isDead) return;

        knockbackVelocity = direction.normalized * force * knockbackIntensity;
        knockbackEndTime = Time.time + knockbackDuration;

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        if (preAttackDelayRoutine != null)
        {
            StopCoroutine(preAttackDelayRoutine);
            preAttackDelayRoutine = null;
            preAttackDelayReady = false;
            wasInAttackRange = false;
        }
        isAttacking = false;

        animator.SetBool("attack", false);
        attackOnCooldown = false;

        ResetRunSpeedBonus();
        CancelAttackAction();
    }

    void FixedUpdate()
    {
        if (isDead)
        {
            rb.velocity = Vector2.zero;
            return;
        }

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

        if ((preAttackDelayRoutine != null || preAttackDelayReady) && distance <= attackRange)
        {
            rb.velocity = Vector2.zero;
            ResetRunSpeedBonus();
            return;
        }

        if (distance <= stopDistance)
        {
            rb.velocity = Vector2.zero;
            ResetRunSpeedBonus();
            return;
        }

        spriteRenderer.flipX = toPlayer.x <= 0;

        float speedMult = 1f;
        if (statusController != null)
        {
            speedMult = statusController.GetEnemyMoveSpeedMultiplier();
        }
        rb.velocity = toPlayer.normalized * (currentMoveSpeed * speedMult);
    }

    IEnumerator AttackRoutine()
    {
        int myToken = BeginAttackAction();
        isAttacking = true;

        ResetRunSpeedBonus();

        float originalSpeed = animator.speed;
        animator.speed = attackAnimSpeed;

        // Force idle off so it doesn't win over attack in the Animator Controller.
        animator.SetBool("idle", false);

        // IMPORTANT: Always use "attack" (there is no attackflip in this controller).
        animator.SetBool("attack", false);

        animator.SetBool("moving", false);
        animator.SetBool("movingflip", false);
        animator.SetBool("running", false);
        animator.SetBool("runningflip", false);

        animator.SetBool("attack", true);

        if (attackDamageDelayV2 > 0f)
        {
            yield return new WaitForSeconds(attackDamageDelayV2);
        }

        if (isDead || myToken != attackActionToken)
        {
            animator.SetBool("attack", false);
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        if (myToken == attackActionToken && playerDamageable != null && playerDamageable.IsAlive &&
            AdvancedPlayerController.Instance != null && AdvancedPlayerController.Instance.enabled)
        {
            Vector3 hitPoint = AdvancedPlayerController.Instance.transform.position;
            Vector3 hitNormal = (AdvancedPlayerController.Instance.transform.position - transform.position).normalized;
            PlayerHealth.RegisterPendingAttacker(gameObject);

            DamageAoeScope.BeginAoeDamage();
            playerDamageable.TakeDamage(attackDamageV2, hitPoint, hitNormal);
            DamageAoeScope.EndAoeDamage();
        }
        else
        {
            animator.SetBool("attack", false);
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        float elapsedAttackTime = Mathf.Max(0f, attackDamageDelayV2);
        float remainingTime = attackDuration - elapsedAttackTime;
        if (remainingTime > 0f)
        {
            yield return new WaitForSeconds(remainingTime);
        }

        if (isDead || myToken != attackActionToken)
        {
            animator.SetBool("attack", false);
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        animator.SetBool("attack", false);

        animator.speed = originalSpeed;
        isAttacking = false;

        float cooldown = attackCooldown;
        if (statusController != null)
        {
            cooldown += statusController.GetLethargyAttackCooldownBonus();
        }
        if (cooldown < 0f)
        {
            cooldown = 0f;
        }

        if (cooldown <= 0f)
        {
            attackOnCooldown = false;
            attackRoutine = null;
        }
        else
        {
            attackOnCooldown = true;
            animator.SetBool("idle", true);

            yield return new WaitForSeconds(cooldown);

            attackOnCooldown = false;
            animator.SetBool("idle", false);
            attackRoutine = null;
        }
    }

    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        CancelAttackAction();

        if (attackRoutine != null) StopCoroutine(attackRoutine);

        bool isFlipped = spriteRenderer.flipX;
        animator.SetBool("dead", !isFlipped);
        animator.SetBool("deadflip", isFlipped);

        animator.SetBool("moving", false);
        animator.SetBool("movingflip", false);
        animator.SetBool("running", false);
        animator.SetBool("runningflip", false);
        animator.SetBool("idle", false);
        animator.SetBool("attack", false);

        rb.velocity = Vector2.zero;
        knockbackVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static;

        if (capsuleCollider != null)
        {
            capsuleCollider.enabled = false;
        }

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