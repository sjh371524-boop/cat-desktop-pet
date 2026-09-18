using System;

namespace CodexCat
{
    internal static class IdleScheduleVerifier
    {
        public static void Run()
        {
            VerifyCoordinator();
            VerifyEarCoordinator();
            Assert(IdleSchedulePolicy.Decide(false, true, 2, 5), IdleDueAction.NumberedIdle1,
                "仅60秒待机到点时应播放待机动画1。");
            Assert(IdleSchedulePolicy.Decide(true, true, 2, 5), IdleDueAction.DefaultLick,
                "与默认待机冲突时，待机动画1必须被默认待机顶替。");
            Assert(IdleSchedulePolicy.Decide(true, true, 5, 5), IdleDueAction.SpecialSleep,
                "与特殊待机冲突时，待机动画1必须被特殊睡眠顶替。");
            Assert(IdleSchedulePolicy.Decide(true, true, 5, 5, true), IdleDueAction.DefaultLick,
                "保持清醒时，自动特殊睡眠必须改为普通舔爪待机。");
            Assert(IdleSchedulePolicy.Decide(false, false, 5, 5), IdleDueAction.None,
                "未到触发时间时不应播放待机动画。");
        }

        private static void VerifyCoordinator()
        {
            AppSettings settings = new AppSettings();
            IdleScheduleCoordinator clock = new IdleScheduleCoordinator(settings, () => 0);
            Assert(clock.Tick(9.99, true, PetState.Stand, 0, 5), IdleDueAction.None, "摇尾不能提前。");
            Assert(clock.Tick(.02, true, PetState.Stand, 0, 5), IdleDueAction.TailWag, "最短间隔10秒。");
            clock.StateChanged(PetState.Stand, PetState.TailIdle);
            clock.Tick(2, true, PetState.TailIdle, 0, 5);
            if (clock.TailElapsed != 0) throw new InvalidOperationException("摇尾动作2秒不能计入下次等待。");
            clock.StateChanged(PetState.TailIdle, PetState.Stand);
            Assert(clock.Tick(9.99, true, PetState.Stand, 0, 5), IdleDueAction.EarWiggle, "摇耳使用独立时钟，摇尾尚未再次到点。");
            Assert(clock.Tick(.02, true, PetState.Stand, 0, 5), IdleDueAction.TailWag, "完成后等待10秒。");

            clock = new IdleScheduleCoordinator(settings, () => 1);
            if (clock.TailDelay != 20 || clock.EarDelay != 20) throw new InvalidOperationException("随机上限必须为20秒。");
            Assert(clock.Tick(20, true, PetState.Petting, 0, 5), IdleDueAction.None, "互动覆盖摇尾。");
            Assert(clock.Tick(.1, true, PetState.Stand, 0, 5), IdleDueAction.None, "被覆盖的摇尾不能立即补播。");
            if (clock.TailElapsed > .11 || clock.EarElapsed > .11) throw new InvalidOperationException("覆盖后未重新计时。");
            clock.Tick(500, false, PetState.Sleeping, 0, 5);
            if (clock.TailElapsed > .11 || clock.EarElapsed > .11) throw new InvalidOperationException("熄屏/睡眠必须暂停计时。");

            clock = new IdleScheduleCoordinator(settings, () => 0);
            Assert(clock.Tick(60, true, PetState.Stand, 0, 5), IdleDueAction.NumberedIdle1, "60秒动作优先于摇尾。");
            if (clock.TailElapsed != 0 || clock.EarElapsed != 0 || clock.NumberedElapsed != 0) throw new InvalidOperationException("同时到点必须消费全部事件。");
            clock = new IdleScheduleCoordinator(settings, () => 0);
            Assert(clock.Tick(300, true, PetState.Petted, 2, 5), IdleDueAction.None, "手动互动覆盖定时动作。");
            if (clock.DefaultElapsed != 0 || clock.NumberedElapsed != 0 || clock.TailElapsed != 0 || clock.EarElapsed != 0) throw new InvalidOperationException("覆盖后所有到点的时钟都应重置。");
            Assert(clock.Tick(.1, true, PetState.Stand, 2, 5), IdleDueAction.None, "不能在互动后补播。");
            clock = new IdleScheduleCoordinator(settings, () => .5);
            Assert(clock.Tick(300, true, PetState.TailIdle, 5, 5), IdleDueAction.SpecialSleep, "特殊睡眠覆盖摇尾。");
            clock = new IdleScheduleCoordinator(settings, () => .5);
            Assert(clock.Tick(300, true, PetState.Stand, 5, 5, true), IdleDueAction.DefaultLick,
                "保持清醒时到达自动睡眠节点应继续普通待机。");
            Assert(clock.Tick(299.9, true, PetState.Stand, 6, 5, false), IdleDueAction.NumberedIdle1,
                "取消保持清醒后不能立即强制入睡，需等待下个默认节点。");
            Assert(clock.Tick(.2, true, PetState.Stand, 6, 5, false), IdleDueAction.SpecialSleep,
                "取消保持清醒后应在下个默认节点恢复特殊睡眠。");
            Assert(IdleSchedulePolicy.Decide(true, true, true, 4, 5), IdleDueAction.DefaultLick, "默认动作优先级。");
            Assert(IdleSchedulePolicy.Decide(false, true, true, 4, 5), IdleDueAction.NumberedIdle1, "序号动作优先级。");
            for (int i = 0; i <= 100; i++)
            {
                double sample = i / 100.0;
                clock = new IdleScheduleCoordinator(settings, () => sample);
                if (clock.TailDelay < 10 || clock.TailDelay > 20 || clock.EarDelay < 10 || clock.EarDelay > 20)
                    throw new InvalidOperationException("随机间隔越界。");
            }
        }

        private static void VerifyEarCoordinator()
        {
            AppSettings settings = new AppSettings();
            Assert(IdleSchedulePolicy.Decide(false, false, true, true, 0, 5), IdleDueAction.TailWag,
                "随机摇尾与摇耳同时到点时必须播放摇尾。");
            Assert(IdleSchedulePolicy.Decide(false, true, true, true, 0, 5), IdleDueAction.NumberedIdle1,
                "序号待机优先于摇尾和摇耳。");
            Assert(IdleSchedulePolicy.Decide(true, true, true, true, 0, 5), IdleDueAction.DefaultLick,
                "默认待机优先于全部随机待机。");
            Assert(IdleSchedulePolicy.Decide(true, true, true, true, 5, 5), IdleDueAction.SpecialSleep,
                "特殊睡眠优先于全部随机待机。");
            foreach (IdleDueAction action in new[] { IdleDueAction.TailWag, IdleDueAction.NumberedIdle1,
                IdleDueAction.DefaultLick, IdleDueAction.SpecialSleep })
                if (!IdleSchedulePolicy.CanPlay(action, PetState.EarIdle))
                    throw new InvalidOperationException("高优先级待机必须能顶替正在进行的摇耳：" + action);
            foreach (PetState state in Enum.GetValues(typeof(PetState)))
                if (IdleSchedulePolicy.CanPlay(IdleDueAction.EarWiggle, state) != (state == PetState.Stand))
                    throw new InvalidOperationException("摇耳只允许从普通站立触发，不能打断其他动作：" + state);

            // Tail waits 20s and ears 10s; subsequent draws are 10s. Each timer
            // retains the other animation's elapsed time but excludes its own.
            int sample = 0;
            IdleScheduleCoordinator clock = new IdleScheduleCoordinator(settings, () => sample++ == 0 ? 1 : 0);
            Assert(clock.Tick(9.99, true, PetState.Stand, 0, 5), IdleDueAction.None, "摇耳不能提前触发。");
            Assert(clock.Tick(.02, true, PetState.Stand, 0, 5), IdleDueAction.EarWiggle, "摇耳最短独立间隔为10秒。");
            clock.StateChanged(PetState.Stand, PetState.EarIdle);
            Assert(clock.Tick(2, true, PetState.EarIdle, 0, 5), IdleDueAction.None, "摇耳中不应重复触发自身。");
            if (clock.EarElapsed != 0 || Math.Abs(clock.TailElapsed - 12.01) > .0001)
                throw new InvalidOperationException("摇耳的2秒必须排除在自身等待之外，其他计时器继续累计。");
            clock.StateChanged(PetState.EarIdle, PetState.Stand);
            Assert(clock.Tick(8, true, PetState.Petting, 0, 5), IdleDueAction.None, "互动覆盖到点的摇尾，不重置未到点的摇耳。");
            Assert(clock.Tick(1.99, true, PetState.Stand, 0, 5), IdleDueAction.None, "摇耳应从上次表演结束重新等待。");
            Assert(clock.Tick(.02, true, PetState.Stand, 0, 5), IdleDueAction.EarWiggle, "摇耳结束后等待满10秒才能再次触发。");

            // A tail event arriving during the ear performance wins immediately.
            sample = 0;
            clock = new IdleScheduleCoordinator(settings, () => sample++ == 0 ? .1 : (sample == 2 ? 0 : .4));
            Assert(clock.Tick(10, true, PetState.Stand, 0, 5), IdleDueAction.EarWiggle, "较早到点的摇耳可先播放。");
            clock.StateChanged(PetState.Stand, PetState.EarIdle);
            Assert(clock.Tick(1, true, PetState.EarIdle, 0, 5), IdleDueAction.TailWag, "正在摇耳时到点的摇尾必须顶替它。");
            clock.StateChanged(PetState.EarIdle, PetState.TailIdle);
            if (clock.EarElapsed != 0 || clock.EarDelay != 14)
                throw new InvalidOperationException("中断的摇耳必须重新随机计时。");
            clock.Tick(2, true, PetState.TailIdle, 0, 5);
            clock.StateChanged(PetState.TailIdle, PetState.Stand);
            Assert(clock.Tick(.1, true, PetState.Stand, 0, 5), IdleDueAction.None, "被摇尾顶替的摇耳不能立即补播。");

            // Conversely, an ear event due during the tail is consumed, not queued.
            sample = 0;
            clock = new IdleScheduleCoordinator(settings, () => sample++ == 0 ? 0 : (sample == 2 ? .1 : .7));
            Assert(clock.Tick(10, true, PetState.Stand, 0, 5), IdleDueAction.TailWag, "摇尾先到点。");
            clock.StateChanged(PetState.Stand, PetState.TailIdle);
            Assert(clock.Tick(1, true, PetState.TailIdle, 0, 5), IdleDueAction.None, "摇耳不能打断正在播放的摇尾。");
            if (clock.EarElapsed != 0 || clock.EarDelay != 17)
                throw new InvalidOperationException("被摇尾覆盖的摇耳事件必须消费并重新抽取间隔。");
            clock.Tick(1, true, PetState.TailIdle, 0, 5);
            clock.StateChanged(PetState.TailIdle, PetState.Stand);
            Assert(clock.Tick(.1, true, PetState.Stand, 0, 5), IdleDueAction.None, "摇尾结束后不能补播被覆盖的摇耳。");
            clock.Reset();
            if (clock.EarElapsed != 0 || clock.TailElapsed != 0 || clock.DefaultElapsed != 0 || clock.NumberedElapsed != 0)
                throw new InvalidOperationException("醒来重置必须包含摇耳随机计时。");
        }

        private static void Assert(IdleDueAction actual, IdleDueAction expected, string message)
        {
            if (actual != expected)
            {
                throw new InvalidOperationException(message + " 实际=" + actual + "，期望=" + expected);
            }
        }
    }
}
