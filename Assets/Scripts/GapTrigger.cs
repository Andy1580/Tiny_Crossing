using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class GapTrigger : MonoBehaviour
{
    // Esto es solo un marcador l�gico. No necesita l�gica adicional.
    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        gameObject.tag = "GapTrigger";
    }
}
