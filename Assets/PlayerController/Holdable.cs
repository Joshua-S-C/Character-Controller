using UnityEngine;

/// <summary>
/// Objects in the scene to be picked up with IK arms
/// </summary>
public class Holdable : MonoBehaviour
{
    [SerializeField] Transform _locatorR, _locatorL;
    public GameObject _pLocR, _pLocL;

    public Transform locL => _locatorL;
    public Transform locR => _locatorR;

    public bool isHeld = false;

    private void Update()
    {
        if (_pLocR != null) _pLocR.transform.position = _locatorR.transform.position;
        if (_pLocL != null) _pLocL.transform.position = _locatorL.transform.position;
    }
}
