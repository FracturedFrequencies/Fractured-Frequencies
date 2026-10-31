using System;
using UnityEngine;

namespace FracturedFrequencies.World
{
    /// <summary>One instruction of the tutorial and the goal that completes it.</summary>
    [Serializable]
    public class TutorialStep
    {
        public enum Goal
        {
            MoveDistance,      // player moved at least `distance` from where the step started
            ReachTarget,       // player is within `distance` of `highlight`
            GravityDirection,  // gravity points along `gravityDirection`
            EnterChamber,      // player has entered `chamber`
            DoorOpen,          // `door` is open
            RoomComplete,      // the room exit was reached
            ObjectGone         // `breakTarget` was destroyed or hidden (e.g. shattered)
        }

        [SerializeField, TextArea] private string instruction;
        [Tooltip("Extra help shown if the step takes longer than hintDelay seconds.")]
        [SerializeField, TextArea] private string hint;
        [SerializeField] private float hintDelay = 15f;
        [Tooltip("Object to circle with the highlight ring (optional).")]
        [SerializeField] private Transform highlight;
        [SerializeField] private float highlightRadius = 1.2f;
        [SerializeField] private Goal goal;
        [SerializeField] private float distance = 2f;
        [SerializeField] private Vector3 gravityDirection = Vector3.down;
        [SerializeField] private Chamber chamber;
        [SerializeField] private Door door;
        [SerializeField] private GameObject breakTarget;

        public string Instruction => instruction;
        public string Hint => hint;
        public float HintDelay => hintDelay;
        public Transform Highlight => highlight;
        public float HighlightRadius => highlightRadius;
        public Goal StepGoal => goal;
        public float Distance => distance;
        public Vector3 GravityDirection => gravityDirection;
        public Chamber TargetChamber => chamber;
        public Door TargetDoor => door;
        public GameObject BreakTarget => breakTarget;
    }
}
