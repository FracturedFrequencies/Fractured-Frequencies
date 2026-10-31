using FracturedFrequencies.Core;
using FracturedFrequencies.World;
using UnityEngine;

namespace FracturedFrequencies.Player
{
    /// <summary>
    /// Fixed isometric camera (orthographic). It frames the current room like a diorama and never rotates;
    /// when the player enters another room it glides over to frame that one.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class IsoCamera : MonoBehaviour
    {
        [SerializeField] private Vector3 isoAngles = new Vector3(30f, 45f, 0f);
        [SerializeField] private float distance = 50f;
        [SerializeField] private float orthographicSize = 8.5f;
        [SerializeField] private float smoothTime = 0.4f;
        [SerializeField] private Chamber startChamber;

        private Camera _camera;
        private Vector3 _focus;
        private Vector3 _targetFocus;
        private Vector3 _velocity;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = orthographicSize;
        }

        private void OnEnable() => EventBus<ChamberEnteredEvent>.Subscribe(OnChamberEntered);
        private void OnDisable() => EventBus<ChamberEnteredEvent>.Unsubscribe(OnChamberEntered);

        private void Start()
        {
            _targetFocus = startChamber != null ? startChamber.CameraFocus : transform.position;
            _focus = _targetFocus;
            Apply();
        }

        private void OnChamberEntered(ChamberEnteredEvent e) => _targetFocus = e.Chamber.CameraFocus;

        private void LateUpdate()
        {
            _focus = Vector3.SmoothDamp(_focus, _targetFocus, ref _velocity, smoothTime);
            Apply();
        }

        private void Apply()
        {
            var rotation = Quaternion.Euler(isoAngles);
            transform.SetPositionAndRotation(_focus - rotation * Vector3.forward * distance, rotation);
        }
    }
}
