namespace CodexCat
{
    internal enum IdleDueAction
    {
        None,
        EarWiggle,
        TailWag,
        NumberedIdle1,
        DefaultLick,
        SpecialSleep
    }

    internal static class IdleSchedulePolicy
    {
        public static IdleDueAction Decide(bool defaultDue, bool numberedDue, bool tailDue, int completed, int beforeSleep)
        {
            return Decide(defaultDue, numberedDue, tailDue, completed, beforeSleep, false);
        }

        public static IdleDueAction Decide(bool defaultDue, bool numberedDue, bool tailDue, int completed, int beforeSleep, bool keepAwake)
        {
            return Decide(defaultDue, numberedDue, tailDue, false, completed, beforeSleep, keepAwake);
        }

        public static IdleDueAction Decide(bool defaultDue, bool numberedDue, bool tailDue, bool earDue,
            int completed, int beforeSleep)
        {
            return Decide(defaultDue, numberedDue, tailDue, earDue, completed, beforeSleep, false);
        }

        public static IdleDueAction Decide(bool defaultDue, bool numberedDue, bool tailDue, bool earDue,
            int completed, int beforeSleep, bool keepAwake)
        {
            IdleDueAction regular = Decide(defaultDue, numberedDue, completed, beforeSleep, keepAwake);
            if (regular != IdleDueAction.None) return regular;
            if (tailDue) return IdleDueAction.TailWag;
            return earDue ? IdleDueAction.EarWiggle : IdleDueAction.None;
        }

        public static bool CanPlay(IdleDueAction action, PetState state)
        {
            if (action == IdleDueAction.None) return false;
            if (state == PetState.Stand) return true;
            if (state == PetState.EarIdle) return action != IdleDueAction.EarWiggle;
            if (state == PetState.TailIdle)
                return action == IdleDueAction.NumberedIdle1 || action == IdleDueAction.DefaultLick || action == IdleDueAction.SpecialSleep;
            return state == PetState.IdleAnimation1 && (action == IdleDueAction.DefaultLick || action == IdleDueAction.SpecialSleep);
        }

        public static IdleDueAction Decide(bool defaultIdleDue, bool numberedIdle1Due, int completedDefaultIdles, int defaultIdlesBeforeSleep)
        {
            return Decide(defaultIdleDue, numberedIdle1Due, completedDefaultIdles, defaultIdlesBeforeSleep, false);
        }

        public static IdleDueAction Decide(bool defaultIdleDue, bool numberedIdle1Due, int completedDefaultIdles,
            int defaultIdlesBeforeSleep, bool keepAwake)
        {
            // Default and special idles always replace a numbered idle when due together.
            if (defaultIdleDue)
            {
                return !keepAwake && completedDefaultIdles >= System.Math.Max(0, defaultIdlesBeforeSleep)
                    ? IdleDueAction.SpecialSleep
                    : IdleDueAction.DefaultLick;
            }

            return numberedIdle1Due ? IdleDueAction.NumberedIdle1 : IdleDueAction.None;
        }
    }
}
