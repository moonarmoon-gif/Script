using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StatusDecayFavour", menuName = "Favour Effects/Status Decay")] 
public class StatusDecayFavour : FavourEffect
{
    [Header("Status Decay Settings")]
    [Tooltip("Decay stacks granted the first time an enemy receives each tracked status effect.")]
    public int DecayStacksPerStatus = 5;

    [Header("Enhanced")]
    [Tooltip("Additional Decay stacks granted per tracked status when enhanced.")]
    public int BonusDecayStacks = 5;

    private int currentDecayStacksPerStatus;

    // Tracks which status IDs have already triggered Decay for each enemy.
    // Key: enemy instance ID, Value: set of StatusIds that have already
    // awarded Decay to that enemy.
    private readonly Dictionary<int, HashSet<StatusId>> triggeredPerEnemy = new Dictionary<int, HashSet<StatusId>>();

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentDecayStacksPerStatus = Mathf.Max(0, DecayStacksPerStatus);
        triggeredPerEnemy.Clear();
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentDecayStacksPerStatus += Mathf.Max(0, BonusDecayStacks);
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        triggeredPerEnemy.Clear();
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

        if (currentDecayStacksPerStatus <= 0)
        {
            return;
        }

        int key = enemy.GetInstanceID();
        if (!triggeredPerEnemy.TryGetValue(key, out var set))
        {
            set = new HashSet<StatusId>();
            triggeredPerEnemy[key] = set;
        }

        // Only grant Decay once per enemy per tracked status type.
        if (set.Contains(statusId))
        {
            return;
        }

        set.Add(statusId);

        StatusController statusController = enemy.GetComponent<StatusController>() ?? enemy.GetComponentInParent<StatusController>();
        if (statusController == null)
        {
            return;
        }

        statusController.AddStatus(StatusId.Decay, currentDecayStacksPerStatus, -1f);
    }

    private static bool IsTrackedStatus(StatusId id)
    {
        switch (id)
        {
            case StatusId.Burn:
            case StatusId.Immolation:
            case StatusId.Slow:
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
