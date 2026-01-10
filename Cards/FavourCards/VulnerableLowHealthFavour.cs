using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "VulnerableLowHealthFavour", menuName = "Favour Effects 2/Vulnerable On Low Health")] 
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

        TryApplyToAllBelowThreshold();
    } 

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard) 
    { 
        currentVulnerableStacks += Mathf.Max(0, BonusVulnerableStacks); 
    } 

    public override void OnEnemyDamageFinalized(GameObject player, GameObject enemy, float finalDamage, bool isStatusTick, FavourEffectManager manager)
    {
        if (enemy == null)
        {
            return;
        }

        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>() ?? enemy.GetComponentInParent<EnemyHealth>();
        if (enemyHealth == null || !enemyHealth.IsAlive)
        {
            return;
        }

        TryApplyToEnemy(enemyHealth);
    }

    private void TryApplyToAllBelowThreshold()
    {
        EnemyHealth[] enemies = Object.FindObjectsOfType<EnemyHealth>();
        if (enemies == null || enemies.Length == 0)
        {
            return;
        }

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyHealth enemy = enemies[i];
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            TryApplyToEnemy(enemy);
        }
    }

    private void TryApplyToEnemy(EnemyHealth enemyHealth)
    {
        if (enemyHealth == null)
        {
            return;
        }

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

        StatusController statusController = enemyHealth.GetComponent<StatusController>() ?? enemyHealth.GetComponentInParent<StatusController>();
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
