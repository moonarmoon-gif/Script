using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(StatusController))]
public class GhostWarrior4Enemy : MonoBehaviour
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

    [Header("First Attack Settings")]
    [SerializeField] private float firstAttackDuration = 0.5f;
    [SerializeField] private float firstAttackDamage = 15f;
    [Tooltip("Delay before FIRST attack damage")]
    [SerializeField] private float firstAttackDamageDelay = 0.2f;

    [SerializeField] private float firstAttackDamageV2 = -1f;
    [SerializeField] private float firstAttackDamageDelayV2 = -1f;

    [Header("Second Attack Settings")]
    [SerializeField] private float secondAttackDuration = 0.6f;
    [SerializeField] private float secondAttackDamage = 20f;
    [Tooltip("Delay before SECOND attack damage")]
    [SerializeField] private float secondAttackDamageDelay = 0.25f;

    [SerializeField] private float secondAttackDamageV2 = -1f;
    [SerializeField] private float secondAttackDamageDelayV2 = -1f;

    [Header("Third Attack Settings")]
    [SerializeField] private float thirdAttackDuration = 0.7f;
    [SerializeField] private float thirdAttackDamage = 25f;
    [Tooltip("Delay before THIRD attack damage")]
    [SerializeField] private float thirdAttackDamageDelay = 0.3f;

    [SerializeField] private float thirdAttackDamageV2 = -1f;
    [SerializeField] private float thirdAttackDamageDelayV2 = -1f;

    [Header("Knockback Settings")]
    [Tooltip("Knockback force when hit by projectiles")]
    public float knockbackIntensity = 5f;
    [Tooltip("How long knockback lasts")]
    public float knockbackDuration = 0.2f;

    [Header("Ghost4 Shield Mechanic")]
    [Tooltip("Triggers shield channel when CurrentHealth/MaxHealth <= this threshold. (0.5 = 50%)")]
    public float ShieldHealthThreshold = 0.5f;

    [Tooltip("Duration (seconds) of the sword-in animation.")]
    public float SwordInAnimationDuration = 0.5f;

    [Tooltip("Duration (seconds) of the sword-out animation.")]
    public float SwordOutAnimationDuration = 0.5f;

    [Tooltip("How long (seconds) Ghost4 channels shield while forced idle.")]
    public float ShieldChannelDuration = 5f;

    [Tooltip("Shield gained per interval as a fraction of MaxHealth. (0.05 = 5%)")]
    public float ShieldGainPerMaxHealth = 0.05f;

    [Tooltip("Interval (seconds) between shield gains while channeling.")]
    public float ShieldGainInterval = 0.25f;

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
    private bool preAttackDelayReady = false;
    private bool wasInAttackRange = false;
    private Vector2 knockbackVelocity = Vector2.zero;
    private float knockbackEndTime = 0f;
    private bool wasOffsetDrivenByAnim = false;

    private int attackActionToken = 0;

    // Ghost4 shield channel state
    private bool shieldChannelUsedThisLife = false;
    private bool isChannelingShield = false;
    private Coroutine shieldChannelRoutine;

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

        if (firstAttackDamageV2 < 0f) firstAttackDamageV2 = firstAttackDamage;
        if (firstAttackDamageDelayV2 < 0f) firstAttackDamageDelayV2 = firstAttackDamageDelay;

        if (secondAttackDamageV2 < 0f) secondAttackDamageV2 = secondAttackDamage;
        if (secondAttackDamageDelayV2 < 0f) secondAttackDamageDelayV2 = secondAttackDamageDelay;

        if (thirdAttackDamageV2 < 0f) thirdAttackDamageV2 = thirdAttackDamage;
        if (thirdAttackDamageDelayV2 < 0f) thirdAttackDamageDelayV2 = thirdAttackDamageDelay;
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
        if (spriteFlipOffset == null || isDead) return;

        bool offsetDrivenByAnim =
            animator.GetBool("moving") || animator.GetBool("movingflip") ||
            animator.GetBool("dead") || animator.GetBool("deadflip") ||
            animator.GetBool("attack1") ||
            animator.GetBool("attack2") || animator.GetBool("attack2flip") ||
            animator.GetBool("attack3") || animator.GetBool("attack3flip") ||
            animator.GetBool("swordin") || animator.GetBool("swordout");

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

        TryStartShieldChannel();

        // While any part of the shield sequence is running, stop normal AI.
        if (isChannelingShield)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        bool ismoving = rb.velocity.sqrMagnitude > 0.0001f && !isAttacking;
        bool isFlipped = spriteRenderer.flipX;

        if (ismoving && isFlipped)
        {
            animator.SetBool("movingflip", true);
            animator.SetBool("moving", false);
        }
        else if (ismoving && !isFlipped)
        {
            animator.SetBool("moving", true);
            animator.SetBool("movingflip", false);
        }
        else
        {
            animator.SetBool("moving", false);
            animator.SetBool("movingflip", false);
        }

        bool shouldIdle =
            !ismoving &&
            !isAttacking &&
            (attackOnCooldown ||
             preAttackDelayRoutine != null ||
             preAttackDelayReady ||
             AdvancedPlayerController.Instance == null ||
             (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled));

        // IMPORTANT: During the swordin/swordout animations, idle must be false.
        // Idle should only be true during the actual channel window.
        if (!isChannelingShield)
        {
            animator.SetBool("idle", shouldIdle);
        }

        bool inRange = false;
        if (AdvancedPlayerController.Instance != null && AdvancedPlayerController.Instance.enabled)
        {
            float distance = Vector2.Distance(transform.position, AdvancedPlayerController.Instance.transform.position);
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
                    ClearAllAttackAnims();
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
        }

        if (!isAttacking && !attackOnCooldown && inRange && preAttackDelayReady && Time.time >= knockbackEndTime)
        {
            attackRoutine = StartCoroutine(AttackRoutine());
        }
    }

    private void TryStartShieldChannel()
    {
        if (shieldChannelUsedThisLife) return;
        if (isChannelingShield) return;
        if (health == null || !health.IsAlive) return;

        float maxHp = Mathf.Max(1f, health.MaxHealth);
        float hpFrac = health.CurrentHealth / maxHp;

        if (hpFrac <= Mathf.Clamp01(ShieldHealthThreshold))
        {
            shieldChannelUsedThisLife = true;
            shieldChannelRoutine = StartCoroutine(ShieldChannelRoutine());
        }
    }

    private void CancelAttackAndPreAttack()
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
        CancelAttackAction();

        animator.speed = 1f;

        animator.SetBool("moving", false);
        animator.SetBool("movingflip", false);

        // IMPORTANT: do NOT force idle here anymore; swordin/swordout must not have idle=true.
        animator.SetBool("idle", false);

        ClearAllAttackAnims();
    }

    private void ForceChannelIdleOnly()
    {
        rb.velocity = Vector2.zero;

        // Cancel any attack/pre-attack so we stay fully idle while channeling
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
        CancelAttackAction();

        animator.speed = 1f;

        animator.SetBool("moving", false);
        animator.SetBool("movingflip", false);

        // Channel idle should be the only time idle is forced true:
        animator.SetBool("idle", true);

        ClearAllAttackAnims();
    }

    private IEnumerator ShieldChannelRoutine()
    {
        isChannelingShield = true;

        // Sword-in phase: idle must be false
        CancelAttackAndPreAttack();
        animator.SetBool("swordout", false);
        animator.SetBool("swordin", true);
        if (SwordInAnimationDuration > 0f)
        {
            yield return new WaitForSeconds(SwordInAnimationDuration);
        }
        animator.SetBool("swordin", false);

        // Channel phase: idle must be true
        ForceChannelIdleOnly();

        float duration = Mathf.Max(0f, ShieldChannelDuration);
        float interval = Mathf.Max(0.01f, ShieldGainInterval);
        float perMax = Mathf.Max(0f, ShieldGainPerMaxHealth);

        float maxHp = Mathf.Max(1f, health.MaxHealth);
        float shieldPerTick = maxHp * perMax;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (isDead) yield break;

            if (statusController != null && shieldPerTick > 0f)
            {
                statusController.AddShield(shieldPerTick);
            }

            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        // Sword-out phase: idle must be false
        CancelAttackAndPreAttack();
        animator.SetBool("swordin", false);
        animator.SetBool("swordout", true);

        if (SwordOutAnimationDuration > 0f)
        {
            yield return new WaitForSeconds(SwordOutAnimationDuration);
        }

        animator.SetBool("swordout", false);

        // Return control to normal AI; idle will be set by normal Update() logic next frame.
        isChannelingShield = false;
        shieldChannelRoutine = null;
    }

    private IEnumerator PreAttackDelayRoutine()
    {
        float delay = Mathf.Max(0f, PreAttackDelay);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        preAttackDelayRoutine = null;

        if (isDead || isChannelingShield || AdvancedPlayerController.Instance == null || !AdvancedPlayerController.Instance.enabled)
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
        if (shieldChannelRoutine != null)
        {
            StopCoroutine(shieldChannelRoutine);
            shieldChannelRoutine = null;
        }

        preAttackDelayReady = false;
        wasInAttackRange = false;
        isAttacking = false;
        attackOnCooldown = false;
        isChannelingShield = false;

        animator.SetBool("swordin", false);
        animator.SetBool("swordout", false);

        ClearAllAttackAnims();

        animator.SetBool("moving", false);
        animator.SetBool("movingflip", false);
        animator.SetBool("idle", true);
        CancelAttackAction();
    }

    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (isDead) return;

        knockbackVelocity = direction.normalized * force * knockbackIntensity;
        knockbackEndTime = Time.time + knockbackDuration;

        if (!isChannelingShield)
        {
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

            ClearAllAttackAnims();

            attackOnCooldown = false;
            CancelAttackAction();
        }
    }

    void FixedUpdate()
    {
        if (isDead)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        // While any part of shield sequence is running, do not move.
        if (isChannelingShield)
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
            return;
        }

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

    private void SetAttackAnim(int attackIndex, bool active)
    {
        bool isFlipped = spriteRenderer != null && spriteRenderer.flipX;

        string normal;
        string flipped;
        switch (attackIndex)
        {
            case 1:
                normal = "attack1"; // Ghost4: no attack1flip
                flipped = null;
                break;
            case 2:
                normal = "attack2";
                flipped = "attack2flip";
                break;
            default:
                normal = "attack3";
                flipped = "attack3flip";
                break;
        }

        if (!active)
        {
            animator.SetBool(normal, false);
            if (!string.IsNullOrEmpty(flipped))
            {
                animator.SetBool(flipped, false);
            }
            return;
        }

        if (attackIndex == 1)
        {
            animator.SetBool(normal, true); // always play attack1 regardless of flip
            return;
        }

        animator.SetBool(normal, !isFlipped);
        animator.SetBool(flipped, isFlipped);
    }

    private void ClearAllAttackAnims()
    {
        animator.SetBool("attack1", false);

        animator.SetBool("attack2", false);
        animator.SetBool("attack2flip", false);
        animator.SetBool("attack3", false);
        animator.SetBool("attack3flip", false);
    }

    IEnumerator AttackRoutine()
    {
        int myToken = BeginAttackAction();
        isAttacking = true;

        float originalSpeed = animator.speed;
        animator.speed = attackAnimSpeed;

        SetAttackAnim(1, true);

        if (firstAttackDamageDelayV2 > 0f)
        {
            yield return new WaitForSeconds(firstAttackDamageDelayV2);
        }

        if (isDead || isChannelingShield || myToken != attackActionToken)
        {
            SetAttackAnim(1, false);
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
            playerDamageable.TakeDamage(firstAttackDamageV2, hitPoint, hitNormal);
        }
        else
        {
            SetAttackAnim(1, false);
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        float elapsedAttackTime = Mathf.Max(0f, firstAttackDamageDelayV2);
        float remainingTime = firstAttackDuration - elapsedAttackTime;
        if (remainingTime > 0)
        {
            yield return new WaitForSeconds(remainingTime);
        }

        if (isDead || isChannelingShield || myToken != attackActionToken)
        {
            ClearAllAttackAnims();
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        SetAttackAnim(1, false);

        SetAttackAnim(2, true);

        if (secondAttackDamageDelayV2 > 0f)
        {
            yield return new WaitForSeconds(secondAttackDamageDelayV2);
        }

        if (isDead || isChannelingShield || myToken != attackActionToken)
        {
            SetAttackAnim(2, false);
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
            playerDamageable.TakeDamage(secondAttackDamageV2, hitPoint, hitNormal);
        }
        else
        {
            SetAttackAnim(2, false);
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        remainingTime = secondAttackDuration - secondAttackDamageDelayV2;
        if (remainingTime > 0)
        {
            yield return new WaitForSeconds(remainingTime);
        }

        if (isDead || isChannelingShield || myToken != attackActionToken)
        {
            ClearAllAttackAnims();
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        SetAttackAnim(2, false);

        SetAttackAnim(3, true);

        if (thirdAttackDamageDelayV2 > 0f)
        {
            yield return new WaitForSeconds(thirdAttackDamageDelayV2);
        }

        if (isDead || isChannelingShield || myToken != attackActionToken)
        {
            SetAttackAnim(3, false);
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
            playerDamageable.TakeDamage(thirdAttackDamageV2, hitPoint, hitNormal);
        }
        else
        {
            SetAttackAnim(3, false);
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        remainingTime = thirdAttackDuration - thirdAttackDamageDelayV2;
        if (remainingTime > 0)
        {
            yield return new WaitForSeconds(remainingTime);
        }

        ClearAllAttackAnims();

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
        if (preAttackDelayRoutine != null) StopCoroutine(preAttackDelayRoutine);
        if (shieldChannelRoutine != null) StopCoroutine(shieldChannelRoutine);

        animator.SetBool("swordin", false);
        animator.SetBool("swordout", false);

        bool isFlipped = spriteRenderer.flipX;
        animator.SetBool("dead", !isFlipped);
        animator.SetBool("deadflip", isFlipped);

        animator.SetBool("moving", false);
        animator.SetBool("movingflip", false);
        animator.SetBool("idle", false);
        ClearAllAttackAnims();

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