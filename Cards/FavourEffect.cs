using UnityEngine;

/// <summary>
/// Optional interface for favour effects that want to limit how many times
/// they can be picked during a run.
/// </summary>
public interface IFavourPickLimit
{
    int MaxPickLimit { get; }
    bool IsAtPickLimit();
}

/// <summary>
/// Base class for all Favour effects. Each concrete effect is a ScriptableObject
/// that can be referenced by a FavourCards asset and instantiated at runtime
/// by the FavourEffectManager on the player.
/// </summary>
public abstract class FavourEffect : ScriptableObject, IFavourPickLimit
{
    [Header("Favour Effect Info")]
    public string effectName;

    [Header("Pick Limit")]
    [Tooltip("Maximum number of times this favour can be picked in a run. 0 = no limit.")]
    [SerializeField]
    private int maxPickLimit = 0;

    private static readonly System.Collections.Generic.Dictionary<FavourEffect, int> _pickCounts
        = new System.Collections.Generic.Dictionary<FavourEffect, int>();

    protected virtual int GetMaxPickLimit()
    {
        return maxPickLimit;
    }

    /// <summary>
    /// Public read-only view of this favour's pick limit (interface implementation).
    /// </summary>
    public int MaxPickLimit => GetMaxPickLimit();

    /// <summary>
    /// Current number of times this favour asset has been picked in the run.
    /// </summary>
    public int GetCurrentPickCount()
    {
        int count;
        if (_pickCounts.TryGetValue(this, out count))
        {
            return count;
        }
        return 0;
    }

    /// <summary>
    /// Register a new pick of this favour asset. Called by FavourEffectManager
    /// whenever the corresponding card is selected.
    /// </summary>
    public void RegisterPick()
    {
        if (MaxPickLimit <= 0)
        {
            return;
        }

        int current = GetCurrentPickCount();
        _pickCounts[this] = current + 1;
    }

    /// <summary>
    /// Returns true when this favour asset has reached its MaxPickLimit.
    /// </summary>
    public bool IsAtPickLimit()
    {
        if (MaxPickLimit <= 0)
        {
            return false;
        }

        return GetCurrentPickCount() >= MaxPickLimit;
    }

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

    public virtual void OnPlayerDamageFinalized(GameObject player, GameObject attacker, float finalDamage, bool isStatusTick, bool isAoeDamage, FavourEffectManager manager) { }

    public virtual void OnBeforeDealDamage(GameObject player, GameObject enemy, ref float damage, FavourEffectManager manager) { }

    public virtual void OnCritResolved(GameObject player, ProjectileCards sourceCard, bool canCrit, bool didCrit, FavourEffectManager manager) { }

    public virtual void OnEnemyDamageFinalized(GameObject player, GameObject enemy, float finalDamage, bool isStatusTick, FavourEffectManager manager) { }

    public virtual float GetExecuteThresholdPercent(GameObject player, GameObject enemy, FavourEffectManager manager) { return 0f; }

    public virtual float PreviewBeforeDealDamage(GameObject player, GameObject enemy, float damage, FavourEffectManager manager)
    {
        return damage;
    }

    public static void ResetPickCounts()
    {
        _pickCounts.Clear();
    }
}
