using System;
using UnityEngine;

public class PlayerMana : MonoBehaviour
{
    [SerializeField] private float maxMana = 100f;
    [SerializeField] private float currentMana = 100f;

    [Header("Regeneration")]
    [Tooltip("Time between mana regeneration ticks (in seconds)")]
    public float manaRegenInterval = 1f;
    [SerializeField] private bool regenEnabled = true;
    [SerializeField] private bool regenDuringDeath = true;

    public event Action<float, float> OnManaChanged; // (current, max)

    private float nextRegenTime = 0f;
    private PlayerStats playerStats;

    // Public integer-facing properties (for existing callers) backed by float pool
    public int MaxMana
    {
        get => Mathf.Max(0, Mathf.RoundToInt(maxMana));
        set
        {
            maxMana = Mathf.Max(0, value);
            currentMana = Mathf.Clamp(currentMana, 0f, maxMana);
            RaiseChanged();
        }
    }

    public int CurrentMana
    {
        get => Mathf.Clamp(Mathf.FloorToInt(currentMana), 0, MaxMana);
        set
        {
            currentMana = Mathf.Clamp(value, 0, MaxMana);
            RaiseChanged();
        }
    }

    // Exact float accessors for systems/UI that want fractional mana
    public float MaxManaExact => Mathf.Max(0f, maxMana);
    public float CurrentManaExact => Mathf.Clamp(currentMana, 0f, MaxManaExact);

    private void OnValidate()
    {
        maxMana = Mathf.Max(0f, maxMana);
        currentMana = Mathf.Clamp(currentMana, 0f, maxMana);
        RaiseChanged();
    }

    private void Start()
    {
        // Don't override inspector values!
        // regenEnabled, manaRegenInterval, regenDuringDeath are set in inspector
        nextRegenTime = Time.time + manaRegenInterval;
        playerStats = GetComponent<PlayerStats>();
        RaiseChanged();
    }

    private void Update()
    {
        bool shouldRegen = regenEnabled && manaRegenInterval > 0f && CurrentManaExact < MaxManaExact;

        if (!regenDuringDeath && PlayerController.Instance != null)
        {
            shouldRegen &= PlayerController.Instance.enabled;
        }

        if (!shouldRegen)
        {
            return;
        }

        if (Time.time < nextRegenTime)
        {
            return;
        }

        float regenPerSecond = playerStats != null ? playerStats.manaRegenPerSecond : 0f;
        if (regenPerSecond <= 0f)
        {
            nextRegenTime = Time.time + manaRegenInterval;
            return;
        }

        float missingMana = MaxManaExact - CurrentManaExact;
        if (missingMana <= 0f)
        {
            nextRegenTime = Time.time + manaRegenInterval;
            return;
        }

        float regenThisTick = regenPerSecond * manaRegenInterval;
        float actualRegen = Mathf.Min(missingMana, regenThisTick);

        if (actualRegen > 0f)
        {
            currentMana = Mathf.Clamp(currentMana + actualRegen, 0f, MaxManaExact);
            RaiseChanged();
        }

        nextRegenTime = Time.time + manaRegenInterval;
    }

    public bool Spend(int cost)
    {
        if (cost <= 0) return true;
        if (CurrentManaExact < cost) return false;
        currentMana = Mathf.Clamp(currentMana - cost, 0f, MaxManaExact);
        RaiseChanged();
        return true;
    }

    public void AddMana(int amount)
    {
        if (amount <= 0) return;
        currentMana = Mathf.Clamp(currentMana + amount, 0f, MaxManaExact);
        RaiseChanged();
    }

    public void SetMaxMana(int newMax, bool refill)
    {
        maxMana = Mathf.Max(0, newMax);
        if (refill) currentMana = maxMana;
        currentMana = Mathf.Clamp(currentMana, 0f, maxMana);
        RaiseChanged();
    }
    
    /// <summary>
    /// Increase maximum mana and restore by the same amount
    /// </summary>
    public void IncreaseMaxMana(float amount)
    {
        int intAmount = Mathf.RoundToInt(amount);
        if (intAmount <= 0) return;
        maxMana += intAmount;
        currentMana += intAmount; // Also restore by the same amount
        currentMana = Mathf.Clamp(currentMana, 0f, maxMana);
        RaiseChanged();
        Debug.Log($"<color=cyan>Max mana increased by {intAmount}! New max: {maxMana}</color>");
    }

    private void RaiseChanged() => OnManaChanged?.Invoke(CurrentManaExact, MaxManaExact);
}