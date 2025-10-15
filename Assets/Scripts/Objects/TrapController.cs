using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrapController : MonoBehaviour
{
    [Header("Configuración de la Trampa")]
    [Tooltip("Sprite del objeto visible (picos o trampa)")]
    [SerializeField] private SpriteRenderer trapSprite;

    [Tooltip("Altura a la que suben los picos al activarse")]
    [SerializeField] private float riseHeight = 1f;

    [Tooltip("Velocidad de movimiento de los picos")]
    [SerializeField] private float riseSpeed = 3f;

    [Tooltip("Tiempo que permanece activa antes de volver a ocultarse (0 = permanente)")]
    [SerializeField] private float activeDuration = 3f;

    private Color hiddenColor;
    private Color previewColor;

    [SerializeField] private Collider2D killCollider;
    [SerializeField] private float activationDelay = 0.3f;

    private Vector3 initialPosition;
    private Vector3 activePosition;
    private bool isActive = false;
    private float activeTimer = 0f;

    void Start()
    {
        if (trapSprite == null)
            trapSprite = GetComponentInChildren<SpriteRenderer>();

        hiddenColor = trapSprite.color;
        previewColor = new Color(hiddenColor.r, hiddenColor.g, hiddenColor.b, 0.5f);

        // Oculta bajo el suelo
        trapSprite.color = new Color(hiddenColor.r, hiddenColor.g, hiddenColor.b, 0f);
        trapSprite.sortingOrder = -1;

        initialPosition = trapSprite.transform.localPosition;
        activePosition = initialPosition + Vector3.up * riseHeight;
    }

    void Update()
    {
        if (isActive)
        {
            trapSprite.transform.localPosition = Vector3.MoveTowards(
                trapSprite.transform.localPosition, activePosition, riseSpeed * Time.deltaTime);

            if (activeDuration > 0f)
            {
                activeTimer -= Time.deltaTime;
                if (activeTimer <= 0f) DeactivateTrap();
            }
        }
        else
        {
            trapSprite.transform.localPosition = Vector3.MoveTowards(
                trapSprite.transform.localPosition, initialPosition, riseSpeed * Time.deltaTime);
        }
    }

    public void ActivateTrap()
    {
        if (isActive)
        {
            Debug.Log("La trampa ya está activa.");
            return;
        }

        Debug.Log("Trampa activada por mecanismo.");
        isActive = true;
        activeTimer = activeDuration;

        // Asegurar que el sprite sea completamente visible
        trapSprite.color = new Color(hiddenColor.r, hiddenColor.g, hiddenColor.b, 1f);
        trapSprite.sortingOrder = 0;

        if (killCollider != null)
            StartCoroutine(EnableKillColliderDelayed());
    }

    private IEnumerator EnableKillColliderDelayed()
    {
        killCollider.enabled = false;
        yield return new WaitForSeconds(activationDelay);
        killCollider.enabled = true;
    }

    public void DeactivateTrap()
    {
        if (!isActive) return;
        Debug.Log("Trampa desactivada.");
        isActive = false;

        // Regresa a invisible bajo el suelo
        trapSprite.color = new Color(hiddenColor.r, hiddenColor.g, hiddenColor.b, 0f);
        trapSprite.sortingOrder = -1;
    }

    public void ShowTrapPreview()
    {
        trapSprite.color = previewColor;
        trapSprite.sortingOrder = 0;
    }

    public void HideTrapPreview()
    {
        trapSprite.color = new Color(hiddenColor.r, hiddenColor.g, hiddenColor.b, 0f);
        trapSprite.sortingOrder = -1;
    }

    public bool IsTrapActive()
    {
        float currentY = trapSprite.transform.localPosition.y;
        float threshold = 0.1f;
        bool visuallyUp = Mathf.Abs(currentY - activePosition.y) <= threshold;
        return isActive || visuallyUp;
    }

    public bool IsRising()
    {
        if (trapSprite == null) return false;
        float y = trapSprite.transform.localPosition.y;
        return y > initialPosition.y + 0.02f && y < activePosition.y - 0.02f;
    }

    public bool IsActiveOrRising()
    {
        if (trapSprite == null) return isActive;
        float y = trapSprite.transform.localPosition.y;
        bool visuallyUp = Mathf.Abs(y - activePosition.y) <= 0.05f;
        return isActive || IsRising() || visuallyUp;
    }
}
