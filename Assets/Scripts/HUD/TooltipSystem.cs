using TMPro;
using UnityEngine;

public class TooltipSystem : MonoBehaviour
{
    public static TooltipSystem Instance;

    [Header("UI References")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI tooltipText;
    public float showDelay = 0.5f;

    private float delayTimer;
    private bool isHovering;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        Hide();
    }

    void Update()
    {
        if (isHovering)
        {
            delayTimer -= Time.deltaTime;
            if (delayTimer <= 0)
            {
                tooltipPanel.SetActive(true);
            }
        }
    }

    public static void Show(string content)
    {
        if (Instance == null) return;

        Instance.delayTimer = Instance.showDelay;
        Instance.isHovering = true;
        Instance.tooltipText.text = content;
    }

    public static void Hide()
    {
        if (Instance == null) return;

        Instance.isHovering = false;
        Instance.tooltipPanel.SetActive(false);
    }
}
