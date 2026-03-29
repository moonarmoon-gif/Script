using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "SlowBurdenWeakFavour", menuName = "Favour Effects/Slow Burden Weak")]
public class SlowBurdenWeakFavour : FavourEffect
{
    [Header("Slow Burden Weak Settings")]
    public int BurdenStack = 1;
    public int WeakStack = 1;

    [Header("Enhanced")]
    public int BonusBurdenStack = 0;
    public int BonusWeakStack = 0;

    [FormerlySerializedAs("MaxPickLimit")]
    [SerializeField, HideInInspector] private int legacyMaxPickLimit = 0;

    private int sourceKey;

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
        sourceKey = Mathf.Abs(GetInstanceID());
        if (sourceKey == 0)
        {
            sourceKey = 1;
        }
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        BurdenStack += Mathf.Max(0, BonusBurdenStack);
        WeakStack += Mathf.Max(0, BonusWeakStack);
    }

    public override void OnStatusApplied(GameObject player, GameObject enemy, StatusId statusId, FavourEffectManager manager)
    {
        if (statusId != StatusId.Slow)
        {
            return;
        }

        if (enemy == null)
        {
            return;
        }

        StatusController status = enemy.GetComponent<StatusController>() ?? enemy.GetComponentInParent<StatusController>();
        if (status == null)
        {
            return;
        }

        int burden = Mathf.Max(0, BurdenStack);
        if (burden > 0)
        {
            status.AddStatus(StatusId.Burden, burden, -1f, 0f, null, sourceKey);
        }

        int weak = Mathf.Max(0, WeakStack);
        if (weak > 0)
        {
            status.AddStatus(StatusId.Weak, weak, -1f, 0f, null, sourceKey);
        }
    }
}
