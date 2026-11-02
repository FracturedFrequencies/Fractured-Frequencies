using Unity.VisualScripting;
using UnityEngine;

public class FieldPlayerDetection : MonoBehaviour
{
    [SerializeField] ChladniEffect ceffect;
    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.tag == "Player")
        {
            if (!other.gameObject.TryGetComponent<PlayerController>(out var controller)) return;
            controller.ReceiveEffect(ceffect);
            if (Time.frameCount %  10 == 0) Debug.Log("Trigger Active");
        }
    }
}
