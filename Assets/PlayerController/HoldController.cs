using UnityEngine;

public class HoldController : MonoBehaviour
{
    public Holdable holdableInCol;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.TryGetComponent(out Holdable obj) && !obj.isHeld)
            holdableInCol = obj;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.TryGetComponent(out Holdable obj) && !obj.isHeld)
            holdableInCol = null;
    }
}
