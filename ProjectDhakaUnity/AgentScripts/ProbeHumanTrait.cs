using System;
using UnityEngine;

public static class ProbeHumanTrait
{
    public static string Run()
    {
        var bones = string.Join(", ", HumanTrait.BoneName);
        var muscles = string.Join(", ", HumanTrait.MuscleName);
        return $"Bones ({HumanTrait.BoneCount}): {bones}\nMuscles ({HumanTrait.MuscleCount}): {muscles}";
    }
}
