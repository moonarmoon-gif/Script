using UnityEngine;
using System.Collections;

/// <summary>
/// Slow effect that reduces enemy movement speed
/// Can be applied by any projectile with slow chance
/// </summary>
public class SlowEffect : MonoBehaviour
{
    [Header("Slow Settings")]
    [Tooltip("Chance to apply slow (0-100%)")]
    [Range(0f, 100f)]
    public float slowChance = 30f;
    
    [Tooltip("Slow stacks granted per successful hit (1-4). 4 total stacks will freeze the enemy.")]
    [Range(1, 4)]
    public int slowStacksPerHit = 1;
    
    [Tooltip("How long slow lasts (seconds)")]
    public float slowDuration = 2f;
    
    [Header("Visual Settings")]
    [Tooltip("Slow VFX prefab to spawn on enemy")]
    public GameObject slowVFXPrefab;
    
    [Tooltip("Tint color for slowed enemies")]
    public Color slowTintColor = new Color(0.5f, 0.7f, 1f, 1f); // Light blue tint
    
    /// <summary>
    /// Try to apply slow to an enemy
    /// Returns true if slow was applied
    /// </summary>
    public bool TryApplySlow(GameObject enemy, Vector3 hitPoint)
    {
        if (enemy == null) return false;
        
        // Check slow chance
        float roll = Random.Range(0f, 100f);
        if (roll > slowChance)
        {
            // Slow roll failed (log removed for cleaner console)
            return false;
        }
        
        // If the enemy is currently frozen via the status system, do not
        // accumulate any new slow stacks.
        StatusController statusController = enemy.GetComponent<StatusController>();
        if (statusController != null && statusController.HasStatus(StatusId.Freeze))
        {
            return false;
        }

        // Get or add SlowStatus component
        SlowStatus slowStatus = enemy.GetComponent<SlowStatus>();
        if (slowStatus == null)
        {
            slowStatus = enemy.AddComponent<SlowStatus>();
        }
        slowStatus.SetStacksPerHit(slowStacksPerHit);
        float duration = slowDuration;

        PlayerStats stats = Object.FindObjectOfType<PlayerStats>();
        if (stats != null)
        {
            if (!Mathf.Approximately(stats.slowDurationBonus, 0f))
            {
                duration = Mathf.Max(0f, duration + stats.slowDurationBonus);
            }
        }

        // Apply slow
        slowStatus.ApplySlow(duration, slowVFXPrefab, slowTintColor, hitPoint);

        // Show SLOW status popup at enemy position
        if (DamageNumberManager.Instance != null)
        {
            DamageNumberManager.Instance.ShowSlow(enemy.transform.position);
        }

        // Inform favour effects that the player has successfully inflicted
        // the SLOW status on this enemy so they can react (e.g., by adding
        // Frostbite stacks via StatusEffectsDebuffFavour).
        FavourEffectManager favourManager = Object.FindObjectOfType<FavourEffectManager>();
        if (favourManager != null)
        {
            favourManager.NotifyStatusApplied(enemy, StatusId.Slow);
        }
        
        return true;
    }
}

/// <summary>
/// Component attached to enemies that are slowed
/// Handles movement speed reduction and visual effects
/// </summary>
public class SlowStatus : MonoBehaviour
{
    private float slowStrength = 0f;
    private float remainingDuration = 0f;
    private GameObject slowVFX;
    private bool isSlowed = false;
    private Color originalColor;
    private Color slowTint;
    private SpriteRenderer spriteRenderer;
    
    // Store original speed for different enemy types
    private float originalMoveSpeed = 0f;
    private bool hasStoredSpeed = false;
    private int slowStacks = 0;

    [SerializeField, Range(1, 4)]
    private int stacksPerHit = 1;

    [SerializeField]
    private float freezeDurationSeconds = 2f;

    public void SetStacksPerHit(int stacks)
    {
        stacksPerHit = Mathf.Clamp(stacks, 1, 4);
    }

    private void TryApplyFreeze()
    {
        StatusController statusController = GetComponent<StatusController>();
        if (statusController == null)
        {
            return;
        }

        if (statusController.HasStatus(StatusId.Freeze))
        {
            return;
        }

        statusController.AddStatus(StatusId.Freeze, 1, freezeDurationSeconds);
    }
    
    /// <summary>
    /// Apply or refresh slow effect
    /// </summary>
    public void ApplySlow(float duration, GameObject vfxPrefab, Color tintColor, Vector3 hitPoint)
    {
        // Get sprite renderer for tint
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
            }
        }
        
        if (!isSlowed)
        {
            remainingDuration = duration;
            slowTint = tintColor;
            isSlowed = true;
            slowStacks = Mathf.Clamp(stacksPerHit, 1, 4);

            slowStrength = Mathf.Clamp01(slowStacks / 4f);

            StoreOriginalSpeed();
            ApplySpeedReduction();

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.Lerp(originalColor, slowTint, 0.5f);
            }

            if (vfxPrefab != null)
            {
                slowVFX = Instantiate(vfxPrefab, transform.position, Quaternion.identity, transform);
            }

            Debug.Log($"<color=cyan>❄️ Slow STARTED! Strength: {slowStrength * 100f:F0}%, Stacks={slowStacks}, Duration: {duration}s</color>");
        }
        else
        {
            slowStacks = Mathf.Clamp(slowStacks + stacksPerHit, 1, 4);
            slowStrength = Mathf.Clamp01(slowStacks / 4f);
            // Refresh duration when reapplying slow
            remainingDuration = Mathf.Max(remainingDuration, duration);
            Debug.Log($"<color=cyan>❄️ Slow REFRESHED! Strength: {slowStrength * 100f:F0}%, Stacks={slowStacks}, Duration: {remainingDuration:F1}s</color>");
        }

        if (slowStacks >= 4)
        {
            slowStacks = 4;
            TryApplyFreeze();
        }
    }
    
    void StoreOriginalSpeed()
    {
        if (hasStoredSpeed) return;
        
        // Try DeathBringer
        var deathBringer = GetComponent<DeathBringerEnemy>();
        if (deathBringer != null)
        {
            originalMoveSpeed = deathBringer.moveSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored DeathBringer speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Try FrogEnemy
        var frog = GetComponent<FrogEnemy>();
        if (frog != null)
        {
            originalMoveSpeed = frog.moveSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored FrogEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Try FireWormEnemy
        var fireWorm = GetComponent<FireWormEnemy>();
        if (fireWorm != null)
        {
            originalMoveSpeed = fireWorm.walkSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored FireWormEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Try GolemEnemy
        var golem = GetComponent<GolemEnemy>();
        if (golem != null)
        {
            originalMoveSpeed = golem.moveSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored GolemEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Try ShadowEnemy
        var shadow = GetComponent<ShadowEnemy>();
        if (shadow != null)
        {
            originalMoveSpeed = shadow.moveSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored ShadowEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Try CrowEnemy
        var crow = GetComponent<CrowEnemy>();
        if (crow != null)
        {
            originalMoveSpeed = crow.moveSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored CrowEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Try BatEnemy
        var bat = GetComponent<BatEnemy>();
        if (bat != null)
        {
            originalMoveSpeed = bat.moveSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored BatEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Try DarkNecromancerEnemy
        var darkNecro = GetComponent<DarkNecromancerEnemy>();
        if (darkNecro != null)
        {
            originalMoveSpeed = darkNecro.moveSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored DarkNecromancerEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Try DemonSlimeEnemy
        var demonSlime = GetComponent<DemonSlimeEnemy>();
        if (demonSlime != null)
        {
            originalMoveSpeed = demonSlime.moveSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored DemonSlimeEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Try EvilWizardEnemy
        var evilWizard = GetComponent<EvilWizardEnemy>();
        if (evilWizard != null)
        {
            originalMoveSpeed = evilWizard.moveSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored EvilWizardEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Try FireWizardEnemy
        var fireWizard = GetComponent<FireWizardEnemy>();
        if (fireWizard != null)
        {
            originalMoveSpeed = fireWizard.moveSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored FireWizardEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Try FlyingDemonEnemy
        var flyingDemon = GetComponent<FlyingDemonEnemy>();
        if (flyingDemon != null)
        {
            originalMoveSpeed = flyingDemon.moveSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored FlyingDemonEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Try NightBorneEnemy
        var nightBorne = GetComponent<NightBorneEnemy>();
        if (nightBorne != null)
        {
            originalMoveSpeed = nightBorne.moveSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored NightBorneEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Try RangedNecromancerEnemy
        var rangedNecro = GetComponent<RangedNecromancerEnemy>();
        if (rangedNecro != null)
        {
            originalMoveSpeed = rangedNecro.walkSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored RangedNecromancerEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Try SkeletonEnemy
        var skeleton = GetComponent<SkeletonEnemy>();
        if (skeleton != null)
        {
            originalMoveSpeed = skeleton.moveSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored SkeletonEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Try ArcaneArcherEnemy
        var arcaneArcher = GetComponent<ArcaneArcherEnemy>();
        if (arcaneArcher != null)
        {
            originalMoveSpeed = arcaneArcher.walkSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored ArcaneArcherEnemy speed: {originalMoveSpeed}</color>");
            return;
        }

        // Try DarkSoulEnemy
        var darkSoul = GetComponent<DarkSoulEnemy>();
        if (darkSoul != null)
        {
            originalMoveSpeed = darkSoul.moveSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored DarkSoulEnemy speed: {originalMoveSpeed}</color>");
            return;
        }

        // Try ExecutionerEnemy
        var executioner = GetComponent<ExecutionerEnemy>();
        if (executioner != null)
        {
            originalMoveSpeed = executioner.moveSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored ExecutionerEnemy speed: {originalMoveSpeed}</color>");
            return;
        }

        // Try Skelly Archer
        var skellyArcher = GetComponent<SkellyArcherEnemy>();
        if (skellyArcher != null)
        {
            originalMoveSpeed = skellyArcher.walkSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored SkellyArcherEnemy speed: {originalMoveSpeed}</color>");
            return;
        }

        // Try Skelly Smith
        var skellySmith = GetComponent<SkellySmithEnemy>();
        if (skellySmith != null)
        {
            originalMoveSpeed = skellySmith.moveSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored SkellySmithEnemy speed: {originalMoveSpeed}</color>");
            return;
        }

        // Try Skelly Sword
        var skellySword = GetComponent<SkellySwordEnemy>();
        if (skellySword != null)
        {
            originalMoveSpeed = skellySword.moveSpeed;
            hasStoredSpeed = true;
            Debug.Log($"<color=cyan>Stored SkellySwordEnemy speed: {originalMoveSpeed}</color>");
            return;
        }

        // FINAL FALLBACK: Try to locate a generic public float field named
        // "moveSpeed" or "walkSpeed" on ANY MonoBehaviour attached to this
        // enemy so new enemies are automatically supported without hardcoding
        // here. We intentionally skip SlowStatus itself.
        var monoBehaviours = GetComponents<MonoBehaviour>();
        foreach (var mb in monoBehaviours)
        {
            if (mb == null || mb == this) continue;

            var type = mb.GetType();
            var moveField = type.GetField("moveSpeed");
            if (moveField != null && moveField.FieldType == typeof(float))
            {
                originalMoveSpeed = (float)moveField.GetValue(mb);
                hasStoredSpeed = true;
                Debug.Log($"<color=cyan>Stored generic moveSpeed for {gameObject.name} on {type.Name}: {originalMoveSpeed}</color>");
                return;
            }

            var walkField = type.GetField("walkSpeed");
            if (walkField != null && walkField.FieldType == typeof(float))
            {
                originalMoveSpeed = (float)walkField.GetValue(mb);
                hasStoredSpeed = true;
                Debug.Log($"<color=cyan>Stored generic walkSpeed for {gameObject.name} on {type.Name}: {originalMoveSpeed}</color>");
                return;
            }
        }

        Debug.LogWarning($"<color=yellow>SlowStatus: Could not find enemy speed field for {gameObject.name}</color>");
    }
    
    void ApplySpeedReduction()
    {
        if (!hasStoredSpeed) return;
        
        float reducedSpeed = originalMoveSpeed * (1f - slowStrength);
        
        // Apply to DeathBringer
        var deathBringer = GetComponent<DeathBringerEnemy>();
        if (deathBringer != null)
        {
            deathBringer.moveSpeed = reducedSpeed;
            return;
        }
        
        // Apply to FrogEnemy
        var frog = GetComponent<FrogEnemy>();
        if (frog != null)
        {
            frog.moveSpeed = reducedSpeed;
            return;
        }
        
        // Apply to FireWormEnemy
        var fireWorm = GetComponent<FireWormEnemy>();
        if (fireWorm != null)
        {
            fireWorm.walkSpeed = reducedSpeed;
            return;
        }
        
        // Apply to ShadowEnemy
        var shadow = GetComponent<ShadowEnemy>();
        if (shadow != null)
        {
            shadow.moveSpeed = reducedSpeed;
            return;
        }
        
        // Apply to GolemEnemy
        var golem = GetComponent<GolemEnemy>();
        if (golem != null)
        {
            golem.moveSpeed = reducedSpeed;
            return;
        }
        
        // Apply to CrowEnemy
        var crow = GetComponent<CrowEnemy>();
        if (crow != null)
        {
            crow.moveSpeed = reducedSpeed;
            return;
        }
        
        // Apply to BatEnemy
        var bat = GetComponent<BatEnemy>();
        if (bat != null)
        {
            bat.moveSpeed = reducedSpeed;
            return;
        }
        
        // Apply to DarkNecromancerEnemy
        var darkNecro = GetComponent<DarkNecromancerEnemy>();
        if (darkNecro != null)
        {
            darkNecro.moveSpeed = reducedSpeed;
            return;
        }
        
        // Apply to DemonSlimeEnemy
        var demonSlime = GetComponent<DemonSlimeEnemy>();
        if (demonSlime != null)
        {
            demonSlime.moveSpeed = reducedSpeed;
            return;
        }
        
        // Apply to EvilWizardEnemy
        var evilWizard = GetComponent<EvilWizardEnemy>();
        if (evilWizard != null)
        {
            evilWizard.moveSpeed = reducedSpeed;
            return;
        }
        
        // Apply to FireWizardEnemy
        var fireWizard = GetComponent<FireWizardEnemy>();
        if (fireWizard != null)
        {
            fireWizard.moveSpeed = reducedSpeed;
            return;
        }
        
        // Apply to FlyingDemonEnemy
        var flyingDemon = GetComponent<FlyingDemonEnemy>();
        if (flyingDemon != null)
        {
            flyingDemon.moveSpeed = reducedSpeed;
            return;
        }
        
        // Apply to NightBorneEnemy
        var nightBorne = GetComponent<NightBorneEnemy>();
        if (nightBorne != null)
        {
            nightBorne.moveSpeed = reducedSpeed;
            return;
        }
        
        // Apply to RangedNecromancerEnemy
        var rangedNecro = GetComponent<RangedNecromancerEnemy>();
        if (rangedNecro != null)
        {
            rangedNecro.walkSpeed = reducedSpeed;
            return;
        }
        
        // Apply to SkeletonEnemy
        var skeleton = GetComponent<SkeletonEnemy>();
        if (skeleton != null)
        {
            skeleton.moveSpeed = reducedSpeed;
            return;
        }
        
        // Apply to ArcaneArcherEnemy
        var arcaneArcher = GetComponent<ArcaneArcherEnemy>();
        if (arcaneArcher != null)
        {
            arcaneArcher.walkSpeed = reducedSpeed;
            return;
        }

        // Apply to DarkSoulEnemy
        var darkSoul = GetComponent<DarkSoulEnemy>();
        if (darkSoul != null)
        {
            darkSoul.moveSpeed = reducedSpeed;
            return;
        }

        // Apply to ExecutionerEnemy
        var executioner = GetComponent<ExecutionerEnemy>();
        if (executioner != null)
        {
            executioner.moveSpeed = reducedSpeed;
            return;
        }

        // Apply to SkellyArcherEnemy
        var skellyArcher = GetComponent<SkellyArcherEnemy>();
        if (skellyArcher != null)
        {
            skellyArcher.walkSpeed = reducedSpeed;
            return;
        }

        // Apply to SkellySmithEnemy
        var skellySmith = GetComponent<SkellySmithEnemy>();
        if (skellySmith != null)
        {
            skellySmith.moveSpeed = reducedSpeed;
            return;
        }

        // Apply to SkellySwordEnemy
        var skellySword = GetComponent<SkellySwordEnemy>();
        if (skellySword != null)
        {
            skellySword.moveSpeed = reducedSpeed;
            return;
        }

        // FINAL FALLBACK: Generic reflection-based apply for any enemy type.
        // Scan all components so we don't rely on a specific ordering.
        var monoBehaviours = GetComponents<MonoBehaviour>();
        foreach (var mb in monoBehaviours)
        {
            if (mb == null || mb == this) continue;

            var type = mb.GetType();
            var moveField = type.GetField("moveSpeed");
            if (moveField != null && moveField.FieldType == typeof(float))
            {
                moveField.SetValue(mb, reducedSpeed);
                return;
            }

            var walkField = type.GetField("walkSpeed");
            if (walkField != null && walkField.FieldType == typeof(float))
            {
                walkField.SetValue(mb, reducedSpeed);
                return;
            }
        }
    }
    
    void RestoreOriginalSpeed()
    {
        if (!hasStoredSpeed) return;
        
        // Restore DeathBringer speed
        var deathBringer = GetComponent<DeathBringerEnemy>();
        if (deathBringer != null)
        {
            deathBringer.moveSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored DeathBringer speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Restore FrogEnemy speed
        var frog = GetComponent<FrogEnemy>();
        if (frog != null)
        {
            frog.moveSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored FrogEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Restore FireWormEnemy speed
        var fireWorm = GetComponent<FireWormEnemy>();
        if (fireWorm != null)
        {
            fireWorm.walkSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored FireWormEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Restore ShadowEnemy speed
        var shadow = GetComponent<ShadowEnemy>();
        if (shadow != null)
        {
            shadow.moveSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored ShadowEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Restore GolemEnemy speed
        var golem = GetComponent<GolemEnemy>();
        if (golem != null)
        {
            golem.moveSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored GolemEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Restore CrowEnemy speed
        var crow = GetComponent<CrowEnemy>();
        if (crow != null)
        {
            crow.moveSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored CrowEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Restore BatEnemy speed
        var bat = GetComponent<BatEnemy>();
        if (bat != null)
        {
            bat.moveSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored BatEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Restore DarkNecromancerEnemy speed
        var darkNecro = GetComponent<DarkNecromancerEnemy>();
        if (darkNecro != null)
        {
            darkNecro.moveSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored DarkNecromancerEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Restore DemonSlimeEnemy speed
        var demonSlime = GetComponent<DemonSlimeEnemy>();
        if (demonSlime != null)
        {
            demonSlime.moveSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored DemonSlimeEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Restore EvilWizardEnemy speed
        var evilWizard = GetComponent<EvilWizardEnemy>();
        if (evilWizard != null)
        {
            evilWizard.moveSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored EvilWizardEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Restore FireWizardEnemy speed
        var fireWizard = GetComponent<FireWizardEnemy>();
        if (fireWizard != null)
        {
            fireWizard.moveSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored FireWizardEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Restore FlyingDemonEnemy speed
        var flyingDemon = GetComponent<FlyingDemonEnemy>();
        if (flyingDemon != null)
        {
            flyingDemon.moveSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored FlyingDemonEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Restore NightBorneEnemy speed
        var nightBorne = GetComponent<NightBorneEnemy>();
        if (nightBorne != null)
        {
            nightBorne.moveSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored NightBorneEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Restore RangedNecromancerEnemy speed
        var rangedNecro = GetComponent<RangedNecromancerEnemy>();
        if (rangedNecro != null)
        {
            rangedNecro.walkSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored RangedNecromancerEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Restore SkeletonEnemy speed
        var skeleton = GetComponent<SkeletonEnemy>();
        if (skeleton != null)
        {
            skeleton.moveSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored SkeletonEnemy speed: {originalMoveSpeed}</color>");
            return;
        }
        
        // Restore ArcaneArcherEnemy speed
        var arcaneArcher = GetComponent<ArcaneArcherEnemy>();
        if (arcaneArcher != null)
        {
            arcaneArcher.walkSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored ArcaneArcherEnemy speed: {originalMoveSpeed}</color>");
            return;
        }

        // Restore DarkSoulEnemy speed
        var darkSoul = GetComponent<DarkSoulEnemy>();
        if (darkSoul != null)
        {
            darkSoul.moveSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored DarkSoulEnemy speed: {originalMoveSpeed}</color>");
            return;
        }

        // Restore ExecutionerEnemy speed
        var executioner = GetComponent<ExecutionerEnemy>();
        if (executioner != null)
        {
            executioner.moveSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored ExecutionerEnemy speed: {originalMoveSpeed}</color>");
            return;
        }

        // Restore SkellyArcherEnemy speed
        var skellyArcher = GetComponent<SkellyArcherEnemy>();
        if (skellyArcher != null)
        {
            skellyArcher.walkSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored SkellyArcherEnemy speed: {originalMoveSpeed}</color>");
            return;
        }

        // Restore SkellySmithEnemy speed
        var skellySmith = GetComponent<SkellySmithEnemy>();
        if (skellySmith != null)
        {
            skellySmith.moveSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored SkellySmithEnemy speed: {originalMoveSpeed}</color>");
            return;
        }

        // Restore SkellySwordEnemy speed
        var skellySword = GetComponent<SkellySwordEnemy>();
        if (skellySword != null)
        {
            skellySword.moveSpeed = originalMoveSpeed;
            Debug.Log($"<color=cyan>Restored SkellySwordEnemy speed: {originalMoveSpeed}</color>");
            return;
        }

        // FINAL FALLBACK: Generic reflection-based restore for any enemy type.
        // Scan all components so we don't rely on a specific ordering.
        var monoBehaviours = GetComponents<MonoBehaviour>();
        foreach (var mb in monoBehaviours)
        {
            if (mb == null || mb == this) continue;

            var type = mb.GetType();
            var moveField = type.GetField("moveSpeed");
            if (moveField != null && moveField.FieldType == typeof(float))
            {
                moveField.SetValue(mb, originalMoveSpeed);
                Debug.Log($"<color=cyan>Restored generic moveSpeed for {gameObject.name} on {type.Name}: {originalMoveSpeed}</color>");
                return;
            }

            var walkField = type.GetField("walkSpeed");
            if (walkField != null && walkField.FieldType == typeof(float))
            {
                walkField.SetValue(mb, originalMoveSpeed);
                Debug.Log($"<color=cyan>Restored generic walkSpeed for {gameObject.name} on {type.Name}: {originalMoveSpeed}</color>");
                return;
            }
        }
    }
    
    void Update()
    {
        if (!isSlowed) return;
        
        // CRITICAL: Continuously apply speed reduction every frame
        // This prevents enemies from resetting their speed
        ApplySpeedReduction();
        
        // Update duration
        remainingDuration -= Time.deltaTime;
        if (remainingDuration <= 0f)
        {
            EndSlow();
        }
    }
    
    void EndSlow()
    {
        isSlowed = false;
        remainingDuration = 0f;
        
        // Restore original speed
        RestoreOriginalSpeed();
        
        // Restore original color
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
        
        // Destroy VFX
        if (slowVFX != null)
        {
            Destroy(slowVFX);
        }
        
        Debug.Log("<color=cyan>❄️ Slow ENDED</color>");
        
        // Remove component
        Destroy(this);
    }
    
    void OnDestroy()
    {
        // Clean up VFX if component is destroyed
        if (slowVFX != null)
        {
            Destroy(slowVFX);
        }
        
        // Restore speed if still slowed
        if (isSlowed)
        {
            RestoreOriginalSpeed();
            
            // Restore color
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
        }
    }
}
