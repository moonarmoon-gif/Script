using UnityEngine;
using UnityEngine.UI;

public class SkillTreeConnectionView : MonoBehaviour
{
    [SerializeField] private Image image;
    [SerializeField] private float thickness = 6f;

    [SerializeField] private Color lockedColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color activeColor = new Color(0.15f, 0.75f, 1f, 1f);

    private RectTransform rt;
    private RectTransform a;
    private RectTransform b;

    private string aId;
    private string bId;

    private void Awake()
    {
        rt = transform as RectTransform;

        if (image == null)
        {
            image = GetComponent<Image>();
        }

        if (image != null)
        {
            image.raycastTarget = false;
        }
    }

    public void Initialize(SkillTreeNodeView nodeA, SkillTreeNodeView nodeB)
    {
        a = nodeA != null ? nodeA.RectTransform : null;
        b = nodeB != null ? nodeB.RectTransform : null;

        aId = nodeA != null ? nodeA.NodeId : null;
        bId = nodeB != null ? nodeB.NodeId : null;

        UpdateTransform();
    }

    private void UpdateTransform()
    {
        if (rt == null || a == null || b == null)
        {
            return;
        }

        Vector2 pa = a.anchoredPosition;
        Vector2 pb = b.anchoredPosition;
        Vector2 mid = (pa + pb) * 0.5f;
        Vector2 dir = pb - pa;

        float len = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        rt.anchoredPosition = mid;
        rt.sizeDelta = new Vector2(len, Mathf.Max(1f, thickness));
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    public void Refresh(SkillTreeRuntimeState state)
    {
        if (image == null)
        {
            return;
        }

        bool active = false;
        if (state != null && !string.IsNullOrEmpty(aId) && !string.IsNullOrEmpty(bId))
        {
            active = state.IsPurchased(aId) && state.IsPurchased(bId);
        }

        image.color = active ? activeColor : lockedColor;
    }
}
