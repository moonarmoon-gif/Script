using UnityEngine;

public class FreezeStatus : MonoBehaviour
{
    private StatusController statusController;
    private StaticStatus cachedStaticStatus;

    private Rigidbody2D enemyRb;
    private Vector2 storedVelocity;
    private float storedAngularVelocity;
    private RigidbodyConstraints2D storedConstraints;
    private bool movementFrozen;

    private Animator animator;
    private float originalAnimatorSpeed = 1f;
    private bool animatorFrozen;

    private int[] storedAnimatorStateHashes;
    private float[] storedAnimatorNormalizedTimes;
    private int storedAnimatorLayerCount;
    private bool hasStoredAnimatorState;

    private EnemyHealth enemyHealth;

    public bool IsInFreezePeriod => movementFrozen || animatorFrozen;

    private void OnEnable()
    {
        statusController = GetComponent<StatusController>() ?? GetComponentInParent<StatusController>();
        enemyRb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();

        enemyHealth = GetComponent<EnemyHealth>() ?? GetComponentInParent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleOwnerDeath;
            enemyHealth.OnDeath += HandleOwnerDeath;
        }

        TryBeginFreezeIfPossible();
    }

    private void Update()
    {
        if (statusController == null)
        {
            statusController = GetComponent<StatusController>() ?? GetComponentInParent<StatusController>();
        }

        if (statusController == null || !statusController.HasStatus(StatusId.Freeze))
        {
            ForceEndFreeze();
            Destroy(this);
            return;
        }

        if (!IsInFreezePeriod)
        {
            TryBeginFreezeIfPossible();
        }
    }

    public void ForceEndFreeze()
    {
        if (movementFrozen)
        {
            RestoreMovement();
        }

        if (animatorFrozen)
        {
            RestoreAnimator();
        }

        movementFrozen = false;
        animatorFrozen = false;
        hasStoredAnimatorState = false;
    }

    private bool IsStaticCurrentlyFreezing()
    {
        if (cachedStaticStatus == null)
        {
            cachedStaticStatus = GetComponent<StaticStatus>();
        }

        return cachedStaticStatus != null && cachedStaticStatus.IsInStaticPeriod;
    }

    private void TryBeginFreezeIfPossible()
    {
        if (IsStaticCurrentlyFreezing())
        {
            return;
        }

        StoreAndFreezeMovement();
        StoreAndFreezeAnimator();
    }

    private void StoreAndFreezeMovement()
    {
        if (movementFrozen)
        {
            return;
        }

        if (enemyRb == null)
        {
            return;
        }

        storedVelocity = enemyRb.velocity;
        storedAngularVelocity = enemyRb.angularVelocity;
        storedConstraints = enemyRb.constraints;

        enemyRb.velocity = Vector2.zero;
        enemyRb.angularVelocity = 0f;
        enemyRb.constraints = RigidbodyConstraints2D.FreezeAll;

        movementFrozen = true;
    }

    private void RestoreMovement()
    {
        if (enemyRb == null)
        {
            return;
        }

        enemyRb.constraints = storedConstraints;
        enemyRb.velocity = storedVelocity;
        enemyRb.angularVelocity = storedAngularVelocity;
    }

    private void StoreAndFreezeAnimator()
    {
        if (animatorFrozen)
        {
            return;
        }

        if (animator == null)
        {
            return;
        }

        if (IsAnimatorInDeathState())
        {
            return;
        }

        originalAnimatorSpeed = animator.speed;

        int layerCount = animator.layerCount;
        if (storedAnimatorStateHashes == null || storedAnimatorStateHashes.Length < layerCount)
        {
            storedAnimatorStateHashes = new int[layerCount];
            storedAnimatorNormalizedTimes = new float[layerCount];
        }
        storedAnimatorLayerCount = layerCount;

        for (int i = 0; i < layerCount; i++)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(i);
            storedAnimatorStateHashes[i] = info.fullPathHash;
            storedAnimatorNormalizedTimes[i] = info.normalizedTime;
        }

        hasStoredAnimatorState = true;
        animator.speed = 0f;
        animatorFrozen = true;
    }

    private void RestoreAnimator()
    {
        if (animator == null)
        {
            return;
        }

        animator.speed = originalAnimatorSpeed;

        bool isDying = IsAnimatorInDeathState();
        if (hasStoredAnimatorState && !isDying)
        {
            int count = Mathf.Min(storedAnimatorLayerCount, animator.layerCount);
            for (int i = 0; i < count; i++)
            {
                animator.Play(storedAnimatorStateHashes[i], i, storedAnimatorNormalizedTimes[i]);
            }
            animator.Update(0f);
        }
    }

    private bool IsAnimatorInDeathState()
    {
        if (animator == null)
        {
            return false;
        }

        if (enemyHealth != null && !enemyHealth.IsAlive)
        {
            return true;
        }

        bool isDying = false;
        try { isDying |= animator.GetBool("dead"); } catch { }
        try { isDying |= animator.GetBool("deadflip"); } catch { }
        try { isDying |= animator.GetBool("IsDead"); } catch { }

        return isDying;
    }

    private void HandleOwnerDeath()
    {
        hasStoredAnimatorState = false;
        ForceEndFreeze();
    }

    private void OnDestroy()
    {
        ForceEndFreeze();

        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleOwnerDeath;
        }
    }
}
