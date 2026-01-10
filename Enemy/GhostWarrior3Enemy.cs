using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(EnemyHealth))]
public class GhostWarrior3Enemy : MonoBehaviour
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

    [SerializeField] private float AfterFirstAttackDamage = -1f;
    [SerializeField] private float AfterFirstAttackDamageDelay = -1f;

    [SerializeField] private float firstAttackDamageV2 = -1f;
    [SerializeField] private float firstAttackDamageDelayV2 = -1f;

    [SerializeField] private float AfterFirstAttackDamageV2 = -1f;
    [SerializeField] private float AfterFirstAttackDamageDelayV2 = -1f;

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
    private Coroutine preAttackDelayRoutine;
    private bool preAttackDelayReady = false;
    private bool wasInAttackRange = false;
    private Vector2 knockbackVelocity = Vector2.zero;
    private float knockbackEndTime = 0f;
    private bool wasOffsetDrivenByAnim = false;

    private int attackActionToken = 0;

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

        if (firstAttackDamageV2 < 0f)
        {
            firstAttackDamageV2 = firstAttackDamage;
        }
        if (firstAttackDamageDelayV2 < 0f)
        {
            firstAttackDamageDelayV2 = firstAttackDamageDelay;
        }

        if (AfterFirstAttackDamageV2 < 0f)
        {
            AfterFirstAttackDamageV2 = AfterFirstAttackDamage;
        }
        if (AfterFirstAttackDamageDelayV2 < 0f)
        {
            AfterFirstAttackDamageDelayV2 = AfterFirstAttackDamageDelay;
        }
        if (secondAttackDamageV2 < 0f)
        {
            secondAttackDamageV2 = secondAttackDamage;
        }
        if (secondAttackDamageDelayV2 < 0f)
        {
            secondAttackDamageDelayV2 = secondAttackDamageDelay;
        }
        if (thirdAttackDamageV2 < 0f)
        {
            thirdAttackDamageV2 = thirdAttackDamage;
        }
        if (thirdAttackDamageDelayV2 < 0f)
        {
            thirdAttackDamageDelayV2 = thirdAttackDamageDelay;
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
        if (spriteFlipOffset == null || isDead) return;

        bool offsetDrivenByAnim =
            animator.GetBool("moving") || animator.GetBool("movingflip") ||
            animator.GetBool("dead") || animator.GetBool("deadflip") ||
            animator.GetBool("attack1") || animator.GetBool("attack1flip") ||
            animator.GetBool("attack2") || animator.GetBool("attack2flip") ||
            animator.GetBool("attack3") || animator.GetBool("attack3flip");

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
        animator.SetBool("idle", shouldIdle);

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

                // If the player left attack range mid-combo, cancel the current attack sequence
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

        animator.SetBool("attack1", false);
        animator.SetBool("attack1flip", false);
        animator.SetBool("attack2", false);
        animator.SetBool("attack2flip", false);
        animator.SetBool("attack3", false);
        animator.SetBool("attack3flip", false);

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

        animator.SetBool("attack1", false);
        animator.SetBool("attack1flip", false);
        animator.SetBool("attack2", false);
        animator.SetBool("attack2flip", false);
        animator.SetBool("attack3", false);
        animator.SetBool("attack3flip", false);

        attackOnCooldown = false;
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

        // Once in attack range and preparing to attack, stop moving so pre-attack uses idle instead of moving
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
                normal = "attack1";
                flipped = "attack1flip";
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
            animator.SetBool(flipped, false);
            return;
        }

        animator.SetBool(normal, !isFlipped);
        animator.SetBool(flipped, isFlipped);
    }

    private void ClearAllAttackAnims()
    {
        animator.SetBool("attack1", false);
        animator.SetBool("attack1flip", false);
        animator.SetBool("attack2", false);
        animator.SetBool("attack2flip", false);
        animator.SetBool("attack3", false);
        animator.SetBool("attack3flip", false);
    }

    IEnumerator AttackRoutine()
    {
        int myToken = BeginAttackAction();
        isAttacking = true;
        hasDealtDamageThisAttack = false;

        float originalSpeed = animator.speed;
        animator.speed = attackAnimSpeed;

        // FIRST ATTACK
        SetAttackAnim(1, true);

        if (firstAttackDamageDelayV2 > 0f)
        {
            yield return new WaitForSeconds(firstAttackDamageDelayV2);
        }

        if (isDead || myToken != attackActionToken)
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

        bool doAfterFirstDamage = AfterFirstAttackDamageV2 > 0f;
        float afterDelay = Mathf.Max(0f, AfterFirstAttackDamageDelayV2);
        if (doAfterFirstDamage)
        {
            float remainingWindow = firstAttackDuration - elapsedAttackTime;
            if (remainingWindow < 0f)
            {
                remainingWindow = 0f;
            }
            if (afterDelay > remainingWindow)
            {
                afterDelay = remainingWindow;
            }

            if (afterDelay > 0f)
            {
                yield return new WaitForSeconds(afterDelay);
            }

            elapsedAttackTime += afterDelay;

            if (isDead || myToken != attackActionToken)
            {
                ClearAllAttackAnims();
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
                playerDamageable.TakeDamage(AfterFirstAttackDamageV2, hitPoint, hitNormal);
            }
        }

        float remainingTime = firstAttackDuration - elapsedAttackTime;
        if (remainingTime > 0)
        {
            yield return new WaitForSeconds(remainingTime);
        }

        if (isDead || myToken != attackActionToken)
        {
            ClearAllAttackAnims();
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        SetAttackAnim(1, false);

        // SECOND ATTACK
        SetAttackAnim(2, true);

        if (secondAttackDamageDelayV2 > 0f)
        {
            yield return new WaitForSeconds(secondAttackDamageDelayV2);
        }

        if (isDead || myToken != attackActionToken)
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

        if (isDead || myToken != attackActionToken)
        {
            ClearAllAttackAnims();
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        SetAttackAnim(2, false);

        // THIRD ATTACK (AOE)
        SetAttackAnim(3, true);

        if (thirdAttackDamageDelayV2 > 0f)
        {
            yield return new WaitForSeconds(thirdAttackDamageDelayV2);
        }

        if (isDead || myToken != attackActionToken)
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

            // NEW: GhostWarrior3 third attack is classified as AOE-type damage.
            DamageAoeScope.BeginAoeDamage();
            playerDamageable.TakeDamage(thirdAttackDamageV2, hitPoint, hitNormal);
            DamageAoeScope.EndAoeDamage();
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