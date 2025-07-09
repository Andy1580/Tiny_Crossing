using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class GradientFill : MonoBehaviour
{
    [Header("Gradient Settings")]
    public Color minColor = Color.red;
    public Color midColor = Color.yellow;
    public Color maxColor = Color.green;
    [Range(0, 1)] public float midPoint = 0.5f;

    private Image fillImage;

    void Start()
    {
        fillImage = GetComponent<Image>();
    }

    public void UpdateColor(float value)
    {
        if (value <= midPoint)
        {
            // Interpolar entre minColor y midColor
            float t = value / midPoint;
            fillImage.color = Color.Lerp(minColor, midColor, t);
        }
        else
        {
            // Interpolar entre midColor y maxColor
            float t = (value - midPoint) / (1 - midPoint);
            fillImage.color = Color.Lerp(midColor, maxColor, t);
        }
    }
}
