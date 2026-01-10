using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ManaOnStatusDeathFavour", menuName = "Favour Effects/Mana On Status Death")]
public class ManaOnStatusDeathFavour : FavourEffect
{
    [Header("Mana On Status Death Settings")]
    [Tooltip("Amount of mana (max and current) gained per qualifying enemy kill.")]
    public float ManaIncrease = 2f;

    [Tooltip("Maximum total mana this favour can grant over the run.")]
    public float MaxMana = 50f;

    [Header("Enhanced")]
    [Tooltip("Additional mana gained per qualifying kill when this favour is enhanced.")]
    public float BonusManaIncrease = 2f;

    [Tooltip("Additional maximum mana this favour can grant when enhanced.")]
    public float BonusMaxMana = 50f;

    // If the enemy dies and the status gets applied in the "same attack" but after death,
    // we still want to count it. This window is intentionally small.
    private const float SameAttackWindowSeconds = 0.25f;

    private PlayerMana playerMana;
    private float currentManaIncrease;
    private float currentMaxExtraMana;
    private float totalGrantedMana;
    private bool initialized;

    // Tracks: enemyInstanceId -> time of last qualifying status application.
    private readonly Dictionary<int, float> lastQualifyingStatusAppliedTime = new Dictionary<int, float>();

    // Tracks: enemyInstanceId -> time OnEnemyKilled was received but enemy did NOT qualify at that instant.
    private readonly Dictionary<int, float> pendingKillTime = new Dictionary<int, float>();

    // Tracks: enemyInstanceId -> time we already granted (prevents double grants if multiple statuses fire).
    private readonly Dictionary<int, float> grantedTime = new Dictionary<int, float>();

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        playerMana = null;
        initialized = false;

        lastQualifyingStatusAppliedTime.Clear();
        pendingKillTime.Clear();
        grantedTime.Clear();

        if (player == null)
        {
            return;
        }

        playerMana = player.GetComponent<PlayerMana>() ?? player.GetComponentInChildren<PlayerMana>();
        if (playerMana == null)
        {
            return;
        }

        currentManaIncrease = Mathf.Max(0f, ManaIncrease);
        currentMaxExtraMana = Mathf.Max(0f, MaxMana);
        totalGrantedMana = 0f;
        initialized = true;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        EnsureInitialized(player);

        if (playerMana == null)
        {
            return;
        }

        currentManaIncrease += Mathf.Max(0f, BonusManaIncrease);
        currentMaxExtraMana += Mathf.Max(0f, BonusMaxMana);
    }

    public override void OnStatusApplied(GameObject player, GameObject enemy, StatusId statusId, FavourEffectManager manager)
    {
        EnsureInitialized(player);

        if (playerMana == null || enemy == null)
        {
            return;
        }

        if (!IsQualifyingStatus(statusId))
        {
            return;
        }

        float now = Time.time;
        int enemyId = enemy.GetInstanceID();

        PruneOldEntries(now);

        // Record that a qualifying status was applied very recently to this enemy.
        lastQualifyingStatusAppliedTime[enemyId] = now;

        // If the enemy already died (kill callback may have happened earlier in the same hit),
        // and we have a pending kill for this enemy within the small window, grant now.
        EnemyHealth eh = enemy.GetComponent<EnemyHealth>() ?? enemy.GetComponentInParent<EnemyHealth>();
        if (eh != null && !eh.IsAlive)
        {
            if (!HasGranted(enemyId) && pendingKillTime.TryGetValue(enemyId, out float killT))
            {
                if (now - killT <= SameAttackWindowSeconds)
                {
                    TryGrantMana(enemyId);
                    pendingKillTime.Remove(enemyId);
                }
            }
        }
    }

    public override void OnEnemyKilled(GameObject player, GameObject enemy, FavourEffectManager manager)
    {
        EnsureInitialized(player);

        if (playerMana == null || enemy == null)
        {
            return;
        }

        float now = Time.time;
        int enemyId = enemy.GetInstanceID();

        PruneOldEntries(now);

        if (HasGranted(enemyId))
        {
            return;
        }

        StatusController status = enemy.GetComponent<StatusController>() ?? enemy.GetComponentInParent<StatusController>();

        bool qualifiesNow = false;

        // Primary: enemy is actually affected by qualifying statuses at kill time.
        if (status != null && HasQualifyingStatus(status))
        {
            qualifiesNow = true;
        }
        else
        {
            // Secondary: status was applied essentially "this same attack", but ordering made it arrive after death.
            if (lastQualifyingStatusAppliedTime.TryGetValue(enemyId, out float t))
            {
                if (now - t <= SameAttackWindowSeconds)
                {
                    qualifiesNow = true;
                }
            }
        }

        if (qualifiesNow)
        {
            TryGrantMana(enemyId);
            pendingKillTime.Remove(enemyId);
            return;
        }

        // Not qualifying right now — store as pending so a status application that occurs a moment later
        // (same hit ordering) can still grant.
        pendingKillTime[enemyId] = now;
    }

    private void EnsureInitialized(GameObject player)
    {
        if (playerMana == null && player != null)
        {
            playerMana = player.GetComponent<PlayerMana>() ?? player.GetComponentInChildren<PlayerMana>();
        }

        if (initialized)
        {
            return;
        }

        currentManaIncrease = Mathf.Max(0f, ManaIncrease);
        currentMaxExtraMana = Mathf.Max(0f, MaxMana);
        totalGrantedMana = Mathf.Max(0f, totalGrantedMana);
        initialized = true;
    }

    private void TryGrantMana(int enemyId)
    {
        if (currentManaIncrease <= 0f || currentMaxExtraMana <= 0f)
        {
            return;
        }

        float remainingRoom = Mathf.Max(0f, currentMaxExtraMana - totalGrantedMana);
        if (remainingRoom <= 0f)
        {
            return;
        }

        float grant = Mathf.Min(currentManaIncrease, remainingRoom);
        if (grant <= 0f)
        {
            return;
        }

        playerMana.IncreaseMaxMana(grant);
        totalGrantedMana += grant;
        grantedTime[enemyId] = Time.time;
    }

    private bool HasGranted(int enemyId)
    {
        return grantedTime.ContainsKey(enemyId);
    }

    private void PruneOldEntries(float now)
    {
        // Keep these short-lived so the dictionaries don't grow forever.
        PruneDict(lastQualifyingStatusAppliedTime, now, SameAttackWindowSeconds * 2f);
        PruneDict(pendingKillTime, now, SameAttackWindowSeconds * 2f);

        // Granted entries can live a bit longer but still prune eventually.
        PruneDict(grantedTime, now, 10f);
    }

    private static void PruneDict(Dictionary<int, float> dict, float now, float maxAgeSeconds)
    {
        if (dict.Count == 0)
        {
            return;
        }

        // Avoid allocation-heavy LINQ; do a manual sweep.
        // Collect keys to remove in a temp list sized conservatively.
        List<int> toRemove = null;

        foreach (var kvp in dict)
        {
            if (now - kvp.Value > maxAgeSeconds)
            {
                if (toRemove == null)
                {
                    toRemove = new List<int>();
                }
                toRemove.Add(kvp.Key);
            }
        }

        if (toRemove == null)
        {
            return;
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            dict.Remove(toRemove[i]);
        }
    }

    private static bool HasQualifyingStatus(StatusController status)
    {
        if (status == null)
        {
            return false;
        }

        // Matches your requested list:
        // burn, slow, freeze, immolation, poison, wound, bleed, static, staticreapply
        return status.HasStatus(StatusId.Burn)
            || status.HasStatus(StatusId.Slow)
            || status.HasStatus(StatusId.Freeze)
            || status.HasStatus(StatusId.Immolation)
            || status.HasStatus(StatusId.Poison)
            || status.HasStatus(StatusId.Wound)
            || status.HasStatus(StatusId.Bleed)
            || status.HasStatus(StatusId.Static)
            || status.HasStatus(StatusId.StaticReapply);
    }

    private static bool IsQualifyingStatus(StatusId id)
    {
        switch (id)
        {
            case StatusId.Burn:
            case StatusId.Slow:
            case StatusId.Freeze:
            case StatusId.Immolation:
            case StatusId.Poison:
            case StatusId.Wound:
            case StatusId.Bleed:
            case StatusId.Static:
            case StatusId.StaticReapply:
                return true;
            default:
                return false;
        }
    }
}
