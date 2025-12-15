using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Universal system that applies instant modifiers to ALL active projectiles
/// Automatically finds and updates all projectiles when a modifier card is selected
/// </summary>
public class UniversalInstantModifier : MonoBehaviour
{
    private static UniversalInstantModifier _instance;
    public static UniversalInstantModifier Instance => _instance;
    
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }
    
    /// <summary>
    /// Apply modifiers to ALL active projectiles in the scene
    /// Called when a new modifier card is selected
    /// </summary>
    public void ApplyInstantModifiersToAll(ProjectileType projectileType, CardModifierStats modifiers)
    {
        Debug.Log($"<color=cyan>╔═══ UNIVERSAL INSTANT MODIFIERS: {projectileType} ═══╗</color>");
        
        int updatedCount = 0;
        
        // Find all projectiles that implement IInstantModifiable
        IInstantModifiable[] modifiables = FindObjectsOfType<MonoBehaviour>()
            .OfType<IInstantModifiable>()
            .ToArray();
        
        foreach (var modifiable in modifiables)
        {
            // Check if this projectile matches the type
            MonoBehaviour mb = modifiable as MonoBehaviour;
            if (mb != null && ShouldApplyToProjectile(mb, projectileType))
            {
                modifiable.ApplyInstantModifiers(modifiers);
                updatedCount++;
            }
        }
        
        // Also handle projectiles that don't implement IInstantModifiable yet
        // Apply basic instant modifiers (speed, size, lifetime)
        updatedCount += ApplyBasicInstantModifiers(projectileType, modifiers);
        
        Debug.Log($"<color=cyan>╚═══ Updated {updatedCount} active projectiles ═══╝</color>");
    }
    
    private bool ShouldApplyToProjectile(MonoBehaviour projectile, ProjectileType targetType)
    {
        // Check projectile type field/property
        var typeField = projectile.GetType().GetField("projectileType");
        if (typeField != null)
        {
            ProjectileType projType = (ProjectileType)typeField.GetValue(projectile);
            return projType == targetType;
        }
        
        var typeProp = projectile.GetType().GetProperty("projectileType");
        if (typeProp != null)
        {
            ProjectileType projType = (ProjectileType)typeProp.GetValue(projectile);
            return projType == targetType;
        }
        
        // Fallback: Check by class name
        string className = projectile.GetType().Name;
        string typeName = targetType.ToString();
        return className.Contains(typeName) || typeName.Contains(className);
    }
    
    private int ApplyBasicInstantModifiers(ProjectileType projectileType, CardModifierStats modifiers)
    {
        int count = 0;
        
        // Find all active projectiles by type
        MonoBehaviour[] allProjectiles = FindObjectsOfType<MonoBehaviour>();
        
        foreach (var proj in allProjectiles)
        {
            if (!ShouldApplyToProjectile(proj, projectileType)) continue;
            if (proj is IInstantModifiable) continue; // Already handled
            
            // Apply basic modifiers using reflection
            var type = proj.GetType();
            
            // Speed
            if (modifiers.speedIncrease != 0f)
            {
                ApplyFieldModifier(proj, type, "speed", modifiers.speedIncrease, true);
                ApplyFieldModifier(proj, type, "flySpeed", modifiers.speedIncrease, true);
                ApplyFieldModifier(proj, type, "moveSpeed", modifiers.speedIncrease, true);
            }
            
            // Size
            if (modifiers.sizeMultiplier != 1f)
            {
                proj.transform.localScale *= modifiers.sizeMultiplier;
            }
            
            // Lifetime
            if (modifiers.lifetimeIncrease != 0f)
            {
                ApplyFieldModifier(proj, type, "lifetime", modifiers.lifetimeIncrease, true);
                ApplyFieldModifier(proj, type, "lifetimeSeconds", modifiers.lifetimeIncrease, true);
            }
            
            // Damage: apply FLAT bonus for generic projectiles that don't
            // implement IInstantModifiable so DamageIncrease behaves
            // consistently across all projectiles.
            if (modifiers.damageFlat != 0f)
            {
                ApplyFieldModifier(proj, type, "damage", modifiers.damageFlat, true);
            }
            
            count++;
        }
        
        return count;
    }
    
    private void ApplyFieldModifier(object obj, System.Type type, string fieldName, float value, bool isAdditive)
    {
        var field = type.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null && field.FieldType == typeof(float))
        {
            float current = (float)field.GetValue(obj);
            float newValue = isAdditive ? current + value : current * value;
            field.SetValue(obj, newValue);
            Debug.Log($"<color=lime>  {fieldName}: {current:F2} → {newValue:F2}</color>");
        }
    }
}
