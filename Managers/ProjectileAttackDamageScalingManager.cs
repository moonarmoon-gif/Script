using UnityEngine;

/// <summary>
/// Central manager for how much the player's Attack stat contributes to each
/// projectile's base damage. This runs inside PlayerDamageHelper so all
/// projectiles share the same Attack-scaling semantics.
/// </summary>
public class ProjectileAttackDamageScalingManager : MonoBehaviour
{
    public static ProjectileAttackDamageScalingManager Instance { get; private set; }

    [System.Serializable]
    private struct ProjectileAttackScaling
    {
        public ProjectileCards.ProjectileType projectileType;
        [Tooltip("Bonus damage granted PER point of Attack for this projectile type. 1 = +1 damage per Attack, 0.5 = +0.5, 0 = no Attack contribution.")]
        public float attackBonusPerPoint;
    }

    [Header("Default Attack Scaling")]
    [Tooltip("Bonus damage granted PER point of Attack for projectile types that do not have an explicit override.")]
    [SerializeField] private float defaultAttackBonusPerPoint = 1f;

    [Header("Per-Projectile Overrides")]
    [SerializeField] private ProjectileAttackScaling[] perProjectileOverrides = new ProjectileAttackScaling[0];

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public static float GetEffectiveAttack(PlayerStats stats)
    {
        if (stats == null)
        {
            return 0f;
        }

        float baseAttack = Mathf.Max(0f, stats.baseAttack);
        if (baseAttack <= 0f)
        {
            return 0f;
        }

        return Mathf.Max(0f, baseAttack);
    }

    /// <summary>
    /// Computes flat bonus damage contributed by the player's Attack stat for a
    /// specific projectile/card. This bonus is added to the projectile's
    /// baseDamage BEFORE PlayerStats.CalculateDamage is applied.
    /// </summary>
    public static float GetAttackBonus(PlayerStats stats, ProjectileCards card, GameObject projectile)
    {
        float attackStat = GetEffectiveAttack(stats);
        if (attackStat <= 0f)
        {
            return 0f;
        }

        float perPoint = 1f;

        if (Instance != null)
        {
            // NEW: if card is null, try to recover it from the projectile instance.
            if (card == null && projectile != null && ProjectileCardModifiers.Instance != null)
            {
                card = ProjectileCardModifiers.Instance.GetCardFromProjectile(projectile);
            }

            perPoint = Instance.GetAttackBonusPerPoint(card);
        }

        if (perPoint <= 0f)
        {
            return 0f;
        }

        return attackStat * perPoint;
    }

    private float GetAttackBonusPerPoint(ProjectileCards card)
    {
        if (perProjectileOverrides == null || perProjectileOverrides.Length == 0)
        {
            return Mathf.Max(0f, defaultAttackBonusPerPoint);
        }

        ProjectileCards.ProjectileType? type = null;
        if (card != null)
        {
            type = card.projectileType;
        }

        if (!type.HasValue)
        {
            return Mathf.Max(0f, defaultAttackBonusPerPoint);
        }

        ProjectileCards.ProjectileType searchType = type.Value;

        for (int i = 0; i < perProjectileOverrides.Length; i++)
        {
            ProjectileAttackScaling entry = perProjectileOverrides[i];
            if (entry.projectileType == searchType)
            {
                return Mathf.Max(0f, entry.attackBonusPerPoint);
            }
        }

        return Mathf.Max(0f, defaultAttackBonusPerPoint);
    }
}