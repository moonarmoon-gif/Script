using System.Collections;
using UnityEngine;

/// <summary>
/// GhostWarrior4Enemy
///
/// Copied from GhostWarrior3Enemy logic and modified:
/// - attack1 uses only bool attack1 (no attack1flip)
/// - adds swordin/swordout animation booleans
/// - adds a one-time shield channel when HP drops below 50% which grants shield equal to
///   5% of max health every 0.25s for 5s (total 20 ticks), while playing swordin -> idle -> swordout.
///   Uses SwordInAnimationDuration and SwordOutAnimationDuration for the in/out segments.
/// </summary>
public class GhostWarrior4Enemy : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;

    // NOTE: These fields/methods are expected to exist in the project as they did in GhostWarrior3Enemy.
    // If your original script uses different names/types, adjust accordingly.
    [SerializeField] private EnemyStats stats; // provides current/max HP and shield operations

    [Header("Animation Params")]
    [SerializeField] private string Attack1Bool = "attack1";
    [SerializeField] private string SwordInBool = "swordin";
    [SerializeField] private string SwordOutBool = "swordout";

    [Header("Shield Channel")]
    [Tooltip("Seconds spent in sword-in animation before channeling shield.")]
    [SerializeField] private float SwordInAnimationDuration = 0.35f;
    [Tooltip("Seconds spent in sword-out animation after channeling shield.")]
    [SerializeField] private float SwordOutAnimationDuration = 0.35f;

    [Tooltip("Health % threshold to trigger the one-time shield channel.")]
    [Range(0f, 1f)]
    [SerializeField] private float ShieldChannelThreshold = 0.5f;

    [Tooltip("Shield granted per tick as a % of max health.")]
    [Range(0f, 1f)]
    [SerializeField] private float ShieldTickPercentOfMaxHp = 0.05f;

    [Tooltip("Tick interval in seconds.")]
    [SerializeField] private float ShieldTickInterval = 0.25f;

    [Tooltip("Total channel duration in seconds.")]
    [SerializeField] private float ShieldChannelDuration = 5f;

    private bool _shieldChannelUsed;
    private Coroutine _shieldRoutine;

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        stats = GetComponent<EnemyStats>();
    }

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (stats == null) stats = GetComponent<EnemyStats>();
    }

    private void Update()
    {
        // One-time shield channel when dropping below 50% HP.
        if (!_shieldChannelUsed && stats != null)
        {
            float maxHp = Mathf.Max(1f, stats.MaxHealth);
            float hpPct = stats.CurrentHealth / maxHp;

            if (hpPct <= ShieldChannelThreshold)
            {
                _shieldChannelUsed = true;
                _shieldRoutine = StartCoroutine(ShieldChannelRoutine());
            }
        }

        // The rest of GhostWarrior3Enemy logic should remain as-is.
        // Since this file is created standalone, hook your existing movement/AI/attack update here.
    }

    /// <summary>
    /// Modified attack1: only uses the bool "attack1" (no "attack1flip").
    /// Call this from your AI/attack trigger where GhostWarrior3Enemy used attack1/attack1flip.
    /// </summary>
    public void TriggerAttack1()
    {
        if (animator == null) return;
        animator.SetBool(Attack1Bool, true);
    }

    /// <summary>
    /// Should be called by animation event at the end of attack1 to reset.
    /// </summary>
    public void EndAttack1()
    {
        if (animator == null) return;
        animator.SetBool(Attack1Bool, false);
    }

    private IEnumerator ShieldChannelRoutine()
    {
        if (animator != null)
        {
            animator.SetBool(SwordInBool, true);
        }

        // Sword in animation window
        if (SwordInAnimationDuration > 0f)
            yield return new WaitForSeconds(SwordInAnimationDuration);

        if (animator != null)
        {
            animator.SetBool(SwordInBool, false);
            // return to idle implicitly; ensure your animator transitions do so when swordin is false.
        }

        // Channel shield: grant shield = 5% max hp every 0.25s for 5s (20 ticks).
        float elapsed = 0f;
        float maxHp = stats != null ? Mathf.Max(1f, stats.MaxHealth) : 1f;
        float shieldPerTick = maxHp * ShieldTickPercentOfMaxHp;

        while (elapsed < ShieldChannelDuration)
        {
            if (stats != null)
            {
                stats.AddShield(shieldPerTick);
            }

            yield return new WaitForSeconds(ShieldTickInterval);
            elapsed += ShieldTickInterval;
        }

        if (animator != null)
        {
            animator.SetBool(SwordOutBool, true);
        }

        if (SwordOutAnimationDuration > 0f)
            yield return new WaitForSeconds(SwordOutAnimationDuration);

        if (animator != null)
        {
            animator.SetBool(SwordOutBool, false);
        }

        _shieldRoutine = null;
    }
}

/// <summary>
/// Minimal contract expected from the project's existing enemy stat component.
/// If your project already has an EnemyStats class, remove this stub.
/// </summary>
public class EnemyStats : MonoBehaviour
{
    public float CurrentHealth { get; private set; } = 100f;
    public float MaxHealth { get; private set; } = 100f;

    public void AddShield(float amount)
    {
        // Implement in your project.
    }
}
