using Unity.VisualScripting;
using UnityEngine;

public class FieldPlayerDetection : MonoBehaviour
{
    [SerializeField] ChladniEffect effect;
    [SerializeField] float factor = 1f;
    [SerializeField] float duration = 1f;
    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.tag == "Player")
        {
            if (!other.gameObject.TryGetComponent<PlayerController>(out var controller)) return;
            controller.ReceiveEffect(effect, factor, duration);
            if (Time.frameCount %  10 == 0) Debug.Log("Trigger Active");
        }
    }
}
