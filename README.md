# 三国乱行 · SanguoLuanxing

一款以三国为背景的**叙事 + 战斗**单机小游戏。玩家是一个无名小人物，从桃林结义一路走到渭水定天下——
每一章的抉择都会写进结局里。

- **引擎**：Unity 2022.3（内置渲染管线 Built-in）
- **语言**：C#（全部运行时代码，无第三方依赖）
- **美术**：程序化生成的低多边形场景 + 自绘/AI 辅助的 2D 立绘与帧动画
- **玩法**：野外大地图自由行走 · 回合制副本战斗 · 仕途（官阶）成长 · 多分支结局

---

## 玩法一览

| 模块 | 说明 |
|---|---|
| 野外探索 | 桃林、汜水关外、长坂坡、虎牢关、洛阳、官渡、渭水等 9 个区域，可自由走动、对话、拾取 |
| 副本战斗 | 7 张关卡地图，各有专属 Boss；出招有前摇/后摇，闪避可取消后摇 |
| 仕途系统 | 靠声望升官阶（乡士→秀才→举人→进士→柱石→…），官阶决定结局走向与解锁内容 |
| 旗号系统 | 5 个主线维度 + 2 个支线维度的选择标记，同维度互斥，结局画面逐条点评 |
| 支线任务 | 每章 4~6 条，含收集、护送、抉择型支线 |
| 野外偶遇 | 随机遭遇 NPC，可触发回访链（救过的人后来会报答你） |
| 昼夜流转 | 场景内太阳角度、光色、雾色随时段变化，10 个时段可手动固定 |
| 战后评级 | 险胜给得最多，鼓励搏命式打法 |

## 目录结构

```
unity/
├── SanguoLuanxing/          # Unity 工程
│   ├── Assets/
│   │   ├── Scripts/         # 全部 C# 代码（Core 引擎层 / Game 玩法层）
│   │   ├── Resources/       # 自定义 Shader（Toon 卡通光照、描边、精灵、后处理）
│   │   └── StreamingAssets/ # 运行时素材：立绘、帧动画、贴图、地图缩略图、数据表
│   ├── ProjectSettings/     # Unity 工程设置
│   └── Tools/               # 素材生成脚本（Python）
├── Tools/                   # 素材生成 / 校验脚本
├── DEV_GUIDE.md             # 开发指南（架构、素材规范、常见坑）
├── MIGRATION.md             # 从网页版迁移到 Unity 的说明
└── _check.ps1               # 不开编辑器也能跑的 C# 编译校验
```

## 代码分层

- `Assets/Scripts/Core/` —— 与玩法无关的地基：素材管线（`Tex`）、场景拼装（`MeshKit`）、
  配色主题（`Theme`）、材质工厂（`Look`）、帧动画播放器（`FrameAnim`）、昼夜循环（`DayCycle`）。
- `Assets/Scripts/Game/` —— 玩法层：野外探索（`WorldExplore`）、场景生成（`WorldBuilder`）、
  战斗（`Battle` / `PlayerCtrl`）、剧情（`Story` / `Quests`）、存档（`Progress`）、旗号（`Flags`）。

## 本地运行

1. 用 Unity **2022.3.x** 打开 `unity/SanguoLuanxing/`。
2. 打开场景 `Assets/Scenes/Main.unity`，按 Play。
3. 运行时素材从 `Assets/StreamingAssets/` 读取（走 `Application.streamingAssetsPath`），
   不需要打包额外资源。

### 不装 Unity 也能校验编译

```powershell
powershell -File unity/_check.ps1
```

输出 `COMPILE OK` 即表示全部 C# 代码编译通过。

## 素材生成

大部分立绘、帧动画、贴图由 Python 脚本生成后手动拷进 `StreamingAssets`：

```bash
cd unity/SanguoLuanxing/Tools
python gen_map_thumbs.py     # 地图缩略图
python gen_boss_yuanshao.py # Boss 立绘 + 帧动画
```

生成后记得补 `.meta` 文件（GUID 唯一），否则 Unity 重新导入会报警告。

## 许可

代码与美术素材版权归本仓库作者所有。
