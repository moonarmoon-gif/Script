using UnityEngine;

public class GhostWarrior2Enemy : MonoBehaviour
{
    public float speed = 2f;
    public float runSpeedBonus = 1.5f;
    public float runSpeedIncreaseRate = 0.5f;
    public float runSpeedBonusMax = 3f;

    public float detectionRange = 10f;
    public float attackRange = 1.5f;
    public float preAttackRange = 2f;

    public float knockbackForce = 5f;

    public float attackDuration = 1f; // maximum fallback duration

    public float minInitialWalkDuration = 0.5f;
    public float maxInitialWalkDuration = 1.5f;

    private Transform player;
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private float currentMoveSpeed;
    private float speedIncreaseTimer;

    private bool isAttacking;
    private bool isKnockedBack;

    private float initialWalkTimer;
    private float initialWalkDuration;
    private bool isInitialWalking;
    private bool initialWalkTimerStarted;

    private Coroutine attackCoroutine;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        currentMoveSpeed = speed;
        speedIncreaseTimer = 0f;

        initialWalkDuration = Random.Range(minInitialWalkDuration, maxInitialWalkDuration);
        initialWalkTimer = 0f;
        isInitialWalking = true;
        initialWalkTimerStarted = false;
    }

    void Update()
    {
        if (player == null)
        {
            // Player died or missing: stop movement and reset running bonus
            ResetRunningSpeedBonus();
            rb.velocity = Vector2.zero;
            animator.SetBool("isWalking", false);
            animator.SetBool("isRunning", false);
            return;
        }

        if (isKnockedBack)
        {
            // Running interrupted by knockback
            ResetRunningSpeedBonus();
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);

        // Only start counting initial walk timer when visible in camera
        if (isInitialWalking)
        {
            if (!initialWalkTimerStarted)
            {
                if (spriteRenderer != null && spriteRenderer.isVisible)
                {
                    initialWalkTimerStarted = true;
                }
            }

            if (initialWalkTimerStarted)
            {
                initialWalkTimer += Time.deltaTime;
                if (initialWalkTimer >= initialWalkDuration)
                {
                    isInitialWalking = false;
                }
            }

            MoveTowardsPlayer(distance, isRunning: false);
            return;
        }

        if (distance <= attackRange && !isAttacking)
        {
            // Entering attack interrupts running
            ResetRunningSpeedBonus();
            StartAttack();
        }
        else if (distance <= preAttackRange && !isAttacking)
        {
            // Stop to prepare attack interrupts running
            ResetRunningSpeedBonus();
            rb.velocity = Vector2.zero;
            animator.SetBool("isWalking", false);
            animator.SetBool("isRunning", false);
        }
        else if (distance <= detectionRange && !isAttacking)
        {
            MoveTowardsPlayer(distance, isRunning: true);
        }
        else
        {
            // Not chasing: reset running bonus
            ResetRunningSpeedBonus();
            rb.velocity = Vector2.zero;
            animator.SetBool("isWalking", false);
            animator.SetBool("isRunning", false);
        }
    }

    private void MoveTowardsPlayer(float distance, bool isRunning)
    {
        Vector2 direction = (player.position - transform.position).normalized;

        // Flip sprite
        if (direction.x < 0)
        {
            transform.localScale = new Vector3(-1, 1, 1);
        }
        else if (direction.x > 0)
        {
            transform.localScale = new Vector3(1, 1, 1);
        }

        if (isRunning)
        {
            // Increase running speed bonus over time
            speedIncreaseTimer += Time.deltaTime * runSpeedIncreaseRate;
            float bonus = Mathf.Min(runSpeedBonus + speedIncreaseTimer, runSpeedBonusMax);
            currentMoveSpeed = speed * bonus;

            animator.SetBool("isRunning", true);
            animator.SetBool("isWalking", false);
        }
        else
        {
            // Walking: reset running bonus
            ResetRunningSpeedBonus();
            currentMoveSpeed = speed;

            animator.SetBool("isRunning", false);
            animator.SetBool("isWalking", true);
        }

        rb.velocity = direction * currentMoveSpeed;
    }

    private void StartAttack()
    {
        isAttacking = true;
        rb.velocity = Vector2.zero;
        animator.SetTrigger("attack");

        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);

        attackCoroutine = StartCoroutine(AttackRoutine());
    }

    private System.Collections.IEnumerator AttackRoutine()
    {
        // Wait until animator actually enters the "attack" state (or timeout)
        float enterTimeout = 0f;
        while (enterTimeout < 0.25f)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("attack"))
                break;
            enterTimeout += Time.deltaTime;
            yield return null;
        }

        // Now wait for completion based on normalizedTime if we're in attack state.
        // Use attackDuration as a max fallback (also covers cases where attack state isn't found).
        float elapsed = 0f;
        while (elapsed < attackDuration)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("attack"))
            {
                // normalizedTime >= 1 means the clip finished one loop
                if (state.normalizedTime >= 1f)
                    break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        isAttacking = false;
        attackCoroutine = null;

        // After attack, ensure running bonus is reset so a new run buildup starts fresh
        ResetRunningSpeedBonus();
    }

    public void ApplyKnockback(Vector2 direction)
    {
        isKnockedBack = true;
        ResetRunningSpeedBonus();
        rb.velocity = Vector2.zero;
        rb.AddForce(direction.normalized * knockbackForce, ForceMode2D.Impulse);
        animator.SetTrigger("knockback");
        Invoke(nameof(EndKnockback), 0.5f);
    }

    private void EndKnockback()
    {
        isKnockedBack = false;
    }

    private void ResetRunningSpeedBonus()
    {
        currentMoveSpeed = speed;
        speedIncreaseTimer = 0f;
    }
}
