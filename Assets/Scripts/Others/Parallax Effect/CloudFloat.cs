using UnityEngine;

/// <summary>
/// Anima un sprite de nubes en ESPACIO LOCAL para no interferir con el parallax del padre.
/// - Desplazamiento horizontal suave.
/// - Flotación vertical (seno).
/// - Loop horizontal opcional basado en el ancho del sprite.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CloudFloat : MonoBehaviour
{
    [Header("Velocidad horizontal")]
    [SerializeField] private float moveSpeed = 0.3f;

    [Header("Flotación vertical")]
    [SerializeField] private float floatAmplitude = 0.2f;
    [SerializeField] private float floatSpeed = 1f;

    [Header("Loop horizontal")]
    [SerializeField] private bool loopHorizontally = true;

    private Vector3 localStart;
    private float spriteWidth;
    private SpriteRenderer sr;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        localStart = transform.localPosition;

        // Tamaño del sprite en unidades del mundo (considera PPU)
        spriteWidth = sr.bounds.size.x;

        // Variación leve para que no sea tan robótico
        floatSpeed *= Random.Range(0.9f, 1.1f);
        moveSpeed *= Random.Range(0.9f, 1.1f);
    }

    void Update()
    {
        // Offset local actual respecto al origen local
        Vector3 lp = transform.localPosition;
        float dx = lp.x - localStart.x;
        float dy = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;

        // Avance horizontal
        dx += moveSpeed * Time.deltaTime;

        // Loop horizontal (desplaza cuando supera media anchura a un lado u otro)
        if (loopHorizontally && spriteWidth > 0.0001f)
        {
            float half = spriteWidth * 0.5f;
            if (dx > half) dx -= spriteWidth;
            if (dx < -half) dx += spriteWidth;
        }

        transform.localPosition = new Vector3(localStart.x + dx, localStart.y + dy, lp.z);
    }
}
