# 2026-09-02 异瞳与互动动作更新

本次用内置 ImageGen 编辑像素参考及照片参考；没有调用用户 API Key。像素输出作为淡蓝虹膜配色来源，由 `scripts/sync-character-eye-palette.ps1` 在已核验的虹膜范围内机械同步到原始精灵帧；不会使用整张生成图替换角色，也不会把生成图的棋盘背景带入透明素材。

## 最终提示词集

像素参考编辑：

```text
Use case: precise-object-edit. Input image 1 is the exact edit target, a pixel-art calico desktop-pet master sprite. Change ONLY the viewer-RIGHT eye iris (the eye surrounded by black facial fur) from yellow-green to delicate pale sky-blue, retaining its black pupil, white catchlights, dark outline, fine pixel shading and original eye size. The viewer-LEFT eye stays exactly original yellow-green. Preserve absolutely everything else: identical pose, body proportions, canvas position, asymmetrical orange-left black-right calico coat, upright ears, tail, paws, crisp pixel clusters, alpha transparency. Do not redraw or beautify the cat. True transparent background, no checkerboard or added text. This is the new heterochromic identity reference for all future animations. Output one full-body master sprite matching the input composition.
```

照片参考编辑：

```text
Use case: precise-object-edit. Image 1 is the exact edit target, a phone screenshot photo of the owner's calico cat. Change ONLY the iris of the viewer-RIGHT eye (cat's anatomical LEFT eye, eye in the black fur patch) to natural delicate light sky-blue. Leave the viewer-LEFT eye original yellow-green. Preserve both black pupils, reflections, eye outlines, face, pose, body proportions, calico patches, all fur details and the entire screenshot background and UI unchanged. This is an identity reference update, not a redesign or stylistic transformation. One edited photograph with the same composition. Do not add labels, text, decorations, or other animals.
```

## 动画与验收

- 哄睡两次顺毛均从猫耳高度开始，下端终点保留；第一段仍为 2 秒，随后 1 秒直接入睡。
- 喵叫伸爪回应仍为 3 秒，单次前爪高到低动作不变；尾巴单独上下两个完整来回。`PetMotionProfile` 以动作进度定义摆动，边界位置和速度都为零，不依赖帧率。
- 93 张既有动作帧逐一核验：只允许受控虹膜像素改色；没有睁眼的帧保持原样。增加 8 张左侧身开眼/闭眼帧；8 张动作图集重组为透明图集。退役动作保持退役，不因更新眼色恢复播放。
- 改动前素材保留于 `source-art/pre-heterochromia`；该目录不打包进运行素材。
- `scripts/verify.ps1` 包含逐像素素材回归、精确两周期轨迹验证、哄睡接缝验证、计算器和缩放等既有回归检查。
- 底层身份文件：`template-input/pixel-calico-heterochromia.png`、`assets/character/reference/pixel-calico.png`、`template-input/cat-reference.png`。
