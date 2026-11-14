using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class PlayerActions : MonoBehaviour
{
    private List<bool> actionQueue = new List<bool>();
    [SerializeField] private float actionTimer = 0.2f;
    [SerializeField] private int sliceDamage = 25;
    [SerializeField] private float sliceLength = 5f;
    [SerializeField] private float sliceWidth = 1.5f;
    [SerializeField] private float sliceHeight = 1.5f;
    [SerializeField] private bool drawDebugHitbox = true;
    private float lastActionTime;

    private void Update()
    {
        FaceMouse();
        ConsumeActions();
    }

    public void OnLeftClick(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            lastActionTime = Time.time;
            actionQueue.Add(true);
        }
    }

    public void OnRightClick(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            lastActionTime = Time.time;
            actionQueue.Add(false);
        }
    }

    private void ConsumeActions()
    {
        if (Time.time - lastActionTime > actionTimer)
        {
            actionQueue.Clear();
            return;
        }
        if (actionQueue.Count == 0)
        {
            return;
        }

        if (actionQueue[0])
        {
            SliceAttack();
            actionQueue.Clear();
            return;
        }
        else if (actionQueue.Count >= 3)
        {
            int ability = 1;

            if (actionQueue[1] && !actionQueue[2])
            {
                ability = 2;
            }
            else if (!actionQueue[1] && actionQueue[2])
            {
                ability = 3;
            }
            else if (actionQueue[1] && actionQueue[2])
            {
                ability = 4;
            }

            Debug.Log($"Ability {ability}");
            actionQueue.Clear();
            return;
        }
    }

    private void SliceAttack()
    {
        Vector3 hitboxCenter =
            transform.position + transform.forward * (sliceLength * 0.5f);

        Vector3 hitboxHalfExtents = new Vector3(
            sliceWidth,
            sliceHeight,
            sliceLength * 0.5f);

        Collider[] hitColliders = Physics.OverlapBox(
            hitboxCenter,
            hitboxHalfExtents,
            transform.rotation);

        var hitEntities = new HashSet<Entity>();

        foreach (Collider hitCollider in hitColliders)
        {
            Entity entity = hitCollider.GetComponentInParent<Entity>();

            if (entity == null || entity.gameObject == gameObject)
            {
                continue;
            }

            Vector3 localHitPosition =
                transform.InverseTransformPoint(hitCollider.ClosestPoint(transform.position));

            if (!IsInsideSlice(localHitPosition))
            {
                continue;
            }

            if (hitEntities.Add(entity))
            {
                entity.TakeDamage(sliceDamage, transform.position);
            }
        }
    }

    private bool IsInsideSlice(Vector3 localPosition)
    {
        if (localPosition.z < 0f || localPosition.z > sliceLength)
        {
            return false;
        }

        float widthPosition = localPosition.x / sliceWidth;
        float lengthPosition = localPosition.z / sliceLength;

        return widthPosition * widthPosition + lengthPosition * lengthPosition <= 1f;
    }

    private void FaceMouse()
    {
        if (TryGetMouseDirection(out Vector3 direction))
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private bool TryGetMouseDirection(out Vector3 direction)
    {
        direction = Vector3.zero;

        if (Mouse.current == null || Camera.main == null)
        {
            return false;
        }

        Ray mouseRay = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane groundPlane = new Plane(Vector3.up, transform.position);

        if (!groundPlane.Raycast(mouseRay, out float distance))
        {
            return false;
        }

        direction = mouseRay.GetPoint(distance) - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            return false;
        }

        direction = direction.normalized;
        return true;
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugHitbox)
        {
            return;
        }

        Gizmos.color = Color.red;

        const int arcSegments = 24;
        Vector3 previousPoint = transform.position;

        for (int i = 0; i <= arcSegments; i++)
        {
            float angle = Mathf.PI * i / arcSegments;
            Vector3 localPoint = new Vector3(
                Mathf.Cos(angle) * sliceWidth,
                0f,
                Mathf.Sin(angle) * sliceLength);

            Vector3 worldPoint = transform.TransformPoint(localPoint);
            Gizmos.DrawLine(previousPoint, worldPoint);
            previousPoint = worldPoint;
        }

        Vector3 leftBase = transform.TransformPoint(new Vector3(-sliceWidth, 0f, 0f));
        Vector3 rightBase = transform.TransformPoint(new Vector3(sliceWidth, 0f, 0f));
        Gizmos.DrawLine(leftBase, rightBase);
    }

}
