using System.Collections.Generic;
using UnityEngine;

public class SkillTreeStatApplier : MonoBehaviour
{
    [SerializeField] private SkillTreeRuntimeState state;

    private PlayerStats playerStats;
    private PlayerHealth playerHealth;
    private PlayerMana playerMana;
    private StatusController statusController;

    private float lastAttackFlat;
    private float lastManaRegenFlat;
    private float lastAttackSpeedPercent;
    private float lastDamageMultiplierPercent;
    private float lastMaxHealthFlat;
    private int lastMaxManaFlat;
    private int lastFocusStacks;

    private const int FocusSourceKey = 735113;

    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
        playerHealth = GetComponent<PlayerHealth>();
        playerMana = GetComponent<PlayerMana>();
        statusController = GetComponent<StatusController>();

        if (state == null)
        {
            state = SkillTreeRuntimeState.Instance != null ? SkillTreeRuntimeState.Instance : FindObjectOfType<SkillTreeRuntimeState>();
        }

        if (state != null)
        {
            state.OnChanged += Reapply;
        }

        Reapply();
    }

    private void OnDestroy()
    {
        if (state != null)
        {
            state.OnChanged -= Reapply;
        }
    }

    private void Reapply()
    {
        if (playerStats != null)
        {
            playerStats.baseAttack = playerStats.baseAttack - lastAttackFlat;
            playerStats.manaRegenPerSecond = playerStats.manaRegenPerSecond - lastManaRegenFlat;
            playerStats.attackSpeedPercent = playerStats.attackSpeedPercent - lastAttackSpeedPercent;
            playerStats.damageMultiplier = playerStats.damageMultiplier - (lastDamageMultiplierPercent / 100f);
        }

        if (playerHealth != null)
        {
            float newMax = playerHealth.MaxHealth - lastMaxHealthFlat;
            playerHealth.SetMaxHealth(newMax, fillToMax: false);
        }

        if (playerMana != null)
        {
            int newMaxMana = playerMana.MaxMana - lastMaxManaFlat;
            playerMana.SetMaxMana(newMaxMana, refill: false);
        }

        if (statusController != null && lastFocusStacks > 0)
        {
            statusController.ConsumeStacks(StatusId.Focus, lastFocusStacks, FocusSourceKey);
        }

        lastAttackFlat = 0f;
        lastManaRegenFlat = 0f;
        lastAttackSpeedPercent = 0f;
        lastDamageMultiplierPercent = 0f;
        lastMaxHealthFlat = 0f;
        lastMaxManaFlat = 0;
        lastFocusStacks = 0;

        if (state == null || state.TreeData == null)
        {
            return;
        }

        SkillTreeData data = state.TreeData;

        float attackFlat = 0f;
        float manaRegenFlat = 0f;
        float attackSpeedPercent = 0f;
        float damageMultiplierPercent = 0f;
        float maxHealthFlat = 0f;
        float maxManaFlat = 0f;
        int focusStacks = 0;

        IReadOnlyCollection<string> purchased = state.GetPurchasedIds();
        Dictionary<string, SkillTreeNodeData> map = data.BuildLookup();

        foreach (string id in purchased)
        {
            if (!map.TryGetValue(id, out SkillTreeNodeData node) || node == null || node.effects == null)
            {
                continue;
            }

            for (int i = 0; i < node.effects.Count; i++)
            {
                SkillTreeEffect e = node.effects[i];
                if (e == null) continue;

                switch (e.stat)
                {
                    case SkillTreeStat.AttackFlat:
                        attackFlat += e.floatValue;
                        break;
                    case SkillTreeStat.ManaRegenFlat:
                        manaRegenFlat += e.floatValue;
                        break;
                    case SkillTreeStat.AttackSpeedPercent:
                        attackSpeedPercent += e.floatValue;
                        break;
                    case SkillTreeStat.DamageMultiplierPercent:
                        damageMultiplierPercent += e.floatValue;
                        break;
                    case SkillTreeStat.MaxHealthFlat:
                        maxHealthFlat += e.floatValue;
                        break;
                    case SkillTreeStat.MaxManaFlat:
                        maxManaFlat += e.floatValue;
                        break;
                    case SkillTreeStat.FocusStacks:
                        focusStacks += Mathf.Max(0, e.intValue);
                        break;
                }
            }
        }

        if (playerStats != null)
        {
            playerStats.baseAttack = playerStats.baseAttack + attackFlat;
            playerStats.manaRegenPerSecond = playerStats.manaRegenPerSecond + manaRegenFlat;
            playerStats.attackSpeedPercent = playerStats.attackSpeedPercent + attackSpeedPercent;
            playerStats.damageMultiplier = playerStats.damageMultiplier + (damageMultiplierPercent / 100f);
        }

        if (playerHealth != null)
        {
            float oldMax = playerHealth.MaxHealth;
            float newMax = Mathf.Max(1f, oldMax + maxHealthFlat);
            playerHealth.SetMaxHealth(newMax, fillToMax: false);

            float delta = newMax - oldMax;
            if (delta > 0f && playerHealth.IsAlive)
            {
                playerHealth.Heal(delta);
            }
        }

        if (playerMana != null)
        {
            int oldMaxMana = playerMana.MaxMana;
            int add = Mathf.RoundToInt(maxManaFlat);
            int newMaxMana = Mathf.Max(0, oldMaxMana + add);
            playerMana.SetMaxMana(newMaxMana, refill: false);
        }

        if (statusController != null && focusStacks > 0)
        {
            statusController.AddStatus(StatusId.Focus, focusStacks, -1f, 0f, null, FocusSourceKey);
        }

        lastAttackFlat = attackFlat;
        lastManaRegenFlat = manaRegenFlat;
        lastAttackSpeedPercent = attackSpeedPercent;
        lastDamageMultiplierPercent = damageMultiplierPercent;
        lastMaxHealthFlat = maxHealthFlat;
        lastMaxManaFlat = Mathf.RoundToInt(maxManaFlat);
        lastFocusStacks = focusStacks;
    }
}
