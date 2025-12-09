using UnityEngine;

public class LookAtTarget : MonoBehaviour
{
    public float minDist, maxDist;
    
    private void OnTriggerStay(Collider other)
    {
        if (other.TryGetComponent(out BasicPlayerController controller))
            controller.TryLookAt(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out BasicPlayerController controller))
            controller.EndLookAt(this);
    }
}
