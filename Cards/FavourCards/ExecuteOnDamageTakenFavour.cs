using UnityEngine;

/// <summary>
/// Favour effect that executes a one-shot (or limited charges) effect whenever
/// the player takes damage. It listens to the player damage pipeline via
/// OnPlayerHit and can apply its effect subject to an internal cooldown and
/// remaining charges. Upgrades adjust charges / cooldown based on the source
/// card's rarity.
///
/// NOTE: The actual gameplay effect to execute is intentionally generic here
/// so it can be authored in the inspector (e.g. a damage burst, heal, etc.)
/// via a UnityEvent-like ScriptableObject, or simply by tweaking player stats.
/// For now, this implementation applies a temporary flat damage reduction on
/// the hit that triggered it.
/// </summary>
[CreateAssetMenu(fileName = "ExecuteOnDamageTakenFavour", menuName = "Favour Effects/Execute On Damage Taken")]
public class ExecuteOnDamageTakenFavour : FavourEffect
{
    [Header("Trigger Settings")]
    [Tooltip("Base number of times this effect can trigger before it expires. 0 or negative = unlimited.")]
    public int baseCharges = 3;

    [Tooltip("Base cooldown in seconds between triggers.")]
    public float baseCooldown = 3f;

    [Tooltip("Flat damage reduction applied when this effect triggers (before damage is applied).")]
    public float damageReductionOnTrigger = 5f;

    [Tooltip("Minimum damage value that can remain after reduction.")]
    public float minimumDamageAfterReduction = 1f;

    [Header("Upgrade Scaling (per rarity)")]
    [Tooltip("Additional charges granted per upgrade for Common rarity.")]
    public int commonChargesPerUpgrade = 1;

    [Tooltip("Additional charges granted per upgrade for Rare rarity.")]
    public int rareChargesPerUpgrade = 2;

    [Tooltip("Additional charges granted per upgrade for Legendary rarity.")]
    public int legendaryChargesPerUpgrade = 3;

    [Tooltip("Cooldown multiplier applied per upgrade for Common rarity (e.g. 0.95 = -5% per upgrade).")]
    public float commonCooldownMultiplierPerUpgrade = 0.95f;

    [Tooltip("Cooldown multiplier applied per upgrade for Rare rarity.")]
    public float rareCooldownMultiplierPerUpgrade = 0.9f;

    [Tooltip("Cooldown multiplier applied per upgrade for Legendary rarity.")]
    public float legendaryCooldownMultiplierPerUpgrade = 0.85f;

    private int currentCharges;
    private float currentCooldown;
    private float lastTriggerTime = -999f;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentCharges = baseCharges;
        currentCooldown = Mathf.Max(0f, baseCooldown);
        lastTriggerTime = -999f;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (sourceCard == null)
        {
            return;
        }

        int extraCharges = 0;
        float cooldownMultiplier = 1f;

        switch (sourceCard.rarity)
        {
            case CardRarity.Common:
                extraCharges = commonChargesPerUpgrade;
                cooldownMultiplier = commonCooldownMultiplierPerUpgrade;
                break;
            case CardRarity.Rare:
                extraCharges = rareChargesPerUpgrade;
                cooldownMultiplier = rareCooldownMultiplierPerUpgrade;
                break;
            case CardRarity.Legendary:
                extraCharges = legendaryChargesPerUpgrade;
                cooldownMultiplier = legendaryCooldownMultiplierPerUpgrade;
                break;
            default:
                extraCharges = commonChargesPerUpgrade;
                cooldownMultiplier = commonCooldownMultiplierPerUpgrade;
                break;
        }

        currentCharges += extraCharges;
        currentCooldown *= cooldownMultiplier;
        if (currentCooldown < 0f)
        {
            currentCooldown = 0f;
        }
    }

    public override void OnPlayerHit(GameObject player, GameObject attacker, ref float damage, FavourEffectManager manager)
    {
        // If charges are limited and we have used them all, do nothing.
        if (baseCharges > 0 && currentCharges <= 0)
        {
            return;
        }

        float now = Time.time;
        if (now - lastTriggerTime < currentCooldown)
        {
            return;
        }

        lastTriggerTime = now;

        if (baseCharges > 0)
        {
            currentCharges--;
        }
        if (attacker != null)
        {
            EnemyHealth enemyHealth = attacker.GetComponent<EnemyHealth>();
            if (enemyHealth == null)
            {
                enemyHealth = attacker.GetComponentInParent<EnemyHealth>();
            }

            if (enemyHealth != null && enemyHealth.IsAlive)
            {
                bool isBoss = false;

                GameObject boss = null;

                EnemySpawner enemySpawner = Object.FindObjectOfType<EnemySpawner>();
                if (enemySpawner != null && enemySpawner.CurrentBossEnemy != null)
                {
                    boss = enemySpawner.CurrentBossEnemy;
                }
                else
                {
                    EnemyCardSpawner spawner = Object.FindObjectOfType<EnemyCardSpawner>();
                    if (spawner != null && spawner.CurrentBossEnemy != null)
                    {
                        boss = spawner.CurrentBossEnemy;
                    }
                }

                if (boss != null)
                {
                    Transform bossTransform = boss.transform;
                    Transform enemyTransform = enemyHealth.transform;

                    if (enemyTransform == bossTransform || enemyTransform.IsChildOf(bossTransform))
                    {
                        isBoss = true;
                    }
                }

                // Only execute NON-BOSS enemies
                if (!isBoss)
                {
                    float killDamage = enemyHealth.MaxHealth * 1000f;
                    Vector3 hitPoint = enemyHealth.transform.position;
                    Vector3 hitNormal = Vector3.up;
                    enemyHealth.TakeDamage(killDamage, hitPoint, hitNormal);

                    // Show "Executed" status popup at the enemy's position
                    if (DamageNumberManager.Instance != null)
                    {
                        DamageNumberManager.Instance.ShowExecuted(enemyHealth.transform.position);
                    }
                }
            }
        }

        if (damageReductionOnTrigger != 0f)
        {
            damage -= damageReductionOnTrigger;
            if (damage < minimumDamageAfterReduction)
            {
                damage = minimumDamageAfterReduction;
            }
        }
    }
}
