using UnityEngine;

/// <summary>
/// Interface for projectiles that can receive instant modifier updates
/// Implement this on projectile scripts to support mid-flight modifier changes
/// </summary>
public interface IInstantModifiable
{
    /// <summary>
    /// Apply modifiers instantly to this projectile
    /// Called when a new modifier card is selected
    /// </summary>
    /// <param name="modifiers">The updated modifier stats</param>
    void ApplyInstantModifiers(CardModifierStats modifiers);
}
