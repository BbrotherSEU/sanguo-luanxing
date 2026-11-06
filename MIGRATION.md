# 《三国 · 乱世行》Unity 迁移工程

路径：`r/unity/SanguoLuanxing`　目标版本：**Unity 2022.3 LTS（2022.3.62f1）· 内置渲染管线 · Gamma 色彩空间**

---

## 0. 为什么要换引擎（对应三条反馈）

| 反馈 | 网页版真实瓶颈 | Unity 怎么解决 |
|---|---|---|
| 贴图太差 | 角色是**单张 AI 图**做的 billboard，没有序列帧、没有骨骼，图再清晰也只是一张会抖的贴纸 | Sprite Sheet / Spine 骨骼动画 + Animation Event，一个走路循环 8~12 帧；材质可加 Normal/Emissive，配上后处理（Bloom、色彩分级）整体质感立刻上一档 |
| 打击感太差 | 打击感 = **命中帧对齐 + 顿帧 + 音效 + 位移**。网页版只能靠定时器凑，帧率一抖就散 | Animation Event 在「刀落」那一帧精确回调，顿帧用 `Time.timeScale`/自写冻结，粒子系统 + 屏幕震动/径向模糊 |
| 行动不顺畅 | 浏览器主线程瓶颈：纹理上传、GC、`requestAnimationFrame` 不稳 | 原生线程 + 独立渲染线程，60/120fps 稳定；Unity 的 `Update`/`FixedUpdate` 时钟更可预测 |

结论：**引擎不是贴图与手感的救命药，但它是这两件事的前提**。真正要补的是「序列帧/骨骼动画 + 命中帧驱动」这条美术管线，Unity 让这条管线可落地。

---

## 1. 怎么跑起来

1. 装 **Unity Hub** → 安装 **2022.3.x LTS**（勾选 Windows Build Support / IL2CPP 可选）。
2. Hub → `Add project from disk` → 选 `r/unity/SanguoLuanxing`。
3. 首次打开会自动执行 `Assets/Editor/ProjectSetup.cs`：建空场景 `Assets/Scenes/Main.unity`、写 BuildSettings、设 Gamma 色彩空间与 1280×720 窗口。
4. 直接按 **Play**。场景是空的，所有内容由 `Boot.cs` 在运行时用代码生成，不需要手动摆物体。

> 如果没有自动建场景，手动：`File → New Scene → Empty`，存为 `Assets/Scenes/Main.unity`，再按 Play 即可。

操作：`WASD` 移动 · 左键轻击（三段） · 右键重击 · 空格/Shift 闪避 · `Q/E/R` 技能 · `7/8/9` 升技能 · `Esc` 回标题 · 空格推进剧情。

---

## 2. 模块对照表（网页版 → Unity C#）

| 网页版文件 | Unity 文件 | 说明 |
|---|---|---|
| `core/utils.js` | `Core/Util.cs` | 距离/角度/帧率无关插值 `K(rate,dt)`、深度排序 `SortOrder(z,bias)` |
| `core/assets.js`（chromaKey / 镜像平铺） | `Core/Tex.cs` | 逐像素移植：四角取背景色 → 阈值去背(58/122) → 去绿边 → 2px 深褐描边 → 水印区裁除 → 包围盒裁切；地面 2×2 镜像无缝平铺 |
| `data/enemies.js` / `maps.js` / `skills.js` / `factions.js` / `items.js` | `Core/GameData.cs`、`Game/Items.cs`、`Core/Story.cs` | 全部数值与中文文案 1:1 迁移；剧情单独放 `Story.cs` |
| `game/progress.js` | `Game/Progress.cs` | 存档 `sanguo.luanxing.save.v1`（PlayerPrefs + JsonUtility），等级/属性/装备/技能点公式一致 |
| `game/arena.js` | `Game/Arena.cs` | 140×140 地面 Quad + Standard 材质 + 镜像贴图 scale 6.5；雾 30~66；18 棵树作遮挡物与碰撞 |
| `game/cameraRig.js` | `Game/CameraRig.cs` | 2.5D 相机、30Hz 阻尼跟随、trauma² 抖动、FOV punch |
| `game/fx.js` | `Game/Fx.cs` | 池化 Sprite 特效：刀光/地环/闪光/光柱/粒子，零 GC |
| `core/audio.js` | `Game/Sfx.cs` | 程序化合成 11 种音效（AudioClip.Create），接口 `Play(key, vol)` 可无痛换成真实音源 |
| `game/player.js` | `Game/PlayerCtrl.cs` | 三段轻击/重击/闪避/三技能状态机；前踏 lunge、顿帧、推镜、挤压拉伸全部保留 |
| `game/enemy.js` | `Game/EnemyCtrl.cs` | idle/chase/windup/attack/recover/stagger/charging/slam；Boss 三招 + 二阶段（+18% 速、召 2 小怪） |
| `game/battle.js` | `Game/Battle.cs` | 敌群/箭矢/拾取/遭遇包/Boss 触发/分离（玩家 30%）/顿帧/结算 |
| `ui/hud.js` | `Game/Hud.cs` | IMGUI HUD：气血气力精力、技能冷却、Boss 血条、飘字、Toast（`Font.CreateDynamicFontFromOSFont` 取系统中文字体） |
| `main.js`（流程） | `Game/Game.cs` + `Core/Boot.cs` | 标题→择阵营→剧情→战斗→结算→下一关；`GamePhase` 状态机 |
| `public/assets/*` | `Assets/StreamingAssets/assets/*` | 17 张图原样搬运，运行时 `File.ReadAllBytes + LoadImage` 解码 |

---

## 3. 与网页版的差异（有意为之）

- **场景用代码生成**：不写 `.unity` YAML，避免手工维护；后续要做正式关卡再换成 Prefab/Scene。
- **HUD 用 IMGUI**：零资源依赖、改代码即改 UI；正式版建议换 uGUI 以便美术排版。
- **SpriteRenderer 排序**：用 `SortOrder(z, bias)` 模拟网页版 `renderOrder` 分层（影子 -2 / 角色 0 / 特效 60~80 / 地环 -30）。
- **音效全程序化**：先保证「有反馈」，真实音源后续直接替换 `clips` 表。

---

## 4. 下一阶段（按优先级）

### P0 · 手感与美术（正对应用户的三条反馈）
1. **角色序列帧**：给每个角色做 `idle / walk / attack1-3 / heavy / hurt / dash / death` 帧图（AI 出图 → 抠像 → 拼 Sprite Sheet），Unity 里用 `Animator` + `Animation Clip`，轻击三段各自独立 clip。
2. **Animation Event 对齐命中帧**：把 `PlayerCtrl.Strike()` 从计时器改成「第 N 帧事件回调」，敌我同理（`ExecuteAttack`）。这是打击感从「凑」变成「准」的关键一步。
3. **顿帧改真实冻结**：命中时 `Time.timeScale = 0.02f` 持续 2~4 帧（或自写 `hitStop` 同现在逻辑），相机抖动用 rawDt。
4. **后处理**：URP 或内置 Post Processing（Bloom + Vignette + 轻微色偏），敌人受击加白闪材质。

### P1 · 内容与系统
5. 装备背包/升级界面（`Items.UpgradeCost/UpgradeChance` 已就绪，缺 UI）。
6. 技能点分配界面（现有 7/8/9 快捷键，缺可视化）。
7. 真实音源：`Sfx` 换 `Resources.Load<AudioClip>`，保持 `Play(key)` 接口。
8. 背景远景（山影/云层视差板），当前只有雾融合。

### P2 · 工程化
9. `Shader.Find("Standard")` 打包时会被剔除 → 打包前在 `Project Settings → Graphics → Always Included Shaders` 里加入 `Standard`。
10. 存档 `ItemDef.uid` 目前每次启动从 1 重新计数，读档后可能与旧 uid 撞号 → 读档时把 `uidSeq` 抬到已有最大 uid 之上。
11. `WebGL / Android` 平台需要把 `StreamingAssets` 读取改成 `UnityWebRequest`（桌面平台 `File.ReadAllBytes` 正常）。

---

## 5. 已知状态

- 全部 C# 已按 Unity 2022 API 手写核对（无 External 依赖，纯内置管线 + UGUI/IMGUI 模块）。
- 角色序列帧已全部到位：`anim/{角色}/{状态}@{帧数}.jpg` 共 40 条帧条 / 145 帧，覆盖
  player / bandit / archer / brute / boss_cheng / boss_huaxiong / boss_zhanghe / boss_lvbu。
  帧条为等宽格 + 人物按身体轴居中，`Tex.CutoutFrames` 按原图宽等分切格，
  因此各帧人物水平位置一致（实测轴漂移 0~2 px），播放不再左右乱跳。
- 帧条总显存约 115 MB（RGBA32 + mipmap），单条帧条最大宽 6276 px（低于 Unity 16384 纹理上限）。
- 网页版仍保留在 `r/game`，可作数值与表现对照基线（`npm run dev`）。
