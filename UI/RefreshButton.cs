using UnityEngine;
using UnityEngine.UI;

public class RefreshButton : MonoBehaviour
{
    public Button Button;

    public int AvailableRefreshPerLevelUp = 1;

    public float RefreshedCardDisplayDelay = 0.2f;

    private void OnEnable()
    {
        if (Button != null)
        {
            Button.onClick.AddListener(OnClick);
        }

        if (CardSelectionManager.Instance != null)
        {
            CardSelectionManager.Instance.ConfigureRefresh(AvailableRefreshPerLevelUp, RefreshedCardDisplayDelay);
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
    }

    private void OnClick()
    {
        CardSelectionManager manager = CardSelectionManager.Instance;
        if (manager == null)
        {
            return;
        }

        manager.ConfigureRefresh(AvailableRefreshPerLevelUp, RefreshedCardDisplayDelay);
        manager.RequestRefreshCurrentLevelUpStage();
    }
}
