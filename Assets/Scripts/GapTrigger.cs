using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class GapTrigger : MonoBehaviour
{
    // Esto es solo un marcador lógico. No necesita lógica adicional.
    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        gameObject.tag = "GapTrigger";
    }
}
