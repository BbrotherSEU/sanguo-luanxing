# SanguoLuanxing · 下一阶段待做清单（三线并行）

> 记录时间：2026-11-07
> 工程：`c:\Users\FanBin\Documents\trae_projects\r\unity\SanguoLuanxing`
> 编译校验：`powershell -File c:\Users\FanBin\Documents\trae_projects\r\unity\_check.ps1`
> （Unity 编辑器被用户占用，无法跑批量截图；只能编译校验 + 请用户 Play 目视确认）
> 远端：`https://github.com/BbrotherSEU/sanguo-luanxing`（public，main 分支）

---

## 上一阶段已完成并已推送（commit `e0a157d`）

- 地面全黑修复：`DayCycle.Update()` 用私有累加器 `t`（初值 0 = 午夜）覆盖 `Time01`，
  把 WorldBuilder 设好的 0.30（上午）冲掉 → 日光强度只有 0.069（正常 1.288），
  地面被压成近黑。改为直接累加在 `Time01` 上。
- 走路滑行：走路帧率 6fps → 11fps（实测素材步长 1.16 世界单位，速度 6.2 u/s 需 ~10.7fps，
  6fps 会让脚比地面慢 78%）；野外移动加加速/减速插值；步频跟随实际速度（`anim.SetRate`）。
- 左右摇摆：去掉野外与战斗端行走时叠加的左右平移；朝向判定加 0.15 死区。
- **素材朝向统一**：`walk@4.png` 原生朝左，而 `player.jpg`/`idle@2`/`walk@6`/`attack1@4`
  全部朝右 → 走停切换时整个人翻面。已镜像 `walk@4.png`（`fix_player_walk.py` 同步加镜像步骤）。

**仍需用户 Play 目视确认**：地面亮度、按 A 键是否面朝左、走停是否还翻面、11fps 脚步是否跟手。

---

## 本阶段三大任务（用户 2026-11-07 提出）

> 原话：
> 1. 优化场景，研究一下什么建模会让整体游戏效果变好
> 2. 增加日常养成玩法（种田/跑环/经商等，适合三国题材的）
> 3. 增加攻城玩法（随声望+武力增长，玩家带兵攻城，AI 自动打：武将+士兵 vs 守城武将+士兵）
>
> 要求：**先分析，形成 TODO 存下来，后续再一个一个做。**

下面 3 个任务已做完调研（见文末「调研来源」），本文件是执行清单。

---

# 任务一：场景建模优化（P0 · 视觉地基）

## 现状结论（调研要点）

场景**全部程序化生成**，无预制体模型。管线：
`WorldBuilder.Build()` (WorldBuilder.cs:52) 依次做
PaintMaterials → Lights → Sky → Terrain → PathRibbon → River → Rocks → Trees →
Grass → PathDressing → Architecture → Landmarks → Scenes → Ambient。

几何层 `MeshKit.cs` 提供 Quad/Box/Cylinder/Blob/Roof/Pyramid/Beam/Tuft 等图元，
顶点色 alpha = 1 − 风摆权重；**无 LOD、无合批、无 SpriteAtlas**。

主题系统 `Look.cs` 有 6 套 Theme：Village/Wild/PeachGrove/Snow/Golden/Dusk，
`Themes.ForDecor(decor)` 按 decor 字符串映射（注意与 Theme.Id 不同名）。

## 优化项（按「改动成本 / 视觉收益」排序，逐条独立可验收）

### A1 ★★★ 环境光接入 Toon 着色器（最低成本、最高收益） ✅ 已完成 commit `6cc80cc`

**问题**：`Toon.shader` **完全没有 ambient 采样**（全文件搜 `ambient|unity_` 只命中 `_ShadowTint`），
暗部只做 `baseCol * _ShadowTint`。
而 `DayCycle.cs:72-75` 已经把天顶/赤道/地面三色写进了 `RenderSettings`
（`ambientMode = Trilight`，ambientSky/Equator/GroundColor）—— **这些光一行都没进画面**。

**最终实现（与最初设想不同，记录原因）**：
- 改成**由 C# 显式传值**到材质（`_AmbSky`/`_AmbEq`/`_AmbGnd`），
  **不走**引擎的 `unity_AmbientSky/Equator/Ground` 全局量，也不用 `ShadeSH9`。
  原因：内置管线的 ambient 走**球谐**（`unity_SH*`），与 Trilight 三色不是一回事，
  且 `ShadeSH9` 内部会做 `LinearToGammaSpace`，与三色 uniform 的色彩空间不同，混用会色偏。
- `Toon.shader` 新增 `AmbientAt(float3 n)`：按法线朝往在
  地面色 ↔ 赤道色 ↔ 天顶色之间插值。朝上的面吃天顶冷光，朝下的面吃地面暖反光。
- 环境光**主要补暗部**：受光面本来已接近饱和，再叠会过曝。
  亮面系数 0.15 / 暗面 0.55，末尾加软压缩 `c / (1 + max(0, max(c)-1))` 保住高光层次。
- 天空与云**不叠**环境光（`_AmbStrength = 0`）：它们已由 DayCycle 用 tint 染过一遍，
  再叠会二次染色发灰。
- 按材质角色分配 `_AmbStrength`：地面 1.0 / 路面 0.9 / 山石 0.95 / 石头 0.8 / 建筑 0.75。

**实测效果（上午 11 点）**：
| 部位 | 改前 | 改后 | 变化 |
|---|---|---|---|
| 草地受光 | 0.680, 0.900, 0.541 | 0.738, 0.968, 0.579 | 1.08x（保住明暗关系） |
| 墙面背光 | 0.496, 0.452, 0.576 | 0.717, 0.674, 0.752 | **1.41x**（暗部有层次） |
| 夜间背光 | 0.341, 0.418, 0.336 | 0.481, 0.601, 0.472 | **1.42x**（不再死黑） |

**验收（待用户 Play）**：背光面不再是纯 `顶点色×ShadowTint`，
而带方向性天光；夜间场景地面不会全黑。

**附带产出**：`Tools/check_shader.ps1` —— 在独立临时工程里用 Unity 批处理
（`ShaderUtil.ShaderHasError`）真实验证着色器编译。
解决 `_check.ps1`（Roslyn 直编）覆盖不到 HLSL 的问题。
用法：`powershell -File unity/Tools/check_shader.ps1`
（许可客户端在沙箱里握手会报 Code 10，**不影响执行**；
脚本内已清代理环境变量避免它连外网）

### A2 ★★★ 物件贴地（HeightAt 复用）
**问题**：`HeightAt` (WorldBuilder.cs:383-384) 在 22m 处地形高 0.5~1.2m，
但石头放 y≈0.2 (:847)、树/草放 y=0 (:548/:715/:666) → **外圈石头埋进山坡、草浮空**。
**改法**：所有摆放点用 `HeightAt(x,z)` 作为 y 基底（树/草/石/建筑/旗杆统一走一个 `GroundY()` 辅助）。
**收益**：消除穿模与浮空，地形起伏立刻"活"起来。
**注意**：树/草是 billboard 卡牌，贴地后还要保证根部不陷入地面（y 偏移 = -0.02 左右）。

### A3 ★★★ 可走区地形起伏（中景层次）
**问题**：`HeightAt:387` 中圈只有 `0.02 + n*0.045`（±4.5cm），
40m×40m 几乎是平网格，**没有中景起伏**。
**改法**：中圈引入低频 Fbm 起伏（振幅 0.3~0.6），保证整体坡度 < 6% 不破坏 2.5D 移动；
起伏要避开官道（沿路高度插值）。
**收益**：地面不再"一块平板"，是地形。

### A4 ★★☆ 接地阴影 / 接触阴影
**问题**：无 AO、无 SSAO、后处理只有泛光+分级+暗角；
接地只有 `BuildShadow` (:751) 一张黑椭圆，**只覆盖建筑与树，草和石头没有**。
**改法**：给草/石头/灌木也生成椭圆接地影（复用 BuildShadow），并按材质深浅调透明度。

### A5 ★★☆ 雾与天空地平线对齐
**问题**：雾色 (`DayCycle:77`) 与天空穹顶 (`SkyCol:258`) **分别计算，地平线不对齐**；
线性雾近处无变化 → 缺纵深。
**改法**：雾色直接采样天空底色（同一函数），让远处地平线无缝；加雾的浓度分层。

### A6 ★★☆ 远景剪影层
**问题**：场景边缘被 fogEnd 直接切断，没有山峦/远树线/城郭剪影。
**改法**：在 mapHalf 外圈加一圈低模剪影山脊（纯顶点色、不受光或轻微受光），
构成三层景深（近景实体 / 中景道具 / 远景剪影）。

### A7 ★★☆ 地面层次补全（path 层被丢弃）
**问题**：`EmitGround:348` 的 `if (pick == 1) pick = 0;` 让 **path 层永远为空**，
路径全靠 `Road()` 一条带子；`GroundKind` 只有草/路/石，无裸土/沙/岩壁过渡。
**改法**：补裸土/沙/岩层，让官道两侧有踩踏过渡带（`GroundCol:399` 只在 edge>0.2 才混 Cliff）。

### A8 ★☆☆ 材质分派稳健化
**问题**：`Toon.shader:141-149` 的 `archMat` 靠**饱和度硬阈值**猜瓦/石/墙/木，
改 Theme 颜色容易误判（建筑突然整片变色）。
**改法**：改用顶点色的具体通道编码（如用 alpha 或某分量做材质 id），
或加一个顶点色 id 通道区分材质。

### A9 ★☆☆ 卡片树的深度排序与 LOD
**问题**：卡片树靠 `Util.SortOrder` (`Util.cs:46`, `-z*8`) 排序，
远处 z 差 < 0.125m 会跳变穿插；每帧 billboard (`FaceCards:785`) 与斜视地面脱节、树像贴纸。
**改法**：SortOrder 分档放大精度；远处树降级为网格或减少数量。

### A10 ★☆☆ 闲置素材启用
`cartoon_path.jpg` / `cartoon_roof.jpg` / `ground_earth.jpg` / `ground_{autumn,dusk,snow}.jpg`
已存在但未使用，可在对应主题启用。

### A11 ★☆☆ Landmarks 补全
`Landmarks:1391` 近乎空白（只有 peachgrove 栅栏柱 + golden 一块石头），
village/wild/snow/dusk 无任何路标层 → 补路碑、旗幡、井、碾盘等主题化小摆设。

### A12 ★☆☆ 天空加日月/星空
`Sky():199-253` 只有顶点色渐变，无日月/星空/晚霞层，dusk 也仅 `SkyCol:261` 两段 lerp。

---

# 任务二：日常养成玩法（P0 · 每日循环）

## 设计原则

三国题材的"日常"应该是**小人物的生活**，不是农场模拟。三个方向：

| 玩法 | 三国意涵 | 产出 | 实现范式（照抄） |
|---|---|---|---|
| **屯田**（种田） | 曹操「屯田许下，以足军资」 | 铜钱 / 粮秣 / 少量属性 | 野外采集 + `bonusHp` 永久加成范式 |
| **缉私/押镖**（跑环） | 乱世商路不通，护送车队 | 声望 / 铜钱 | 野外偶遇 + 冷却范式 |
| **市易**（经商） | 煮酒/贩马/卖粮 | 铜钱 → 换装备/属性 | `Items` + `AutoEquipBetter` 范式 |

## 统一机制设计

### D1 存档字段（新增，注意兼容）
`SaveData`（Progress.cs:11-37）新增：
```csharp
public string dailyDoneCsv = "";   // 今日已完成的日常 id（| 分隔）
public int dailyDay = 0;           // 日常周期计数（每次重置 +1）
public string farmCsv = "";        // 屯田格子状态
```
**兼容铁律**：`SaveData` 无版本号，只有 Key 的 v1/v2 回退（Progress.cs:42,48-65）；
新增字段一律给默认值 + 在 `Normalize()`（Progress.cs:67-110）里补齐；
**不要动 `cleared[]`/`renownMark[]` 长度语义**（Progress.cs:77-85），否则清档。

### D2 每日刷新判定
没有真实时钟，用**累计游戏内天数**：
- 判定条件：累计游玩时长 ≥ 阈值（如 30 分钟）或完成任意一次副本 / 关键剧情后推进一天
- `Progress.DailyDay` 递增时，`dailyDoneCsv` 清空、`farmCsv` 重置

### D3 挂载位置
- 入口挂在**野外地图**（`WorldExplore.DrawRegionMap`，WorldExplore.cs:964-995）加「日常」页签，
  以及 **Hub 层**（`Game.Phase = GamePhase.Hub`）加入口
- 交互点在野外：照 `WorldEvents` 的 `EvSpot` 模式（WorldEvents.cs:33-43）
- 界面用 `SettingsUI` 的**全屏遮罩 + 面板 + 页签**范式（SettingsUI.cs:67-77，
  Game.cs:103-116 吞输入、Game.cs:676-682 画在最上层）

### D4 三个玩法的具体规则

**屯田（farm）**
- 野外开垦格子（每格消耗铜钱），选择种粟/麦/桑
- 每日按格子产出，产出随「田亩数」和（可选）「水利」等级增长
- 产出 = 铜钱 + 粮秣；粮秣可换永久 `bonusHp/bonusAtk`（走 `Progress.Stats()` 读取，Progress.cs:232）

**缉私 / 押镖（escort）**
- 每日 N 次委托，在地图上选一个目标点，限时往返
- 途中触发随机事件（用 `WorldEvents.Pick` 的 `Ambush` 变体）
- 成功给声望 + 铜钱，失败给少量补偿

**市易（trade）**
- 低买高卖：每日市价随机浮动（种子化，可复现）
- 买入货物需要铜钱，卖出得更多；运价随官阶解锁更高收益
- 商人立绘可复用 `npc/merchant.jpg`（已存在）

### D5 复用与新增代码
- **新增** `Assets/Scripts/Game/Daily.cs` —— 日常定义表 + 状态机 + 结算
- **新增** `Assets/Scripts/Game/Farm.cs` —— 屯田格子与产出（或并入 Daily.cs）
- **新增** `Assets/StreamingAssets/data/daily.json` —— 日常配置表
- **改** `DataDb.cs` —— 加 DTO + `ReadDaily`，写进 `DB.Daily`（DataDb.cs:80-124）
  （`DataDb.WrapArray` 包成 `{"items":[...]}`，DataDb.cs:130-135）
- **改** `Progress.cs` —— `SaveData` 加字段 + `Normalize()` 补齐 + `DailyDay`/`MarkDaily`/`ResetDaily`
- **改** `Game.cs` —— 场景切换与日常入口
- **改** `WorldExplore.cs` —— 野外交互点 + 地图页签

**数据表硬约束**：不要把日常塞进 quests/enemies 表 —— 会污染主线 Chapter 语义，
且 `DB.Maps.Count → cleared[]` 长度是耦合的（Progress.cs:77-83）。

---

# 任务三：攻城玩法（P1 · 声望→武力→带兵）

## 设计目标
玩家凭**声望**（招兵买马的名义与钱粮）与**个人武力**（武将战力）组建部队，
攻打城池；守城方有武将 + 士兵；**双方由 AI 自动对战**（玩家不手动操作）。

## 关键现状（决定路线）

- `Battle`（Battle.cs:15）持有单一 `Player`、单一 `Boss`、`List<EnemyCtrl> Enemies`
- 胜利判定 `CheckVictory` (Battle.cs:620-622) **硬绑 Boss**：Boss 死 + 全员死
- `EnemyCtrl.Update(float dt, PlayerCtrl player)` (EnemyCtrl.cs:91-92) **把目标写死为玩家**；
  `B.IsPlayerInArc` / `B.DamagePlayer` 只判 `Player`（Battle.cs:301-309）；箭矢只撞玩家（L379）
- `UnitDef`（GameData.cs:6-20）**没有阵营字段** ← 这是最大结构性缺口
- `MapDef.Mobs` **不生成敌人**，只用于贴图预热（Battle.cs:116-119）；
  真正生成源是 `MapDef.Packs` → `SpawnWave` → `DrainSpawnQueue`（每帧只生成 1 个，Battle.cs:335）
- 同屏峰值现约 **15 个小兵 + 1 Boss**（第七章，GameData.cs:396-403）
- `Separate()` 是 O(n²)（Battle.cs:544-562）→ n=60 时 1770 对/帧可接受，n=200 时 2 万对需改空间哈希
- 素材：仅 `bandit/archer/brute/rider`（rider 复用 brute 贴图，enemies.json:22）+ 7 boss，
  **没有守军/士兵专属美术**
- 官阶加成 `Career.ApplyToStats` (Career.cs:159-166) 是**乘算个人属性**，
  攻城需要它分别作用于「武将」和「部队」两个层级

## 三条路线对比

| 路线 | 做法 | 优点 | 风险 |
|---|---|---|---|
| **A** | 复用 Battle + PlayerCtrl，把玩家输入换成 AI | 手感表现最完整 | **阻塞点绕不开**：`EnemyCtrl` 目标硬编码，一旦引入阵营抽象就已是 C。A 只是"多改一个玩家" |
| **B** | 纯数值 `SiegeSim` 批量结算 | 数百单位无压力、平衡好调、零回归风险 | 表现层零复用，丢掉刀光/顿帧/震屏/死亡演出；与已有战斗割裂；两套数值公式易漂移 |
| **C** ★推荐 | AI 决策 + 现有表现层：`EnemyCtrl` 泛化成任意阵营单位，双方都用它；武将用 `PlayerCtrl` + 合成 `FrameInput`；士兵用 `EnemyCtrl` | 士兵表现（帧动画/受击闪白/死亡下沉/刀光/顿帧/震屏）全复用；士兵复用 EnemyCtrl 状态机省一套 AI | 中等重构，集中在 Battle + EnemyCtrl 约 200~300 行；需回归验证 7 个原有关卡 |

## 推荐落地路径：C 分两步

### Step 1（低风险，先验证玩法好不好玩）
新写 `SiegeSim`（纯数值，**不碰 Battle**）：
- 攻防推演：攻方 = 武将 + 士兵队列，守方 = 守将 + 守军队列
- 输出战报（伤亡、回合数、胜负原因），配结算 UI
- 先验证核心循环「声望+武力组军 → 攻防推演 → 拿城池/地块/资源」好不好玩
- **零回归风险**（不碰现有战斗）

### Step 2（确认好玩后再做表现层）
把 Step 1 的兵种/公式/胜负规则**平移**到 `EnemyCtrl` + `Battle` 的阵营化改造：
1. `UnitDef` 加 `Team/Faction` 字段（GameData.cs:6）
2. `EnemyCtrl` 加 `Team` 与 `Target` 选择（替换 EnemyCtrl.cs:115-116、338-340、261）
3. `Battle` 加 `List<EnemyCtrl> Attackers/Defenders`；`HitArc` 改为打敌方阵营（Battle.cs:255-299）；
   `IsPlayerInArc` → `IsTargetInArc`（:301）；箭矢带阵营（:344-387）
4. 胜利判定从「Boss 死+全员死」改为「守城方全灭 或 城破」——**必须新写**（CheckVictory:620 硬绑 Boss）
5. `Separate`（Battle.cs:544）按阵营免推挤同侧；相机跟随双方质心（:205）
6. HUD：`SetBoss`（Hud.cs:48）单 Boss 条要扩成「两方血条」
7. `SiegeSim` 的数值可直接当 `UnitDef` 平衡基准

## Step 0 前置（无论走哪条都要做）
- **新增士兵/守军美术**：当前只有 4 个兵种贴图，`rider` 还在复用 `brute`。
  至少要区分「攻方步兵/弓手/骑兵」与「守方军士」，用 PIL 调色复用现有兵种贴图。
- **官阶加成双层化**：`Career.ApplyToStats` 拆成「个人战力加成」与「部队规模/士气加成」两套，
  这是现有声望体系（`Progress.Data.renown`，Progress.cs:34）与攻城对接的主要接口点。

## 攻城地图
- 新建 `MapDef`（GameData.cs:36-46），加进 `DB.BuildMaps()`（GameData.cs:218-）
- **注意** `Pickups` 不能为 null，否则 `Battle.cs:395` NPE
- 地图尺寸：`wide` 由 `map.Id.StartsWith("world_")` 判定（WorldBuilder.cs:58），
  攻城战双方同场，**PlayBound 要收窄**，否则单位会散开
- 守城方**不需要独立地图**，同场反而更符合攻城表现

## 存档字段（新增，注意兼容）
```csharp
public string siegeCsv = "";     // 已攻下的城池 id（| 分隔）
public int siegeDay = 0;         // 攻城冷却计数
public int[] troops = new int[3]; // 三兵种各自数量
```
同样要在 `Normalize()` 补齐默认值。

---

## 执行顺序建议

1. **任务一 A1（环境光）** —— 一处着色器改动，全场景受益，最快看到效果，先做
2. **任务一 A2 + A3（贴地 + 起伏）** —— 同属地形，一起做
3. **任务二 D1~D5（日常养成）** —— 独立成线，不碰战斗
4. **任务三 Step 0 + Step 1（攻城前置 + 数值推演）** —— 先验证玩法
5. **任务一 A4~A12** —— 视觉打磨，按 A4/A5/A6 顺序
6. **任务三 Step 2（战斗阵营化）** —— 改动最大，放最后

---

## 关键实现备忘

### 通用
- 编译校验必须到 `COMPILE OK`：`powershell -File c:\Users\FanBin\Documents\trae_projects\r\unity\_check.ps1`
- **Unity 编辑器被用户占用，无法跑批量截图**，只能编译校验 + 请用户 Play 目视确认
- Git 推送需走系统代理：`git -c http.proxy=http://127.0.0.1:7897 -c https.proxy=http://127.0.0.1:7897 push`
  （git 直连 github.com:443 会被拦；api.github.com 走 Invoke-RestMethod 反而通）
- `.Opts()` 是 `Line` **实例方法**，必须写成 `Q(...).Opts(...)` 同一表达式，不能跨行断开
- 中文引号 `"` 不能出现在 C# 字符串里（会解析成语法错）
- 新增立绘/素材要生成 `.meta`（GUID 唯一），否则 Unity 重新导入会警告
- 新 Boss 需要自己的 `assets/anim/boss_<id>/` 目录（含 .meta），缺目录会静默不播动画
- 新地图缩略图要加进 `MetaUI.MapThumbs`（长度 = 章节数），
  `ThumbCache`/`ChapterNames`/`NodePos` 三数组同步加项
- 新野外区域要同时加：`Regions`/`MapOrder`/`RegionSpawn`（长度必须匹配）/`BuildSpots` 的 case

### 存档
- `SaveData` 字段：`faction/level/exp/gold/sp/attrPts/talentPts/atrStr/atrVit/atrAgi/
  talentRank[9]/questDoneCsv/flagsCsv/evSeenCsv/region/totalKills/bonusHp/bonusAtk/bonusDef/
  weapon/armor/accessory/bag/skillLv[3]/cleared[4]/renown/renownThisLevel/renownMark[6]`
- 序列化：`JsonUtility` + PlayerPrefs，Key `sanguo.luanxing.save.v2`（读时回退 v1）
- **无结构化版本号**，迁移靠 `Normalize()`（Progress.cs:67-110）
- 存档写入时机：手动 `Progress.Save()`，散落 Game.cs/Quests.cs/Battle.cs/WorldExplore.cs，
  **没有自动存档/退出存档**
- 升阶发奖唯一入口：`Game.OnRenownGained()`（Game.cs:242-267）

### 数据表
- `Assets/StreamingAssets/data/` 5 张：enemies / bosses / skills / factions（纯数组）
  + story（`{Scenes:[...]}`）
- `DataDb.WrapArray` 把纯数组包成 `{"items":[...]}`（DataDb.cs:130-135）
- 原则：**读不到就用内置默认**（DataDb.cs:4-8）

### 场景生成
- `WorldBuilder.Build()` (WorldBuilder.cs:52) 是唯一入口，由 `Arena.Build` 调用（Arena.cs:27-28）
- 主题映射 `Themes.ForDecor(decor)`：peach→PeachGrove(id=peachgrove)、pass→Snow(id=snow)、
  ruins→Golden(id=golden)、fortress→Dusk、village→Village、default→Wild（Look.cs:134-143）
- **注意 Theme.Id 与 decor 字符串不同名**
- `_PaintMode`：0/1 = 直接混贴图色；2 = 建筑分派（按顶点色饱和度硬猜）；
  3 = 贴图只给相对明暗（色相全由顶点色给）
- 摆放约束已有：`KeepClear` (WorldBuilder.cs:1501)、`NearPack` (:1513)、
  `OnPath`/`OnRiver`、`Open(x,z,r)` (:1076) —— 新摆放物照抄这套

### UI
- 全 IMGUI 手绘。`Game.OnGUI` (Game.cs:640-666) 是唯一绘制总闸，`Phase` 决定画哪层
- `GamePhase`：Title/Faction/Hub/Character/Playing/Story/Victory/Defeat/Ending/Pause
- Hud 缓存 GUIStyle（Hud.cs:107-116）；MetaUI/SettingsUI 不缓存，走 `Game.Label`/`Game.Fill`
  回调（Game.cs:963-981）
- `SettingsUI.Open` 静态开关 + `OpenPanel/Close`（SettingsUI.cs:67-77）是**现成的全屏弹窗范式**
- `MetaUI.DrawHub:35` 与 `HubClick:184` 是**死代码**，现在的天下地图是 `WorldExplore.DrawRegionMap`

### 战斗
- 无 `BossCtrl` —— Boss 就是 `EnemyCtrl` 的 `IsBoss=true`（Battle.cs:316）
- 伤害：玩家→敌人 `Battle.HitArc`（Battle.cs:255-299）扇形判定；
  敌人→玩家 `dmg*(1-Def/(Def+95))`（EnemyCtrl.cs:422）
- 敌人 AI 状态机：`idle/chase/windup/attack/recover/stagger/charging/slam`（EnemyCtrl.cs:118-229）
- `windup` 前 45% 跟人、后半段锁朝向可被侧步躲（EnemyCtrl.cs:183-187）
- 箭矢 `Battle.SpawnArrow`(Battle.cs:344) → `UpdateArrows`(:362) **只与 `Player.Pos` 碰撞**(:379)
- 士兵表现全在 `EnemyCtrl`：受击闪白、死亡下沉淡出 0.8s（:93-107）

---

## 调研来源（本轮三个探索代理）

- 场景建模管线：WorldBuilder / MeshKit / Look / Toon.shader 全量阅读，
  得出 A1~A12 十二项优化点
- 养成与存档体系：Progress / Career / Quests / Story / Flags / WorldEvents /
  Hud / SettingsUI / MetaUI / Game / DataDb 全量阅读，得出 D1~D5
- 战斗可复用性：Battle / PlayerCtrl / EnemyCtrl / GameData / Arena / enemies.json
  全量阅读，得出三路线对比与 Step 0~2 落地路径
