using Godot;
using System.Collections.Generic;

namespace StreetChaos
{
    [GlobalClass]
    public partial class FightingStyleData : Resource
    {
        [Export] public string StyleName { get; set; } = "";
        [Export] public string Description { get; set; } = "";
        [Export] public Texture2D Icon { get; set; }

        [ExportGroup("Stats")]
        [Export] public float DamageMultiplier { get; set; } = 1.0f;
        [Export] public float SpeedMultiplier { get; set; } = 1.0f;
        [Export] public float DefenseMultiplier { get; set; } = 1.0f;
        [Export] public float StaminaRegenMultiplier { get; set; } = 1.0f;

        [ExportGroup("Attacks")]
        [Export] public AttackData LightPunch { get; set; }
        [Export] public AttackData HeavyPunch { get; set; }
        [Export] public AttackData LightKick { get; set; }
        [Export] public AttackData HeavyKick { get; set; }
        [Export] public AttackData Uppercut { get; set; }
        [Export] public AttackData Knee { get; set; }
        [Export] public AttackData Elbow { get; set; }
        [Export] public AttackData SpecialAttack { get; set; }

        [ExportGroup("Combos")]
        [Export] public ComboData[] Combos { get; set; }

        [ExportGroup("Animations")]
        [Export] public string IdleAnim { get; set; } = "idle";
        [Export] public string WalkAnim { get; set; } = "walk";
        [Export] public string RunAnim { get; set; } = "run";
        [Export] public string JumpAnim { get; set; } = "jump";
        [Export] public string BlockAnim { get; set; } = "idle";
        [Export] public string DodgeAnim { get; set; } = "idle";
        [Export] public string StunnedAnim { get; set; } = "idle";
        [Export] public string KnockdownAnim { get; set; } = "idle";
        [Export] public string GetUpAnim { get; set; } = "idle";
        [Export] public string GrabAnim { get; set; } = "idle";
        [Export] public string GrabbedAnim { get; set; } = "idle";
        [Export] public string ThrowAnim { get; set; } = "idle";

        public AttackData GetAttack(AttackInput input)
        {
            return input switch
            {
                AttackInput.Light => LightPunch,
                AttackInput.Heavy => HeavyPunch,
                AttackInput.KickLight => LightKick,
                AttackInput.KickHeavy => HeavyKick,
                AttackInput.Uppercut => Uppercut,
                AttackInput.Special => SpecialAttack,
                _ => LightPunch
            };
        }
    }
}
