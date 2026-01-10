using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SkillTreeNodeView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private Image frame;

    [Header("Colors")]
    [SerializeField] private Color lockedColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color availableColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField] private Color purchasedColor = new Color(0.25f, 0.95f, 0.35f, 1f);
    [SerializeField] private Color purchasedLockedColor = new Color(0.25f, 0.65f, 0.35f, 1f);

    private SkillTreeNodeData node;
    private SkillTreeRuntimeState state;
    private SkillTreeTooltip tooltip;

    public RectTransform RectTransform { get; private set; }
    public string NodeId => node != null ? node.id : null;

    private void Awake()
    {
        RectTransform = transform as RectTransform;

        if (frame == null)
        {
            frame = GetComponent<Image>();
        }

        if (icon == null)
        {
            Image[] images = GetComponentsInChildren<Image>(includeInactive: true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i] != frame)
                {
                    icon = images[i];
                    break;
                }
            }
        }
    }

    public void Initialize(SkillTreeNodeData nodeData, SkillTreeRuntimeState runtimeState, SkillTreeTooltip tooltipUi)
    {
        node = nodeData;
        state = runtimeState;
        tooltip = tooltipUi;

        if (icon != null)
        {
            icon.sprite = node != null ? node.icon : null;
            icon.enabled = icon.sprite != null;
        }

        Refresh();
    }

    public void Refresh()
    {
        if (frame == null || node == null)
        {
            return;
        }

        if (icon != null)
        {
            icon.sprite = node.icon;
            icon.enabled = icon.sprite != null;
        }

        bool purchased = state != null && state.IsPurchased(node.id);
        bool canPurchase = state != null && state.CanPurchase(node.id);
        bool canUnpurchase = purchased && state != null && state.CanUnpurchase(node.id);

        if (purchased)
        {
            frame.color = canUnpurchase ? purchasedColor : purchasedLockedColor;
        }
        else if (canPurchase)
        {
            frame.color = availableColor;
        }
        else
        {
            frame.color = lockedColor;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (node == null || state == null)
        {
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (SkillTreeUI.Instance != null)
            {
                SkillTreeUI.Instance.HandleNodeClick(node.id, leftClick: true);
            }
            else
            {
                state.Purchase(node.id);
            }
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (SkillTreeUI.Instance != null)
            {
                SkillTreeUI.Instance.HandleNodeClick(node.id, leftClick: false);
            }
            else
            {
                state.Unpurchase(node.id);
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltip == null || node == null)
        {
            return;
        }

        tooltip.Show(node, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltip != null)
        {
            tooltip.Hide();
        }
    }
}
