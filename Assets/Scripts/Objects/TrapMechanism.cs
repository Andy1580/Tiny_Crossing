using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrapMechanism : MonoBehaviour
{
    [Header("Configuración del Mecanismo")]
    public TrapController linkedTrap;
    public InventoryItem.ItemType requiredItemType = InventoryItem.ItemType.Lever;
    public float activeDuration = 2f;

    [Header("Referencias")]
    [SerializeField] private SpriteRenderer mechanismSprite;
    [SerializeField] private Sprite mechanismSpritePre;
    [SerializeField] private Sprite mechanismSpriteDefault;

    [Header("Colores de estado")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color previewColor = Color.cyan;
    [SerializeField] private Color activatedColor = Color.green;
    [SerializeField] private Color missingKeyColor = Color.red;

    private bool isPreActivating = false;
    private bool isActivated = false;
    private float activationTimer = 0f;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) originalColor = spriteRenderer.color;

        if (mechanismSprite == null)
            mechanismSprite = GetComponent<SpriteRenderer>();
        mechanismSprite.color = normalColor;

        mechanismSprite.sprite = mechanismSpriteDefault;
    }

    void Update()
    {
        if (isActivated)
        {
            activationTimer -= Time.deltaTime;
            if (activationTimer <= 0)
            {
                isActivated = false;
                if (spriteRenderer != null)
                    spriteRenderer.color = originalColor;
            }
        }
    }

    public void TryActivate()
    {
        if (isActivated) return;

        if (!InventorySystem.Instance.HasItemOfType(InventoryItem.ItemType.Lever))
        {
            Debug.Log("Se necesita una palanca para activar este mecanismo.");

            StopAllCoroutines(); // Por si ya había una corrutina anterior
            StartCoroutine(FlashMissingKeyColor());
            return;
        }

        if (!isPreActivating)
        {
            EnterPreActivation();
            return;
        }

        ExecuteActivation();
    }

    private IEnumerator FlashMissingKeyColor()
    {
        if (mechanismSprite == null) yield break;

        Color original = mechanismSprite.color;
        mechanismSprite.color = missingKeyColor;

        yield return new WaitForSeconds(0.5f); //Duración del flash rojo

        mechanismSprite.color = normalColor;
    }

    private void EnterPreActivation()
    {
        isPreActivating = true;
        mechanismSprite.color = previewColor;
        mechanismSprite.sprite = mechanismSpritePre;
        linkedTrap?.ShowTrapPreview();
        Debug.Log("Mecanismo en modo de selección (preactivación).");
    }

    private void ExecuteActivation()
    {
        isPreActivating = false;
        isActivated = true;

        mechanismSprite.color = activatedColor;
        linkedTrap?.HideTrapPreview();
        linkedTrap?.ActivateTrap();

        InventorySystem.Instance.RemoveItemOfType(InventoryItem.ItemType.Lever);
        Debug.Log("Trampa activada tras confirmación.");

        StartCoroutine(ResetMechanismColor(linkedTrap));
    }

    private IEnumerator ResetMechanismColor(TrapController trap)
    {
        yield return new WaitForSeconds(3f);
        mechanismSprite.color = normalColor;
        isActivated = false;
    }

    public bool IsPreActivating() { return isPreActivating; }
}

