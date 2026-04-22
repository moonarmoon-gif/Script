using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RefreshButton : MonoBehaviour
{
    public Button Button;
    [SerializeField] private CanvasGroup visibilityCanvasGroup;

    public float AvailableRefreshPerLevelUp = 1f;

    public int RefreshAmountOnStart = 0;

    public TextMeshProUGUI RefreshAmountText;

    public float RefreshedCardDisplayDelay = 0.2f;

    private void OnEnable()
    {
        if (visibilityCanvasGroup == null)
        {
            visibilityCanvasGroup = GetComponent<CanvasGroup>();
            if (visibilityCanvasGroup == null)
            {
                visibilityCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (Button != null)
        {
            Button.onClick.AddListener(OnClick);
        }

        if (CardSelectionManager.Instance != null)
        {
            CardSelectionManager.Instance.ConfigureRefresh(AvailableRefreshPerLevelUp, RefreshedCardDisplayDelay, RefreshAmountOnStart);
        }
    }

    private void OnDisable()
    {
        if (Button != null)
        {
            Button.onClick.RemoveListener(OnClick);
        }
    }

    private void Update()
    {
        if (Button == null)
        {
            return;
        }

        CardSelectionManager manager = CardSelectionManager.Instance;
        bool shouldShow = manager != null && manager.ShouldShowRefreshButton();
        if (visibilityCanvasGroup != null)
        {
            visibilityCanvasGroup.alpha = shouldShow ? 1f : 0f;
            visibilityCanvasGroup.interactable = shouldShow;
            visibilityCanvasGroup.blocksRaycasts = shouldShow;
        }

        Button.interactable = shouldShow && manager.CanRefreshCurrentLevelUpStage();

        if (RefreshAmountText != null && manager != null)
        {
            RefreshAmountText.text = $"x{Mathf.Max(0, manager.CurrentRefreshCurrencyWhole)}";
        }
    }

    private void OnClick()
    {
        CardSelectionManager manager = CardSelectionManager.Instance;
        if (manager == null)
        {
            return;
        }

        manager.ConfigureRefresh(AvailableRefreshPerLevelUp, RefreshedCardDisplayDelay, RefreshAmountOnStart);
        manager.RequestRefreshCurrentLevelUpStage();
    }
}
