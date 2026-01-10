using UnityEngine;

[CreateAssetMenu(fileName = "ExtraExpPerRarityFavour", menuName = "Favour Effects/Extra EXP Per Rarity")]
public class ExtraExpPerRarityFavour : FavourEffect
{
    [Header("Extra EXP Per Rarity Settings")]
    [Tooltip("Extra EXP granted per rarity tier (e.g. 1 = +1 EXP per rarity rank). Common=1x, Uncommon=2x, Rare=3x, etc.")]
    public int ExtraExp = 1;

    [Tooltip("Extra EXP granted when killing a Boss enemy.")]
    public int BossExtraExp = 100;

    private int stacks = 0;

    private static int globalStacks = 0;
    private static int globalExtraExpPerRank = 0;
    private static int globalBossExtraExp = 0;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        stacks = 1;
        RecomputeGlobals();
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        // Enhanced: increase ExtraExp by the same base value each time this
        // favour is upgraded.
        stacks++;
        RecomputeGlobals();
    }

    private void RecomputeGlobals()
    {
        int clampedExtra = Mathf.Max(0, ExtraExp);
        globalExtraExpPerRank = clampedExtra;
        globalBossExtraExp = Mathf.Max(0, BossExtraExp);
        globalStacks = Mathf.Max(0, stacks);
    }

    public static int GetBonusExpForRarity(CardRarity rarity)
    {
        if (globalStacks <= 0) return 0;

        if (rarity == CardRarity.Boss)
        {
            if (globalBossExtraExp <= 0) return 0;
            return Mathf.Max(0, globalBossExtraExp * globalStacks);
        }

        if (globalExtraExpPerRank <= 0) return 0;

        int rarityIndex = Mathf.Max(0, (int)rarity);
        int rarityRank = rarityIndex + 1;
        int totalBonusPerRank = globalExtraExpPerRank * globalStacks;
        int total = rarityRank * totalBonusPerRank;
        return Mathf.Max(0, total);
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        ResetRunState();
    }

    public static void ResetRunState()
    {
        globalStacks = 0;
        globalExtraExpPerRank = 0;
        globalBossExtraExp = 0;
    }
}
