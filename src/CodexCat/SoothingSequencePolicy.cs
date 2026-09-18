using System;

namespace CodexCat
{
    internal static class SoothingSequencePolicy
    {
        public static PetState NextState(PetState current)
        {
            switch (current)
            {
                case PetState.SoothingStroke: return PetState.SoothedLoweringToSleep;
                case PetState.SoothedLoweringToSleep: return PetState.Sleeping;
                default: throw new ArgumentOutOfRangeException("current");
            }
        }

        public static bool IsActive(PetState state)
        {
            return state == PetState.SoothingStroke || state == PetState.SoothedLoweringToSleep;
        }
    }
}
