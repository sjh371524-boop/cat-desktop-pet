using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace CodexCat
{
    internal sealed class PetWindow : Window
    {
        private readonly AppSettings settings;
        private readonly CatVisual cat;
        private readonly OutlinedText sleepingMessage;
        private readonly InteractionBubbleWindow bubble;
        private readonly WeatherService weather;
        private readonly DispatcherTimer animationTimer;
        private readonly DispatcherTimer sleepingMessageTimer;
        private readonly DispatcherTimer weatherRefreshTimer;
        private readonly Stopwatch clock;
        private readonly Forms.NotifyIcon trayIcon;

        private ScreenPowerTracker powerTracker;
        private CalculatorWindow calculatorWindow;
        private PetState state;
        private TimeSpan stateStartedAt;
        private TimeSpan previousTick;
        private readonly IdleScheduleCoordinator idleSchedule;
        private int completedDefaultIdles;
        private double phase;
        private bool dragging;
        private bool dragPoseActivated;
        private Point dragStartScreen;
        private Point lastDragScreen;
        private double dragStartLeft;
        private double dragStartTop;
        private Vector lastMovement;
        private bool exiting;
        private double currentScale;

        public PetWindow(AppSettings settings)
        {
            this.settings = settings;
            idleSchedule = new IdleScheduleCoordinator(settings);
            state = PetState.Stand;
            currentScale = PetScalePolicy.ClampToDesktop(settings.PetScale, SystemParameters.WorkArea);
            Width = PetScalePolicy.BaseWidth * currentScale;
            Height = PetScalePolicy.BaseHeight * currentScale;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = settings.AlwaysOnTop;
            Title = settings.PetName;

            Grid root = new Grid { Background = Brushes.Transparent };
            Content = root;
            cat = new CatVisual { Margin = new Thickness(5, 0, 5, 38) };
            root.Children.Add(cat);

            sleepingMessage = new OutlinedText
            {
                Text = "睡着了",
                Height = 44,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(12, 0, 12, 18),
                Visibility = Visibility.Collapsed,
                Opacity = 0
            };
            root.Children.Add(sleepingMessage);

            weather = new WeatherService(settings);
            bubble = new InteractionBubbleWindow();
            bubble.SetKeepAwake(settings.KeepAwake);
            bubble.PetRequested += delegate { PetTheCat(bubble.CurrentSide); };
            bubble.CalculatorRequested += delegate { OpenCalculator(); };
            bubble.SootheRequested += delegate { SootheToSleep(bubble.CurrentSide); };
            bubble.KeepAwakeChanged += delegate(object changedSender, KeepAwakeChangedEventArgs args)
            {
                ChangeKeepAwake(args.KeepAwake);
            };
            bubble.WakeRequested += delegate { WakeUp(); };
            bubble.ScaleUpRequested += delegate { ChangePetScale(true); };
            bubble.ScaleDownRequested += delegate { ChangePetScale(false); };
            bubble.ExitRequested += delegate { ExitApplication(); };
            weatherRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            weatherRefreshTimer.Tick += async delegate
            {
                if (powerTracker == null || powerTracker.IsScreenOn) await RefreshWeatherAsync();
            };

            UpdatePetVisualScale();

            sleepingMessageTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1450) };
            sleepingMessageTimer.Tick += delegate
            {
                sleepingMessageTimer.Stop();
                DoubleAnimation fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(260));
                fade.Completed += delegate { sleepingMessage.Visibility = Visibility.Collapsed; };
                sleepingMessage.BeginAnimation(OpacityProperty, fade);
            };

            clock = Stopwatch.StartNew();
            previousTick = clock.Elapsed;
            animationTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
            animationTimer.Tick += AnimationTick;

            trayIcon = CreateTrayIcon();

            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;
            Closing += OnClosing;
            MouseLeftButtonDown += OnLeftButtonDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnLeftButtonUp;
            MouseRightButtonUp += OnRightButtonUp;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Rect work = GetCurrentWorkArea();
            if (settings.StartNearBottomRight)
            {
                Left = work.Right - Width - 36;
                Top = work.Bottom - Height - 22;
            }
            else
            {
                Left = work.Left + (work.Width - Width) / 2.0;
                Top = work.Top + (work.Height - Height) / 2.0;
            }
            animationTimer.Start();
            trayIcon.Visible = true;
            cat.Update(state, phase, 0, new Vector());
            weatherRefreshTimer.Start();
            await RefreshWeatherAsync();
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            powerTracker = new ScreenPowerTracker(handle);
        }

        private void AnimationTick(object sender, EventArgs e)
        {
            TimeSpan now = clock.Elapsed;
            double delta = Math.Max(0, Math.Min(0.1, (now - previousTick).TotalSeconds));
            previousTick = now;

            bool screenOn = powerTracker == null || powerTracker.IsScreenOn;
            StartIdleAction(idleSchedule.Tick(delta, screenOn && !PausesScreenOnIdleTime(state),
                state, completedDefaultIdles, settings.DefaultIdlesBeforeSleep, settings.KeepAwake), now);

            double progress = 0;
            switch (state)
            {
                case PetState.Stand:
                    phase = Wrap(phase + delta / 4.2);
                    break;
                case PetState.TailIdle:
                    phase = Wrap(phase + delta / 4.2);
                    progress = (now - stateStartedAt).TotalSeconds / PetMotionProfile.TailIdleSeconds;
                    if (progress >= 1)
                    {
                        SetState(PetState.Stand, now);
                        progress = 0;
                    }
                    break;
                case PetState.EarIdle:
                    phase = Wrap(phase + delta / 4.2);
                    progress = (now - stateStartedAt).TotalSeconds / PetMotionProfile.EarIdleSeconds;
                    if (progress >= 1)
                    {
                        SetState(PetState.Stand, now);
                        progress = 0;
                    }
                    break;
                case PetState.IdleAnimation1:
                    progress = (now - stateStartedAt).TotalSeconds / 1.5;
                    phase = Wrap(phase + delta * 1.45);
                    if (progress >= 1)
                    {
                        SetState(PetState.Stand, now);
                        progress = 0;
                    }
                    break;
                case PetState.Dragging:
                    progress = (now - stateStartedAt).TotalSeconds / AnimationTransitionProfile.DragTurnSeconds;
                    phase = Wrap(phase + delta * 0.12);
                    break;
                case PetState.SettlingFromDrag:
                    progress = (now - stateStartedAt).TotalSeconds / AnimationTransitionProfile.DragSettleSeconds;
                    if (progress >= 1)
                    {
                        SetState(PetState.Stand, now);
                        progress = 0;
                    }
                    break;
                case PetState.Licking:
                    progress = (now - stateStartedAt).TotalSeconds / 2.0;
                    phase = Wrap(phase + delta * 1.7);
                    if (progress >= 1)
                    {
                        completedDefaultIdles++;
                        SetState(PetState.Stand, now);
                        progress = 0;
                    }
                    break;
                case PetState.LoweringToSleep:
                    progress = (now - stateStartedAt).TotalSeconds / 2.0;
                    phase = Wrap(phase + delta * 0.8);
                    if (progress >= 1)
                    {
                        SetState(PetState.Sleeping, now);
                        progress = 1;
                    }
                    break;
                case PetState.Sleeping:
                    phase = Wrap(phase + delta / 2.4);
                    progress = 1;
                    break;
                case PetState.Waking:
                    progress = (now - stateStartedAt).TotalSeconds / 2.0;
                    phase = Wrap(phase + delta * 0.9);
                    if (progress >= 1)
                    {
                        completedDefaultIdles = 0;
                        idleSchedule.Reset();
                        SetState(PetState.Stand, now);
                        progress = 0;
                    }
                    break;
                case PetState.Petting:
                    progress = (now - stateStartedAt).TotalSeconds / 2.0;
                    phase = Wrap(phase + delta * 0.72);
                    if (progress >= 1)
                    {
                        Vector reactionDirection = lastMovement;
                        SetState(PetState.Petted, now);
                        lastMovement = reactionDirection;
                        progress = 0;
                    }
                    break;
                case PetState.Petted:
                    progress = (now - stateStartedAt).TotalSeconds / 3.0;
                    phase = Wrap(phase + delta * 0.70);
                    if (progress >= 1)
                    {
                        SetState(PetState.Stand, now);
                        progress = 0;
                    }
                    break;
                case PetState.SoothingStroke:
                    progress = (now - stateStartedAt).TotalSeconds / AnimationTransitionProfile.SoothingStrokeSeconds;
                    phase = Wrap(phase + delta * 0.42);
                    if (progress >= 1)
                    {
                        Vector sootheDirection = lastMovement;
                        SetState(SoothingSequencePolicy.NextState(state), now);
                        lastMovement = sootheDirection;
                        progress = 0;
                    }
                    break;
                case PetState.SoothedLoweringToSleep:
                    progress = (now - stateStartedAt).TotalSeconds / AnimationTransitionProfile.SoothedLoweringSeconds;
                    phase = Wrap(phase + delta * 0.82);
                    if (progress >= 1)
                    {
                        SetState(SoothingSequencePolicy.NextState(state), now);
                        progress = 1;
                    }
                    break;
            }

            cat.Update(state, phase, Clamp(progress, 0, 1), lastMovement);
            UpdateCursorGaze(delta, screenOn);
        }

        private void UpdateCursorGaze(double deltaSeconds, bool screenOn)
        {
            Point cursor;
            bool track = PetGazeProfile.CanTrack(state, screenOn)
                && !dragging && IsVisible && PresentationSource.FromVisual(cat) != null;
            if (track && DesktopCursor.TryGetPosition(out cursor))
            {
                // Global cursor coordinates include other apps and negative
                // monitor positions. WPF handles the screen-pixel/DIP conversion.
                cat.UpdateGaze(cat.PointFromScreen(cursor), deltaSeconds, true);
            }
            else cat.UpdateGaze(new Point(), deltaSeconds, false);
        }

        private void StartIdleAction(IdleDueAction action, TimeSpan now)
        {
            if (action == IdleDueAction.None) return;
            if (action == IdleDueAction.EarWiggle)
            {
                SetState(PetState.EarIdle, now);
                return;
            }
            if (action == IdleDueAction.TailWag)
            {
                SetState(PetState.TailIdle, now);
                return;
            }

            if (action == IdleDueAction.NumberedIdle1)
            {
                StartIdleAnimation1(now);
                return;
            }

            // The coordinator has already consumed all due events, including
            // suppressed ones; unrelated timers are not reset by this action.
            SetState(action == IdleDueAction.SpecialSleep ? PetState.LoweringToSleep : PetState.Licking, now);
        }

        private void StartIdleAnimation1(TimeSpan now)
        {
            Rect workArea = GetCurrentWorkArea();
            Rect petBounds = new Rect(Left, Top, Width, Height);
            BubbleSide bubbleSide = InteractionBubbleWindow.GetPreferredSide(petBounds, currentScale, workArea);
            BubbleSide pawSide = bubbleSide == BubbleSide.Right ? BubbleSide.Left : BubbleSide.Right;
            SetState(PetState.IdleAnimation1, now);
            lastMovement = new Vector(pawSide == BubbleSide.Right ? 1 : -1, 0);
        }

        private void SetState(PetState newState, TimeSpan now)
        {
            idleSchedule.StateChanged(state, newState);
            state = newState;
            stateStartedAt = now;
            lastMovement = new Vector();
        }

        private void OnLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            bubble.Hide();

            if (IsSleepingForInteraction(state))
            {
                ShowSleepingMessage();
                e.Handled = true;
                return;
            }
            if (IsManualSootheSequence(state))
            {
                e.Handled = true;
                return;
            }
            if (state == PetState.Waking)
            {
                e.Handled = true;
                return;
            }

            dragging = true;
            CaptureMouse();
            dragStartScreen = ToDip(PointToScreen(e.GetPosition(this)));
            lastDragScreen = dragStartScreen;
            dragStartLeft = Left;
            dragStartTop = Top;
            dragPoseActivated = false;
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!dragging || e.LeftButton != MouseButtonState.Pressed) return;
            Point current = ToDip(PointToScreen(e.GetPosition(this)));
            Vector total = current - dragStartScreen;
            Vector movement = current - lastDragScreen;
            lastDragScreen = current;

            if (!dragPoseActivated)
            {
                // Wait for a real displacement so the turn bridge knows whether
                // the cat should face left or right. This also keeps a simple click
                // from causing a one-frame walk flash.
                if (total.Length < 2.5) return;
                dragPoseActivated = true;
                SetState(PetState.Dragging, clock.Elapsed);
                movement = total;
            }
            lastMovement = movement;

            Rect work = GetCurrentWorkArea();
            Left = Clamp(dragStartLeft + total.X, work.Left, work.Right - Width);
            Top = Clamp(dragStartTop + total.Y, work.Top, work.Bottom - Height);
            phase = Wrap(phase + movement.Length / 82.0);
            double turnProgress = (clock.Elapsed - stateStartedAt).TotalSeconds / AnimationTransitionProfile.DragTurnSeconds;
            cat.Update(state, phase, Clamp(turnProgress, 0, 1), movement);
        }

        private void OnLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!dragging) return;
            dragging = false;
            ReleaseMouseCapture();
            if (dragPoseActivated)
            {
                Vector settleDirection = lastMovement;
                SetState(PetState.SettlingFromDrag, clock.Elapsed);
                lastMovement = settleDirection;
            }
            dragPoseActivated = false;
            e.Handled = true;
        }

        private async void OnRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            ShowInteractionBubble();
            await RefreshWeatherAsync();
        }

        private async System.Threading.Tasks.Task RefreshWeatherAsync()
        {
            await weather.RefreshIfNeededAsync();
            if (!exiting) bubble.UpdateWeather(weather.CurrentDisplayInfo);
        }

        private void ShowInteractionBubble()
        {
            Rect petBounds = new Rect(Left, Top, Width, Height);
            if (calculatorWindow != null && calculatorWindow.IsVisible)
            {
                bubble.HoldOpenForCalculator();
            }
            bubble.SetKeepAwake(settings.KeepAwake);
            bubble.ShowNear(
                petBounds,
                weather.CurrentDisplayInfo,
                IsSleepingForInteraction(state),
                currentScale,
                GetCurrentWorkArea());
        }

        private void ChangeKeepAwake(bool value)
        {
            // This switch affects only the scheduler's automatic special sleep.
            // It intentionally leaves elapsed time/completion counts untouched,
            // so cancelling waits for the next normal five-minute node instead
            // of forcing an immediate sleep. Manual soothing remains independent.
            settings.KeepAwake = value;
            bubble.SetKeepAwake(value);
            try
            {
                settings.SaveLocal();
            }
            catch
            {
                // A read-only config folder should not block the live toggle.
            }
        }

        private void ChangePetScale(bool enlarge)
        {
            Rect work = GetCurrentWorkArea();
            double nextScale = PetScalePolicy.Step(currentScale, enlarge, work);
            if (Math.Abs(nextScale - currentScale) < 0.0001)
            {
                ShowInteractionBubble();
                return;
            }

            double anchorX = Left + Width / 2.0;
            double anchorBottom = Top + Height;
            currentScale = nextScale;
            Width = PetScalePolicy.BaseWidth * currentScale;
            Height = PetScalePolicy.BaseHeight * currentScale;
            UpdatePetVisualScale();

            Left = Clamp(anchorX - Width / 2.0, work.Left, work.Right - Width);
            Top = Clamp(anchorBottom - Height, work.Top, work.Bottom - Height);
            settings.PetScale = currentScale;
            try
            {
                settings.SaveLocal();
            }
            catch
            {
                // A read-only config folder should not prevent the visual resize.
            }

            ShowInteractionBubble();
        }

        private void UpdatePetVisualScale()
        {
            cat.Margin = new Thickness(5 * currentScale, 0, 5 * currentScale, 38 * currentScale);
            sleepingMessage.Height = 44 * currentScale;
            sleepingMessage.Margin = new Thickness(12 * currentScale, 0, 12 * currentScale, 18 * currentScale);
            sleepingMessage.StrokeThickness = 1.7 * currentScale;
        }

        private void PetTheCat()
        {
            Rect work = GetCurrentWorkArea();
            BubbleSide preferredSide = Left + Width / 2.0 < work.Left + work.Width / 2.0
                ? BubbleSide.Right
                : BubbleSide.Left;
            PetTheCat(preferredSide);
        }

        private void PetTheCat(BubbleSide reactionSide)
        {
            if (IsSleepingForInteraction(state))
            {
                ShowSleepingMessage();
                return;
            }
            if (IsManualSootheSequence(state)) return;
            if (state != PetState.Waking)
            {
                SetState(PetState.Petting, clock.Elapsed);
                lastMovement = new Vector(reactionSide == BubbleSide.Right ? 1 : -1, 0);
            }
        }

        private void WakeUp()
        {
            if (!IsSleepingForInteraction(state)) return;
            sleepingMessage.Visibility = Visibility.Collapsed;
            SetState(PetState.Waking, clock.Elapsed);
        }

        private void SootheToSleep(BubbleSide handSide)
        {
            if (IsSleepingForInteraction(state))
            {
                ShowSleepingMessage();
                return;
            }
            if (state == PetState.Waking || IsManualSootheSequence(state)) return;

            SetState(PetState.SoothingStroke, clock.Elapsed);
            lastMovement = new Vector(handSide == BubbleSide.Right ? 1 : -1, 0);
        }

        private void OpenCalculator()
        {
            Rect petBounds = new Rect(Left, Top, Width, Height);
            bool isSleeping = IsSleepingForInteraction(state);
            Rect workArea = GetCurrentWorkArea();
            Rect bubbleBounds = bubble.IsVisible
                ? bubble.CurrentBounds
                : InteractionBubbleWindow.GetBoundsNear(petBounds, isSleeping, currentScale, workArea);
            Rect calculatorBounds = CalculatorPlacementPolicy.GetBounds(bubbleBounds, currentScale, workArea);

            if (calculatorWindow == null)
            {
                calculatorWindow = new CalculatorWindow { Owner = this };
                calculatorWindow.Closed += delegate
                {
                    bubble.ReleaseCalculatorVisibilityLock();
                    calculatorWindow = null;
                };
            }
            if (bubble.IsVisible) bubble.HoldOpenForCalculator();
            calculatorWindow.ShowAt(calculatorBounds);
            // Opening arithmetic is a UI-only action. Keep the current pet
            // animation, direction and idle schedule running without resetting.
        }

        private static bool IsManualSootheSequence(PetState value)
        {
            return SoothingSequencePolicy.IsActive(value);
        }

        private static bool IsSleepingForInteraction(PetState value)
        {
            return value == PetState.Sleeping
                || value == PetState.LoweringToSleep
                || value == PetState.SoothedLoweringToSleep;
        }

        private static bool PausesScreenOnIdleTime(PetState value)
        {
            return value == PetState.Sleeping
                || value == PetState.LoweringToSleep
                || value == PetState.Waking
                || value == PetState.SoothedLoweringToSleep;
        }

        private void ShowSleepingMessage()
        {
            sleepingMessageTimer.Stop();
            sleepingMessage.BeginAnimation(OpacityProperty, null);
            sleepingMessage.Visibility = Visibility.Visible;
            sleepingMessage.Opacity = 1;
            sleepingMessageTimer.Start();
        }

        private Forms.NotifyIcon CreateTrayIcon()
        {
            Forms.NotifyIcon icon = new Forms.NotifyIcon
            {
                Icon = Drawing.SystemIcons.Application,
                Text = settings.PetName,
                Visible = false
            };
            Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
            menu.Items.Add("显示 / 隐藏", null, delegate
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (IsVisible) Hide(); else { Show(); Activate(); }
                }));
            });
            menu.Items.Add("摸摸它", null, delegate { Dispatcher.BeginInvoke(new Action(PetTheCat)); });
            menu.Items.Add("算数", null, delegate { Dispatcher.BeginInvoke(new Action(OpenCalculator)); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { Dispatcher.BeginInvoke(new Action(ExitApplication)); });
            icon.ContextMenuStrip = menu;
            icon.DoubleClick += delegate { Dispatcher.BeginInvoke(new Action(delegate { Show(); Activate(); })); };
            return icon;
        }

        private void ExitApplication()
        {
            if (exiting) return;
            exiting = true;
            bubble.Close();
            if (calculatorWindow != null) calculatorWindow.Close();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            if (powerTracker != null) powerTracker.Dispose();
            animationTimer.Stop();
            weatherRefreshTimer.Stop();
            Close();
            Application.Current.Shutdown();
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!exiting)
            {
                e.Cancel = true;
                Hide();
            }
        }

        private Point ToDip(Point screenPixels)
        {
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
            {
                return source.CompositionTarget.TransformFromDevice.Transform(screenPixels);
            }
            return screenPixels;
        }

        private Rect GetCurrentWorkArea()
        {
            try
            {
                Point centerPixels;
                if (IsLoaded)
                {
                    centerPixels = PointToScreen(new Point(Math.Max(0, ActualWidth) / 2.0, Math.Max(0, ActualHeight) / 2.0));
                }
                else
                {
                    Forms.Screen primary = Forms.Screen.PrimaryScreen;
                    centerPixels = new Point(primary.WorkingArea.Left + primary.WorkingArea.Width / 2.0,
                        primary.WorkingArea.Top + primary.WorkingArea.Height / 2.0);
                }

                Forms.Screen screen = Forms.Screen.FromPoint(new Drawing.Point((int)Math.Round(centerPixels.X), (int)Math.Round(centerPixels.Y)));
                Drawing.Rectangle bounds = screen.WorkingArea;
                Point topLeft = ToDip(new Point(bounds.Left, bounds.Top));
                Point bottomRight = ToDip(new Point(bounds.Right, bounds.Bottom));
                return new Rect(topLeft, bottomRight);
            }
            catch
            {
                return SystemParameters.WorkArea;
            }
        }

        private static double Wrap(double value)
        {
            value = value - Math.Floor(value);
            return value < 0 ? value + 1 : value;
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
