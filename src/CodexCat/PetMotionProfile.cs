using System;
using System.Windows;

namespace CodexCat
{
    internal static class PetMotionProfile
    {
        public const double TailIdleSeconds = 2.0;
        public const double EarIdleSeconds = 2.0;
        public const double EarIdleMaxAngle = 2.25;

        public static double IdleEarAngle(double progress)
        {
            double p = AnimationTransitionProfile.Clamp01(progress);
            if (p == 0 || p == 0.5 || p == 1) return 0;
            double cycle = p * 2;
            return EarIdleMaxAngle
                * Math.Sin(AnimationTransitionProfile.Ease(cycle - Math.Floor(cycle)) * Math.PI * 2);
        }

        public static double IdleTailAngle(double progress)
        {
            double p = AnimationTransitionProfile.Clamp01(progress);
            if (p == 0 || p == 1 || p == 0.5) return 0;
            double cycles = p * 2;
            return 10 * Math.Sin(AnimationTransitionProfile.Ease(cycles - Math.Floor(cycles)) * Math.PI * 2);
        }

        public static double IdleTailBlink(double progress)
        {
            if (progress < 0.52) return AnimationTransitionProfile.Ease((progress - 0.48) / 0.04);
            return 1 - AnimationTransitionProfile.Ease((progress - 0.54) / 0.10);
        }

        public static double IdleEarBlink(double progress)
        {
            // Ear and tail idles share the exact same midpoint blink timing.
            return IdleTailBlink(progress);
        }

        public static Rect SoothingHandBounds(double strokeProgress)
        {
            double local = AnimationTransitionProfile.Clamp01(strokeProgress);
            // The fingers are near 83% of the hand's height: at the start they
            // touch y=41, level with the ears, then stroke down the same flank.
            double down = AnimationTransitionProfile.Ease(local) * 217.0;
            double followCurve = Math.Sin(local * Math.PI) * 6.0;
            return new Rect(170.0 + followCurve, -45.0 + down, 92.0, 104.0);
        }

        public static double PettedTailAngle(double progress)
        {
            // Two complete up/down cycles, independent of wall-clock/phase.
            // Zero angular velocity at both ends keeps action joins smooth.
            double local = AnimationTransitionProfile.Clamp01((progress - 0.08) / 0.86);
            if (local <= 0.0 || local >= 1.0) return 0.0;
            return 20.0 * Math.Sin(AnimationTransitionProfile.Ease(local) * Math.PI * 4.0);
        }
    }
}
