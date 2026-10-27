# Aquabro 双端联机实机验收（2026-10-27）

## 结论

**部分通过，整体验收未通过。** 双端进入 Workshop `3715087178`（`Test Evan2`），BroMaker 能在同一房间生成 `AquabroMod.Aquabro`。四技能释放消息和远端视觉构造均有计数证据；技能高频测试中实际发送的施放均到达并构造。置零弹药后的拒绝行为符合预期。角色移动曾在一次短测中双端坐标一致。

普通与蓄力攻击的房主发射及网络生成调用成立，但逐帧检查和排除基线 prefab 的复测显示，双端新增对象都是未初始化 clone（位置和速度为 0、`playerNum=-1`、没有 `firedBy`），不是实际飞行投射物。特殊弹药最终状态不一致。水花/固定目标伤害、四个 HUD 图标的逐项切换和加入端拥有 Aquabro 的反向测试仍未完成。

本轮没有修改正式源码、配置或 DLL，没有构建、部署、提交或推送。临时探针已卸载，TestMod 自动清敌开关已恢复，双端游戏已关闭。测试辅助脚本和证据保留于仓库根 `.tmp`。

## 环境与证据

- 房主为本机，加入端为 `5700G`。主要测试 Lobby：`109775242484066237`、`109775242485351880`；基线排除复测 Lobby：`109775242487128229`。
- Workshop `3715087178`，场景 `Test Evan2`，双端 `playerCount=2`。房主 P0 经 BroMaker `StoredHero -> LoadHero.WithCustomBroInfo` 切换为 Aquabro；加入端本地 P1 为 TankBro。MCP `swap_bro(Aquabro)` 因 Swap Bros Mod 未激活未能使用。
- 房主截图：[本机联机验收截图](E:/SteamLibrary/steamapps/common/Broforce/r2mod/Broforce/profiles/Broforce/UMM/Mods/alexneargarder-UnityInspectorMod/Screenshots/screenshot_20261027_144650_334f2bef.png)
- 加入端截图：[5700G 联机验收截图](//5700G/Epan/Games/Broforce%20Mods/Broforce/profiles/Broforce/UMM/Mods/alexneargarder-UnityInspectorMod/Screenshots/screenshot_20261027_144648_bfd33ea4.png)
- 逐阶段原始摘要（28 条双端 JSONL）：[aquabro-acceptance-raw-summaries-20261027.jsonl](../../.tmp/aquabro-acceptance-raw-summaries-20261027.jsonl)。操作步骤和关键原始输出：[aquabro-acceptance-evidence-20261027.md](../../.tmp/aquabro-acceptance-evidence-20261027.md)。
- 两端被动探针安装 26 个 Harmony 钩子，并补充投射物/水花逐帧采样；有效采样阶段 `observerErrors=0`。另有一次初始 `execute_code` 动态类型读取触发双端原生访问冲突，原生堆栈不足以确认根因；后续改用反射读取器，不能把该工具故障计为游戏功能结果。诊断见 [aquabro-observer-crash-notes.md](../../.tmp/aquabro-observer-crash-notes.md)。
- 首轮 CustomMapMultiplayer 最新 `.log` 与 `.trace.log` 均为 0 字节，已实际读取；两端有效 startup 日志中有 BroMaker 的 `Player.SpawnHero` 钩子记录。它们证明模组入口加载，不证明战斗同步通过。

## 实测结果

### 角色位置

Aquabro 短距离移动约 600ms 后，房主与加入端对同一 P0 都读到 `(155.698,88)`，有一组位置同步通过证据。之后重新切换/入房的快照出现 `(148.135,104)` 与 `(96,144)` 的差异，但没有相同出生阶段的连续样本解释差异；因此持续位置失步尚未确认，需从相同起点复测。加入端拥有 Aquabro 的反向拥有测试未覆盖。

### 技能、图标与次数

游戏 `gesture` 选择序列记录为 `0 -> 1 -> 2 -> 3 -> 0`，四次技能均观察到释放消息及远端对应视觉构造。涌泉一次构造 7 个水柱；鲨鱼阶段双方各记录 73 次状态收发/应用及一次 `BreachShark` 回调，但未逐帧对照鲨鱼坐标、下潜和伤害目标。漩涡、水柱的具体位置、持续时间和目标伤害也未逐项测量。

技能 HUD 材质和纹理对象两端均存在，纹理为 `16×16`；纹理名为空是资源未命名，不能作为材质缺失证据。截图能看到 HUD 区域图标，但尚未逐项确认四个图标内容是否随选择正确切换。最终 `SelectedSkill=0` 两端一致；中间每次选择及 HUD 刷新没有完整对照。

| 测试 | 房主结果 | 加入端结果 | 判定 |
|---|---|---|---|
| 狂潮、鲨鱼、漩涡、涌泉单次释放 | 四类视觉均有构造记录；涌泉一次 7 柱 | 释放消息到达并构造相应视觉 | 消息/视觉构造通过；位置和命中未覆盖 |
| 狂潮高频，30 次请求、约 100ms 间隔 | 接受、扣次、广播、构造各 10；其余 20 次被动画门控拒绝 | 接收守卫通过并构造 10 次 | 实际发送的施放 10/10 到达，无丢包证据；门控拒绝不是丢包 |
| 狂潮低压，10 次请求、约 600ms 间隔 | 接受、广播、构造各 10，Ammo `30 -> 20` | 接收、构造各 10，Ammo 最终为 0 | 消息/视觉 10/10；弹药最终值不一致 |
| 零弹药，背字段临时置 0 后请求 3 次 | 无扣次、广播或视觉，Ammo 保持 0 | 无接收或视觉，Ammo 保持 0 | 零弹拒绝和非负次数通过；属性 setter 曾被 Utility Mod 前缀改回 5，该次不计 |
| 自然耗尽，初值 5 次 | 记录 4 次实际扣次；Utility Mod `infiniteSpecials` 在末次回满 Ammo | 收到 5 次消息/视觉，最终 Ammo 0 | 受无限技能修改干扰，自然耗尽未验收 |

高频期间远端接收事件曾记录 Ammo `29 -> 20`，最终摘要却回到 0；低压 10 次后也是房主 20、远端 0。故施放消息的高频到达通过计数验证，但技能次数状态未收敛。

### 普通攻击、蓄力与效果

| 测试 | 房主结果 | 加入端结果 | 判定 |
|---|---|---|---|
| 普通短按 80ms | `FireTrident=1`、网络生成 `1`；蓄力时间 `0.100–0.133s` | 新增 prefab clone，但位置 `(0,0)`、速度 0、`playerNum=-1`、`firedBy=null` | 输入和房主生成成立；远端实际投射物未同步 |
| 蓄力按住 650ms | 生成 `Predabro Spear Charged`，速度 `664`，超过 0.5 秒满蓄阈值 | 新增对象仍为未初始化 clone | 房主满蓄成立；远端飞行实体未同步 |
| 基线排除复测 | 每次发射新增一个对象 | 每次发射也新增一个对象，但四端样本均为 `(0,0)`、速度 0、`playerNum=-1`、`firedBy=null`、原版 `Predabro Spear` 材质 | 排除关卡既有 prefab 后仍不是有效投射物；自定义材质未同步 |
| 普通攻击高频，30 次输入、50ms 按下/150ms 间隔 | 冷却限制下实际 `FireTrident/SpawnProjectile=15` | 新增 15 个 prefab clone；单发复测证明同类对象未初始化 | 15/30 个房主投掷成立；远端飞行实体未同步 |
| 墙面水花 | 两次朝稳定墙面试射 | 未观察到 `Aquabro Water Impact` 回调/对象 | 本次未获得水花命中证据；固定单位伤害未验收 |

水花测试未能确保游戏实际碰撞回调，故记录为未证实，不据此推断所有命中场景必然失败。测试没有固定同一网络目标或采集命中前后生命值，普通/蓄力伤害结算与击杀同步均未验收。

## 后续待验收

1. 逐项视觉确认四个技能 HUD 图标及选择切换，并在相同出生阶段对 Aquabro 位置进行重复移动对照。
2. 查明远端为何只出现未初始化 spear clone；随后复测投射物自定义材质、实际飞行、水花，以及固定目标两端同 NID 的健康值和击杀。
3. 排除 `infiniteSpecials` 干扰后自然耗尽技能次数，并继续定位远端 Ammo 在接收更新后最终回到 0 的原因。
4. 完成漩涡、水柱和鲨鱼控制/下潜的坐标与命中对照，以及加入端拥有 Aquabro 的反向测试。

以上待验收项不能记为通过。本轮未修改生产源码、配置、构建产物或部署文件。
