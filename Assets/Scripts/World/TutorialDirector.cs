using System.Collections.Generic;
using FracturedFrequencies.Core;
using FracturedFrequencies.Gravity;
using FracturedFrequencies.Player;
using UnityEngine;

namespace FracturedFrequencies.World
{
    /// <summary>
    /// Walks the player through the room one instruction at a time.
    /// Each step shows a short instruction, circles the relevant object with the highlight ring,
    /// shows an extra hint if the player takes long, and advances as soon as its goal is met.
    /// The GameHUD displays the current instruction and hint.
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        [SerializeField] private PlayerMotor player;
        [SerializeField] private DirectionalGravity gravity;
        [SerializeField] private TargetMarker marker;
        [SerializeField] private List<TutorialStep> steps = new();
        [SerializeField, TextArea] private string completeText = "Room complete!";

        public int StepIndex { get; private set; }
        public int StepCount => steps.Count;
        public bool IsFinished { get; private set; }
        public string Instruction => IsFinished ? completeText : steps.Count > 0 ? steps[StepIndex].Instruction : "";
        /// <summary>The current hint once its delay has passed, otherwise null.</summary>
        public string Hint
        {
            get
            {
                if (IsFinished || steps.Count == 0) return null;
                var step = steps[StepIndex];
                if (string.IsNullOrEmpty(step.Hint)) return null;
                return Time.time - _stepStartTime >= step.HintDelay ? step.Hint : null;
            }
        }
        /// <summary>Time since the current step started (for HUD animations).</summary>
        public float StepAge => Time.time - _stepStartTime;

        private Chamber _lastChamber;
        private bool _roomComplete;
        private float _stepStartTime;
        private Vector3 _stepStartPosition;

        private void OnEnable()
        {
            EventBus<ChamberEnteredEvent>.Subscribe(OnChamberEntered);
            EventBus<RoomCompletedEvent>.Subscribe(OnRoomCompleted);
        }

        private void OnDisable()
        {
            EventBus<ChamberEnteredEvent>.Unsubscribe(OnChamberEntered);
            EventBus<RoomCompletedEvent>.Unsubscribe(OnRoomCompleted);
        }

        private void Start() => BeginStep(0);

        private void OnChamberEntered(ChamberEnteredEvent e) => _lastChamber = e.Chamber;
        private void OnRoomCompleted(RoomCompletedEvent e) => _roomComplete = true;

        private void Update()
        {
            if (IsFinished || steps.Count == 0) return;
            if (IsGoalMet(steps[StepIndex])) BeginStep(StepIndex + 1);
        }

        private void BeginStep(int index)
        {
            if (index >= steps.Count)
            {
                IsFinished = true;
                if (marker != null) marker.SetTarget(null, 0f);
                return;
            }
            StepIndex = index;
            _stepStartTime = Time.time;
            _stepStartPosition = player != null ? player.transform.position : Vector3.zero;
            if (marker != null) marker.SetTarget(steps[index].Highlight, steps[index].HighlightRadius);
        }

        private bool IsGoalMet(TutorialStep step)
        {
            switch (step.StepGoal)
            {
                case TutorialStep.Goal.MoveDistance:
                    return player != null && Vector3.Distance(player.transform.position, _stepStartPosition) >= step.Distance;
                case TutorialStep.Goal.ReachTarget:
                    return player != null && step.Highlight != null &&
                           Vector3.Distance(player.transform.position, step.Highlight.position) <= step.Distance;
                case TutorialStep.Goal.GravityDirection:
                    return gravity != null && Vector3.Dot(gravity.Direction, step.GravityDirection.normalized) > 0.99f;
                case TutorialStep.Goal.EnterChamber:
                    return step.TargetChamber != null && _lastChamber == step.TargetChamber;
                case TutorialStep.Goal.DoorOpen:
                    return step.TargetDoor != null && step.TargetDoor.IsOpen;
                case TutorialStep.Goal.RoomComplete:
                    return _roomComplete;
                case TutorialStep.Goal.ObjectGone:
                    return step.BreakTarget == null || !step.BreakTarget.activeInHierarchy;
                default:
                    return false;
            }
        }
    }
}
