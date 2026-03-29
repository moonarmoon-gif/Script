using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "StatusDecayFavour", menuName = "Favour Effects/Status Decay")] 
public class StatusDecayFavour : FavourEffect
{
    [Header("Status Decay Settings")]
    [FormerlySerializedAs("DecayStacksPerStatus")]
    [Tooltip("Decay stacks granted each time an enemy receives a tracked elemental status effect.")]
    public int DecayStacks = 5;

    [Tooltip("Maximum total Decay stacks this favour can apply to a single enemy per run.")]
    public int MaxDecayStacks = 10;

    [Header("Enhanced")]
    [FormerlySerializedAs("BonusDecayStacks")]
    [Tooltip("Additional maximum Decay stacks per enemy granted when this favour is enhanced.")]
    public int BonusMaxDecayStacks = 5;

    private int currentDecayStacks;
    private int currentMaxDecayStacks;

    private readonly Dictionary<int, int> appliedDecayPerEnemy = new Dictionary<int, int>();

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentDecayStacks = Mathf.Max(0, DecayStacks);
        currentMaxDecayStacks = Mathf.Max(0, MaxDecayStacks);
        appliedDecayPerEnemy.Clear();
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentMaxDecayStacks += Mathf.Max(0, BonusMaxDecayStacks);
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        appliedDecayPerEnemy.Clear();
    }

    public override void OnStatusApplied(GameObject player, GameObject enemy, StatusId statusId, FavourEffectManager manager)
    {
        if (enemy == null)
        {
            return;
        }

        if (!IsTrackedStatus(statusId))
        {
            return;
        }

        if (currentDecayStacks <= 0 || currentMaxDecayStacks <= 0)
        {
            return;
        }

        int key = enemy.GetInstanceID();
        int appliedSoFar = 0;
        appliedDecayPerEnemy.TryGetValue(key, out appliedSoFar);
        int room = currentMaxDecayStacks - appliedSoFar;
        if (room <= 0)
        {
            return;
        }

        int toAdd = Mathf.Min(room, currentDecayStacks);
        if (toAdd <= 0)
        {
            return;
        }

        StatusController statusController = enemy.GetComponent<StatusController>() ?? enemy.GetComponentInParent<StatusController>();
        if (statusController == null)
        {
            return;
        }

        statusController.AddStatus(StatusId.Decay, toAdd, -1f);
        appliedDecayPerEnemy[key] = appliedSoFar + toAdd;
    }

    private static bool IsTrackedStatus(StatusId id)
    {
        switch (id)
        {
            case StatusId.Burn:
            case StatusId.Scorched:
            case StatusId.Immolation:
            case StatusId.Slow:
            case StatusId.Frostbite:
            case StatusId.Freeze:
            case StatusId.Static:
            case StatusId.StaticReapply:
            case StatusId.Poison:
            case StatusId.Bleed:
            case StatusId.Wound:
                return true;
            default:
                return false;
        }
    }
}
