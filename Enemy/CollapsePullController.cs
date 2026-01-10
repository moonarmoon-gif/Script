using UnityEngine;

/// <summary>
/// Helper attached to enemies that are affected by Collapse pull.
/// While an enemy is fully pulled to the Collapse core, this component
/// forces a walk/move animation (no idle flicker) without touching the
/// enemy's own AI or movement logic.
/// </summary>
public class CollapsePullController : MonoBehaviour
{
    [Tooltip("Optional explicit Animator reference. If null, the first Animator on this GameObject or its children is used.")]
    [SerializeField] private Animator animator;

    private SpriteRenderer spriteRenderer;

    [Tooltip("How long (in seconds) after the last pull update we keep forcing walk before releasing control.")]
    [SerializeField] private float releaseDelay = 0.15f;

    private EnemyHealth enemyHealth;

    private int idleHash;
    private int movingHash;
    private int movingFlipHash;
    private int deadHash;

    private int isAttackingHash;
    private int attackHash;
    private int attackFlipHash;
    private int attackFarHash;

    private bool hasIdle;
    private bool hasMoving;
    private bool hasMovingFlip;
    private bool hasDead;

    private bool hasIsAttacking;
    private bool hasAttack;
    private bool hasAttackFlip;
    private bool hasAttackFar;

    private bool isPulled;
    private float lastPulledTime;
    private Vector2 lastPullDirection;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        enemyHealth = GetComponent<EnemyHealth>();

        if (animator != null)
        {
            idleHash = Animator.StringToHash("idle");
            movingHash = Animator.StringToHash("moving");
            movingFlipHash = Animator.StringToHash("movingflip");
            deadHash = Animator.StringToHash("dead");

            isAttackingHash = Animator.StringToHash("IsAttacking");
            attackHash = Animator.StringToHash("attack");
            attackFlipHash = Animator.StringToHash("attackflip");
            attackFarHash = Animator.StringToHash("attackfar");

            var parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.type != AnimatorControllerParameterType.Bool) continue;

                if (p.nameHash == idleHash) hasIdle = true;
                else if (p.nameHash == movingHash) hasMoving = true;
                else if (p.nameHash == movingFlipHash) hasMovingFlip = true;
                else if (p.nameHash == deadHash) hasDead = true;
                else if (p.nameHash == isAttackingHash) hasIsAttacking = true;
                else if (p.nameHash == attackHash) hasAttack = true;
                else if (p.nameHash == attackFlipHash) hasAttackFlip = true;
                else if (p.nameHash == attackFarHash) hasAttackFar = true;
            }
        }
    }

    public bool IsPulled
    {
        get { return isPulled; }
    }

    /// <summary>
    /// Called by Collapse while this enemy is within its pull radius.
    /// When <paramref name="pulled"/> is true and the enemy is close
    /// enough to the core, we will force walk animation.
    /// </summary>
    public void SetPulled(bool pulled, Vector2 pullDirection)
    {
        if (pulled)
        {
            lastPulledTime = Time.time;
            lastPullDirection = pullDirection;
        }

        isPulled = pulled;
    }

    private void LateUpdate()
    {
        if (animator == null)
        {
            return;
        }

        // Automatically release control a short time after the last pull
        // update so enemies recover when Collapse disappears.
        if (isPulled && (Time.time - lastPulledTime) > releaseDelay)
        {
            isPulled = false;
        }

        if (!isPulled)
        {
            return;
        }

        // Do not override animations for dead enemies.
        if (enemyHealth != null && !enemyHealth.IsAlive)
        {
            return;
        }

        if (hasDead && animator.GetBool(deadHash))
        {
            return;
        }

        if ((hasIsAttacking && animator.GetBool(isAttackingHash))
            || (hasAttack && animator.GetBool(attackHash))
            || (hasAttackFlip && animator.GetBool(attackFlipHash))
            || (hasAttackFar && animator.GetBool(attackFarHash)))
        {
            return;
        }

        // Force a walk/move animation and disable idle while pulled.
        if (hasIdle)
        {
            animator.SetBool(idleHash, false);
        }

        bool hasFlipInfo = spriteRenderer != null;
        bool isFlipped = hasFlipInfo && spriteRenderer.flipX;
        bool goingRight = lastPullDirection.x >= 0f;

        if (hasMoving && hasMovingFlip)
        {
            if (hasFlipInfo)
            {
                animator.SetBool(movingHash, !isFlipped);
                animator.SetBool(movingFlipHash, isFlipped);
            }
            else
            {
                animator.SetBool(movingHash, goingRight);
                animator.SetBool(movingFlipHash, !goingRight);
            }
        }
        else if (hasMoving)
        {
            animator.SetBool(movingHash, true);
        }
        else if (hasMovingFlip)
        {
            animator.SetBool(movingFlipHash, true);
        }
    }
}
