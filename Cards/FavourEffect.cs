using UnityEngine;

/// <summary>
/// Base class for all Favour effects. Each concrete effect is a ScriptableObject
/// that can be referenced by a FavourCards asset and instantiated at runtime
/// by the FavourEffectManager on the player.
/// </summary>
public abstract class FavourEffect : ScriptableObject
{
    [Header("Favour Effect Info")]
    public string effectName;

    /// <summary>
    /// Called when this effect is first applied to the player via a Favour card.
    /// Use this to set up any state, subscribe to events, etc.
    /// </summary>
    public virtual void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard) { }

    /// <summary>
    /// Called when the SAME favour card is selected again. Default behaviour
    /// is to treat this as a fresh apply so effects can opt-in to custom
    /// upgrade logic by overriding this method.
    /// </summary>
    public virtual void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        OnApply(player, manager, sourceCard);
    }

    /// <summary>
    /// Called when this effect is removed or expires.
    /// Use this to clean up any state or event subscriptions.
    /// </summary>
    public virtual void OnRemove(GameObject player, FavourEffectManager manager) { }

    /// <summary>
    /// Called every frame while this effect is active.
    /// </summary>
    public virtual void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime) { }

    /// <summary>
    /// Optional: called when an enemy is killed while this effect is active.
    /// Hooked up later to the enemy death pipeline.
    /// </summary>
    public virtual void OnEnemyKilled(GameObject player, GameObject enemy, FavourEffectManager manager) { }

    /// <summary>
    /// Optional: called when the player successfully applies a status effect
    /// to an enemy (e.g., Burn, Slow, Static). Used by favours that react to
    /// status inflictions.
    /// </summary>
    public virtual void OnStatusApplied(GameObject player, GameObject enemy, StatusId statusId, FavourEffectManager manager) { }

    /// <summary>
    /// Optional: called when the player is about to take damage from an attacker.
    /// Effects can modify the incoming damage via the ref parameter and can
    /// also react to the specific attacker GameObject.
    /// Hooked up to the player damage pipeline.
    /// </summary>
    public virtual void OnPlayerHit(GameObject player, GameObject attacker, ref float damage, FavourEffectManager manager) { }

    public virtual void OnBeforeDealDamage(GameObject player, GameObject enemy, ref float damage, FavourEffectManager manager) { }

    public virtual float PreviewBeforeDealDamage(GameObject player, GameObject enemy, float damage, FavourEffectManager manager)
    {
        return damage;
    }
}
