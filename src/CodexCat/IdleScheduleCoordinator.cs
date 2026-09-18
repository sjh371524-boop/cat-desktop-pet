using System;

namespace CodexCat
{
    // A due event is consumed even when it loses to another animation. It does
    // not increment completion counts and is never queued for later replay.
    internal sealed class IdleScheduleCoordinator
    {
        private readonly double defaultSeconds;
        private readonly double numberedSeconds;
        private readonly Func<double> random;
        internal double DefaultElapsed { get; private set; }
        internal double NumberedElapsed { get; private set; }
        internal double TailElapsed { get; private set; }
        internal double TailDelay { get; private set; }
        internal double EarElapsed { get; private set; }
        internal double EarDelay { get; private set; }

        public IdleScheduleCoordinator(AppSettings settings) : this(settings, new Random().NextDouble) { }
        internal IdleScheduleCoordinator(AppSettings settings, Func<double> random)
        {
            this.random = random;
            defaultSeconds = Math.Max(3, settings.IdleMinutes * 60);
            numberedSeconds = Math.Max(1, settings.IdleAnimation1Seconds);
            RearmTail();
            RearmEar();
        }

        public IdleDueAction Tick(double seconds, bool awakeScreenTime, PetState current, int completed, int beforeSleep)
        {
            return Tick(seconds, awakeScreenTime, current, completed, beforeSleep, false);
        }

        public IdleDueAction Tick(double seconds, bool awakeScreenTime, PetState current, int completed,
            int beforeSleep, bool keepAwake)
        {
            if (!awakeScreenTime) return IdleDueAction.None;
            double elapsed = Math.Max(0, seconds);
            DefaultElapsed += elapsed;
            NumberedElapsed += elapsed;
            // The entire two-second tail performance is outside its next gap.
            if (current != PetState.TailIdle) TailElapsed += elapsed;
            if (current != PetState.EarIdle) EarElapsed += elapsed;
            bool defaultDue = DefaultElapsed >= defaultSeconds;
            bool numberedDue = NumberedElapsed >= numberedSeconds;
            bool tailDue = current != PetState.TailIdle && TailElapsed >= TailDelay;
            bool earDue = current != PetState.EarIdle && EarElapsed >= EarDelay;
            if (defaultDue) DefaultElapsed = 0;
            if (numberedDue) NumberedElapsed = 0;
            IdleDueAction action = IdleSchedulePolicy.Decide(defaultDue, numberedDue, tailDue, earDue, completed, beforeSleep, keepAwake);
            bool canPlay = IdleSchedulePolicy.CanPlay(action, current);
            if (tailDue)
            {
                if (action == IdleDueAction.TailWag && canPlay) TailElapsed = 0;
                else RearmTail();
            }
            if (earDue)
            {
                if (action == IdleDueAction.EarWiggle && canPlay) EarElapsed = 0;
                else RearmEar();
            }
            return canPlay ? action : IdleDueAction.None;
        }

        public void StateChanged(PetState from, PetState to)
        {
            if (from == to) return;
            if (from == PetState.TailIdle) RearmTail();
            if (from == PetState.EarIdle) RearmEar();
            if (from == PetState.IdleAnimation1 && to != PetState.Stand) NumberedElapsed = 0;
            if (from == PetState.Licking && to != PetState.Stand) DefaultElapsed = 0;
        }

        public void Reset()
        {
            DefaultElapsed = NumberedElapsed = 0;
            RearmTail();
            RearmEar();
        }

        private void RearmTail()
        {
            TailElapsed = 0;
            TailDelay = 10.0 + Math.Max(0, Math.Min(1, random())) * 10.0;
        }

        private void RearmEar()
        {
            EarElapsed = 0;
            EarDelay = 10.0 + Math.Max(0, Math.Min(1, random())) * 10.0;
        }
    }
}
