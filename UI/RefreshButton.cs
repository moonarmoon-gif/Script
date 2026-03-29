using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RefreshButton : MonoBehaviour
{
    public Button Button;

    public float AvailableRefreshPerLevelUp = 1f;

    public int RefreshAmountOnStart = 0;

    public TextMeshProUGUI RefreshAmountText;

    public float RefreshedCardDisplayDelay = 0.2f;

    private void OnEnable()
    {
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
        Button.interactable = manager != null && manager.CanRefreshCurrentLevelUpStage();

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
