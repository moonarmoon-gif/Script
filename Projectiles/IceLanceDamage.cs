using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Damage component for individual ice lance spawned by IceLancer
/// </summary>
public class IceLanceDamage : MonoBehaviour
{
    [HideInInspector] public float damage = 20f;
    [HideInInspector] public LayerMask enemyLayer;
    
    [Header("Hit Effect")]
    [Tooltip("Hit effect prefab to spawn on enemy hit")]
    public GameObject hitEffectPrefab;
    [Tooltip("Duration before hit effect is destroyed (seconds)")]
    public float hitEffectDuration = 1f;
    [Tooltip("X offset for hit effect position")]
    public float hitEffectOffsetX = 0f;
    [Tooltip("Y offset for hit effect position")]
    public float hitEffectOffsetY = 0f;
    [Tooltip("Parent hit effect to enemy (follows enemy until destroyed)")]
    public bool parentHitEffectToEnemy = true;
    
    [Header("Slow Effect")]
    [Tooltip("Chance to apply slow effect (0-1)")]
    public float slowChance = 0.2f;
    [Tooltip("Slow multiplier (0.5 = 50% speed)")]
    public float slowMultiplier = 0.5f;
    [Tooltip("Slow duration in seconds")]
    public float slowDuration = 5f;
    
    private HashSet<GameObject> damagedEnemies = new HashSet<GameObject>();
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        Transform t = other != null ? other.transform : null;
        while (t != null)
        {
            if (t.name == "ClickHitbox")
            {
                return;
            }
            t = t.parent;
        }

        if (((1 << other.gameObject.layer) & enemyLayer) == 0) return;
        
        // Check if already damaged this enemy (ONLY DAMAGE ONCE PER LANCE)
        if (damagedEnemies.Contains(other.gameObject)) return;
        
        IDamageable damageable = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
        
        if (damageable != null && damageable.IsAlive)
        {
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 hitNormal = (transform.position - hitPoint).normalized;

            float finalDamage = damage;

            // Apply favour-based outgoing damage modifiers so IceLance hits
            // respect the same damage pipeline as other projectiles.
            PlayerStats stats = FindObjectOfType<PlayerStats>();
            if (stats != null)
            {
                FavourEffectManager favourManager = stats.GetComponent<FavourEffectManager>();
                if (favourManager != null)
                {
                    Component damageableComponent = damageable as Component;
                    GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : null;
                    if (enemyObject != null)
                    {
                        favourManager.NotifyBeforeDealDamage(enemyObject, ref finalDamage);
                    }
                }
            }

            EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>() ?? other.GetComponentInParent<EnemyHealth>();

            if (enemyHealth != null)
            {
                // Tag EnemyHealth so it knows to render this hit using the ice
                // damage color (including when armor/defense reduce it to 0).
                enemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Ice);
            }

            // Let EnemyHealth handle final damage application and all
            // damage-number display based on the post-mitigation amount.
            damageable.TakeDamage(finalDamage, hitPoint, hitNormal);
            damagedEnemies.Add(other.gameObject);
            
            // Spawn hit effect
            if (hitEffectPrefab != null)
            {
                Vector3 effectPosition = hitPoint + new Vector3(hitEffectOffsetX, hitEffectOffsetY, 0f);
                GameObject effect = Instantiate(hitEffectPrefab, effectPosition, Quaternion.identity);
                
                // Parent to enemy if enabled
                if (parentHitEffectToEnemy)
                {
                    effect.transform.SetParent(other.transform, true);
                    Debug.Log($"<color=cyan>IceLance hit effect parented to {other.gameObject.name}</color>");
                }
                
                PauseSafeSelfDestruct.Schedule(effect, hitEffectDuration);
            }
            
            // Apply slow effect
            if (Random.value <= slowChance)
            {
                EnemyHealth ownerHealth = other.GetComponent<EnemyHealth>() ?? other.GetComponentInParent<EnemyHealth>();
                GameObject ownerGO = ownerHealth != null ? ownerHealth.gameObject : other.gameObject;

                StatusController statusController = ownerGO.GetComponent<StatusController>() ?? ownerGO.GetComponentInParent<StatusController>();
                if (statusController == null)
                {
                    statusController = ownerGO.AddComponent<StatusController>();
                }

                int slowStacksBefore = statusController.GetStacks(StatusId.Slow);

                float duration = slowDuration;
                int stacksToAdd = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(1f - slowMultiplier) * 4f), 1, 4);
                if (stats != null)
                {
                    if (!Mathf.Approximately(stats.slowDurationBonus, 0f))
                    {
                        duration = Mathf.Max(0f, duration + stats.slowDurationBonus);
                    }

                    if (!Mathf.Approximately(stats.slowStrengthBonus, 0f))
                    {
                        float baseStrength = Mathf.Clamp01(stacksToAdd / 4f);
                        float strength = Mathf.Clamp01(baseStrength + Mathf.Max(0f, stats.slowStrengthBonus));
                        int adjustedStacks = Mathf.Clamp(Mathf.RoundToInt(strength * 4f), 1, 4);
                        stacksToAdd = Mathf.Max(stacksToAdd, adjustedStacks);
                    }
                }

                ProjectileCards sourceCard = null;
                if (ProjectileCardModifiers.Instance != null)
                {
                    sourceCard = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
                }

                statusController.AddStatus(StatusId.Slow, stacksToAdd, duration, 0f, sourceCard);

                int slowStacksAfter = statusController.GetStacks(StatusId.Slow);
                if (slowStacksAfter > slowStacksBefore && DamageNumberManager.Instance != null)
                {
                    Vector3 anchor = DamageNumberManager.Instance.GetAnchorWorldPosition(ownerGO, ownerGO.transform.position);
                    DamageNumberManager.Instance.ShowSlow(anchor);
                }
            }
        }
    }
}
