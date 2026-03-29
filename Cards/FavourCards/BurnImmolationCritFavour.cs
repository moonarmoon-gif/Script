using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "BurnBlazeCritFavour", menuName = "Favour Effects/Burn Blaze Crit")]
public class BurnImmolationCritFavour : FavourEffect
{
    [FormerlySerializedAs("MaxPickLimit")]
    [SerializeField, HideInInspector] private int legacyMaxPickLimit = 0;

    private PlayerStats playerStats;
    private int stacks;

    private void OnEnable()
    {
        MigratePickLimitIfNeeded();
    }

    private void OnValidate()
    {
        MigratePickLimitIfNeeded();
    }

    private void MigratePickLimitIfNeeded()
    {
        if (maxPickLimit <= 0 && legacyMaxPickLimit > 0)
        {
            maxPickLimit = legacyMaxPickLimit;
        }

        if (legacyMaxPickLimit != 0 && maxPickLimit == legacyMaxPickLimit)
        {
            legacyMaxPickLimit = 0;
        }
    }

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        if (playerStats == null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        if (playerStats == null)
        {
            return;
        }

        stacks = 1;
        playerStats.burnImmolationCanCrit = true;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (playerStats == null && player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        if (playerStats == null)
        {
            return;
        }

        stacks++;
        playerStats.burnImmolationCanCrit = true;
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (playerStats == null || stacks <= 0)
        {
            return;
        }

        stacks = Mathf.Max(0, stacks - 1);
        if (stacks <= 0)
        {
            playerStats.burnImmolationCanCrit = false;
        }
    }
}
