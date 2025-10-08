using UnityEngine;

/// <summary>
/// Efecto Parallax simple y compatible con Pixel Perfect Camera.
/// Mueve el fondo en relación al desplazamiento de la cámara.
/// </summary>
[DisallowMultipleComponent]
public class ParallaxLayer : MonoBehaviour
{
    [Tooltip("Factor de desplazamiento (0 = se mueve igual que la cámara, 1 = se queda fijo)")]
    [Range(0f, 1f)]
    public float parallaxFactor = 0.5f;

    private Transform cam;
    private Vector3 prevCamPos;

    private void Start()
    {
        cam = Camera.main.transform;
        prevCamPos = cam.position;
    }

    private void LateUpdate()
    {
        Vector3 delta = cam.position - prevCamPos;
        transform.position += new Vector3(delta.x * parallaxFactor, delta.y * parallaxFactor, 0f);
        prevCamPos = cam.position;
    }
}
