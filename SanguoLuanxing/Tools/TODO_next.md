# SanguoLuanxing · 待做清单（交接给新 session）

> 记录时间：2026-11-06
> 工程：`c:\Users\FanBin\Documents\trae_projects\r\unity\SanguoLuanxing`
> 编译校验：`powershell -File c:\Users\FanBin\Documents\trae_projects\r\unity\_check.ps1`
> （Unity 编辑器被用户占用，无法跑批量截图；只能编译校验 + 请用户 Play 目视确认）

---

## 核心目标（用户原话）

「后端不用 单机可以了 但是 玩法继续丰富 继续丰富任务和玩法
  是要一个小人物 最后帮助君主一同三国」

已落地：仕途（官阶）系统 + 官阶门槛 + 分支结局 + 支线任务 + 野外偶遇
      + 第七章「定天下 · 渭水」（终章闭环）
      + **旗号分支结局系统**（本轮）
      + **NPC 立绘去重**（本轮）
      + **主公形象随官阶变化**（本轮）
      + **野外偶遇连锁**（本轮）
      + **副本战后评级**（本轮）

---

## 本轮完成：旗号分支结局系统（P0）

### 新增文件
- `Assets/Scripts/Game/Flags.cs` —— 旗号系统本体

### 旗号模型
- **维度（Dim）**：同维度的旗号互斥。`Flags.Set` 会顶掉同维度旧旗号
  （玩家重刷副本改主意时，后选覆盖先选）。
- **Core 标记**：Core 的旗号串成结局画面上的「你走的这条路」；
  非 Core 的只在结局里补一句评语。
- 存储：`Progress.Data.flagsCsv`（`|` 分隔），由 `Game.PickChoice` → `Flags.Set` 写入。

### 维度表（主线 5 个 + 支线 2 个）
| 维度 | 章节 | 选择 A | 选择 B |
|---|---|---|---|
| origin | 一章 桃林 | swore 桃园立誓 | wander 先走一走 |
| sishui | 二章 汜水 | drank 温酒出战 | sober 刀未出鞘 |
| changban | 三章 长坂 | mercy 长坂护民 | ruthless 长坂斩将 |
| luoyang | 五章 洛阳 | burn 献策焚城 | spare 力保洛阳 |
| weihe | 七章 渭水 | pardon 渭水释降 | slay 渭水斩尽 |
| refugee | 五章 粥棚（非 Core） | saved 分粟救人 | walked 转身就走 |
| guandu | 六章 官渡（非 Core） | ambitious 志在有名 | nameless 仍是无名 |

### 新增的选择点（Story.cs）
- `T3_Intro`（长坂）：刘备问「护百姓过河 vs 随张飞冲阵斩将」→ mercy / ruthless
- `T5_Intro`（洛阳）：曹操问「烧城断后路 vs 留一条生路」→ burn / spare
- `T7_Outro`（渭水）：曹操问「追上去斩尽 vs 遣使说降」→ slay / pardon
- 旧有的 `swore`/`wander`/`drank`/`sober`/`saved`/`walked`/`nameless`/`ambitious`
  全部登记进 `Flags.Defs`，此前它们只是裸字符串、结局完全没用上。

### 结局画面（Game.DrawEnding）
- 新增一行「你走的这条路　桃园立誓 · 温酒出战 · 长坂护民 …」
- 下方逐条追加每个旗号的评语（主线在前，支线在后）
- 排版重排：标题 72 → 官阶 132 → 路径 160 → 正文 192 → 旗号评语 306 → 小结 440 → 评语 472

### 仕途页（MetaUI.DrawCareerTab）
- 左面板加「旗号 · 你走的路」区块
- 逐条列出 5 个主线维度的当前旗号（未定则灰显）

---

## 本轮完成：NPC 立绘去重（P1）

### 新增素材（`Tools/gen_npc_variants.py` 生成，14 张）
`npc/` 下：`zhaocang`(斥候) `muren`(牧人) `fisher`(渔翁) `boy`(孩童)
`herd`(牧童) `scribe`(书吏) `elder`(老兵) `postman`(驿卒) `clerk`(关吏)
`merchant`(店主) `veteran`(老卒) `weaver`(织工) `maiden`(织女) `woman`(妇人)

外加主公「倚重版」：`npc/cao_cao_trust.jpg`（紫袍）`npc/liu_bei_trust.jpg`（赭金）

### 复用问题的修复
原复用情况（一个立绘被 6 个角色共用）：refugee×6 / guard×5 / scout×3 / wine×2 / suwan×2 / afu×3
现每个角色一张独立立绘。

### 关键踩坑（调色脚本）
1. **不能靠色相区间区分皮肤和衣服**：褐色粗布袍和肤色在 HSV 上几乎重合，
   用「h∈8..48 就算皮肤」会把整件袍子当成皮肤保留 —— 结果衣服根本没变色。
   正确做法：`face_skin_mask()` 自动定位人脸（YCrCb 候选 + 上部 24% 内最大连通块），
   取脸部肤色作基准，再按「与肤色高度接近」扩散出全部皮肤（含手），
   并用 `binary_dilation` 扩一圈防脸颊边缘被染色。
2. **金饰判定也要卡紧**（h 38..60 + s>0.45 + v>0.45），否则褐色布衣被当金饰保留。
3. **绿幕掩膜要放宽**（h 50..185 + s>0.16 + v>0.14），把边缘半透明像素也算进去，
   否则人物边缘会出色晕。
4. **孩童/牧童用「缩小主体」**：抠掉绿幕取包围盒 → 缩到 66~70% → 贴回绿幕画布底部，
   人物在世界里就明显比别的 NPC 矮一截。
5. 绿幕像素必须原样保留；不能直接刷纯色（会把褶皱压平成剪影）。

---

## 本轮完成：主公形象随官阶变化（P1）

- `Game.LeaderArt(path)`：官阶 ≥ 柱石（index 4）时，
  `npc/cao_cao.jpg` → `npc/cao_cao_trust.jpg`，
  `npc/liu_bei.jpg` → `npc/liu_bei_trust.jpg`
- 剧情立绘（`Game.StoryBody`）与野外 NPC 立绘（`WorldExplore.AddNpc`）都走这个转换

---

## 本轮完成：野外偶遇连锁（P2）

### 机制（`WorldEvents.cs` + `Progress.cs`）
- `EvDef` 新增：`Id`（存档用）、`After`（前置事件 id）、`SetsFlag`（留下的旗号）
- `SaveData` 新增 `evSeenCsv`（已遇过的偶遇 id）
- `Progress.EvSeen(id)` / `MarkEvSeen(id)`
- `WorldEvents.Pick(rng, progress)` 改为带 Progress：
  1. 先看有没有「回访」事件可触发（前置已遇过且自己还没遇过）→ 优先级最高
  2. 否则按稀有度随机（避开已遇过的）
  3. 同类都遇过了 → 退回任意未遇过的

### 4 条连锁
| 前置 | 回访 | 旗号 |
|---|---|---|
| ev_veteran 路边老卒 | ev_veteran_return 回访的老卒 | ev_veteran_kept |
| ev_children 逃难的孩子 | ev_children_return 孩子的回信 | ev_children_saved |
| ev_wounded 路边的伤者 | ev_wounded_return 伤者的报答 | ev_wounded_helped |
| ev_bundle 道旁的包袱 | ev_bundle_owner 寻来的失主 | ev_bundle_returned |

这 4 个旗号已登记进 `Flags.Defs`（非 Core），结局里各补一句。

---

## 本轮完成：副本战后评级（P2）

- `Game.BattleGrade(battle)`：按剩余血量比例分四档
  - ≥80% 毫发无伤（铜钱 +30）
  - ≥50% 从容破敌（+15）
  - ≥25% 浴血而出（+8）
  - <25% 险胜（铜钱 +40、声望 +5）—— **险胜给得最多**，鼓励敢冒险
- 胜利画面新增一行「战后评级」（`Game.gradeLine`），排版整体上移 10px

---

## 待做 1：请用户 Play 目视确认的点

1. **旗号是否真的触发**：走一遍第 3/5/7 章，确认选择后弹「旗号 · 长坂护民」提示，
   结局画面出现「你走的这条路」。
2. **结局画面排版**：旗号评语最多 6 条，126px 高度够不够？会不会挤到「一生小结」？
3. **仕途页左面板**：新加了 5 行旗号列表，会不会溢出面板底部（y > 634）？
4. **新立绘观感**：14 张新立绘的调色是否协调？boy/herd 的「矮一截」在世界里明显吗？
5. **主公倚重版**：官阶到柱石后，刘备/曹操换紫袍/赭金版，会不会太突兀？
6. **偶遇连锁**：连上后先遇「路边老卒」，隔一会该出现「回访的老卒」——
   现在回访优先级最高，是否会打断普通事件的节奏？
7. **战后评级**：险胜给 +40 铜钱 +5 声望，会不会让刷副本收益过高？

---

## 待做 2：可继续丰富的玩法（按性价比排序）

### P0 ★ 更多支线章节 / 第八章
终章已闭环，若要继续扩展：
- 加第八章（南中之战？）——需要新 Boss + 新地图 + 新剧情
- 或在现有章节里加更多支线任务（当前每章 4~6 条）

### P1 NPC 立绘补充
`npc/` 现有 26 张。仍可补：
- 苏婉之外的更多女性角色（绣娘、医女、女将）
- 更现代化的甲胄（弓手、骑兵）
- `char/player.jpg` 只有一张主角像，可用同样手法做「换装版」

### P1 主公形象进一步细分
现在只有「普通版 / 倚重版」两档。可做成三档（白身 / 将军 / 与君同席）。

### P2 旗号系统扩展
- 现在只在结局用。可以在：
  - 野外偶遇里根据旗号给不同台词（如「长坂护民」路过的伤者更愿意跟你说话）
  - 仕途升阶评语按旗号微调
  - Boss 战开场白按旗号变化（长坂斩将 vs 长坂护民 对张郃说的话不同）

### P2 副本难度梯度
现在七张地图难度如何？后期 Boss 的血量/招式是否需要再调。

---

## 关键实现备忘

- **结局在 `Game.DrawEnding()`**，按 `Career.RankOf(Progress)` 分 4 档（官阶），
  再叠加 `Flags.AllTaken(Progress)`（旗号）。旗号维度 = 「味道」，官阶 = 「高度」。
- **旗号机制**：`Progress.SetFlag(id)` / `HasFlag(id)` / `ClearFlag(id)`，
  存档字段 `flagsCsv`（`|` 分隔）。`Game.PickChoice` 已自动 `Flags.Set`。
  **新增旗号必须登记进 `Flags.Defs`**，否则结局和仕途页看不到。
- **同维度互斥**：新增旗号时给正确的 `Dim`，`Flags.Set` 会自动顶掉同维度兄弟。
- **`.Opts()` 是 `Line` 实例方法**，必须写成 `Q(...).Opts(...)` 同一表达式，不能跨行断开。
- **中文引号 `"` 不能出现在 C# 字符串里**（会解析成语法错）。
- `Choice` 第三个参数是 flag id：`new Choice("文本", new[]{...}, "flagName")`
- **每个 Boss 需要自己的 `assets/anim/boss_<id>/` 目录**（含 .meta），缺目录会静默不播动画。
- **新 Boss 还要写进 `Assets/StreamingAssets/data/bosses.json`**（DataDb 会覆盖内置值）。
- **新地图缩略图**要加进 `MetaUI.MapThumbs`（长度 = 章节数），
  `ThumbCache` / `ChapterNames` / `NodePos` 三个数组同步加项。
- **新野外区域**要同时加：`Regions` / `MapOrder` / `RegionSpawn`（长度必须匹配）/ `BuildSpots` 的 case。
- **DataDb 会覆盖内置默认值**：新剧情段若要能被 story.json 覆盖，
  记得把段落 id 加进 `story.json` 的 `_可用段落id` 列表（那个列表只是文档说明，不影响运行）。
- **素材调色**：见上方「关键踩坑」5 条。
- 新增立绘要生成 `.meta`（GUID 唯一），否则 Unity 重新导入会有警告。

---

## 本轮改动文件清单

新增：
- `Assets/Scripts/Game/Flags.cs`
- `Assets/StreamingAssets/assets/npc/{zhaocang,muren,fisher,boy,herd,scribe,elder,postman,clerk,merchant,veteran,weaver,maiden,woman}.jpg`
- `Assets/StreamingAssets/assets/npc/{cao_cao_trust,liu_bei_trust}.jpg`
- `Tools/gen_npc_variants.py`

修改：
- `Assets/Scripts/Game/Game.cs`（结局旗号叠加 + 主公倚重版 + 战后评级）
- `Assets/Scripts/Game/Progress.cs`（ClearFlag / evSeenCsv / EvSeen / MarkEvSeen）
- `Assets/Scripts/Game/Flags.cs`（新建）
- `Assets/Scripts/Game/MetaUI.cs`（仕途页旗号区块）
- `Assets/Scripts/Game/WorldEvents.cs`（Id / After / SetsFlag + 4 条连锁 + Pick 带 Progress）
- `Assets/Scripts/Game/WorldExplore.cs`（立绘引用更新 + 偶遇记 Seen/旗号 + 主公倚重版）
- `Assets/Scripts/Game/Quests.cs`（立绘引用更新）
- `Assets/Scripts/Core/Story.cs`（T3/T5/T7 三处选择点 + 14 处立绘常量与引用）
- `Assets/StreamingAssets/data/story.json`（可用段落 id 补齐 + 加 _旗号 说明）
