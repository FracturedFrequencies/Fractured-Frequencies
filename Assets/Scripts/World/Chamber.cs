using System.Collections.Generic;
using FracturedFrequencies.Core;
using FracturedFrequencies.Gravity;
using FracturedFrequencies.Player;
using UnityEngine;

namespace FracturedFrequencies.World
{
    /// <summary>
    /// One room (or part of a room) and its checkpoint. The trigger covers the room's inside.
    /// When the player enters, it becomes the current room (camera frames it, tutorial reacts).
    /// Restoring it brings back broken objects, moves crates back, resets gravity and places the player
    /// at the spawn point.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Chamber : MonoBehaviour
    {
        [SerializeField] private string displayName = "Room";
        [SerializeField] private Transform spawnPoint;
        [Tooltip("Gravity when this room starts or resets.")]
        [SerializeField] private Vector3 gravityDirection = Vector3.down;
        [Tooltip("Physics objects moved back to their start pose on reset (crates).")]
        [SerializeField] private List<Rigidbody> resetBodies = new();
        [Tooltip("Breakable objects re-activated and reset on reset (glass, cracked stone).")]
        [SerializeField] private List<GameObject> resetObjects = new();

        public string DisplayName => displayName;
        public Bounds RoomBounds => _box != null ? _box.bounds : new Bounds(transform.position, Vector3.one);
        public Vector3 CameraFocus => RoomBounds.center;

        private BoxCollider _box;
        private readonly List<Pose> _startPoses = new();

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            foreach (var body in resetBodies)
                _startPoses.Add(body != null ? new Pose(body.position, body.rotation) : default);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerMotor>() == null) return;
            EventBus<ChamberEnteredEvent>.Raise(new ChamberEnteredEvent { Chamber = this });
        }

        /// <summary>Puts the room back to its starting state and places the player at the spawn point.</summary>
        public void Restore(PlayerMotor player, DirectionalGravity gravity)
        {
            foreach (var go in resetObjects)
            {
                if (go == null) continue;
                go.SetActive(true);
                foreach (var resettable in go.GetComponents<IResettable>()) resettable.ResetState();
            }

            for (int i = 0; i < resetBodies.Count; i++)
            {
                var body = resetBodies[i];
                if (body == null) continue;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = _startPoses[i].position;
                body.rotation = _startPoses[i].rotation;
                body.transform.SetPositionAndRotation(_startPoses[i].position, _startPoses[i].rotation);
            }

            if (gravity != null) gravity.SetDirection(gravityDirection);
            if (player != null && spawnPoint != null)
                player.Teleport(spawnPoint.position, -gravityDirection.normalized);
        }
    }
}
