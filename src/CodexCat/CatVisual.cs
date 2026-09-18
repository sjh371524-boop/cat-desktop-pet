using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodexCat
{
    internal sealed class CatVisual : FrameworkElement
    {
        private readonly Dictionary<PetState, BitmapSource[]> spriteFrames;
        private readonly BitmapSource[] directionalDragOpenFrames;
        private readonly BitmapSource[] directionalDragBlinkFrames;
        private readonly BitmapSource[] directionalDragLeftOpenFrames;
        private readonly BitmapSource[] directionalDragLeftBlinkFrames;
        private readonly BitmapSource[] pettingRightFrames;
        private readonly Dictionary<BitmapSource, PetTailLayer> pettedTailLayers = new Dictionary<BitmapSource, PetTailLayer>();
        private readonly BitmapSource[] dragTransitionRightFrames;
        private readonly BitmapSource[] dragTransitionLeftFrames;
        private readonly BitmapSource[] petGestureRightFrames;
        private readonly BitmapSource[] petGestureLeftFrames;
        private readonly BitmapSource[] idle1PawRightFrames;
        private readonly BitmapSource[] idle1PawLeftFrames;
        private readonly BitmapSource[] soothedLoweringFrames;
        private readonly BitmapSource pettingHand;
        private readonly BitmapSource[] soothingHandFrames;
        private readonly PetEyeLayer standingEyes;
        private readonly PetEarLayer standingEars;
        private readonly PetEarLayer standingBlinkEars;
        private readonly PetEyeLayer standingTransitionEyes;
        private bool drawingSnapshot;
        private Vector gaze;
        private PetState state;
        private double phase;
        private double progress;
        private Vector dragVector;
        private int dragFacing = 1;
        private BubbleSide petReactionSide = BubbleSide.Right;
        private BubbleSide idle1PawSide = BubbleSide.Left;
        private BubbleSide sootheHandSide = BubbleSide.Right;
        private PoseSnapshot transitionFrom;
        private DateTime transitionStartedUtc;
        private double transitionDurationSeconds;

        private sealed class PoseSnapshot
        {
            public PetState State;
            public double Phase;
            public double Progress;
            public Vector DragVector;
            public int DragFacing;
            public BubbleSide PetReactionSide;
            public BubbleSide Idle1PawSide;
            public BubbleSide SootheHandSide;
            public Vector Gaze;
        }

        internal bool? DragBlinkOverride { get; set; }
        internal bool SuppressStateTransitions { get; set; }

        public CatVisual()
        {
            state = PetState.Stand;
            spriteFrames = LoadSpriteFrames();
            BitmapSource[] openFrames;
            BitmapSource[] blinkFrames;
            LoadDirectionalDragFrames(out openFrames, out blinkFrames);
            directionalDragOpenFrames = openFrames;
            directionalDragBlinkFrames = blinkFrames;
            directionalDragLeftOpenFrames = LoadDirectory(Path.Combine("drag_side", "left_open"));
            directionalDragLeftBlinkFrames = LoadDirectory(Path.Combine("drag_side", "left_blink"));
            dragTransitionRightFrames = LoadDirectory(Path.Combine("drag_transition", "right"));
            dragTransitionLeftFrames = LoadDirectory(Path.Combine("drag_transition", "left"));

            BitmapSource neutralFrame = null;
            BitmapSource[] neutralFrames;
            if (spriteFrames.TryGetValue(PetState.Stand, out neutralFrames) && neutralFrames.Length > 0)
            {
                neutralFrame = neutralFrames[0];
                standingEyes = new PetEyeLayer(neutralFrame);
                standingEars = new PetEarLayer(neutralFrame);
                standingTransitionEyes = new PetEyeLayer(neutralFrame);
            }
            if (neutralFrames != null && neutralFrames.Length > 1)
                standingBlinkEars = new PetEarLayer(neutralFrames[1]);
            petGestureRightFrames = LoadDirectory(Path.Combine("pet_gesture", "right"));
            petGestureLeftFrames = LoadDirectory(Path.Combine("pet_gesture", "left"));
            if (neutralFrame != null) pettedTailLayers[neutralFrame] = new PetTailLayer(neutralFrame, false);
            if (neutralFrames != null && neutralFrames.Length > 1)
                pettedTailLayers[neutralFrames[1]] = new PetTailLayer(neutralFrames[1], false);
            foreach (BitmapSource frame in petGestureRightFrames) pettedTailLayers[frame] = new PetTailLayer(frame, false);
            foreach (BitmapSource frame in petGestureLeftFrames) pettedTailLayers[frame] = new PetTailLayer(frame, true);
            pettingRightFrames = (BitmapSource[])spriteFrames[PetState.Petting].Clone();
            // Only the closed-eye nuzzle pose is directional. Never mirror the
            // open-eyed neutral endpoints: it would swap the heterochromic eyes.
            if (pettingRightFrames.Length > 1)
            {
                TransformedBitmap nuzzle = new TransformedBitmap(pettingRightFrames[1], new ScaleTransform(-1, 1));
                nuzzle.Freeze();
                pettingRightFrames[1] = nuzzle;
            }
            idle1PawRightFrames = BuildActionSequence(LoadDirectory(Path.Combine("idle_1", "paw_right")), neutralFrame);
            idle1PawLeftFrames = BuildActionSequence(LoadDirectory(Path.Combine("idle_1", "paw_left")), neutralFrame);
            soothedLoweringFrames = BuildSoothedLoweringSequence();
            pettingHand = LoadUiImage("petting-hand-v1.png");
            soothingHandFrames = LoadUiDirectory("soothing_hand");
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            IsHitTestVisible = false;
        }

        public PetState State { get { return state; } }
        public bool UsesReferenceSprites { get { return spriteFrames.Values.Any(x => x.Length > 0); } }

        internal Vector Gaze { get { return gaze; } }

        public void UpdateGaze(Point localCursor, double deltaSeconds, bool screenOn)
        {
            Vector target = PetGazeProfile.CanTrack(state, screenOn)
                ? PetGazeProfile.Target(localCursor, new Size(ActualWidth, ActualHeight), phase)
                : new Vector();
            gaze = PetGazeProfile.Smooth(gaze, target, deltaSeconds);
            InvalidateVisual();
        }

        public void Update(PetState newState, double newPhase, double newProgress, Vector movement)
        {
            if (newState != state)
            {
                if (!SuppressStateTransitions)
                {
                    PoseSnapshot sourcePose = CapturePose();
                    if (transitionFrom != null && transitionDurationSeconds > 0.0)
                    {
                        double activeAmount = AnimationTransitionProfile.Ease(
                            (DateTime.UtcNow - transitionStartedUtc).TotalSeconds / transitionDurationSeconds);
                        if (activeAmount < 0.5)
                        {
                            // If another action interrupts very early, the older
                            // pose is still visually dominant. Carry it forward
                            // instead of flashing through the barely-entered pose.
                            sourcePose = transitionFrom;
                        }
                    }
                    transitionFrom = sourcePose;
                    transitionStartedUtc = DateTime.UtcNow;
                    transitionDurationSeconds = AnimationTransitionProfile.DurationSeconds(state, newState);
                }
                else
                {
                    transitionFrom = null;
                    transitionDurationSeconds = 0.0;
                }
                // The outgoing snapshot keeps its previous gaze for the blend.
                // New special poses always use their own authored expressions;
                // on returning to stand the eyes smoothly reacquire the cursor.
                gaze = new Vector();
            }
            state = newState;
            phase = newPhase;
            progress = Clamp01(newProgress);
            dragVector = movement;
            if ((newState == PetState.Dragging || newState == PetState.SettlingFromDrag) && Math.Abs(movement.X) > 0.35)
            {
                dragFacing = movement.X < 0 ? -1 : 1;
            }
            if ((newState == PetState.Petting || newState == PetState.Petted)
                && Math.Abs(movement.X) > 0.01)
            {
                petReactionSide = movement.X < 0 ? BubbleSide.Left : BubbleSide.Right;
            }
            if (newState == PetState.IdleAnimation1 && Math.Abs(movement.X) > 0.01)
            {
                idle1PawSide = movement.X < 0 ? BubbleSide.Left : BubbleSide.Right;
            }
            if ((newState == PetState.SoothingStroke
                || newState == PetState.SoothedLoweringToSleep)
                && Math.Abs(movement.X) > 0.01)
            {
                sootheHandSide = movement.X < 0 ? BubbleSide.Left : BubbleSide.Right;
            }
            InvalidateVisual();
        }

        private PoseSnapshot CapturePose()
        {
            return new PoseSnapshot
            {
                State = state,
                Phase = phase,
                Progress = progress,
                DragVector = dragVector,
                DragFacing = dragFacing,
                PetReactionSide = petReactionSide,
                Idle1PawSide = idle1PawSide,
                SootheHandSide = sootheHandSide,
                Gaze = gaze
            };
        }

        private void DrawPoseSnapshot(DrawingContext dc, PoseSnapshot pose)
        {
            PetState savedState = state;
            double savedPhase = phase;
            double savedProgress = progress;
            Vector savedDragVector = dragVector;
            int savedDragFacing = dragFacing;
            BubbleSide savedPetReactionSide = petReactionSide;
            BubbleSide savedIdle1PawSide = idle1PawSide;
            BubbleSide savedSootheHandSide = sootheHandSide;
            Vector savedGaze = gaze;
            bool savedDrawingSnapshot = drawingSnapshot;

            state = pose.State;
            phase = pose.Phase;
            progress = pose.Progress;
            dragVector = pose.DragVector;
            dragFacing = pose.DragFacing;
            petReactionSide = pose.PetReactionSide;
            idle1PawSide = pose.Idle1PawSide;
            sootheHandSide = pose.SootheHandSide;
            gaze = pose.Gaze;
            drawingSnapshot = true;
            DrawPoseCore(dc);

            state = savedState;
            phase = savedPhase;
            progress = savedProgress;
            dragVector = savedDragVector;
            dragFacing = savedDragFacing;
            petReactionSide = savedPetReactionSide;
            idle1PawSide = savedIdle1PawSide;
            sootheHandSide = savedSootheHandSide;
            gaze = savedGaze;
            drawingSnapshot = savedDrawingSnapshot;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double width = Math.Max(1.0, ActualWidth);
            double height = Math.Max(1.0, ActualHeight);
            double scale = Math.Min(width / 300.0, height / 300.0);
            double offsetX = (width - (300.0 * scale)) / 2.0;
            double offsetY = (height - (300.0 * scale)) / 2.0;

            dc.PushTransform(new TranslateTransform(offsetX, offsetY));
            dc.PushTransform(new ScaleTransform(scale, scale));

            double transitionAmount = 1.0;
            if (transitionFrom != null && transitionDurationSeconds > 0.0)
            {
                double elapsed = (DateTime.UtcNow - transitionStartedUtc).TotalSeconds;
                transitionAmount = AnimationTransitionProfile.Ease(elapsed / transitionDurationSeconds);
                if (transitionAmount >= 0.9999)
                {
                    transitionFrom = null;
                    transitionAmount = 1.0;
                }
            }

            if (transitionFrom != null && transitionAmount < 1.0)
            {
                dc.PushOpacity(1.0 - transitionAmount);
                DrawPoseSnapshot(dc, transitionFrom);
                dc.Pop();

                dc.PushOpacity(transitionAmount);
                DrawPoseCore(dc);
                dc.Pop();
            }
            else
            {
                DrawPoseCore(dc);
            }

            dc.Pop();
            dc.Pop();
        }

        private void DrawPoseCore(DrawingContext dc)
        {

            BitmapSource[] frames;
            if (state == PetState.TailIdle)
            {
                DrawTailIdle(dc);
            }
            else if (state == PetState.EarIdle)
            {
                DrawEarIdle(dc);
            }
            else if (state == PetState.Dragging && directionalDragOpenFrames.Length > 0)
            {
                BitmapSource[] turnFrames = GetDragTransitionFrames();
                if (progress < 1.0 && turnFrames.Length > 0)
                {
                    DrawDragTransition(dc, turnFrames, false);
                }
                else
                {
                    DrawDirectionalWalk(dc);
                }
            }
            else if (state == PetState.SettlingFromDrag && GetDragTransitionFrames().Length > 0)
            {
                DrawDragTransition(dc, GetDragTransitionFrames(), true);
            }
            else if (state == PetState.SoothingStroke)
            {
                DrawSoothingDrowsyCat(dc);
            }
            else if (state == PetState.SoothedLoweringToSleep)
            {
                DrawSoothedLowering(dc);
            }
            else if (state == PetState.Petted && GetPetGestureFrames().Length > 0)
            {
                DrawPettedGestureSmooth(dc, GetPetGestureFrames());
            }
            else if (state == PetState.Petting && spriteFrames.ContainsKey(PetState.Petting)
                && spriteFrames[PetState.Petting].Length > 0)
            {
                DrawPettingCat(dc, spriteFrames[PetState.Petting]);
            }
            else if (state == PetState.IdleAnimation1 && GetIdle1Frames().Length > 0)
            {
                DrawSpriteSequence(dc, GetIdle1Frames());
            }
            else if (spriteFrames.TryGetValue(state, out frames) && frames.Length > 0)
            {
                DrawSpriteSequence(dc, frames);
            }
            else
            {
                DrawProceduralCat(dc);
            }

            if (state == PetState.Petted)
            {
                DrawPetReactionEffects(dc);
            }

            if (state == PetState.Petting)
            {
                DrawPettingHand(dc);
            }

            if (state == PetState.SoothingStroke)
            {
                DrawSoothingBodyHand(dc);
            }

            if (state == PetState.Sleeping || state == PetState.LoweringToSleep || state == PetState.SoothedLoweringToSleep)
            {
                DrawZzz(dc);
            }
        }

        private Dictionary<PetState, BitmapSource[]> LoadSpriteFrames()
        {
            Dictionary<PetState, BitmapSource[]> result = new Dictionary<PetState, BitmapSource[]>();
            result[PetState.Stand] = LoadDirectory("stand");
            result[PetState.Dragging] = LoadDirectory("drag");
            result[PetState.Licking] = LoadDirectory("lick");
            result[PetState.LoweringToSleep] = LoadDirectory("sleep_transition");
            result[PetState.Sleeping] = LoadDirectory("sleep");
            result[PetState.Waking] = LoadDirectory("wake");
            result[PetState.Petting] = LoadDirectory("pet");
            result[PetState.Petted] = LoadDirectory("pet");
            return result;
        }

        private static BitmapSource LoadUiImage(string fileName)
        {
            string path = Path.Combine(AppPaths.UiDirectory, fileName);
            if (!File.Exists(path)) return null;
            try
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        private static BitmapSource[] LoadUiDirectory(string directoryName)
        {
            string directory = Path.Combine(AppPaths.UiDirectory, directoryName);
            if (!Directory.Exists(directory)) return new BitmapSource[0];

            List<BitmapSource> frames = new List<BitmapSource>();
            string[] files = Directory.GetFiles(directory, "*.png")
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (string file in files)
            {
                BitmapSource frame = LoadUiImage(Path.Combine(directoryName, Path.GetFileName(file)));
                if (frame != null) frames.Add(frame);
            }
            return frames.ToArray();
        }

        private BitmapSource[] LoadDirectory(string directoryName)
        {
            string directory = Path.Combine(AppPaths.CharacterDirectory, directoryName);
            if (!Directory.Exists(directory))
            {
                return new BitmapSource[0];
            }

            List<BitmapSource> frames = new List<BitmapSource>();
            string[] files = Directory.GetFiles(directory, "*.png").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (string file in files)
            {
                try
                {
                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(file, UriKind.Absolute);
                    image.EndInit();
                    image.Freeze();
                    frames.Add(image);
                }
                catch
                {
                    // A malformed optional frame must not prevent the pet from starting.
                }
            }

            return frames.ToArray();
        }

        private static BitmapSource[] BuildActionSequence(BitmapSource[] actionFrames, BitmapSource neutralFrame)
        {
            if (neutralFrame == null || actionFrames.Length == 0) return actionFrames;
            List<BitmapSource> sequence = new List<BitmapSource>(actionFrames.Length + 2);
            sequence.Add(neutralFrame);
            sequence.AddRange(actionFrames);
            sequence.Add(neutralFrame);
            return sequence.ToArray();
        }

        private static BitmapSource[] BuildRoundTripSequence(BitmapSource[] actionFrames, BitmapSource neutralFrame)
        {
            if (neutralFrame == null || actionFrames.Length == 0) return actionFrames;
            List<BitmapSource> sequence = new List<BitmapSource>(actionFrames.Length * 2 + 1);
            sequence.Add(neutralFrame);
            sequence.AddRange(actionFrames);
            for (int index = actionFrames.Length - 2; index >= 0; index--)
            {
                sequence.Add(actionFrames[index]);
            }
            sequence.Add(neutralFrame);
            return sequence.ToArray();
        }

        private void LoadDirectionalDragFrames(out BitmapSource[] openFrames, out BitmapSource[] blinkFrames)
        {
            List<BitmapSource> open = new List<BitmapSource>(LoadDirectory(Path.Combine("drag_side", "open")));
            List<BitmapSource> blink = new List<BitmapSource>(LoadDirectory(Path.Combine("drag_side", "blink")));
            if (open.Count == 4 && blink.Count == 4)
            {
                openFrames = open.ToArray();
                blinkFrames = blink.ToArray();
                return;
            }

            open.Clear();
            blink.Clear();
            string path = Path.Combine(AppPaths.CharacterDirectory, "drag_side", "drag-side-sheet-v2-rgba.png");
            if (!File.Exists(path))
            {
                openFrames = open.ToArray();
                blinkFrames = blink.ToArray();
                return;
            }

            try
            {
                BitmapImage sheet = new BitmapImage();
                sheet.BeginInit();
                sheet.CacheOption = BitmapCacheOption.OnLoad;
                sheet.UriSource = new Uri(path, UriKind.Absolute);
                sheet.EndInit();
                sheet.Freeze();

                for (int row = 0; row < 2; row++)
                {
                    int top = (int)Math.Round(row * sheet.PixelHeight / 2.0);
                    int bottom = (int)Math.Round((row + 1) * sheet.PixelHeight / 2.0);
                    for (int column = 0; column < 4; column++)
                    {
                        int left = (int)Math.Round(column * sheet.PixelWidth / 4.0);
                        int right = (int)Math.Round((column + 1) * sheet.PixelWidth / 4.0);
                        CroppedBitmap frame = new CroppedBitmap(sheet, new Int32Rect(left, top, right - left, bottom - top));
                        frame.Freeze();
                        if (row == 0) open.Add(frame); else blink.Add(frame);
                    }
                }
            }
            catch
            {
                open.Clear();
                blink.Clear();
            }

            openFrames = open.ToArray();
            blinkFrames = blink.ToArray();
        }

        private void DrawDirectionalWalk(DrawingContext dc)
        {
            bool hasLeftFrames = dragFacing < 0 && directionalDragLeftOpenFrames.Length == directionalDragOpenFrames.Length;
            BitmapSource[] open = hasLeftFrames ? directionalDragLeftOpenFrames : directionalDragOpenFrames;
            BitmapSource[] blink = hasLeftFrames ? directionalDragLeftBlinkFrames : directionalDragBlinkFrames;
            double wrapped = phase - Math.Floor(phase);
            double exact = wrapped * directionalDragOpenFrames.Length;
            int first = (int)Math.Floor(exact) % directionalDragOpenFrames.Length;
            int second = (first + 1) % directionalDragOpenFrames.Length;
            double frameBlend = AnimationTransitionProfile.Ease(exact - Math.Floor(exact));
            double blinkAmount = GetDragBlinkAmount();

            double stride = Math.Sin(wrapped * Math.PI * 2.0);
            double bob = Math.Abs(stride) * 4.0;
            double forwardLean = Math.Min(2.2, Math.Abs(dragVector.X) / 18.0);

            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(38, 35, 40, 52)), null,
                new Point(150, 271), 104, 10 + Math.Abs(stride) * 1.5);
            if (dragFacing < 0 && !hasLeftFrames)
            {
                dc.PushTransform(new ScaleTransform(-1, 1, 150, 0));
            }
            dc.PushTransform(new RotateTransform(hasLeftFrames ? -forwardLean : forwardLean, 150, 244));
            Rect destination = new Rect(2, -2 - bob, 296, 296);
            if (blinkAmount <= 0.001 || blink.Length != open.Length)
            {
                DrawFrameBlend(dc, open, first, second, frameBlend, destination);
            }
            else
            {
                if (blinkAmount < 0.999)
                {
                    dc.PushOpacity(1.0 - blinkAmount);
                    DrawFrameBlend(dc, open, first, second, frameBlend, destination);
                    dc.Pop();
                }
                dc.PushOpacity(blinkAmount);
                DrawFrameBlend(dc, blink, first, second, frameBlend, destination);
                dc.Pop();
            }
            dc.Pop();
            if (dragFacing < 0 && !hasLeftFrames)
            {
                dc.Pop();
            }
        }

        private double GetDragBlinkAmount()
        {
            if (DragBlinkOverride.HasValue) return DragBlinkOverride.Value ? 1.0 : 0.0;
            uint tick = unchecked((uint)Environment.TickCount);
            double blinkCycle = (tick % 3600) / 3600.0;
            if (blinkCycle < 0.90 || blinkCycle > 0.98) return 0.0;
            if (blinkCycle <= 0.94)
            {
                return AnimationTransitionProfile.Ease((blinkCycle - 0.90) / 0.04);
            }
            return 1.0 - AnimationTransitionProfile.Ease((blinkCycle - 0.94) / 0.04);
        }

        private BitmapSource[] GetDragTransitionFrames()
        {
            return dragFacing < 0 ? dragTransitionLeftFrames : dragTransitionRightFrames;
        }

        private void DrawDragTransition(DrawingContext dc, BitmapSource[] frames, bool settling)
        {
            double amount = AnimationTransitionProfile.Ease(progress);
            double exact = (settling ? 1.0 - amount : amount) * (frames.Length - 1);
            int first = Math.Max(0, Math.Min(frames.Length - 1, (int)Math.Floor(exact)));
            int second = Math.Max(0, Math.Min(frames.Length - 1, first + 1));
            double blend = AnimationTransitionProfile.Ease(exact - Math.Floor(exact));
            double shadowWidth = 72.0 + amount * 31.0;
            if (settling) shadowWidth = 72.0 + (1.0 - amount) * 31.0;
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(34, 35, 40, 52)), null,
                new Point(150, 271), shadowWidth, 10);
            DrawFrameBlend(dc, frames, first, second, blend, new Rect(8, 2, 284, 284));
        }

        private BitmapSource[] GetPetGestureFrames()
        {
            return petReactionSide == BubbleSide.Right ? petGestureRightFrames : petGestureLeftFrames;
        }

        private void DrawPettedGestureSmooth(DrawingContext dc, BitmapSource[] actionFrames)
        {
            BitmapSource[] standFrames;
            if (actionFrames.Length == 0
                || !spriteFrames.TryGetValue(PetState.Stand, out standFrames)
                || standFrames.Length == 0)
            {
                DrawSpriteSequence(dc, actionFrames);
                return;
            }

            BitmapSource neutral = standFrames[0];
            BitmapSource high = actionFrames[0];
            BitmapSource upperMiddle = actionFrames[Math.Min(1, actionFrames.Length - 1)];
            BitmapSource lowerMiddle = actionFrames[Math.Min(2, actionFrames.Length - 1)];
            BitmapSource low = actionFrames[actionFrames.Length - 1];
            BitmapSource first;
            BitmapSource second;
            double blend;

            // Hold the unchanged standing silhouette at both ends, then use
            // longer full dissolves as runtime in-between frames. The paw makes
            // one restrained high-to-low sweep without snapping into or out of
            // the tilted meow pose.
            if (progress < 0.14)
            {
                first = neutral;
                second = neutral;
                blend = 0.0;
            }
            else if (progress < 0.31)
            {
                first = neutral;
                second = high;
                blend = AnimationTransitionProfile.Ease((progress - 0.14) / 0.17);
            }
            else if (progress < 0.47)
            {
                first = high;
                second = upperMiddle;
                blend = AnimationTransitionProfile.Ease((progress - 0.31) / 0.16);
            }
            else if (progress < 0.62)
            {
                first = upperMiddle;
                second = lowerMiddle;
                blend = AnimationTransitionProfile.Ease((progress - 0.47) / 0.15);
            }
            else if (progress < 0.76)
            {
                first = lowerMiddle;
                second = low;
                blend = AnimationTransitionProfile.Ease((progress - 0.62) / 0.14);
            }
            else
            {
                first = low;
                second = neutral;
                blend = AnimationTransitionProfile.Ease((progress - 0.76) / 0.24);
            }

            Rect destination = new Rect(8, 2, 284, 284);
            double tailAngle = PetMotionProfile.PettedTailAngle(progress);
            if (first == second || blend <= 0.001) DrawPettedFrame(dc, first, destination, tailAngle);
            else if (blend >= 0.999) DrawPettedFrame(dc, second, destination, tailAngle);
            else
            {
                dc.PushOpacity(1.0 - blend);
                DrawPettedFrame(dc, first, destination, tailAngle);
                dc.Pop();
                dc.PushOpacity(blend);
                DrawPettedFrame(dc, second, destination, tailAngle);
                dc.Pop();
            }
        }

        private void DrawPettedFrame(DrawingContext dc, BitmapSource frame, Rect destination, double tailAngle)
        {
            PetTailLayer layers;
            if (pettedTailLayers.TryGetValue(frame, out layers)) layers.Draw(dc, destination, tailAngle);
            else dc.DrawImage(frame, destination);
        }

        private void DrawSoothingDrowsyCat(DrawingContext dc)
        {
            BitmapSource[] standFrames;
            if (!spriteFrames.TryGetValue(PetState.Stand, out standFrames) || standFrames.Length == 0)
            {
                DrawProceduralCat(dc);
                return;
            }

            BitmapSource open = standFrames[0];
            BitmapSource sleepy = standFrames[Math.Min(1, standFrames.Length - 1)];
            double firstStrokeDrowsiness = AnimationTransitionProfile.Ease(Clamp01(progress / 0.50)) * 0.48;
            double secondStrokeDrowsiness = AnimationTransitionProfile.Ease(Clamp01((progress - 0.50) / 0.50)) * 0.52;
            double eyelidAmount = Clamp01(firstStrokeDrowsiness + secondStrokeDrowsiness);

            // Both source images share the exact original 256x256 silhouette.
            // Only the eyelids dissolve, so soothing can never flatten, widen
            // or shorten the cat before it lowers to sleep.
            DrawBitmapBlend(dc, open, sleepy, eyelidAmount, new Rect(8, 2, 284, 284));
        }

        private void DrawEarIdle(DrawingContext dc)
        {
            if (standingEars == null) { DrawProceduralCat(dc); return; }
            double breath = Math.Sin(phase * Math.PI * 2) * 1.4;
            Rect destination = new Rect(8, 2 - breath, 284, 284 + breath);
            double angle = PetMotionProfile.IdleEarAngle(progress);
            double blink = standingBlinkEars == null ? 0 : PetMotionProfile.IdleEarBlink(progress);
            if (blink < 0.999)
            {
                dc.PushOpacity(1 - blink);
                standingEars.Draw(dc, destination, angle);
                dc.Pop();
            }
            if (blink > 0.001)
            {
                dc.PushOpacity(blink);
                standingBlinkEars.Draw(dc, destination, angle);
                dc.Pop();
            }
        }

        private void DrawTailIdle(DrawingContext dc)
        {
            BitmapSource[] standing = spriteFrames[PetState.Stand];
            if (standing.Length == 0) { DrawProceduralCat(dc); return; }
            double breath = Math.Sin(phase * Math.PI * 2) * 1.4;
            Rect destination = new Rect(8, 2 - breath, 284, 284 + breath);
            double angle = PetMotionProfile.IdleTailAngle(progress);
            double blink = standing.Length > 1 ? PetMotionProfile.IdleTailBlink(progress) : 0;
            if (blink < 0.999)
            {
                dc.PushOpacity(1 - blink);
                DrawPettedFrame(dc, standing[0], destination, angle);
                dc.Pop();
            }
            if (blink > 0.001)
            {
                dc.PushOpacity(blink);
                DrawPettedFrame(dc, standing[1], destination, angle);
                dc.Pop();
            }
        }

        private BitmapSource[] BuildSoothedLoweringSequence()
        {
            BitmapSource[] lowering;
            if (!spriteFrames.TryGetValue(PetState.LoweringToSleep, out lowering) || lowering.Length == 0)
                return new BitmapSource[0];

            // Copy the sequence: scheduled sleep must keep its existing entry.
            BitmapSource[] result = (BitmapSource[])lowering.Clone();
            BitmapSource[] standing;
            if (spriteFrames.TryGetValue(PetState.Stand, out standing) && standing.Length > 1)
                result[0] = standing[1];
            BitmapSource[] sleeping;
            if (result.Length > 1 && spriteFrames.TryGetValue(PetState.Sleeping, out sleeping) && sleeping.Length > 0)
                result[result.Length - 1] = sleeping[0];
            return result;
        }

        private void DrawSoothedLowering(DrawingContext dc)
        {
            if (soothedLoweringFrames.Length == 0)
            {
                DrawTransitionToSleep(dc, progress);
                return;
            }

            // Start with exactly the closed-eye pose where stroking ended.
            // Keep the shared short dissolve window: long dissolves between
            // upright and crouched silhouettes produce two visible heads.
            // Exact endpoint matching supplies continuity at the state joins.
            double exact = AnimationTransitionProfile.Ease(progress) * (soothedLoweringFrames.Length - 1);
            int first = Math.Min(soothedLoweringFrames.Length - 1, (int)Math.Floor(exact));
            int second = Math.Min(soothedLoweringFrames.Length - 1, first + 1);
            double blend = AnimationTransitionProfile.FrameBlend(exact - first);
            double breathing = Math.Sin(phase * Math.PI * 2.0)
                * AnimationTransitionProfile.Ease((progress - 0.85) / 0.15);
            DrawFrameBlend(dc, soothedLoweringFrames, first, second, blend,
                new Rect(8, 2 - breathing, 284, 284 + breathing));
        }

        private BitmapSource[] GetIdle1Frames()
        {
            return idle1PawSide == BubbleSide.Right ? idle1PawRightFrames : idle1PawLeftFrames;
        }

        private void DrawPetReactionEffects(DrawingContext dc)
        {
            double appear = SmoothStep((progress - 0.20) / 0.13);
            double disappear = 1.0 - SmoothStep((progress - 0.70) / 0.16);
            double opacity = Clamp01(appear * disappear);
            if (opacity <= 0.001) return;

            bool onRight = petReactionSide == BubbleSide.Right;
            dc.PushOpacity(opacity);

            Typeface typeface = new Typeface(
                new FontFamily("SimSun, 宋体"),
                FontStyles.Normal,
                FontWeights.Normal,
                FontStretches.Normal);
            FormattedText meow = new FormattedText(
                "喵～",
                System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),
                FlowDirection.LeftToRight,
                typeface,
                20.8333333333,
                Brushes.Black,
                1.0);
            double textX = onRight ? 296.0 - meow.Width : 4.0;
            Geometry textGeometry = meow.BuildGeometry(new Point(textX, 105.0));
            Pen whiteOutline = new Pen(Brushes.White, 2.0) { LineJoin = PenLineJoin.Round };
            dc.DrawGeometry(Brushes.Black, whiteOutline, textGeometry);
            dc.Pop();
        }

        private void DrawPettingHand(DrawingContext dc)
        {
            if (pettingHand == null) return;

            double enter = AnimationTransitionProfile.Ease(progress / 0.18);
            double leave = 1.0 - AnimationTransitionProfile.Ease((progress - 0.80) / 0.20);
            double opacity = Clamp01(enter * leave);
            if (opacity <= 0.001) return;

            double contactProgress = Clamp01((progress - 0.18) / 0.62);
            // Two deliberate down-and-release strokes make the petting readable
            // at desktop scale. The fingertips remain centred between the ears
            // instead of sliding down either temple.
            double strokeCycle = contactProgress * Math.PI * 4.0;
            double downwardStroke = (1.0 - Math.Cos(strokeCycle)) * 6.0;
            double alongFurStroke = Math.Sin(strokeCycle) * 3.8;
            double offscreenY = -37.0 * (1.0 - enter) - 37.0 * (1.0 - leave);
            Rect destination = new Rect(138.0 + alongFurStroke, -24.0 + offscreenY + downwardStroke, 72.0, 85.0);

            dc.PushOpacity(opacity);
            if (petReactionSide == BubbleSide.Left)
            {
                dc.PushTransform(new ScaleTransform(-1, 1, 150, 0));
            }
            dc.DrawImage(pettingHand, destination);
            if (petReactionSide == BubbleSide.Left)
            {
                dc.Pop();
            }
            dc.Pop();
        }

        private void DrawSoothingBodyHand(DrawingContext dc)
        {
            if (soothingHandFrames.Length == 0) return;

            double enter = AnimationTransitionProfile.Ease(progress / 0.07);
            double leave = 1.0 - AnimationTransitionProfile.Ease((progress - 0.93) / 0.07);
            double twoStrokePosition = Clamp01((progress - 0.04) / 0.92) * 2.0;
            double local = twoStrokePosition - Math.Floor(twoStrokePosition);
            if (twoStrokePosition >= 2.0) local = 1.0;

            // Fade the hand between the two strokes so returning from the lower
            // back to ear level is a lifted reset rather than a visible jump.
            double contactIn = AnimationTransitionProfile.Ease(local / 0.09);
            double contactOut = 1.0 - AnimationTransitionProfile.Ease((local - 0.86) / 0.14);
            double opacity = Clamp01(enter * leave * contactIn * contactOut);
            if (opacity <= 0.001) return;

            Rect destination = PetMotionProfile.SoothingHandBounds(local);
            double exactFrame = Clamp01(progress) * (soothingHandFrames.Length - 1);
            int firstFrame = Math.Max(0, Math.Min(soothingHandFrames.Length - 1, (int)Math.Floor(exactFrame)));
            int secondFrame = Math.Min(soothingHandFrames.Length - 1, firstFrame + 1);
            double frameBlend = AnimationTransitionProfile.Ease(exactFrame - Math.Floor(exactFrame));

            dc.PushOpacity(opacity);
            if (sootheHandSide == BubbleSide.Left)
            {
                // The authored cat's head/flank axis is right of the square's
                // center. Mirror around the cat, not the 300px canvas, so the
                // left fingertips actually contact the ear and flank too.
                dc.PushTransform(new ScaleTransform(-1, 1, 173, 0));
            }
            DrawFrameBlend(dc, soothingHandFrames, firstFrame, secondFrame, frameBlend, destination);
            if (sootheHandSide == BubbleSide.Left)
            {
                dc.Pop();
            }
            dc.Pop();
        }

        private void DrawPettingCat(DrawingContext dc, BitmapSource[] frames)
        {
            DrawSpriteSequence(dc, petReactionSide == BubbleSide.Right ? pettingRightFrames : frames);
        }

        private void DrawSpriteSequence(DrawingContext dc, BitmapSource[] frames)
        {
            if (frames.Length == 0) return;
            double exact;
            int first;
            int second;
            bool cyclic = false;
            if (state == PetState.Stand && frames.Length >= 2)
            {
                // The default pose stays open-eyed most of the time and performs one
                // soft blink late in each breathing loop.
                double wrapped = phase - Math.Floor(phase);
                if (wrapped >= 0.90 && wrapped <= 0.975)
                {
                    double blinkPosition = (wrapped - 0.90) / 0.075;
                    exact = (1.0 - Math.Abs(blinkPosition * 2.0 - 1.0)) * (frames.Length - 1);
                }
                else
                {
                    exact = 0;
                }
                first = (int)Math.Floor(exact);
            }
            else if (state == PetState.Stand || state == PetState.Dragging || state == PetState.Sleeping)
            {
                cyclic = true;
                double wrapped = phase - Math.Floor(phase);
                exact = wrapped * frames.Length;
                first = (int)Math.Floor(exact) % frames.Length;
            }
            else
            {
                exact = AnimationTransitionProfile.Ease(progress) * (frames.Length - 1);
                first = (int)Math.Floor(exact);
            }

            first = Math.Max(0, Math.Min(frames.Length - 1, first));
            second = cyclic ? (first + 1) % frames.Length : Math.Min(frames.Length - 1, first + 1);
            double frameBlend = AnimationTransitionProfile.FrameBlend(exact - Math.Floor(exact));
            double bob = state == PetState.Dragging ? Math.Abs(Math.Sin(phase * Math.PI * 2.0)) * 5.0 : 0.0;
            double breathing = state == PetState.Stand ? Math.Sin(phase * Math.PI * 2.0) * 1.4 :
                (state == PetState.Sleeping ? Math.Sin(phase * Math.PI * 2.0) * 1.0 : 0.0);
            Rect destination = new Rect(8, 2 - bob - breathing, 284, 284 + breathing);
            if (state == PetState.Stand && standingEyes != null)
            {
                // Separate mutable textures prevent a rapid interruption from
                // updating the outgoing pose's gaze while it is being blended.
                BitmapSource open = (drawingSnapshot ? standingTransitionEyes : standingEyes).Frame(gaze);
                DrawBitmapBlend(dc, first == 0 ? open : frames[first],
                    second == 0 ? open : frames[second], frameBlend, destination);
                return;
            }
            DrawFrameBlend(dc, frames, first, second, frameBlend, destination);
        }

        private static void DrawFrameBlend(
            DrawingContext dc,
            BitmapSource[] frames,
            int first,
            int second,
            double amount,
            Rect destination)
        {
            if (frames.Length == 0) return;
            first = Math.Max(0, Math.Min(frames.Length - 1, first));
            second = Math.Max(0, Math.Min(frames.Length - 1, second));
            amount = AnimationTransitionProfile.Clamp01(amount);
            if (first == second || amount <= 0.001)
            {
                dc.DrawImage(frames[first], destination);
                return;
            }
            if (amount >= 0.999)
            {
                dc.DrawImage(frames[second], destination);
                return;
            }

            dc.PushOpacity(1.0 - amount);
            dc.DrawImage(frames[first], destination);
            dc.Pop();
            dc.PushOpacity(amount);
            dc.DrawImage(frames[second], destination);
            dc.Pop();
        }

        private static void DrawBitmapBlend(
            DrawingContext dc,
            BitmapSource first,
            BitmapSource second,
            double amount,
            Rect destination)
        {
            amount = AnimationTransitionProfile.Clamp01(amount);
            if (first == null && second == null) return;
            if (first == null || amount >= 0.999)
            {
                dc.DrawImage(second, destination);
                return;
            }
            if (second == null || first == second || amount <= 0.001)
            {
                dc.DrawImage(first, destination);
                return;
            }
            dc.PushOpacity(1.0 - amount);
            dc.DrawImage(first, destination);
            dc.Pop();
            dc.PushOpacity(amount);
            dc.DrawImage(second, destination);
            dc.Pop();
        }

        private void DrawProceduralCat(DrawingContext dc)
        {
            if (state == PetState.Sleeping)
            {
                DrawSleeping(dc, 1.0);
                return;
            }

            if (state == PetState.LoweringToSleep)
            {
                DrawTransitionToSleep(dc, progress);
                return;
            }

            if (state == PetState.Waking)
            {
                DrawTransitionToSleep(dc, 1.0 - progress);
                return;
            }

            if (state == PetState.SoothedLoweringToSleep)
            {
                DrawTransitionToSleep(dc, progress);
                return;
            }

            DrawStandingFamily(dc);
        }

        private void DrawStandingFamily(DrawingContext dc)
        {
            bool dragging = state == PetState.Dragging;
            bool licking = state == PetState.Licking;
            bool petted = state == PetState.Petted;
            bool petting = state == PetState.Petting;

            double gait = dragging ? Math.Sin(phase * Math.PI * 2.0) : 0.0;
            double breathe = dragging ? Math.Abs(gait) * -3.5 : Math.Sin(phase * Math.PI * 2.0) * 2.0;
            double directionLean = dragging ? Clamp(dragVector.X / 24.0, -1.0, 1.0) * 7.0 : 0.0;
            double sit = licking ? SmoothStep(Math.Min(1.0, progress * 4.0)) : 0.0;
            double bodyY = 128.0 + sit * 30.0 + breathe;
            double bodyHeight = 100.0 - sit * 18.0;
            double headY = 57.0 + sit * 22.0 + breathe * 0.4;
            double headTilt = petting
                ? (petReactionSide == BubbleSide.Right ? 9.0 : -9.0) * Math.Sin(progress * Math.PI)
                : (licking ? -7.0 * SmoothStep(progress) : (dragging ? directionLean * 0.35 : Math.Sin(phase * Math.PI * 2.0) * 1.2));

            DrawShadow(dc, 150 + directionLean, 250, dragging ? 92 : 78, dragging ? 14 : 11, 0.17);

            // Rear legs are darker and rendered first; this creates stable depth while walking.
            double rearA = dragging ? gait * 16.0 : 0.0;
            double rearB = -rearA;
            DrawLeg(dc, 112 + rearA + directionLean, bodyY + 63, 25, 69 - sit * 22, Color.FromRgb(151, 116, 98), false);
            DrawLeg(dc, 188 + rearB + directionLean, bodyY + 63, 25, 69 - sit * 22, Color.FromRgb(143, 107, 91), false);

            DrawTail(dc, bodyY, sit, petted ? PetMotionProfile.PettedTailAngle(progress) / 20.0 :
                (dragging ? gait : Math.Sin(phase * Math.PI * 2.0) * 0.25));

            LinearGradientBrush bodyBrush = CoatBrush(72 + (int)(phase * 80) % 36);
            dc.DrawEllipse(bodyBrush, new Pen(new SolidColorBrush(Color.FromRgb(89, 62, 55)), 3.2),
                new Point(150 + directionLean, bodyY + bodyHeight / 2.0), 73, bodyHeight / 2.0);

            // Front legs are brighter and cross the rear legs in alternating phase.
            double frontA = dragging ? -gait * 19.0 : 0.0;
            double frontB = -frontA;
            bool pawRaised = licking && progress > 0.25;
            if (pawRaised)
            {
                DrawRaisedPaw(dc, 101 + directionLean, 142 + sit * 14, progress);
                DrawLeg(dc, 180 + directionLean, bodyY + 61, 27, 72 - sit * 25, Color.FromRgb(205, 167, 139), true);
            }
            else
            {
                DrawLeg(dc, 112 + frontA + directionLean, bodyY + 61, 27, 72 - sit * 25, Color.FromRgb(211, 174, 145), true);
                DrawLeg(dc, 188 + frontB + directionLean, bodyY + 61, 27, 72 - sit * 25, Color.FromRgb(202, 159, 134), true);
            }

            dc.PushTransform(new RotateTransform(headTilt, 150 + directionLean, headY + 59));
            DrawHead(dc, 150 + directionLean, headY, licking, petted || petting);
            dc.Pop();

        }

        private void DrawHead(DrawingContext dc, double centerX, double top, bool licking, bool petted)
        {
            Brush edge = new SolidColorBrush(Color.FromRgb(89, 62, 55));
            Pen outline = new Pen(edge, 3.4);
            DrawEar(dc, new Point(centerX - 48, top + 28), new Point(centerX - 37, top - 2), new Point(centerX - 15, top + 20));
            DrawEar(dc, new Point(centerX + 48, top + 28), new Point(centerX + 37, top - 2), new Point(centerX + 15, top + 20));
            dc.DrawEllipse(CoatBrush(95), outline, new Point(centerX, top + 57), 61, 53);

            double blinkCycle = phase - Math.Floor(phase);
            bool blink = petted || (blinkCycle > 0.91 && blinkCycle < 0.98);
            double eyeY = top + 54;
            if (blink)
            {
                Pen eyeLine = new Pen(new SolidColorBrush(Color.FromRgb(49, 38, 38)), 4.2);
                dc.DrawLine(eyeLine, new Point(centerX - 31, eyeY), new Point(centerX - 15, eyeY + 1));
                dc.DrawLine(eyeLine, new Point(centerX + 15, eyeY + 1), new Point(centerX + 31, eyeY));
            }
            else
            {
                dc.DrawEllipse(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(55, 43, 42)), 2.0), new Point(centerX - 23, eyeY), 12, 14);
                dc.DrawEllipse(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(55, 43, 42)), 2.0), new Point(centerX + 23, eyeY), 12, 14);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(92, 126, 96)), null, new Point(centerX - 23, eyeY + 1), 6.0, 9.5);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(143, 208, 235)), null, new Point(centerX + 23, eyeY + 1), 6.0, 9.5);
                dc.DrawEllipse(Brushes.Black, null, new Point(centerX - 23, eyeY + 1), 2.6, 7.0);
                dc.DrawEllipse(Brushes.Black, null, new Point(centerX + 23, eyeY + 1), 2.6, 7.0);
                dc.DrawEllipse(Brushes.White, null, new Point(centerX - 25, eyeY - 4), 2.0, 2.0);
                dc.DrawEllipse(Brushes.White, null, new Point(centerX + 21, eyeY - 4), 2.0, 2.0);
            }

            StreamGeometry nose = Polygon(new Point(centerX - 6, top + 75), new Point(centerX + 6, top + 75), new Point(centerX, top + 82));
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(232, 135, 148)), null, nose);
            Pen mouth = new Pen(new SolidColorBrush(Color.FromRgb(82, 53, 57)), 2.1);
            dc.DrawLine(mouth, new Point(centerX, top + 82), new Point(centerX - 8, top + 88));
            dc.DrawLine(mouth, new Point(centerX, top + 82), new Point(centerX + 8, top + 88));

            if (licking && progress > 0.42 && progress < 0.88)
            {
                double tongueLength = 9 + Math.Abs(Math.Sin(progress * Math.PI * 9.0)) * 11;
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(244, 125, 147)), new Pen(new SolidColorBrush(Color.FromRgb(137, 71, 84)), 1.5),
                    new Rect(centerX - 6, top + 86, 12, tongueLength), 6, 6);
            }

            Pen whisker = new Pen(new SolidColorBrush(Color.FromArgb(150, 87, 69, 66)), 1.4);
            dc.DrawLine(whisker, new Point(centerX - 38, top + 79), new Point(centerX - 69, top + 72));
            dc.DrawLine(whisker, new Point(centerX - 39, top + 86), new Point(centerX - 70, top + 91));
            dc.DrawLine(whisker, new Point(centerX + 38, top + 79), new Point(centerX + 69, top + 72));
            dc.DrawLine(whisker, new Point(centerX + 39, top + 86), new Point(centerX + 70, top + 91));
        }

        private void DrawTransitionToSleep(DrawingContext dc, double amount)
        {
            amount = SmoothStep(amount);
            if (amount > 0.72)
            {
                DrawSleeping(dc, (amount - 0.72) / 0.28);
                return;
            }

            double local = amount / 0.72;
            double bodyY = 135 + local * 65;
            double bodyHeight = 96 - local * 42;
            double headY = 62 + local * 99;
            double headX = 150 - local * 24;
            DrawShadow(dc, 150, 252, 78 + local * 28, 11 + local * 3, 0.18);
            DrawTail(dc, bodyY, local, local * 1.3);
            dc.DrawEllipse(CoatBrush(112), new Pen(new SolidColorBrush(Color.FromRgb(89, 62, 55)), 3.2),
                new Point(150, bodyY + bodyHeight / 2), 76 + local * 14, bodyHeight / 2);
            DrawLeg(dc, 110 - local * 12, bodyY + bodyHeight - 12, 25, 48 - local * 20, Color.FromRgb(158, 119, 100), false);
            DrawLeg(dc, 189 + local * 12, bodyY + bodyHeight - 12, 26, 48 - local * 20, Color.FromRgb(207, 165, 139), true);
            dc.PushTransform(new RotateTransform(-local * 17, headX, headY + 55));
            DrawHead(dc, headX, headY, false, local > 0.78);
            dc.Pop();
        }

        private void DrawSleeping(DrawingContext dc, double curl)
        {
            curl = Clamp01(curl);
            DrawShadow(dc, 150, 252, 106, 16, 0.20);
            dc.DrawEllipse(CoatBrush(123), new Pen(new SolidColorBrush(Color.FromRgb(87, 60, 54)), 3.5), new Point(151, 208), 101, 49);
            dc.DrawEllipse(CoatBrush(91), new Pen(new SolidColorBrush(Color.FromRgb(87, 60, 54)), 3.0), new Point(104, 201), 49, 42);

            Pen closedEye = new Pen(new SolidColorBrush(Color.FromRgb(61, 43, 43)), 3.3);
            dc.DrawLine(closedEye, new Point(78, 199), new Point(93, 202));
            dc.DrawLine(closedEye, new Point(111, 202), new Point(126, 198));
            StreamGeometry nose = Polygon(new Point(98, 211), new Point(108, 211), new Point(103, 217));
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(232, 135, 148)), null, nose);

            // The tail wraps around the body as the final sleep pose settles.
            StreamGeometry tail = new StreamGeometry();
            using (StreamGeometryContext ctx = tail.Open())
            {
                ctx.BeginFigure(new Point(230, 207), false, false);
                ctx.BezierTo(new Point(279, 214), new Point(255, 264), new Point(181, 252), true, true);
                ctx.BezierTo(new Point(141, 245), new Point(119, 232), new Point(121 - curl * 10, 222), true, true);
            }
            tail.Freeze();
            Pen tailPen = new Pen(CoatBrush(146), 25);
            tailPen.StartLineCap = PenLineCap.Round;
            tailPen.EndLineCap = PenLineCap.Round;
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(87, 60, 54)), 32), tail);
            dc.DrawGeometry(null, tailPen, tail);
        }

        private void DrawLeg(DrawingContext dc, double x, double y, double width, double height, Color color, bool front)
        {
            if (height < 14) height = 14;
            Brush brush = new LinearGradientBrush(
                front ? Lighten(color, 18) : Darken(color, 8),
                Darken(color, 24),
                new Point(0, 0), new Point(1, 1));
            Pen outline = new Pen(new SolidColorBrush(Color.FromRgb(91, 65, 57)), front ? 3.0 : 2.5);
            dc.DrawRoundedRectangle(brush, outline, new Rect(x - width / 2, y - height / 2, width, height), width / 2, width / 2);
            dc.DrawEllipse(new SolidColorBrush(Lighten(color, 22)), outline, new Point(x, y + height / 2 - 2), width * 0.72, width * 0.38);
        }

        private void DrawRaisedPaw(DrawingContext dc, double x, double y, double animationProgress)
        {
            double lift = SmoothStep(Math.Min(1.0, (animationProgress - 0.25) * 3.1));
            double pawY = y - lift * 49;
            double pawX = x + lift * 20;
            Pen outline = new Pen(new SolidColorBrush(Color.FromRgb(91, 65, 57)), 3.0);
            dc.DrawRoundedRectangle(CoatBrush(80), outline, new Rect(pawX - 13, pawY - 15, 26, 70), 13, 13);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(231, 188, 174)), outline, new Point(pawX, pawY - 9), 15, 12);
        }

        private void DrawTail(DrawingContext dc, double bodyY, double sit, double wave)
        {
            StreamGeometry tail = new StreamGeometry();
            using (StreamGeometryContext ctx = tail.Open())
            {
                ctx.BeginFigure(new Point(206, bodyY + 44), false, false);
                ctx.BezierTo(new Point(250 + wave * 14, bodyY + 33), new Point(269 + wave * 18, bodyY + 83), new Point(236, bodyY + 104 - sit * 15), true, true);
            }
            tail.Freeze();
            Pen outline = new Pen(new SolidColorBrush(Color.FromRgb(89, 62, 55)), 29);
            outline.StartLineCap = PenLineCap.Round;
            outline.EndLineCap = PenLineCap.Round;
            Pen fill = new Pen(CoatBrush(142), 22);
            fill.StartLineCap = PenLineCap.Round;
            fill.EndLineCap = PenLineCap.Round;
            dc.DrawGeometry(null, outline, tail);
            dc.DrawGeometry(null, fill, tail);
        }

        private void DrawEar(DrawingContext dc, Point a, Point b, Point c)
        {
            StreamGeometry ear = Polygon(a, b, c);
            dc.DrawGeometry(CoatBrush(71), new Pen(new SolidColorBrush(Color.FromRgb(89, 62, 55)), 3.2), ear);
            Point innerA = new Point(a.X * 0.76 + b.X * 0.24, a.Y * 0.76 + b.Y * 0.24);
            Point innerB = new Point(b.X * 0.72 + a.X * 0.28, b.Y * 0.72 + a.Y * 0.28);
            Point innerC = new Point(c.X * 0.70 + b.X * 0.30, c.Y * 0.70 + b.Y * 0.30);
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(226, 152, 157)), null, Polygon(innerA, innerB, innerC));
        }

        private void DrawShadow(DrawingContext dc, double x, double y, double radiusX, double radiusY, double opacity)
        {
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(255 * opacity), 42, 35, 46)), null, new Point(x, y), radiusX, radiusY);
        }

        private void DrawZzz(DrawingContext dc)
        {
            double appear = state == PetState.Sleeping ? 1.0 : Clamp01((progress - 0.76) / 0.24);
            if (appear <= 0) return;
            double floatAmount = (Math.Sin(phase * Math.PI * 2.0) + 1.0) * 4.0;
            Typeface typeface = new Typeface(new FontFamily("Comic Sans MS"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            FormattedText text = new FormattedText("Zzz", System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 30, new SolidColorBrush(Color.FromArgb((byte)(230 * appear), 142, 95, 208)), 1.0);
            dc.DrawText(text, new Point(219, 118 - floatAmount));
        }

        private static StreamGeometry Polygon(params Point[] points)
        {
            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                ctx.BeginFigure(points[0], true, true);
                if (points.Length > 1)
                {
                    Point[] rest = new Point[points.Length - 1];
                    Array.Copy(points, 1, rest, 0, rest.Length);
                    ctx.PolyLineTo(rest, true, true);
                }
            }
            geometry.Freeze();
            return geometry;
        }

        private static LinearGradientBrush CoatBrush(int shift)
        {
            byte warmth = (byte)Math.Max(0, Math.Min(22, shift % 23));
            Color light = Color.FromRgb((byte)(224 + warmth / 3), (byte)(190 + warmth / 4), (byte)(159 + warmth / 5));
            Color mid = Color.FromRgb(190, 145, 121);
            Color dark = Color.FromRgb(139, 101, 88);
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0.12, 0.05);
            brush.EndPoint = new Point(0.88, 0.96);
            brush.GradientStops.Add(new GradientStop(light, 0.0));
            brush.GradientStops.Add(new GradientStop(mid, 0.52));
            brush.GradientStops.Add(new GradientStop(dark, 1.0));
            return brush;
        }

        private static Color Lighten(Color color, int amount)
        {
            return Color.FromRgb((byte)Math.Min(255, color.R + amount), (byte)Math.Min(255, color.G + amount), (byte)Math.Min(255, color.B + amount));
        }

        private static Color Darken(Color color, int amount)
        {
            return Color.FromRgb((byte)Math.Max(0, color.R - amount), (byte)Math.Max(0, color.G - amount), (byte)Math.Max(0, color.B - amount));
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static double Clamp01(double value)
        {
            return Clamp(value, 0.0, 1.0);
        }

        private static double SmoothStep(double value)
        {
            value = Clamp01(value);
            return value * value * (3.0 - 2.0 * value);
        }
    }
}
