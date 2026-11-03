using UnityEngine;

public class LookAt : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothTime = 2f;
    [SerializeField] private Vector3 offset;
    private Vector3 velocity = Vector3.zero;

    void Update()
    {
        if (target != null)
        {
            Vector3 direction = target.position + offset - transform.position;

            if (direction.sqrMagnitude > 0.001f)
            {
                Vector3 smoothedDirection = Vector3.SmoothDamp(
                    transform.forward,
                    direction,
                    ref velocity,
                    smoothTime);

                transform.rotation = Quaternion.LookRotation(smoothedDirection);
            }
        }
    }
}
