using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "VulnerableLowHealthFavour", menuName = "Favour Effects/Vulnerable On Low Health")] 
public class VulnerableLowHealthFavour : FavourEffect
{ 
    [Header("Vulnerable Low Health Settings")] 
    [Tooltip("Vulnerable stacks applied to low-health enemies per hit.")] 
    public int VulnerableStacks = 1; 

    [Tooltip("Health threshold as fraction 0-1. Enemies at or below this health will be inflicted with Vulnerable when damaged.")] 
    public float HealthThreshold = 0.5f; 

    [Header("Enhanced")] 
    [Tooltip("Additional Vulnerable stacks applied per enhancement.")] 
    public int BonusVulnerableStacks = 1; 

    private int currentVulnerableStacks; 

    // Track which enemies have already received Vulnerable from this favour
    // so we only apply stacks once per enemy when below the threshold.
    private HashSet<EnemyHealth> processedEnemies = new HashSet<EnemyHealth>();

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard) 
    { 
        currentVulnerableStacks = Mathf.Max(0, VulnerableStacks); 
        if (processedEnemies == null)
        {
            processedEnemies = new HashSet<EnemyHealth>();
        }
        else
        {
            processedEnemies.Clear();
        }
    } 

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard) 
    { 
        currentVulnerableStacks += Mathf.Max(0, BonusVulnerableStacks); 
    } 

    public override void OnBeforeDealDamage(GameObject player, GameObject enemy, ref float damage, FavourEffectManager manager) 
    { 
        if (enemy == null || damage <= 0f) 
        { 
            return; 
        } 

        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>() ?? enemy.GetComponentInParent<EnemyHealth>(); 
        if (enemyHealth == null) 
        { 
            return; 
        } 

        // Only apply Vulnerable once per enemy for this favour.
        if (processedEnemies != null && processedEnemies.Contains(enemyHealth))
        {
            return;
        }

        float maxHealth = enemyHealth.MaxHealth; 
        if (maxHealth <= 0f) 
        { 
            return; 
        } 

        float normalizedHealth = enemyHealth.CurrentHealth / maxHealth; 
        float threshold = Mathf.Clamp01(HealthThreshold); 

        if (normalizedHealth > threshold) 
        { 
            return; 
        } 

        if (currentVulnerableStacks <= 0) 
        { 
            return; 
        } 

        StatusController statusController = enemy.GetComponent<StatusController>() ?? enemy.GetComponentInParent<StatusController>(); 
        if (statusController == null) 
        { 
            return; 
        } 

        statusController.AddStatus(StatusId.Vulnerable, currentVulnerableStacks, -1f); 

        if (processedEnemies == null)
        {
            processedEnemies = new HashSet<EnemyHealth>();
        }

        processedEnemies.Add(enemyHealth);
    } 
}
