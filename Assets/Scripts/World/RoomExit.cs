using FracturedFrequencies.Core;
using FracturedFrequencies.Player;
using FracturedFrequencies.Tools;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FracturedFrequencies.World
{
    /// <summary>
    /// The glowing crack at the end of a room. Loads the next room's scene if it is in the build
    /// settings, otherwise only shows a "room complete" message (until Room 2 exists).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RoomExit : MonoBehaviour
    {
        [SerializeField] private string roomName = "Room 1: Tuning Workshop";
        [SerializeField] private string nextSceneName = "Room2_InvertedBellTower";
        [SerializeField] private float loadDelay = 2f;
        [SerializeField] private Transform glow;

        private bool _triggered;
        private Vector3 _glowBaseScale;

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void Awake()
        {
            if (glow != null) _glowBaseScale = glow.localScale;
        }

        private void Update()
        {
            // Gentle breathing so the crack reads as "alive".
            if (glow != null) glow.localScale = _glowBaseScale * (1f + Mathf.Sin(Time.time * 3f) * 0.08f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered || other.GetComponentInParent<PlayerMotor>() == null) return;
            _triggered = true;

            ProceduralAudio.PlayGravityShift(transform.position);
            EventBus<RoomCompletedEvent>.Raise(new RoomCompletedEvent { RoomName = roomName, NextSceneName = nextSceneName });
            EventBus<HudMessagePostedEvent>.Raise(new HudMessagePostedEvent { Text = roomName + " complete", Duration = 5f });

            if (!string.IsNullOrEmpty(nextSceneName) && Application.CanStreamedLevelBeLoaded(nextSceneName))
                Invoke(nameof(LoadNext), loadDelay);
        }

        private void LoadNext() => SceneManager.LoadScene(nextSceneName);
    }
}
