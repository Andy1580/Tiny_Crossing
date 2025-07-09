using UnityEngine;
using UnityEngine.UI;

public class PowerBarController : MonoBehaviour
{
    [Header("References")]
    public Slider powerSlider;
    public Image fillImage;
    public GradientFill gradientFill; // Referencia al nuevo script

    [Header("Settings")]
    public float minSpeed = 0.5f;
    public float maxSpeed = 2.5f;

    private bool isActive;
    private bool isIncreasing = true;
    private float currentValue;
    private float currentSpeed;
    private System.Action<float> onPowerSelected;

    void Start()
    {
        Hide();
    }

    void Update()
    {
        if (!isActive) return;

        currentValue += (isIncreasing ? 1 : -1) * currentSpeed * Time.deltaTime;

        if (currentValue >= 1f)
        {
            currentValue = 1f;
            isIncreasing = false;
        }
        else if (currentValue <= 0f)
        {
            currentValue = 0f;
            isIncreasing = true;
        }

        powerSlider.value = currentValue;
        gradientFill.UpdateColor(currentValue); // Actualizar color

        if (Input.GetMouseButtonDown(0))
        {
            isActive = false;
            onPowerSelected?.Invoke(currentValue);
        }
    }

    public void StartPowerBar(Interactable.WeaponType weaponType, System.Action<float> callback)
    {
        switch (weaponType)
        {
            case Interactable.WeaponType.FlySwatter:
                currentSpeed = minSpeed;
                break;
            case Interactable.WeaponType.Bat:
                currentSpeed = (minSpeed + maxSpeed) / 2;
                break;
            case Interactable.WeaponType.Wrench:
                currentSpeed = maxSpeed;
                break;
            default:
                currentSpeed = (minSpeed + maxSpeed) / 2;
                break;
        }

        onPowerSelected = callback;
        isActive = true;
        currentValue = 0f;
        isIncreasing = true;
        powerSlider.value = 0f;
        gradientFill.UpdateColor(0f); // Iniciar con color inicial
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
