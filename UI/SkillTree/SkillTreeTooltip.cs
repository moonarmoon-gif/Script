using TMPro;
using UnityEngine;

public class SkillTreeTooltip : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Vector2 screenOffset = new Vector2(16f, -16f);

    [SerializeField] private bool useFixedPosition = true;
    [SerializeField] private Vector2 fixedScreenPosition = new Vector2(260f, 220f);

    private RectTransform rt;

    private void Awake()
    {
        rt = transform as RectTransform;
        Hide();
    }

    public void Show(SkillTreeNodeData node, Vector2 screenPos)
    {
        if (root == null || node == null)
        {
            return;
        }

        root.SetActive(true);

        if (titleText != null)
        {
            titleText.text = node.title;
        }

        if (bodyText != null)
        {
            bodyText.text = BuildBody(node);
        }

        if (rt != null)
        {
            if (useFixedPosition)
            {
                rt.position = fixedScreenPosition;
            }
            else
            {
                rt.position = screenPos + screenOffset;
            }
        }
    }

    public void Hide()
    {
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    private string BuildBody(SkillTreeNodeData node)
    {
        string desc = string.IsNullOrEmpty(node.description) ? "" : node.description;

        string effects = "";
        if (node.effects != null && node.effects.Count > 0)
        {
            for (int i = 0; i < node.effects.Count; i++)
            {
                SkillTreeEffect e = node.effects[i];
                if (e == null) continue;

                string line = DescribeEffect(e);
                if (!string.IsNullOrEmpty(line))
                {
                    effects += (effects.Length == 0 ? "" : "\n") + line;
                }
            }
        }

        if (desc.Length > 0 && effects.Length > 0)
        {
            return desc + "\n\n" + effects;
        }

        if (effects.Length > 0)
        {
            return effects;
        }

        return desc;
    }

    private string DescribeEffect(SkillTreeEffect e)
    {
        switch (e.stat)
        {
            case SkillTreeStat.AttackFlat:
                return "+" + e.floatValue + " Attack";
            case SkillTreeStat.ManaRegenFlat:
                return "+" + e.floatValue + " Mana Regen";
            case SkillTreeStat.AttackSpeedPercent:
                return "+" + e.floatValue + "% Attack Speed";
            case SkillTreeStat.DamageMultiplierPercent:
                return "+" + e.floatValue + "% Damage";
            case SkillTreeStat.MaxHealthFlat:
                return "+" + e.floatValue + " Max Health";
            case SkillTreeStat.MaxManaFlat:
                return "+" + e.floatValue + " Max Mana";
            case SkillTreeStat.FocusStacks:
                return "+" + e.intValue + " Focus";
        }

        return string.Empty;
    }
}
