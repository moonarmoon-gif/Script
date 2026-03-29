using UnityEngine;

public class FavourRarityManager : MonoBehaviour
{
    public static FavourRarityManager Instance { get; private set; }

    public int CommonRarityQuota = 10;
    public int UncommonRarityQuota = 5;
    public int RareRarityQuota = 0;
    public int EpicRarityQuota = 0;
    public int LegendaryRarityQuota = 0;

    private int totalFavourStagesDrawn = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ResetRunState()
    {
        totalFavourStagesDrawn = 0;
    }

    public CardRarity GetNextFavourRarity()
    {
        int index = totalFavourStagesDrawn;
        totalFavourStagesDrawn++;

        int commonEnd = Mathf.Max(0, CommonRarityQuota);
        int uncommonEnd = commonEnd + Mathf.Max(0, UncommonRarityQuota);
        int rareEnd = uncommonEnd + Mathf.Max(0, RareRarityQuota);
        int epicEnd = rareEnd + Mathf.Max(0, EpicRarityQuota);
        int legendaryEnd = epicEnd + Mathf.Max(0, LegendaryRarityQuota);

        if (index < commonEnd) return CardRarity.Common;
        if (index < uncommonEnd) return CardRarity.Uncommon;
        if (index < rareEnd) return CardRarity.Rare;
        if (index < epicEnd) return CardRarity.Epic;
        if (index < legendaryEnd) return CardRarity.Legendary;

        return CardRarity.Mythic;
    }
}
