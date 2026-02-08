using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class StatusIcon : MonoBehaviour
{
    [Serializable]
    private class StatusIconBinding
    {
        public StatusId statusId;
        public RectTransform iconRoot;
        public TextMeshProUGUI stackText;
        public bool showStackText;
    }

    [SerializeField] private RectTransform iconsContainer;
    [SerializeField] private RectTransform iconsBoundary;
    [SerializeField] private float iconSpacing = 2f;
    [SerializeField] private float minIconScale = 0.5f;
    [SerializeField] private bool showStackTextAtOneStack;
    [SerializeField] private List<StatusIconBinding> statusIcons = new List<StatusIconBinding>();

    private StatusController playerStatusController;
    private int layoutHash;

    private void Awake()
    {
        TryAutoWireStatusIcons();
    }

    private void Update()
    {
        UpdateStatusIcons();
    }

    private StatusController GetPlayerStatusController()
    {
        if (playerStatusController != null) return playerStatusController;
        AdvancedPlayerController player = AdvancedPlayerController.Instance;
        if (player == null) player = FindObjectOfType<AdvancedPlayerController>();
        if (player == null) return null;
        playerStatusController = player.GetComponent<StatusController>();
        return playerStatusController;
    }

    private void UpdateStatusIcons()
    {
        if (iconsContainer == null && iconsBoundary != null)
        {
            iconsContainer = iconsBoundary;
        }
        if (iconsContainer == null) return;
        StatusController statusController = GetPlayerStatusController();
        if (statusController == null) return;

        int hash = 17;
        int activeCount = 0;

        for (int i = 0; i < statusIcons.Count; i++)
        {
            StatusIconBinding binding = statusIcons[i];
            if (binding == null || binding.iconRoot == null) continue;

            int stacks = statusController.GetStacks(binding.statusId);
            bool active = stacks > 0;

            if (binding.iconRoot.gameObject.activeSelf != active)
            {
                binding.iconRoot.gameObject.SetActive(active);
            }

            if (binding.stackText != null)
            {
                bool showText = active && binding.showStackText && (showStackTextAtOneStack || stacks > 1);
                if (binding.stackText.enabled != showText) binding.stackText.enabled = showText;
                if (showText) binding.stackText.text = "x " + stacks;
            }

            if (active)
            {
                activeCount++;
                unchecked
                {
                    hash = (hash * 31) + (int)binding.statusId;
                    hash = (hash * 31) + stacks;
                }
            }
        }

        if (hash != layoutHash)
        {
            layoutHash = hash;
            LayoutStatusIcons(activeCount);
        }
    }

    private void TryAutoWireStatusIcons()
    {
        if (iconsContainer == null)
        {
            Transform t = transform.Find("Background/Icons");
            if (t == null) t = transform.Find("BackGround/Icons");
            if (t == null) t = transform.Find("Icons");
            if (t != null) iconsContainer = t as RectTransform;
        }

        if (iconsContainer == null && iconsBoundary != null)
        {
            iconsContainer = iconsBoundary;
        }

        if (iconsContainer == null) return;
        if (iconsBoundary == null) iconsBoundary = iconsContainer;
        if (statusIcons == null) statusIcons = new List<StatusIconBinding>();

        HashSet<RectTransform> existingRoots = new HashSet<RectTransform>();
        for (int i = 0; i < statusIcons.Count; i++)
        {
            StatusIconBinding binding = statusIcons[i];
            if (binding == null || binding.iconRoot == null)
            {
                continue;
            }

            existingRoots.Add(binding.iconRoot);
            if (binding.stackText == null)
            {
                binding.stackText = binding.iconRoot.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        List<RectTransform> scanRoots = new List<RectTransform>(2);
        scanRoots.Add(iconsContainer);
        if (iconsBoundary != null && iconsBoundary != iconsContainer)
        {
            scanRoots.Add(iconsBoundary);
        }

        for (int rootIndex = 0; rootIndex < scanRoots.Count; rootIndex++)
        {
            RectTransform root = scanRoots[rootIndex];
            if (root == null)
            {
                continue;
            }

            RectTransform[] children = root.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                RectTransform child = children[i];
                if (child == null || child == root) continue;
                if (existingRoots.Contains(child)) continue;
                if (!TryParseStatusIdFromIconName(child.name, out StatusId statusId)) continue;

                TextMeshProUGUI stackText = child.GetComponentInChildren<TextMeshProUGUI>(true);
                statusIcons.Add(new StatusIconBinding
                {
                    statusId = statusId,
                    iconRoot = child,
                    stackText = stackText,
                    showStackText = stackText != null,
                });
                existingRoots.Add(child);
            }
        }

        EnsureIconsAreWithinBoundary();
    }

    private void EnsureIconsAreWithinBoundary()
    {
        if (iconsBoundary == null || statusIcons == null)
        {
            return;
        }

        for (int i = 0; i < statusIcons.Count; i++)
        {
            StatusIconBinding binding = statusIcons[i];
            if (binding == null || binding.iconRoot == null)
            {
                continue;
            }

            if (binding.iconRoot.parent != iconsBoundary)
            {
                binding.iconRoot.SetParent(iconsBoundary, false);
            }
        }
    }

    private bool TryParseStatusIdFromIconName(string iconName, out StatusId statusId)
    {
        statusId = default;
        if (string.IsNullOrEmpty(iconName)) return false;

        string name = iconName;
        int paren = name.IndexOf('(');
        if (paren > 0)
        {
            name = name.Substring(0, paren);
        }

        name = name.Trim();

        if (name.EndsWith("StatusIcon", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(0, name.Length - 10);
        }
        if (name.EndsWith("Icon", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(0, name.Length - 4);
        }
        if (name.EndsWith("Status", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(0, name.Length - 6);
        }

        string cleaned = StripNonAlphanumeric(name);
        if (Enum.TryParse(cleaned, true, out statusId))
        {
            return true;
        }

        Array values = Enum.GetValues(typeof(StatusId));
        for (int i = 0; i < values.Length; i++)
        {
            StatusId candidate = (StatusId)values.GetValue(i);
            string candidateName = StripNonAlphanumeric(candidate.ToString());
            if (string.IsNullOrEmpty(candidateName))
            {
                continue;
            }

            if (cleaned.IndexOf(candidateName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                statusId = candidate;
                return true;
            }
        }

        return false;
    }

    private string StripNonAlphanumeric(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private void LayoutStatusIcons(int activeCount)
    {
        if (iconsContainer == null || iconsBoundary == null) return;
        if (activeCount <= 0) return;

        EnsureIconsAreWithinBoundary();

        float availableWidth = iconsBoundary.rect.width;
        float availableHeight = iconsBoundary.rect.height;
        if (availableWidth <= 0f) return;

        float totalWidth = 0f;
        float maxHeight = 0f;

        for (int i = 0; i < statusIcons.Count; i++)
        {
            StatusIconBinding binding = statusIcons[i];
            if (binding == null || binding.iconRoot == null || !binding.iconRoot.gameObject.activeSelf) continue;
            totalWidth += binding.iconRoot.rect.width;
            maxHeight = Mathf.Max(maxHeight, binding.iconRoot.rect.height);
        }

        totalWidth += Mathf.Max(0, activeCount - 1) * Mathf.Max(0f, iconSpacing);
        if (totalWidth <= 0f) return;

        float scaleW = availableWidth / totalWidth;
        float scaleH = (maxHeight > 0f && availableHeight > 0f) ? (availableHeight / maxHeight) : 1f;
        float scaleFit = Mathf.Min(scaleW, scaleH, 1f);
        float scale;
        if (scaleFit >= minIconScale)
        {
            scale = Mathf.Clamp(scaleFit, minIconScale, 1f);
        }
        else
        {
            scale = Mathf.Clamp(scaleFit, 0.01f, 1f);
        }

        float x = 0f;

        for (int i = 0; i < statusIcons.Count; i++)
        {
            StatusIconBinding binding = statusIcons[i];
            if (binding == null || binding.iconRoot == null || !binding.iconRoot.gameObject.activeSelf) continue;

            RectTransform rt = binding.iconRoot;

            if (rt.anchorMin.x != 0f || rt.anchorMax.x != 0f || rt.anchorMin.y != 0.5f || rt.anchorMax.y != 0.5f)
            {
                Vector2 a = rt.anchorMin; a.x = 0f; a.y = 0.5f; rt.anchorMin = a;
                a = rt.anchorMax; a.x = 0f; a.y = 0.5f; rt.anchorMax = a;
            }

            rt.localScale = new Vector3(scale, scale, 1f);
            float w = rt.rect.width * scale;
            float pivotX = rt.pivot.x;
            Vector2 pos = rt.anchoredPosition;
            pos.x = x + w * pivotX;
            pos.y = 0f;
            rt.anchoredPosition = pos;
            x += w + Mathf.Max(0f, iconSpacing) * scale;
        }
    }
}
