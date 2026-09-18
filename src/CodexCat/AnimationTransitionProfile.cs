using System;

namespace CodexCat
{
    /// <summary>
    /// Shared transition policy for every present and future pet animation.
    /// Durations are deliberately short enough to keep interaction responsive,
    /// while SmootherStep gives both ends zero velocity and zero acceleration.
    /// </summary>
    internal static class AnimationTransitionProfile
    {
        public const double DragTurnSeconds = 0.28;
        public const double DragSettleSeconds = 0.28;
        public const double SoothingStrokeSeconds = 2.0;
        public const double SoothedLoweringSeconds = 1.0;

        public static double DurationSeconds(PetState from, PetState to)
        {
            if (from == to) return 0.0;

            if (from == PetState.Stand && to == PetState.Dragging) return 0.18;
            if (from == PetState.Dragging && to == PetState.SettlingFromDrag) return 0.12;
            if (from == PetState.SettlingFromDrag && to == PetState.Stand) return 0.18;

            if (from == PetState.LoweringToSleep && to == PetState.Sleeping) return 0.16;
            if (from == PetState.Sleeping && to == PetState.Waking) return 0.24;
            if (from == PetState.Waking && to == PetState.Stand) return 0.24;

            if (from == PetState.Stand && to == PetState.Petting) return 0.22;
            if (from == PetState.Petting && to == PetState.Petted) return 0.30;
            if (from == PetState.Petted && to == PetState.Stand) return 0.34;


            if (to == PetState.SoothingStroke) return 0.26;
            if (from == PetState.SoothingStroke && to == PetState.SoothedLoweringToSleep) return 0.30;
            if (from == PetState.SoothedLoweringToSleep && to == PetState.Sleeping) return 0.16;

            if (from == PetState.Stand || to == PetState.Stand) return 0.24;
            return 0.20;
        }

        public static double Ease(double value)
        {
            double t = Clamp01(value);
            return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
        }

        public static double FrameBlend(double fractionalFrame)
        {
            // Keep the source frame crisp for most of its exposure and use a
            // compact dissolve around the hand-off. Large pose changes therefore
            // read as motion, not as two translucent cats occupying one canvas.
            return Ease((Clamp01(fractionalFrame) - 0.40) / 0.20);
        }

        public static double Clamp01(double value)
        {
            return Math.Max(0.0, Math.Min(1.0, value));
        }
    }
}
