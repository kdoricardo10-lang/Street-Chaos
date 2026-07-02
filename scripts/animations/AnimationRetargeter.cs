using Godot;
using System.Collections.Generic;

namespace StreetChaos
{
    public static class AnimationRetargeter
    {
        public static readonly Dictionary<string, string> Ue5ToGodotBones = new()
        {
            { "pelvis", "DEF-hips" },
            { "spine_01", "DEF-spine.001" },
            { "spine_02", "DEF-spine.002" },
            { "spine_03", "DEF-spine.003" },
            { "neck_01", "DEF-neck" },
            { "Head", "DEF-head" },
            { "clavicle_l", "DEF-shoulder.L" },
            { "clavicle_r", "DEF-shoulder.R" },
            { "upperarm_l", "DEF-upper_arm.L" },
            { "upperarm_r", "DEF-upper_arm.R" },
            { "lowerarm_l", "DEF-forearm.L" },
            { "lowerarm_r", "DEF-forearm.R" },
            { "hand_l", "DEF-hand.L" },
            { "hand_r", "DEF-hand.R" },
            { "thigh_l", "DEF-thigh.L" },
            { "thigh_r", "DEF-thigh.R" },
            { "calf_l", "DEF-shin.L" },
            { "calf_r", "DEF-shin.R" },
            { "foot_l", "DEF-foot.L" },
            { "foot_r", "DEF-foot.R" },
            { "ball_l", "DEF-toe.L" },
            { "ball_r", "DEF-toe.R" },
            { "thumb_01_l", "DEF-thumb.01.L" },
            { "thumb_01_r", "DEF-thumb.01.R" },
            { "thumb_02_l", "DEF-thumb.02.L" },
            { "thumb_02_r", "DEF-thumb.02.R" },
            { "thumb_03_l", "DEF-thumb.03.L" },
            { "thumb_03_r", "DEF-thumb.03.R" },
            { "index_01_l", "DEF-f_index.01.L" },
            { "index_01_r", "DEF-f_index.01.R" },
            { "index_02_l", "DEF-f_index.02.L" },
            { "index_02_r", "DEF-f_index.02.R" },
            { "index_03_l", "DEF-f_index.03.L" },
            { "index_03_r", "DEF-f_index.03.R" },
            { "index_04_leaf_l", "DEF-f_index.03.L" },
            { "index_04_leaf_r", "DEF-f_index.03.R" },
            { "middle_01_l", "DEF-f_middle.01.L" },
            { "middle_01_r", "DEF-f_middle.01.R" },
            { "middle_02_l", "DEF-f_middle.02.L" },
            { "middle_02_r", "DEF-f_middle.02.R" },
            { "middle_03_l", "DEF-f_middle.03.L" },
            { "middle_03_r", "DEF-f_middle.03.R" },
            { "middle_04_leaf_l", "DEF-f_middle.03.L" },
            { "middle_04_leaf_r", "DEF-f_middle.03.R" },
            { "ring_01_l", "DEF-f_ring.01.L" },
            { "ring_01_r", "DEF-f_ring.01.R" },
            { "ring_02_l", "DEF-f_ring.02.L" },
            { "ring_02_r", "DEF-f_ring.02.R" },
            { "ring_03_l", "DEF-f_ring.03.L" },
            { "ring_03_r", "DEF-f_ring.03.R" },
            { "ring_04_leaf_l", "DEF-f_ring.03.L" },
            { "ring_04_leaf_r", "DEF-f_ring.03.R" },
            { "pinky_01_l", "DEF-f_pinky.01.L" },
            { "pinky_01_r", "DEF-f_pinky.01.R" },
            { "pinky_02_l", "DEF-f_pinky.02.L" },
            { "pinky_02_r", "DEF-f_pinky.02.R" },
            { "pinky_03_l", "DEF-f_pinky.03.L" },
            { "pinky_03_r", "DEF-f_pinky.03.R" },
            { "pinky_04_leaf_l", "DEF-f_pinky.03.L" },
            { "pinky_04_leaf_r", "DEF-f_pinky.03.R" },
        };

        public static readonly Dictionary<string, string> MixamoShortToGodotBones = new()
        {
            { "Hips", "DEF-hips" },
            { "Spine", "DEF-spine.001" },
            { "Spine1", "DEF-spine.002" },
            { "Spine2", "DEF-spine.003" },
            { "Neck", "DEF-neck" },
            { "Head", "DEF-head" },
            { "LeftShoulder", "DEF-shoulder.L" },
            { "RightShoulder", "DEF-shoulder.R" },
            { "LeftArm", "DEF-upper_arm.L" },
            { "RightArm", "DEF-upper_arm.R" },
            { "LeftForeArm", "DEF-forearm.L" },
            { "RightForeArm", "DEF-forearm.R" },
            { "LeftHand", "DEF-hand.L" },
            { "RightHand", "DEF-hand.R" },
            { "LeftUpLeg", "DEF-thigh.L" },
            { "RightUpLeg", "DEF-thigh.R" },
            { "LeftLeg", "DEF-shin.L" },
            { "RightLeg", "DEF-shin.R" },
            { "LeftFoot", "DEF-foot.L" },
            { "RightFoot", "DEF-foot.R" },
            { "LeftToeBase", "DEF-toe.L" },
            { "RightToeBase", "DEF-toe.R" },
            { "LeftHandThumb1", "DEF-thumb.01.L" },
            { "RightHandThumb1", "DEF-thumb.01.R" },
            { "LeftHandThumb2", "DEF-thumb.02.L" },
            { "RightHandThumb2", "DEF-thumb.02.R" },
            { "LeftHandThumb3", "DEF-thumb.03.L" },
            { "RightHandThumb3", "DEF-thumb.03.R" },
            { "LeftHandIndex1", "DEF-f_index.01.L" },
            { "RightHandIndex1", "DEF-f_index.01.R" },
            { "LeftHandIndex2", "DEF-f_index.02.L" },
            { "RightHandIndex2", "DEF-f_index.02.R" },
            { "LeftHandIndex3", "DEF-f_index.03.L" },
            { "RightHandIndex3", "DEF-f_index.03.R" },
            { "LeftHandMiddle1", "DEF-f_middle.01.L" },
            { "RightHandMiddle1", "DEF-f_middle.01.R" },
            { "LeftHandMiddle2", "DEF-f_middle.02.L" },
            { "RightHandMiddle2", "DEF-f_middle.02.R" },
            { "LeftHandMiddle3", "DEF-f_middle.03.L" },
            { "RightHandMiddle3", "DEF-f_middle.03.R" },
            { "LeftHandRing1", "DEF-f_ring.01.L" },
            { "RightHandRing1", "DEF-f_ring.01.R" },
            { "LeftHandRing2", "DEF-f_ring.02.L" },
            { "RightHandRing2", "DEF-f_ring.02.R" },
            { "LeftHandRing3", "DEF-f_ring.03.L" },
            { "RightHandRing3", "DEF-f_ring.03.R" },
            { "LeftHandPinky1", "DEF-f_pinky.01.L" },
            { "RightHandPinky1", "DEF-f_pinky.01.R" },
            { "LeftHandPinky2", "DEF-f_pinky.02.L" },
            { "RightHandPinky2", "DEF-f_pinky.02.R" },
            { "LeftHandPinky3", "DEF-f_pinky.03.L" },
            { "RightHandPinky3", "DEF-f_pinky.03.R" },
        };

        public static Animation RemapAnimationPaths(Animation sourceAnim, string sourceSkeletonPrefix, string targetSkeletonPrefix, Dictionary<string, string> boneMap = null)
        {
            var result = (Animation)sourceAnim.Duplicate(true);

            for (int i = 0; i < result.GetTrackCount(); i++)
            {
                string trackPath = result.TrackGetPath(i);
                if (trackPath.StartsWith(sourceSkeletonPrefix))
                {
                    string boneName = trackPath.Substring(sourceSkeletonPrefix.Length);
                    if (boneMap != null && boneMap.TryGetValue(boneName, out string mappedBone))
                        boneName = mappedBone;
                    string newPath = targetSkeletonPrefix + boneName;
                    result.TrackSetPath(i, new NodePath(newPath));
                }
            }

            return result;
        }

        public static string DetectPrefix(AnimationLibrary lib)
        {
            foreach (var animName in lib.GetAnimationList())
            {
                var anim = lib.GetAnimation(animName);
                if (anim != null && anim.GetTrackCount() > 0)
                {
                    string trackPath = anim.TrackGetPath(0);
                    int colonIdx = trackPath.IndexOf(':');
                    if (colonIdx >= 0)
                        return trackPath.Substring(0, colonIdx + 1);
                }
            }
            return null;
        }

        public static Dictionary<string, string> DetectBoneMap(AnimationLibrary lib)
        {
            foreach (var animName in lib.GetAnimationList())
            {
                var anim = lib.GetAnimation(animName);
                if (anim != null && anim.GetTrackCount() > 0)
                {
                    string trackPath = anim.TrackGetPath(0);
                    int colonIdx = trackPath.IndexOf(':');
                    if (colonIdx >= 0 && colonIdx + 1 < trackPath.Length)
                    {
                        string boneName = trackPath.Substring(colonIdx + 1);
                        if (boneName.StartsWith("mixamorig"))
                            return Ue5ToGodotBones;
                        if (boneName == "Hips" || boneName == "Spine")
                            return MixamoShortToGodotBones;
                        return Ue5ToGodotBones;
                    }
                }
            }
            return Ue5ToGodotBones;
        }
    }
}
