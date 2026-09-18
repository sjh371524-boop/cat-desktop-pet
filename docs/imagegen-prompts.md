# Pixel Calico ImageGen 提示规范

## 全局身份锁

以下约束应用于每一个动作：

```text
Use case: stylized-concept or precise-object-edit
Asset type: pixel-art Windows desktop-pet sprite
Identity reference: template-input/pixel-calico-heterochromia.png; photo support: template-input/cat-reference.png. Never revert to the archived two-green-eye reference.
Preserve: very large round heterochromic eyes, viewer-left iris original yellow-green and viewer-right iris pale sky-blue, with unchanged dark pupils and white highlights. Anatomical LEFT eye is always blue: full right-facing profile exposes the green eye; full left-facing profile exposes the blue eye. Preserve this identity through turns and eyelid changes. Small pink triangular nose; white muzzle, central forehead blaze, chest, belly, and front legs; viewer-left crown and face dominated by warm orange mixed with black; viewer-right crown and eye area predominantly black; upright triangular ears; original body proportions; ringed black-brown-orange tail with a bright orange tip; asymmetric coat map recognizable as the same cat.
Style: refined cute pixel art, deliberate crisp square pixel clusters, hand-placed dimensional shading, subtle ordered dithering, rich limited palette, premium indie-game companion sprite.
Canvas: centered full body, consistent square canvas, stable paw baseline, transparent padding.
Required: true transparent RGBA background, one cat, coherent anatomy, crisp nearest-neighbor-compatible pixels.
Exclude: source photo background, vehicle, pavement, phone UI, checkerboard, floor, cast shadow, text, border, accessories, watermark, smoothing, generic-tabby redesign.
```

## 动作提示集

### 默认站立

```text
Full body standing naturally on all four paws, front-facing and looking directly at the viewer. Preserve the original cat's attentive expression. Four visible coherent legs, gentle neutral front lighting, rear legs slightly darker and front legs subtly brighter for depth.
```

### 眨眼

```text
Change only both eyelids: a calm natural blink with short curved dark eyelid lines. Keep pose, coat, silhouette, tail, proportions, shading, baseline, and every unrelated pixel invariant.
```

### 带序号待机动画1

```text
Create two independently drawn four-stage front-facing grooming rows: one raises the screen-right front paw and one raises the screen-left front paw, without mirroring the asymmetric coat identity. Stages: settle seated and extend the selected paw; turn head to paw, squint both eyes into smiling crescents and begin extending a short pink tongue; tongue visibly contacts the paw with a subtle second lick and wrist/head/highlight change; retract tongue, begin closing mouth and lower the paw toward neutral. The raised paw is brighter in front, while the support paw and far limbs are slightly darker. Preserve screen-left orange and screen-right black face patches in both directions. No heart, text, speech effect, sound indicator, floor, shadow or background.
```

### 位移步态 A / B

```text
Create strict full-body side-profile walk cycles for both directions. The right-facing profile shows the original yellow-green iris; the left-facing profile shows the pale-blue iris. Do not mirror eye identity. The body, neck, head, ears, muzzle, nose and visible eye all face the travel direction. Use contact, down, passing and opposite-leg lift phases. All four legs and paws are anatomically connected and offset in depth. Far legs are darker and partly occluded, near legs brighter. Provide a matching closed-eyelid version of every gait phase so blinking remains independent of the step cycle.
```

### 拖动转身桥

```text
Create one strict 4 columns x 2 rows transparent RGBA sprite sheet. Top row: front-facing neutral stand, head/shoulders turn right, three-quarter-right body turn with front paw preparing to step, full right-facing side contact pose. Bottom row independently draws the equivalent left turn while preserving the fixed asymmetric calico coat map rather than mirroring it. Keep identical square cells, scale, paw baseline, lighting, outline and canvas margins; no text, border, shadow or background.
```

### 舔爪

```text
Elegant seated grooming pose: raise the viewer-left front paw to mouth height, tilt head toward it, softly half-close both eyes, open the mouth, and visibly extend a pink tongue to the raised paw. The other front paw supports the body. Make head, eyelids, mouth, tongue, raised paw, support paw, hindquarters, and tail all readable.
```

### 入睡过渡

```text
Four-stage 2-second sequence: front-facing stand; early bend with lowering shoulders, bent elbows, tucking hindquarters, half-lidded eyes and tail beginning to sweep; low pose with chest near ground, paws folded, eyes nearly closed and tail curling forward; final compact curled sleep with paws tucked, head resting low, both eyes closed, and tail wrapped around the body.
```

### 睡眠

```text
Deeply asleep curled pose, all paws tucked naturally, head resting low, eyes fully closed, ears relaxed, ringed orange-tipped tail wrapping around the front and side. No Zzz in the bitmap because the application renders animated Zzz separately.
```

### 醒来

```text
Distinct waking motion, not reverse sleep: ears perk forward, eyes open halfway, tail loosens, both front paws stretch forward, head and chest lift while hindquarters remain low, then rise to the original standing pose.
```

### 醒来半起身补帧

```text
Create one transparent 256-square in-between sprite halfway between the low forward stretch and the fully upright front-facing stand. Lift the torso and shoulders, draw both front paws back underneath the chest so they are nearly vertical but still slightly bent, raise the hindquarters and head, keep the eyes softly awake and ears relaxed. Match the two endpoint sprites in identity, asymmetric coat, pixel style, scale, baseline and lighting; one cat only, no floor, shadow, text or background.
```

### 被摸

```text
Create two independently drawn front-facing interaction rows, one for an effect on screen-right and one for screen-left. Preserve asymmetric coat and heterochromic eye identity. Tilt the head toward the effect; open the mouth with a short pink tongue, NO visible canine teeth or fangs. The selected front paw makes one restrained high-to-low sweep over three seconds. The existing tail moves up/down for exactly two full cycles as a separate runtime layer, with eased neutral endpoints; never stretch the body. Do not draw a human hand, heart, Chinese text or speech effect into the cat bitmap; the application renders those separately.
```

## 后处理

ImageGen 原始输出保留在 Codex 生成目录，项目最终帧位于 `assets/character`。若生成结果没有真实 Alpha，运行：

```powershell
<bundled-python> .\scripts\clean_generated_sprite.py <generated.png> <assets\character\action\frame.png>
```

清理器只移除与画布边缘连通的浅色中性背景，再以最近邻算法缩放到 `256×256` 并对齐底部基线。
