using UnityEngine;

/// <summary>
/// C�mara estilo Super Mario Bros (1985):
/// - Sigue horizontalmente con "zona muerta" y sin retroceder (one-way).
/// - Eje Y bloqueado por defecto (opcional seguir con zona muerta vertical).
/// - Clampea a l�mites del nivel usando un Collider2D como bounds.
/// Colocar en la Main Camera (Orthographic).
/// </summary>
[DisallowMultipleComponent]
public class CameraFollow1985 : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("World Bounds (Collider2D)")]
    [Tooltip("Un BoxCollider2D (u otro Collider2D) que define los l�mites del nivel.")]
    [SerializeField] private Collider2D worldBounds;

    [Header("Dead Zone (en unidades del mundo)")]
    [Tooltip("Distancia desde el centro de la c�mara hacia la IZQUIERDA antes de que empiece a mover a la izquierda (no se usa si preventBacktracking=true).")]
    [SerializeField] private float leftMargin = 4f;
    [Tooltip("Distancia desde el centro de la c�mara hacia la DERECHA antes de que empiece a mover a la derecha (�til para empujar la c�mara con el jugador).")]
    [SerializeField] private float rightMargin = 3f;
    [Tooltip("Distancia desde el centro hacia ARRIBA para activar movimiento vertical (si lockVertical=false).")]
    [SerializeField] private float topMargin = 2f;
    [Tooltip("Distancia desde el centro hacia ABAJO para activar movimiento vertical (si lockVertical=false).")]
    [SerializeField] private float bottomMargin = 2f;

    [Header("Comportamiento")]
    [Tooltip("Evitar que la c�mara se mueva hacia atr�s. Recomendado para estilo SMB.")]
    [SerializeField] private bool preventBacktracking = true;
    [Tooltip("Bloquear el seguimiento vertical (estilo SMB original).")]
    [SerializeField] private bool lockVertical = true;
    [Tooltip("Suavizado del movimiento (menor = m�s reactivo).")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float smoothTime = 0.12f;

    // Estado interno
    private Camera cam;
    private float velX, velY;
    private float startY;
    private float maxXReached;

    private void Reset()
    {
        // Intento autollenar target con Player si existe
        if (target == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player) target = player.transform;
        }
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam != null && !cam.orthographic)
        {
            Debug.LogWarning("[CameraFollow1985] La c�mara debe ser Orthographic para 2D.");
        }
    }

    private void Start()
    {
        startY = transform.position.y;
        maxXReached = transform.position.x;
    }

    private void LateUpdate()
    {
        if (target == null || cam == null) return;

        Vector3 camPos = transform.position;
        float desiredX = camPos.x;
        float desiredY = camPos.y;

        // --- Horizontal (zona muerta + no retroceso) ---
        float tx = target.position.x;

        // Empuja la c�mara cuando el target cruza la "pared" derecha de la zona muerta
        if (tx > camPos.x + rightMargin)
        {
            desiredX = tx - rightMargin;
        }
        // Permitir ir a la izquierda solo si NO se previene retroceso
        else if (!preventBacktracking && tx < camPos.x - leftMargin)
        {
            desiredX = tx + leftMargin;
        }
        else
        {
            desiredX = camPos.x; // dentro de zona muerta, no mover X
        }

        // No retroceder (mantener el X m�ximo alcanzado)
        if (preventBacktracking)
        {
            desiredX = Mathf.Max(desiredX, maxXReached);
        }

        // --- Vertical (bloqueado por defecto, opcional con zona muerta) ---
        if (lockVertical)
        {
            desiredY = startY;
        }
        else
        {
            float ty = target.position.y;
            if (ty > camPos.y + topMargin)
                desiredY = ty - topMargin;
            else if (ty < camPos.y - bottomMargin)
                desiredY = ty + bottomMargin;
            else
                desiredY = camPos.y;
        }

        // --- Suavizado ---
        float newX = Mathf.SmoothDamp(camPos.x, desiredX, ref velX, smoothTime);
        float newY = Mathf.SmoothDamp(camPos.y, desiredY, ref velY, smoothTime);

        Vector3 newPos = new Vector3(newX, newY, camPos.z);

        // --- Clamping a bounds del mundo (si se proporcionan) ---
        if (worldBounds != null)
        {
            Bounds b = worldBounds.bounds;
            float extY = cam.orthographicSize;
            float extX = extY * cam.aspect;

            // Evita que la c�mara muestre fuera de los l�mites
            float minX = b.min.x + extX;
            float maxX = b.max.x - extX;
            float minY = b.min.y + extY;
            float maxY = b.max.y - extY;

            // Si el nivel es m�s peque�o que la vista, centra.
            if (minX > maxX) newPos.x = (b.min.x + b.max.x) * 0.5f;
            else newPos.x = Mathf.Clamp(newPos.x, minX, maxX);

            if (minY > maxY) newPos.y = (b.min.y + b.max.y) * 0.5f;
            else newPos.y = Mathf.Clamp(newPos.y, minY, maxY);
        }

        transform.position = newPos;

        // Actualiza el m�ximo X alcanzado despu�s de clamping
        if (preventBacktracking)
            maxXReached = Mathf.Max(maxXReached, transform.position.x);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) return;

        Gizmos.color = Color.yellow;

        // Dibuja rect�ngulo de zona muerta relativo al centro de la c�mara
        Vector3 c = transform.position;
        Vector3 right = new Vector3(rightMargin, 0f, 0f);
        Vector3 left = new Vector3(-leftMargin, 0f, 0f);
        Vector3 up = new Vector3(0f, topMargin, 0f);
        Vector3 down = new Vector3(0f, -bottomMargin, 0f);

        Vector3 p1 = c + new Vector3(-leftMargin, topMargin, 0);
        Vector3 p2 = c + new Vector3(rightMargin, topMargin, 0);
        Vector3 p3 = c + new Vector3(rightMargin, -bottomMargin, 0);
        Vector3 p4 = c + new Vector3(-leftMargin, -bottomMargin, 0);

        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p4);
        Gizmos.DrawLine(p4, p1);
    }
#endif
}
