using Godot;
using System;

namespace StreetChaos
{
    [GlobalClass]
    public partial class ComboData : Resource
    {
        [Export] public string ComboName { get; set; } = "";
        [Export] public Godot.Collections.Array<int> InputSequenceData { get; set; } = new();
        [Export] public float DamageMultiplier { get; set; } = 1.5f;
        [Export] public float StaminaCostMultiplier { get; set; } = 0.8f;
        [Export] public bool LaunchesEnemy { get; set; } = false;
        [Export] public bool Unblockable { get; set; } = false;
        [Export] public float FinalHitKnockback { get; set; } = 10f;

        public AttackInput[] InputSequence
        {
            get
            {
                var sequence = new AttackInput[InputSequenceData.Count];
                for (int i = 0; i < InputSequenceData.Count; i++)
                    sequence[i] = (AttackInput)InputSequenceData[i];
                return sequence;
            }
            set
            {
                InputSequenceData.Clear();
                if (value == null) return;

                for (int i = 0; i < value.Length; i++)
                    InputSequenceData.Add((int)value[i]);
            }
        }
    }
}
