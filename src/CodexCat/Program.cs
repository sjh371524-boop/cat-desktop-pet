using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace CodexCat
{
    internal static class Program
    {
        private const string SingletonMutexName = "CodexCat.DesktopPet.Singleton";
        private const string ActivationEventName = "CodexCat.DesktopPet.Activate";

        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--verify-gaze")
            {
                string outputDirectory = Path.Combine(AppPaths.Root, "tests", "snapshots");
                string errorPath = Path.Combine(AppPaths.Root, "tests", "gaze-error.txt");
                try
                {
                    new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    GazeVerifier.Run(outputDirectory);
                    if (File.Exists(errorPath)) File.Delete(errorPath);
                }
                catch (Exception ex)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(errorPath));
                    File.WriteAllText(errorPath, ex.ToString());
                    Environment.ExitCode = 1;
                }
                return;
            }
            if (args.Length > 0 && (args[0] == "--verify-weather" || args[0] == "--verify-weather-live"))
            {
                try
                {
                    if (args[0] == "--verify-weather-live") WeatherVerifier.RunLive(); else WeatherVerifier.Run();
                    string oldError = Path.Combine(AppPaths.Root, "tests", "weather-error.txt");
                    if (File.Exists(oldError)) File.Delete(oldError);
                }
                catch (Exception ex)
                {
                    Directory.CreateDirectory(Path.Combine(AppPaths.Root, "tests"));
                    File.WriteAllText(Path.Combine(AppPaths.Root, "tests", "weather-error.txt"), ex.ToString());
                    Environment.ExitCode = 1;
                }
                return;
            }
            if (args.Length > 0 && string.Equals(args[0], "--verify-calculator", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    CalculatorVerifier.Run();
                }
                catch
                {
                    Environment.ExitCode = 1;
                }
                return;
            }

            if (args.Length > 0 && string.Equals(args[0], "--verify-pet-scaling", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    PetScaleVerifier.Run();
                }
                catch
                {
                    Environment.ExitCode = 1;
                }
                return;
            }

            if (args.Length > 0 && string.Equals(args[0], "--verify-idle-priority", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    IdleScheduleVerifier.Run();
                }
                catch
                {
                    Environment.ExitCode = 1;
                }
                return;
            }

            if (args.Length > 0 && string.Equals(args[0], "--verify-animation-transitions", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    AnimationTransitionVerifier.Run();
                }
                catch
                {
                    Environment.ExitCode = 1;
                }
                return;
            }

            if (args.Length > 0 && string.Equals(args[0], "--render-preview", StringComparison.OrdinalIgnoreCase))
            {
                string outputDirectory = args.Length > 1 ? args[1] : Path.Combine(AppPaths.Root, "tests", "snapshots");
                try
                {
                    Directory.CreateDirectory(outputDirectory);
                    // Preview tests create and close several independent windows.
                    // Closing an early snapshot must not shut down later clocks.
                    new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    SnapshotRenderer.RenderAll(outputDirectory);
                    string oldError = Path.Combine(outputDirectory, "snapshot-error.txt");
                    if (File.Exists(oldError)) File.Delete(oldError);
                }
                catch (Exception ex)
                {
                    Directory.CreateDirectory(outputDirectory);
                    File.WriteAllText(Path.Combine(outputDirectory, "snapshot-error.txt"), ex.ToString());
                    Environment.ExitCode = 1;
                }
                return;
            }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, SingletonMutexName, out createdNew))
            {
                if (!createdNew)
                {
                    SignalRunningInstance();
                    return;
                }

                using (EventWaitHandle activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName))
                {
                    AppSettings settings = AppSettings.Load();
                    Application app = new Application();
                    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    PetWindow window = new PetWindow(settings);
                    RegisteredWaitHandle activationRegistration = ThreadPool.RegisterWaitForSingleObject(
                        activationEvent,
                        delegate(object state, bool timedOut)
                        {
                            if (timedOut) return;
                            window.Dispatcher.BeginInvoke(new Action(delegate
                            {
                                if (!window.IsVisible) window.Show();
                                if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                                window.Activate();
                            }));
                        },
                        null,
                        Timeout.Infinite,
                        false);

                    try
                    {
                        app.Run(window);
                    }
                    finally
                    {
                        activationRegistration.Unregister(null);
                    }
                }
            }
        }

        private static void SignalRunningInstance()
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    using (EventWaitHandle activationEvent = EventWaitHandle.OpenExisting(ActivationEventName))
                    {
                        activationEvent.Set();
                        return;
                    }
                }
                catch (WaitHandleCannotBeOpenedException)
                {
                    if (attempt < 9) Thread.Sleep(100);
                }
            }
        }
    }
}
