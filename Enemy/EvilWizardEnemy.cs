using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(EnemyHealth))]
public class EvilWizardEnemy : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float stopDistance = 1.4f;
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

		if (IsStaticFrozen())
		{
			return;
		}

        // Check if moving/movingflip animation is playing
        bool isMoving = animator.GetBool("moving") || animator.GetBool("movingflip");

        // Only update if state changed
        if (isMoving != wasMoving)
        {
            if (isMoving)
            {
                // Disable collider offset control when walking (animation controls it)
                spriteFlipOffset.SetColliderOffsetEnabled(false);
                Debug.Log("<color=yellow>EvilWizard: Walking started - Disabled collider offset control</color>");
            }
            else
            {
                // Enable collider offset control when not walking (script controls it)
                spriteFlipOffset.SetColliderOffsetEnabled(true);
                Debug.Log("<color=yellow>EvilWizard: Walking stopped - Enabled collider offset control</color>");
            }

            wasMoving = isMoving;
        }

        if (isDead) return;

        bool ismoving = rb.velocity.sqrMagnitude > 0.0001f && !isAttacking;
        bool isFlipped = spriteRenderer.flipX;
        
        // Set moving or movingflip based on flip state
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
        
        animator.SetBool("idle", !ismoving && !isAttacking && attackOnCooldown);

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
        animator.SetBool("attack2", false);
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
        
        // Stop current attack
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        isAttacking = false;
        animator.SetBool("attack", false);
        animator.SetBool("attack2", false);
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
        isAttacking = true;
        hasDealtDamageThisAttack = false;
        
        float originalSpeed = animator.speed;
        animator.speed = attackAnimSpeed;

        // FIRST ATTACK
        Debug.Log("<color=magenta>EvilWizard: First Attack</color>");
        animator.SetBool("attack", true);
        
        // Wait for first attack damage delay
        if (firstAttackDamageDelayV2 > 0f)
        {
			yield return WaitForSecondsPauseSafeAndStatic(firstAttackDamageDelayV2, myToken);
        }

        if (isDead || myToken != attackActionToken)
        {
            animator.SetBool("attack", false);
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        // Deal first attack damage
        if (myToken == attackActionToken && playerDamageable != null && playerDamageable.IsAlive && 
            AdvancedPlayerController.Instance != null && AdvancedPlayerController.Instance.enabled)
        {
			yield return WaitWhileStatic(myToken);
            Vector3 hitPoint = AdvancedPlayerController.Instance.transform.position;
            Vector3 hitNormal = (AdvancedPlayerController.Instance.transform.position - transform.position).normalized;
            PlayerHealth.RegisterPendingAttacker(gameObject);
            playerDamageable.TakeDamage(firstAttackDamageV2, hitPoint, hitNormal);
            Debug.Log($"<color=magenta>EvilWizard dealt {firstAttackDamageV2} damage (First Attack)</color>");
        }
        else
        {
            // Player died, stop
            animator.SetBool("attack", false);
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        // Wait for rest of first attack duration
        float remainingTime = firstAttackDuration - firstAttackDamageDelayV2;
        if (remainingTime > 0)
        {
			yield return WaitForSecondsPauseSafeAndStatic(remainingTime, myToken);
        }

        if (isDead || myToken != attackActionToken)
        {
            animator.SetBool("attack", false);
            animator.SetBool("attack2", false);
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        animator.SetBool("attack", false);
        
        // IMMEDIATELY transition to SECOND ATTACK (no idle, no delay)
        Debug.Log("<color=magenta>EvilWizard: Second Attack (IMMEDIATE transition)</color>");
        animator.SetBool("attack2", true);
        
        // Wait for second attack damage delay
        if (secondAttackDamageDelayV2 > 0f)
        {
			yield return WaitForSecondsPauseSafeAndStatic(secondAttackDamageDelayV2, myToken);
        }

        if (isDead || myToken != attackActionToken)
        {
            animator.SetBool("attack2", false);
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        // Deal second attack damage
        if (myToken == attackActionToken && playerDamageable != null && playerDamageable.IsAlive && 
            AdvancedPlayerController.Instance != null && AdvancedPlayerController.Instance.enabled)
        {
			yield return WaitWhileStatic(myToken);
            Vector3 hitPoint = AdvancedPlayerController.Instance.transform.position;
            Vector3 hitNormal = (AdvancedPlayerController.Instance.transform.position - transform.position).normalized;
            PlayerHealth.RegisterPendingAttacker(gameObject);
            playerDamageable.TakeDamage(secondAttackDamageV2, hitPoint, hitNormal);
            Debug.Log($"<color=magenta>EvilWizard dealt {secondAttackDamageV2} damage (Second Attack)</color>");
        }
        else
        {
            // Player died, stop
            animator.SetBool("attack2", false);
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        // Wait for rest of second attack duration
        remainingTime = secondAttackDuration - secondAttackDamageDelayV2;
        if (remainingTime > 0)
        {
			yield return WaitForSecondsPauseSafeAndStatic(remainingTime, myToken);
        }

        animator.SetBool("attack2", false);

        // BOTH ATTACKS COMPLETE - Now apply cooldown
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
            // Transition to idle during cooldown
            attackOnCooldown = true;
            animator.SetBool("idle", true);

			yield return WaitForSecondsPauseSafeAndStatic(cooldown, myToken);
            
            attackOnCooldown = false;
            animator.SetBool("idle", false);
            attackRoutine = null;
        }
    }

    private bool IsStaticFrozen()
    {
        if (cachedStaticStatus == null)
        {
            cachedStaticStatus = GetComponent<StaticStatus>();
        }

        return cachedStaticStatus != null && cachedStaticStatus.IsInStaticPeriod;
    }

    private IEnumerator WaitForSecondsPauseSafeAndStatic(float seconds, int myToken)
    {
        if (seconds <= 0f)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (isDead || myToken != attackActionToken)
            {
                yield break;
            }

            if (IsStaticFrozen())
            {
                yield return null;
                continue;
            }

            float dt = GameStateManager.GetPauseSafeDeltaTime();
            if (dt > 0f)
            {
                elapsed += dt;
            }
            yield return null;
        }

        yield return WaitWhileStatic(myToken);
    }

    private IEnumerator WaitWhileStatic(int myToken)
    {
        while (IsStaticFrozen())
        {
            if (isDead || myToken != attackActionToken)
            {
                yield break;
            }
            yield return null;
        }
    }

    void LateUpdate()
    {
        if (spriteFlipOffset == null) return;

        bool isWalking = animator.GetBool("moving") || animator.GetBool("movingflip");
        bool isDying = animator.GetBool("dead") || animator.GetBool("deadflip");

        if (isWalking || isDying)
        {
            spriteFlipOffset.SetColliderOffsetEnabled(false);
            spriteFlipOffset.SetShadowOffsetEnabled(false);
        }
        else
        {
            spriteFlipOffset.SetColliderOffsetEnabled(true);
            spriteFlipOffset.SetShadowOffsetEnabled(true);
        }
    }

    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        CancelAttackAction();

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
        animator.SetBool("attack2", false);
        
        Debug.Log($"<color=magenta>EvilWizard: Death animation - flipped={isFlipped}, dead={!isFlipped}, deadflip={isFlipped}</color>");
        
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
