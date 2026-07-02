using Godot;
using System;

namespace StreetChaos
{
    public partial class StateComponent : Node
    {
        [Signal] public delegate void StateChangedEventHandler(PlayerState oldState, PlayerState newState);

        public PlayerState CurrentState { get; private set; } = PlayerState.Idle;
        public PlayerState PreviousState { get; private set; } = PlayerState.Idle;

        private PlayerController _player;

        public override void _Ready()
        {
            _player = GetParent<PlayerController>();
        }

        public void TransitionTo(PlayerState newState)
        {
            if (CurrentState == newState) return;
            if (CurrentState == PlayerState.Dead) return;
            if (CurrentState == PlayerState.Downed && newState != PlayerState.Dead && newState != PlayerState.Idle)
                return;

            PreviousState = CurrentState;
            CurrentState = newState;

            EmitSignal(SignalName.StateChanged, (int)PreviousState, (int)CurrentState);
        }

        public void SetState(PlayerState state)
        {
            if (CurrentState == state) return;
            PreviousState = CurrentState;
            CurrentState = state;
            EmitSignal(SignalName.StateChanged, (int)PreviousState, (int)CurrentState);
        }

        public bool CanTransitionTo(PlayerState state)
        {
            if (CurrentState == PlayerState.Dead && state != PlayerState.Dead)
                return false;

            if (CurrentState == PlayerState.Downed)
                return state == PlayerState.Dead || state == PlayerState.Idle;

            if (CurrentState == PlayerState.Stunned || CurrentState == PlayerState.KnockedDown
                || CurrentState == PlayerState.GettingUp || CurrentState == PlayerState.Thrown)
            {
                return state == PlayerState.Idle || state == PlayerState.Dead;
            }

            if (CurrentState == PlayerState.Grabbed || CurrentState == PlayerState.Grabbing)
            {
                return state == PlayerState.Idle || state == PlayerState.Dead || state == PlayerState.Thrown;
            }

            return true;
        }

        public bool IsGrounded()
        {
            return CurrentState == PlayerState.Idle
                || CurrentState == PlayerState.Walking
                || CurrentState == PlayerState.Running
                || CurrentState == PlayerState.Blocking
                || CurrentState == PlayerState.LightAttack
                || CurrentState == PlayerState.HeavyAttack
                || CurrentState == PlayerState.Kick;
        }

        public bool IsImmobilized()
        {
            return CurrentState == PlayerState.Stunned
                || CurrentState == PlayerState.KnockedDown
                || CurrentState == PlayerState.GettingUp
                || CurrentState == PlayerState.Grabbed
                || CurrentState == PlayerState.Thrown
                || CurrentState == PlayerState.Dead
                || CurrentState == PlayerState.Downed;
        }

        public bool CanAttack()
        {
            return CurrentState == PlayerState.Idle
                || CurrentState == PlayerState.Walking
                || CurrentState == PlayerState.Running
                || CurrentState == PlayerState.Downed;
        }
    }
}
