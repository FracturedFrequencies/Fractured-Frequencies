using UnityEngine;

public class LookAt : MonoBehaviour
{
    public Transform target;
    public float smoothTime = 2f;
    public Vector3 offset;
    private Vector3 velocity = Vector3.zero;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
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
