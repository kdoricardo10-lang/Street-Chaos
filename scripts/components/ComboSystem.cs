using Godot;
using System.Collections.Generic;

namespace StreetChaos
{
    public partial class ComboSystem : Node
    {
        private readonly List<AttackInput> _inputBuffer = new();
        private float _inputTimeout = 0.5f;
        private float _inputTimer;

        public ComboData CurrentCombo { get; private set; }
        public bool IsComboActive => CurrentCombo != null;
        public int CurrentComboStep { get; private set; }

        public override void _Process(double delta)
        {
            if (_inputTimer > 0)
            {
                _inputTimer -= (float)delta;
                if (_inputTimer <= 0 && !IsComboActive)
                    _inputBuffer.Clear();
            }
        }

        public bool RegisterInput(AttackInput input, FightingStyleData style)
        {
            _inputBuffer.Add(input);
            _inputTimer = _inputTimeout;

            foreach (var combo in style.Combos)
            {
                int match = TryMatchCombo(combo.InputSequence);
                if (match == combo.InputSequence.Length)
                {
                    CurrentCombo = combo;
                    CurrentComboStep = match;
                    _inputBuffer.Clear();
                    return true;
                }
                if (match > 0)
                {
                    CurrentCombo = combo;
                    CurrentComboStep = match;
                    return false;
                }
            }

            CurrentCombo = null;
            CurrentComboStep = 0;

            if (_inputBuffer.Count > 5)
                _inputBuffer.RemoveAt(0);

            return false;
        }

        private int TryMatchCombo(AttackInput[] sequence)
        {
            if (sequence.Length == 0) return 0;

            int inputCount = _inputBuffer.Count;
            if (inputCount < 1) return 0;

            for (int start = 0; start < inputCount; start++)
            {
                int matched = 0;
                for (int si = 0; si < sequence.Length && start + si < inputCount; si++)
                {
                    if (_inputBuffer[start + si] == sequence[si])
                        matched++;
                    else
                        break;
                }
                if (matched == inputCount - start)
                    return matched;
            }
            return 0;
        }

        public void Reset()
        {
            _inputBuffer.Clear();
            CurrentCombo = null;
            CurrentComboStep = 0;
            _inputTimer = 0;
        }
    }
}
