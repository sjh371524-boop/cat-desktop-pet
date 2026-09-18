$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'build.ps1')
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'sync-character-eye-palette.ps1') -VerifyOnly
if ($LASTEXITCODE -ne 0) { throw 'Heterochromic character palette regression failed.' }

$requiredPaths = @(
    'dist\CodexCat.exe',
    'dist\config\settings.json',
    'dist\assets\character\manifest.json',
    'dist\assets\character\drag_side\open\000.png',
    'dist\assets\character\drag_side\blink\000.png',
    'dist\assets\character\drag_side\left_open\000.png',
    'dist\assets\character\drag_side\left_blink\000.png',
    'dist\assets\character\drag_transition\right\000.png',
    'dist\assets\character\drag_transition\left\000.png',
    'dist\assets\character\pet_gesture\right\000.png',
    'dist\assets\character\pet_gesture\left\000.png',
    'dist\assets\character\idle_1\paw_right\000.png',
    'dist\assets\character\idle_1\paw_left\000.png',
    'dist\assets\character\stand\001.png',
    'dist\assets\character\sleep_transition\001.png',
    'dist\assets\character\sleep_transition\003.png',
    'dist\assets\ui\cloud-bubble-v2.png',
    'dist\assets\ui\petting-hand-v1.png',
    'dist\assets\ui\soothing_hand\000.png',
    'dist\assets\ui\soothing_hand\007.png',
    'src\CodexCat\PetWindow.cs',
    'src\CodexCat\CatVisual.cs',
    'src\CodexCat\WeatherService.cs',
    'src\CodexCat\CalculatorEngine.cs',
    'src\CodexCat\CalculatorWindow.cs',
    'src\CodexCat\CalculatorVerifier.cs',
    'src\CodexCat\CalculatorPlacementPolicy.cs'
)

foreach ($relativePath in $requiredPaths) {
    $fullPath = Join-Path $projectRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "Verification failed. Missing: $relativePath"
    }
}

$settingsPath = Join-Path $projectRoot 'config\settings.json'
$settings = Get-Content -LiteralPath $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($settings.idleMinutes -le 0) { throw 'idleMinutes must be greater than zero.' }
if ($settings.idleAnimation1Seconds -ne 60) { throw 'idleAnimation1Seconds must be exactly 60 seconds.' }
if ($settings.defaultIdlesBeforeSleep -lt 0) { throw 'defaultIdlesBeforeSleep cannot be negative.' }
if ($settings.keepAwake -isnot [bool]) { throw 'keepAwake must be a boolean.' }
if ([string]::IsNullOrWhiteSpace($settings.locationName)) { throw 'locationName cannot be empty.' }

$sourceText = (Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\CodexCat') -Filter '*.cs' -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8 }) -join "`n"
$requiredBehaviorMarkers = @(
    'PetState.Dragging',
    'PetState.SettlingFromDrag',
    'PetState.Licking',
    'PetState.LoweringToSleep',
    'PetState.Sleeping',
    'PetState.Waking',
    'PetState.Petting',
    'PetState.SoothingStroke',
    'PetState.SoothedLoweringToSleep',
    'api.open-meteo.com',
    'ScaleUpRequested',
    'ScaleDownRequested',
    'ExitRequested',
    'CalculatorRequested',
    'SootheRequested',
    'KeepAwakeChanged',
    'CreateKeepAwakeControl',
    ([string][char]0x53D6 + [char]0x6D88 + [char]0x4FDD + [char]0x6301 + [char]0x6E05 + [char]0x9192),
    'CalculatorEngine',
    'OpenCalculator',
    'M+',
    ('M' + [char]0x2212),
    'ApplyPercent',
    'ApplyEquals',
    'GetBoundsNear',
    'CountryName',
    'WeatherIconKind',
    'dragFacing',
    'DragBlinkOverride',
    'DrawPetReactionEffects',
    'DrawPettingHand',
    'DrawPettedGestureSmooth',
    'PetMotionProfile.PettedTailAngle',
    'PetMotionProfile.SoothingHandBounds',
    'directionalDragLeftOpenFrames',
    'DrawSoothingDrowsyCat',
    'DrawSoothingBodyHand',
    'CalculatorPlacementPolicy.GetBounds',
    'HoldOpenForCalculator',
    'LoadUiDirectory',
    'CurrentSide',
    ([string][char]0x55B5 + [char]0xFF5E),
    'PetState.IdleAnimation1',
    'IdleSchedulePolicy.Decide',
    'IdleScheduleCoordinator',
    'DrawTailIdle',
    'PetGazeProfile.CanTrack',
    'UpdateCursorGaze',
    'PetEyeLayer',
    'cat.PointFromScreen(cursor)',
    'PetMotionProfile.IdleTailBlink',
    'SecurityProtocolType.Tls12',
    'AnimateWeatherIcon',
    'AnimationTransitionProfile.Ease',
    'SoothingStrokeSeconds',
    'SoothingSequencePolicy.NextState',
    'BuildSoothedLoweringSequence',
    'DrawSoothedLowering',
    'VerifySoothingJoins',
    'SoothedLoweringSeconds',
    'DrawFrameBlend',
    'DrawDragTransition'
)
foreach ($marker in $requiredBehaviorMarkers) {
    if ($sourceText -notmatch [regex]::Escape($marker)) {
        throw "Verification failed. Missing behavior marker: $marker"
    }
}

$forbiddenBehaviorMarkers = @(
    'DrawTinyFangs', 'DrawSpriteTongueFangs', 'SleepyStretch', 'sleepyStretchFrames',
    'CalculatorGreeting', 'calculator_greeting', 'DrawCircularTongue',
    'TongueMouthAnchor', 'EatingTailAngle', 'UsesRaisedPaw'
)
foreach ($marker in $forbiddenBehaviorMarkers) {
    if ($sourceText -match [regex]::Escape($marker)) {
        throw "Verification failed. Removed behavior returned: $marker"
    }
}

# Arithmetic may open/reposition its window but must never change the pet's
# animation, movement direction or idle timers, regardless of the entry point.
$petWindowSource = Get-Content -LiteralPath (Join-Path $projectRoot 'src\CodexCat\PetWindow.cs') -Raw -Encoding UTF8
$calculatorMethod = [regex]::Match($petWindowSource, '(?s)private void OpenCalculator\(\)\s*\{.*?(?=\r?\n        private )')
if (-not $calculatorMethod.Success) {
    throw 'Verification failed. The shared arithmetic entry point was not found.'
}
if ($calculatorMethod.Value -match 'SetState\s*\(|cat\.Update\s*\(|\b(state|phase|progress|lastMovement|stateStartedAt|screenOnInterval|screenOnIdleAnimation1Interval)\s*=') {
    throw 'Verification failed. Opening arithmetic must not change pet animation state or idle timing.'
}
if ($petWindowSource -notmatch 'bubble\.CalculatorRequested\s*\+=\s*delegate\s*\{\s*OpenCalculator\(\);\s*\}') {
    throw 'Verification failed. The bubble must use the same UI-only arithmetic entry point as the tray.'
}
if ($petWindowSource -notmatch 'settings\.DefaultIdlesBeforeSleep,\s*settings\.KeepAwake') {
    throw 'Verification failed. The keep-awake preference is not connected to automatic idle scheduling.'
}
$interactionBubbleSource = Get-Content -LiteralPath (Join-Path $projectRoot 'src\CodexCat\InteractionBubbleWindow.cs') -Raw -Encoding UTF8
if ($interactionBubbleSource -match 'buttonPanel\.Children\.Add\(keepAwake') {
    throw 'Verification failed. Keep-awake returned to the cloud button stack instead of the side controls.'
}
if ($interactionBubbleSource -notmatch 'Canvas\.SetTop\(keepAwakeControl,\s*200\)') {
    throw 'Verification failed. Keep-awake is no longer positioned beneath the exit side control.'
}
if ($petWindowSource -notmatch 'bubble\.SootheRequested\s*\+=\s*delegate\s*\{\s*SootheToSleep\(bubble\.CurrentSide\);\s*\}') {
    throw 'Verification failed. Manual soothing must remain independent of the keep-awake preference.'
}

$calculatorProcess = Start-Process -FilePath (Join-Path $projectRoot 'dist\CodexCat.exe') `
    -ArgumentList '--verify-calculator' -WorkingDirectory $projectRoot -WindowStyle Hidden -Wait -PassThru
if ($calculatorProcess.ExitCode -ne 0) {
    throw "Calculator verification failed with exit code $($calculatorProcess.ExitCode)."
}

$petScaleProcess = Start-Process -FilePath (Join-Path $projectRoot 'dist\CodexCat.exe') `
    -ArgumentList '--verify-pet-scaling' -WorkingDirectory $projectRoot -WindowStyle Hidden -Wait -PassThru
if ($petScaleProcess.ExitCode -ne 0) {
    throw "Pet scaling verification failed with exit code $($petScaleProcess.ExitCode)."
}

$idlePriorityProcess = Start-Process -FilePath (Join-Path $projectRoot 'dist\CodexCat.exe') `
    -ArgumentList '--verify-idle-priority' -WorkingDirectory $projectRoot -WindowStyle Hidden -Wait -PassThru
if ($idlePriorityProcess.ExitCode -ne 0) {
    throw "Idle priority verification failed with exit code $($idlePriorityProcess.ExitCode)."
}

$transitionProcess = Start-Process -FilePath (Join-Path $projectRoot 'dist\CodexCat.exe') `
    -ArgumentList '--verify-animation-transitions' -WorkingDirectory $projectRoot -WindowStyle Hidden -Wait -PassThru
if ($transitionProcess.ExitCode -ne 0) {
    throw "Animation transition verification failed with exit code $($transitionProcess.ExitCode)."
}

$snapshotRoot = Join-Path $projectRoot 'tests\snapshots'
$gazeProcess = Start-Process -FilePath (Join-Path $projectRoot 'dist\CodexCat.exe') `
    -ArgumentList '--verify-gaze' -WorkingDirectory $projectRoot -WindowStyle Hidden -Wait -PassThru
if ($gazeProcess.ExitCode -ne 0) {
    $gazeErrorPath = Join-Path $projectRoot 'tests\gaze-error.txt'
    $gazeError = if (Test-Path -LiteralPath $gazeErrorPath) { Get-Content -LiteralPath $gazeErrorPath -Raw } else { 'No gaze error report.' }
    throw "Gaze verification failed. $gazeError"
}
$weatherProcess = Start-Process -FilePath (Join-Path $projectRoot 'dist\CodexCat.exe') `
    -ArgumentList '--verify-weather' -WorkingDirectory $projectRoot -WindowStyle Hidden -Wait -PassThru
if ($weatherProcess.ExitCode -ne 0) { throw 'Weather cache/parser/transport-policy regression failed. See tests/weather-error.txt.' }
New-Item -ItemType Directory -Force -Path $snapshotRoot | Out-Null
$snapshotProcess = Start-Process -FilePath (Join-Path $projectRoot 'dist\CodexCat.exe') `
    -ArgumentList @('--render-preview', $snapshotRoot) -WorkingDirectory $projectRoot -WindowStyle Hidden -Wait -PassThru
if ($snapshotProcess.ExitCode -ne 0) {
    $snapshotErrorPath = Join-Path $snapshotRoot 'snapshot-error.txt'
    $snapshotError = if (Test-Path -LiteralPath $snapshotErrorPath) { Get-Content -LiteralPath $snapshotErrorPath -Raw } else { 'No error report was created.' }
    throw "Snapshot renderer failed with exit code $($snapshotProcess.ExitCode). $snapshotError"
}
$expectedSnapshots = @(
    '17-gaze-center.png', '17-gaze-left.png', '17-gaze-right.png', '17-gaze-up.png', '17-gaze-down.png',
    '01-stand.png', '02-drag.png', '02b-drag-left-blink.png', '02c-idle1-paw-right.png',
    '14-idle-tail-0000.png', '14-idle-tail-0530.png', '14-idle-tail-1000.png',
    '19-ear-idle-00.png', '19-ear-idle-06.png', '19-ear-idle-25.png', '19-ear-idle-50.png',
    '15-weather-icon-test-Sunny.png', '15-weather-icon-test-Cloudy.png', '15-weather-icon-test-Rain.png',
    '02ba-drag-left-blue-eye.png', '02bb-drag-right-original-eye.png',
    '02d-idle1-paw-left.png', '03-lick.png', '04-lowering.png', '05-sleep.png',
    '02e-turn-right.png', '02f-turn-left.png', '02g-settle-right.png',
    '06-wake.png', '07a-petting-right.png', '07aa-petting-left.png',
    '07ab-petted-enter.png', '07-petted.png', '07b-petted-left.png',
    '07ba-petted-exit.png', '08-bubble.png', '09-bubble-sleep.png',
    '07e-soothe-first-stroke.png', '07f-soothe-second-stroke.png',
    '07ea-soothe-ear-start-right.png', '07eb-soothe-ear-start-left.png',
    '13-tail-left-00.png', '13-tail-left-12.png', '13-tail-right-00.png', '13-tail-right-12.png',
    '07g-soothe-stroke-end.png', '07ga-soothed-lowering-start.png',
    '07gb-soothed-lowering-early.png', '07h-soothed-lowering.png', '07ha-soothed-lowering-end.png',
    '10-sleeping-label.png', '11-calculator.png', '12-bubble-calculator-below.png'
)
foreach ($snapshot in $expectedSnapshots) {
    if (-not (Test-Path -LiteralPath (Join-Path $snapshotRoot $snapshot))) {
        throw "Expected snapshot was not created: $snapshot"
    }
}

Write-Host 'Static checks and compilation passed.' -ForegroundColor Green
