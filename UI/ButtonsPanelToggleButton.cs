using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonsPanelToggleButton : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("When disabled, this toggle button does nothing.")]
    [SerializeField] private bool enableToggle = false;

    [Tooltip("Container GameObject that holds all UI buttons to show/hide. If left empty, a GameObject named 'Button' will be searched in the scene.")]
    [SerializeField] private GameObject buttonsContainer;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnClick);
        }

        if (buttonsContainer == null)
        {
            GameObject found = GameObject.Find("Button");
            if (found != null)
            {
                buttonsContainer = found;
            }
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
        }
    }

    private void OnClick()
    {
        if (!enableToggle)
        {
            return;
        }

        if (buttonsContainer == null)
        {
            return;
        }

        bool newActive = !buttonsContainer.activeSelf;
        buttonsContainer.SetActive(newActive);
    }
}
