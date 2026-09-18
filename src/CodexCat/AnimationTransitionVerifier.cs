using System;

namespace CodexCat
{
    internal static class AnimationTransitionVerifier
    {
        public static void Run()
        {
            VerifyPetMotion();
            if (Math.Abs(AnimationTransitionProfile.SoothingStrokeSeconds - 2.0) > 0.0001
                || Math.Abs(AnimationTransitionProfile.SoothedLoweringSeconds - 1.0) > 0.0001)
            {
                throw new InvalidOperationException("The soothing sleep sequence must use 2 + 1 second timing, without a stretch stage.");
            }

            PetState lowering = SoothingSequencePolicy.NextState(PetState.SoothingStroke);
            if (lowering != PetState.SoothedLoweringToSleep
                || SoothingSequencePolicy.NextState(lowering) != PetState.Sleeping
                || !SoothingSequencePolicy.IsActive(PetState.SoothingStroke)
                || !SoothingSequencePolicy.IsActive(lowering)
                || SoothingSequencePolicy.IsActive(PetState.Stand)
                || SoothingSequencePolicy.IsActive(PetState.Sleeping))
                throw new InvalidOperationException("Stroking must lead directly to lowering and then sleeping.");

            if (Math.Abs(AnimationTransitionProfile.DurationSeconds(PetState.SoothingStroke, lowering) - 0.30) > 0.0001)
                throw new InvalidOperationException("The direct soothing join must retain its 300ms easing bridge.");

            foreach (PetState from in Enum.GetValues(typeof(PetState)))
            {
                foreach (PetState to in Enum.GetValues(typeof(PetState)))
                {
                    double duration = AnimationTransitionProfile.DurationSeconds(from, to);
                    if (from == to && duration != 0.0)
                    {
                        throw new InvalidOperationException("A state must not transition to itself.");
                    }
                    if (from != to && (duration < 0.10 || duration > 0.35))
                    {
                        throw new InvalidOperationException("Transition duration is outside the responsive range.");
                    }
                }
            }

            if (AnimationTransitionProfile.Ease(0.0) != 0.0 || AnimationTransitionProfile.Ease(1.0) != 1.0)
            {
                throw new InvalidOperationException("Transition easing endpoints are invalid.");
            }
            if (AnimationTransitionProfile.FrameBlend(0.39) != 0.0 || AnimationTransitionProfile.FrameBlend(0.61) != 1.0)
            {
                throw new InvalidOperationException("Frame blend window is invalid.");
            }

            double previous = 0.0;
            for (int step = 1; step <= 100; step++)
            {
                double current = AnimationTransitionProfile.Ease(step / 100.0);
                if (current < previous)
                {
                    throw new InvalidOperationException("Transition easing must be monotonic.");
                }
                previous = current;
            }
        }

        private static void VerifyPetMotion()
        {
            if (PetMotionProfile.EarIdleSeconds != 2 || PetMotionProfile.EarIdleMaxAngle > 2.25
                || PetMotionProfile.IdleEarAngle(0) != 0
                || PetMotionProfile.IdleEarAngle(.5) != 0 || PetMotionProfile.IdleEarAngle(1) != 0)
                throw new InvalidOperationException("Ear idle must have two equal one-second neutral-ended cycles.");
            for (int sample = 0; sample <= 100; sample++)
            {
                double p = sample / 200.0;
                if (Math.Abs(PetMotionProfile.IdleEarAngle(p) - PetMotionProfile.IdleEarAngle(p + .5)) > .00001)
                    throw new InvalidOperationException("The two ear cycles must have equal timing and amplitude.");
                double fullProgress = sample / 100.0;
                if (PetMotionProfile.IdleEarBlink(fullProgress) != PetMotionProfile.IdleTailBlink(fullProgress))
                    throw new InvalidOperationException("Ear idle must reuse the tail idle blink curve exactly.");
            }
            if (PetMotionProfile.IdleEarBlink(.48) > .000001 || PetMotionProfile.IdleEarBlink(.50) <= 0
                || Math.Abs(PetMotionProfile.IdleEarBlink(.53) - 1) > .000001
                || PetMotionProfile.IdleEarBlink(.64) > .000001 || PetMotionProfile.IdleEarBlink(1) > .000001)
                throw new InvalidOperationException("Ear blink must finish near the start of the second wiggle.");
            if (PetMotionProfile.TailIdleSeconds != 2 || PetMotionProfile.IdleTailAngle(0) != 0
                || PetMotionProfile.IdleTailAngle(.5) != 0 || PetMotionProfile.IdleTailAngle(1) != 0)
                throw new InvalidOperationException("Idle tail must use two seconds and two neutral-ended cycles.");
            if (PetTailLayer.BendWeight(188) != 0 || PetTailLayer.BendWeight(174) != 0
                || PetTailLayer.BendWeight(128) != 1)
                throw new InvalidOperationException("The tail bend must pin the root and reach full motion only above the hip blend.");
            double priorBend = 0;
            for (int y = 188; y >= 128; y--)
            {
                double bend = PetTailLayer.BendWeight(y);
                if (bend < priorBend || bend < 0 || bend > 1)
                    throw new InvalidOperationException("The tail bend must increase smoothly from the root toward the tip.");
                priorBend = bend;
            }
            int up = 0, down = 0, priorSign = 0;
            for (int step = 1; step <= 10000; step++)
            {
                double p = step / 10000.0;
                double angle = PetMotionProfile.IdleTailAngle(p);
                int sign = Math.Abs(angle) < .0001 ? 0 : Math.Sign(angle);
                if (sign != 0 && sign != priorSign) { if (sign > 0) up++; else down++; priorSign = sign; }
                if (Math.Abs(angle) > 10.0001) throw new InvalidOperationException("Idle tail amplitude must stay gentle.");
                if ((p < .48 || p > .64) && PetMotionProfile.IdleTailBlink(p) > .001)
                    throw new InvalidOperationException("The blink must stay at the boundary of the first and second wag.");
            }
            if (up != 2 || down != 2 || PetMotionProfile.IdleTailBlink(.53) != 1 || PetMotionProfile.IdleTailBlink(1) != 0)
                throw new InvalidOperationException("Idle tail cycle count or blink completion is incorrect.");
            if (PetMotionProfile.PettedTailAngle(0) != 0 || PetMotionProfile.PettedTailAngle(1) != 0)
                throw new InvalidOperationException("Tail motion must match the neutral endpoints.");
            int positiveLobes = 0, negativeLobes = 0, lastSign = 0;
            double previousHandY = PetMotionProfile.SoothingHandBounds(0).Y;
            for (int step = 0; step <= 10000; step++)
            {
                double p = step / 10000.0;
                double angle = PetMotionProfile.PettedTailAngle(p);
                int sign = Math.Abs(angle) < 0.0001 ? 0 : Math.Sign(angle);
                if (sign != 0 && sign != lastSign)
                {
                    if (sign > 0) positiveLobes++; else negativeLobes++;
                    lastSign = sign;
                }
                double y = PetMotionProfile.SoothingHandBounds(p).Y;
                if (y < previousHandY - 0.0001) throw new InvalidOperationException("A soothing stroke must move downward.");
                previousHandY = y;
            }
            if (positiveLobes != 2 || negativeLobes != 2)
                throw new InvalidOperationException("Petted tail must make exactly two complete cycles.");
            double fingertipsY = PetMotionProfile.SoothingHandBounds(0).Y + 104 * 0.83;
            if (fingertipsY < 15 || fingertipsY > 55)
                throw new InvalidOperationException("The hand must start touching the ears, not the torso.");
            if (Math.Abs(PetMotionProfile.SoothingHandBounds(1).Y - 172) > 0.001)
                throw new InvalidOperationException("The stroke must retain its original lower-body endpoint.");
        }
    }
}
