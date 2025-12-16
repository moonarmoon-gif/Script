using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class ClawProjectile : MonoBehaviour, IInstantModifiable
{
    [Header("Damage Settings")]
    [SerializeField] private float damage = 20f;
    [SerializeField] private float manaCost = 10f;
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private ProjectileType projectileType = ProjectileType.Fire;

    [Header("Timing")]
    [Tooltip("Delay before the claw actually deals damage to its target.")]
    public float DamageDelay = 0.5f;
    [SerializeField] private float lifetimeSeconds = 1.0f;

    [Header("Collider Scaling")]
    [SerializeField] private float colliderSizeOffset = 0f;

    [Header("Damage Cooldown")]
    [Tooltip("Minimum time between damage instances (safety for future multi-hit variants).")]
    [SerializeField] private float damageCooldown = 0.1f;

    private Rigidbody2D _rigidbody2D;
    private Collider2D _collider2D;

    private float baseDamage;
    private float baseLifetime;
    private Vector3 baseScale;

    private PlayerStats cachedPlayerStats;
    private float baseDamageAfterCards;
    private float lastDamageTime = -999f;

    private static System.Collections.Generic.Dictionary<string, float> lastFireTimes = new System.Collections.Generic.Dictionary<string, float>();
    private string prefabKey;

    public ProjectileType ProjectileElement
    {
        get { return projectileType; }
    }

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _collider2D = GetComponent<Collider2D>();

        baseDamage = damage;
        baseLifetime = lifetimeSeconds;
        baseScale = transform.localScale;

        if (_rigidbody2D != null)
        {
            _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rigidbody2D.gravityScale = 0f;
            _rigidbody2D.velocity = Vector2.zero;
        }

        if (_collider2D != null)
        {
            _collider2D.isTrigger = true;
        }
    }

    public void Launch(Vector2 direction, Transform target, Collider2D colliderToIgnore, PlayerMana playerMana = null)
    {
        if (_rigidbody2D == null || _collider2D == null)
        {
            Destroy(gameObject);
            return;
        }

        // Resolve card + modifiers for this projectile instance.
        ProjectileCards card = null;
        CardModifierStats modifiers = new CardModifierStats();
        if (ProjectileCardModifiers.Instance != null)
        {
            card = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
            if (card != null)
            {
                modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
            }
        }

        float finalLifetime = baseLifetime + modifiers.lifetimeIncrease;
        float finalCooldown = Mathf.Max(0.1f, cooldown * (1f - modifiers.cooldownReductionPercent / 100f));
        int finalManaCost = Mathf.Max(1, Mathf.CeilToInt(manaCost * (1f - modifiers.manaCostReduction)));
        float finalDamage = baseDamage + modifiers.damageFlat;

        // Cache PlayerStats so we can run the shared projectile damage pipeline per hit.
        cachedPlayerStats = null;
        if (colliderToIgnore != null)
        {
            cachedPlayerStats = colliderToIgnore.GetComponent<PlayerStats>();
        }
        if (cachedPlayerStats == null)
        {
            cachedPlayerStats = FindObjectOfType<PlayerStats>();
        }

        baseDamageAfterCards = finalDamage;
        damage = finalDamage;

        // ACTIVE projectile cards rely on AdvancedPlayerController for attack-speed
        // cooldowns; only non-active contexts use an internal prefab cooldown gate.
        bool useInternalCooldown = (card == null || card.projectileSystem != ProjectileCards.ProjectileSystemType.Active);

        prefabKey = "ClawProjectile";

        if (useInternalCooldown)
        {
            if (lastFireTimes.ContainsKey(prefabKey))
            {
                if (Time.time - lastFireTimes[prefabKey] < finalCooldown)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            lastFireTimes[prefabKey] = Time.time;
        }

        // Mana check always applies.
        if (playerMana != null && !playerMana.Spend(finalManaCost))
        {
            Destroy(gameObject);
            return;
        }

        if (_collider2D != null && colliderToIgnore != null)
        {
            Physics2D.IgnoreCollision(_collider2D, colliderToIgnore, true);
        }

        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>() ?? target.GetComponentInParent<EnemyHealth>();
        if (enemyHealth == null || !enemyHealth.IsAlive)
        {
            Destroy(gameObject);
            return;
        }

        Collider2D enemyCollider = target.GetComponent<Collider2D>() ?? target.GetComponentInChildren<Collider2D>();
        if (enemyCollider == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 enemyCenter = enemyCollider.bounds.center;

        // Offscreen safety: respect global offscreen damage rules before committing.
        if (!OffscreenDamageChecker.CanTakeDamage(enemyCenter))
        {
            Destroy(gameObject);
            return;
        }

        // Reposition the claw directly onto the enemy's collider center and parent
        // it so any animation stays anchored.
        transform.position = enemyCenter;
        transform.SetParent(target, true);

        // Apply size modifier + collider scaling from card.
        if (modifiers.sizeMultiplier != 1f)
        {
            transform.localScale = baseScale * modifiers.sizeMultiplier;
            ColliderScaler.ScaleCollider(_collider2D, modifiers.sizeMultiplier, colliderSizeOffset);
        }

        // Destroy the visual after its lifetime; damage is handled by coroutine.
        Destroy(gameObject, finalLifetime);

        StartCoroutine(DelayedStrike(enemyHealth, enemyCollider));
    }

    private IEnumerator DelayedStrike(EnemyHealth enemyHealth, Collider2D enemyCollider)
    {
        float delay = Mathf.Max(0f, DamageDelay);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (enemyHealth == null || !enemyHealth.IsAlive)
        {
            yield break;
        }

        // Damage cooldown safety (mainly for future multi-hit variants).
        if (Time.time - lastDamageTime < damageCooldown)
        {
            yield break;
        }

        IDamageable damageable = enemyHealth as IDamageable;
        if (damageable == null || !damageable.IsAlive)
        {
            yield break;
        }

        Vector3 hitPoint = enemyCollider != null ? (Vector3)enemyCollider.bounds.center : enemyHealth.transform.position;
        if (!OffscreenDamageChecker.CanTakeDamage(hitPoint))
        {
            yield break;
        }

        float baseForHit = baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
        float finalDamage = baseForHit;

        if (cachedPlayerStats != null)
        {
            finalDamage = PlayerDamageHelper.ComputeProjectileDamage(cachedPlayerStats, enemyHealth.gameObject, baseForHit, gameObject);
        }

        if (finalDamage <= 0f)
        {
            yield break;
        }

        // Tag EnemyHealth so EnemyHealth.TakeDamage renders this hit using the
        // fire damage color (including when armor/defense reduce it to 0).
        enemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Fire);

        Vector3 hitNormal = Vector3.zero;
        damageable.TakeDamage(finalDamage, hitPoint, hitNormal);
        lastDamageTime = Time.time;

        // Apply any attached status effects (Burn/Slow/Static) so ClawProjectile
        // participates in the same status pipeline as other projectiles.
        BurnEffect burnEffect = GetComponent<BurnEffect>();
        if (burnEffect != null)
        {
            burnEffect.Initialize(finalDamage, projectileType);
            burnEffect.TryApplyBurn(enemyHealth.gameObject, hitPoint);
        }

        SlowEffect slowEffect = GetComponent<SlowEffect>();
        if (slowEffect != null)
        {
            slowEffect.TryApplySlow(enemyHealth.gameObject, hitPoint);
        }

        StaticEffect staticEffect = GetComponent<StaticEffect>();
        if (staticEffect != null)
        {
            staticEffect.TryApplyStatic(enemyHealth.gameObject, hitPoint);
        }
    }

    public void ApplyInstantModifiers(CardModifierStats mods)
    {
        Debug.Log("<color=lime>╔ CLAW PROJECTILE ╗</color>");

        float newLifetime = baseLifetime + mods.lifetimeIncrease;
        if (!Mathf.Approximately(newLifetime, lifetimeSeconds))
        {
            lifetimeSeconds = newLifetime;
            Debug.Log($"<color=lime>Lifetime: {baseLifetime:F2} + {mods.lifetimeIncrease:F2} = {lifetimeSeconds:F2}</color>");
        }

        float newDamage = baseDamage * mods.damageMultiplier;
        if (!Mathf.Approximately(newDamage, damage))
        {
            damage = newDamage;
            Debug.Log($"<color=lime>Damage: {baseDamage:F2} * {mods.damageMultiplier:F2}x = {damage:F2}</color>");
        }

        if (!Mathf.Approximately(mods.sizeMultiplier, 1f))
        {
            transform.localScale = baseScale * mods.sizeMultiplier;
            Debug.Log($"<color=lime>Size: {baseScale} * {mods.sizeMultiplier:F2}x = {transform.localScale}</color>");
        }

        Debug.Log("<color=lime>╚═══════════════════════════════════════╝</color>");
    }

    public float GetCurrentDamage()
    {
        return damage;
    }
}
