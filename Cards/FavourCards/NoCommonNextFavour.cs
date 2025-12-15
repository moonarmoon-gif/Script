using UnityEngine;

[CreateAssetMenu(fileName = "NoCommonNextFavour", menuName = "Favour Effects/No Common Next")]
public class NoCommonNextFavour : FavourEffect
{
    [Header("No Common Next Settings")]
    [Tooltip("Number of upcoming LEVEL UPS (Core+Projectile stages) where COMMON cards are removed from both core and projectile level-up selections.")]
    public int AmountOfTimes = 1;

    private int totalRegisteredStages = 0;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        RegisterStages();
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        // Enhanced: increase the value by the same base amount each time.
        AmountOfTimes = Mathf.Max(0, AmountOfTimes);
        RegisterStages();
    }

    private void RegisterStages()
    {
        if (CardSelectionManager.Instance == null)
        {
            return;
        }

        int perLevelUp = Mathf.Max(0, AmountOfTimes);
        if (perLevelUp <= 0)
        {
            return;
        }

        // Each level-up consists of up to two LEVEL-UP STAGES (Core + Projectile).
        // To make "AmountOfTimes" mean "number of LEVEL UPS", we convert it to
        // stage counts here.
        int stagesToAdd = perLevelUp * 2;
        CardSelectionManager.Instance.RegisterNoCommonLevelUpStages(stagesToAdd);
        totalRegisteredStages += stagesToAdd;
    }
}
