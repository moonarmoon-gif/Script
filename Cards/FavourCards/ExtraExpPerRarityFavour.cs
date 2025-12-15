using UnityEngine;

[CreateAssetMenu(fileName = "ExtraExpPerRarityFavour", menuName = "Favour Effects/Extra EXP Per Rarity")]
public class ExtraExpPerRarityFavour : FavourEffect
{
    [Header("Extra EXP Per Rarity Settings")]
    [Tooltip("Extra EXP granted per rarity tier (e.g. 1 = +1 EXP per rarity rank). Common=1x, Uncommon=2x, Rare=3x, etc. Boss gets no bonus.")]
    public int ExtraExp = 1;

    private int stacks = 0;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        stacks = 1;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        // Enhanced: increase ExtraExp by the same base value each time this
        // favour is upgraded.
        stacks++;
    }

    public override void OnEnemyKilled(GameObject player, GameObject enemy, FavourEffectManager manager)
    {
        if (enemy == null || stacks <= 0)
        {
            return;
        }

        EnemyExpData expData = enemy.GetComponent<EnemyExpData>();
        if (expData == null)
        {
            return;
        }

        EnemyCardTag tag = enemy.GetComponent<EnemyCardTag>() ?? enemy.GetComponentInParent<EnemyCardTag>();
        if (tag == null)
        {
            return;
        }

        // No extra EXP for Boss enemies as per design
        if (tag.rarity == CardRarity.Boss)
        {
            return;
        }

        // Map rarity to a 1-based rank so Common=1, Uncommon=2, Rare=3, ...
        int rarityIndex = Mathf.Max(0, (int)tag.rarity);
        int rarityRank = rarityIndex + 1;
        int bonusPerStack = Mathf.Max(0, ExtraExp);
        int totalBonus = rarityRank * bonusPerStack * stacks;
        if (totalBonus <= 0)
        {
            return;
        }

        GameObject playerObj = player;
        if (playerObj == null)
        {
            playerObj = GameObject.FindGameObjectWithTag("Player");
        }

        if (playerObj == null)
        {
            return;
        }

        PlayerLevel level = playerObj.GetComponent<PlayerLevel>();
        if (level == null)
        {
            return;
        }

        level.GainExperience(totalBonus);
    }
}
