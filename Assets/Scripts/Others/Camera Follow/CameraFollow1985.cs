using UnityEngine;

/// <summary>
/// Cámara estilo Super Mario Bros (1985):
/// - Sigue horizontalmente con "zona muerta" y sin retroceder (one-way).
/// - Eje Y bloqueado por defecto (opcional seguir con zona muerta vertical).
/// - Clampea a límites del nivel usando un Collider2D como bounds.
/// Colocar en la Main Camera (Orthographic).
/// </summary>
[DisallowMultipleComponent]
public class CameraFollow1985 : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("World Bounds")]
    [SerializeField] private Collider2D worldBounds;

    [Header("Dead Zone (unidades de mundo)")]
    [SerializeField] private float leftMargin = 4f;
    [SerializeField] private float rightMargin = 3f;
    [SerializeField] private float topMargin = 2f;
    [SerializeField] private float bottomMargin = 2f;

    [Header("Comportamiento")]
    [SerializeField] private bool preventBacktracking = false;
    [SerializeField] private bool lockVertical = true;
    [Range(0.01f, 0.5f)]
    [SerializeField] private float smoothTime = 0.12f;

    [Header("Suavizado y retardo")]
    [SerializeField] private float followDelay = 0.15f;

    private Camera cam;
    private float velX, velY;
    private float startY;
    private float maxXReached;
    private Vector3 prevTargetPos;
    private float moveDirX = 1f; //dirección de movimiento (1=derecha, -1=izquierda)
    private float outsideTimer = 0f;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
    }

    private void Start()
    {
        if (target == null) return;

        startY = transform.position.y;
        maxXReached = transform.position.x;
        prevTargetPos = target.position;

        //Colocar la cámara para que Tiny esté dentro del margen inicial
        Vector3 initial = ApplyBounds(ComputeDesiredPosition(transform.position, true));
        transform.position = initial;
    }

    private void LateUpdate()
    {
        if (target == null || cam == null) return;

        //1 Detectar dirección real de movimiento del target
        float deltaX = target.position.x - prevTargetPos.x;
        if (Mathf.Abs(deltaX) > 0.001f)
            moveDirX = Mathf.Sign(deltaX);

        prevTargetPos = target.position;

        //2 Calcular posición deseada
        Vector3 desired = ComputeDesiredPosition(transform.position, false);

        //3 Suavizado
        float newX = Mathf.SmoothDamp(transform.position.x, desired.x, ref velX, smoothTime);
        float newY = Mathf.SmoothDamp(transform.position.y, desired.y, ref velY, smoothTime);

        //4 Limitar a bounds
        Vector3 finalPos = ApplyBounds(new Vector3(newX, newY, transform.position.z));
        transform.position = finalPos;

        if (preventBacktracking)
            maxXReached = Mathf.Max(maxXReached, transform.position.x);
    }

    private Vector3 ComputeDesiredPosition(Vector3 camPos, bool forceSnap)
    {
        float desiredX = camPos.x;
        float desiredY = camPos.y;

        float tx = target.position.x;

        bool moveRight = moveDirX > 0;
        bool moveLeft = moveDirX < 0;

        bool outsideMargin = false;

        //Solo se evalúa el margen correspondiente a la dirección actual
        if (moveRight && tx > camPos.x + rightMargin)
        {
            outsideMargin = true;
            desiredX = tx - rightMargin;
        }
        else if (moveLeft && !preventBacktracking && tx < camPos.x - leftMargin)
        {
            outsideMargin = true;
            desiredX = tx + leftMargin;
        }

        if (preventBacktracking)
            desiredX = Mathf.Max(desiredX, maxXReached);

        //Delay de seguimiento
        if (!forceSnap)
        {
            if (outsideMargin)
            {
                outsideTimer += Time.deltaTime;
                if (outsideTimer < followDelay)
                    desiredX = camPos.x;
            }
            else
            {
                outsideTimer = 0f;
                desiredX = camPos.x;
            }
        }

        //Control vertical opcional
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

        return new Vector3(desiredX, desiredY, camPos.z);
    }

    private Vector3 ApplyBounds(Vector3 pos)
    {
        if (worldBounds == null || cam == null) return pos;

        Bounds b = worldBounds.bounds;
        float extY = cam.orthographicSize;
        float extX = extY * cam.aspect;

        float minX = b.min.x + extX;
        float maxX = b.max.x - extX;
        float minY = b.min.y + extY;
        float maxY = b.max.y - extY;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        return pos;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (target == null) return;

        Gizmos.color = Color.yellow;

        Vector3 camPos = Application.isPlaying ? transform.position : cam.transform.position;

        Gizmos.DrawWireCube(
            new Vector3(camPos.x, camPos.y, 0),
            new Vector3(leftMargin + rightMargin, topMargin + bottomMargin, 0));
    }
#endif
}
