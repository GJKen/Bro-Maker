# ISSUES-2026-11-08 FRP Direct 首次建房后角色不可见

## 状态

角色不可见的回退缺陷已修复，并完成编译及本机 FRP Direct 实机回归；新 DLL 已复制到本机和 5700G 的现用 BroMaker 目录。原始首次建房时选角返回空的具体触发条件仍未查明，不能把本次定向回归记为该触发条件已经解决。远端未进行修复后实机回归。

## 现象与复现线索

退出游戏后重新进入，首次创建 FRP Direct 房间并进入地图时，角色头像和生命 UI 正常显示，但地图中看不到角色。用户报告的是首次创建房间后的现象；其它地图、角色、后续房间以及不同加入顺序是否受影响尚未确认。

## 运行时确认

本机 MCP 检查时，房主位于 `Test Evan2`，`Connect.IsHost=True`、`Connect.IsOffline=False`，连接层为 `FrpDirectLayer`。截图中左下角角色头像及一条生命正常显示，场景内没有可见角色。

`HeroController.players[0]`、玩家对象和 `Player.character` 均存在且启用；`Player.character` 指向 `Rambo(Clone) [local]`。角色 `health=1`、`IsAlive=True`、`actionState=Idle`、`HidingPlayer=False`，出生位置为 `(64,80,0.0466)`。角色根 GameObject 的 `activeSelf=False`、`activeInHierarchy=False`，而角色组件、Renderer 和 prefab 均启用。这说明角色已经分配并有生命，头像 UI 正常，但角色根对象被停用。

同一生成时段 BroMaker 记录：

```text
[Warning] No network-compatible BroMaker character is available; keeping the vanilla spawn.
```

MCP 返回的游戏进程状态提示可能误报；现场读取时本机 Unity Inspector 仍可正常查询。该提示本身不作为游戏进程退出或故障的证据。

## 直接原因

`BroMakerLib/HarmonyPatches.cs` 中的 `Player_InstantiateHero_Patch` 在 `LoadHero.willReplaceBro[playerNum]` 为 true 时，先调用 `SetActive(false)` 停用原生角色。随后 `Player_SpawnHero_Patch.Postfix` 在网络兼容角色选择 `choice == null` 时报告将保留原生角色，清理替换标志后返回，但没有重新激活原生角色 GameObject。于是 `Player.character` 仍引用存活的原生角色，UI 可见而角色实体不可见。

为何本次 `choice` 返回 null 尚未定位。现场的 BroMaker 自动选择已开启，当前网络可用角色数为 10；现有日志没有给出该次选择失败的具体条件。

## 与 CustomMapMultiplayer 精简的关系

已静态审查 CustomMapMultiplayer 提交 `8e32da7`、`828ff62`、`4aa8202`、`f46d6c8`、`d6e8416` 和 `f12603a`。这些提交没有修改角色选择、`Player.SpawnHero`、`InstantiateHero` 或 BroMaker GameObject 激活路径；目前没有证据表明精简直接引入此问题。Workshop 加载时序是否有间接影响，仍不能仅凭静态差异完全排除。

更接近该回退缺陷的 BroMaker 代码历史为提交 `5e5cc29f1ab2591f0a21a05a64c415c6d1994f58`。该提交加入网络兼容角色筛选及 `choice == null` 的 vanilla spawn 回退；该回退没有配套恢复前面停用的原生角色。

加入方首次加入时延迟生成角色的日志是另一个现象：该客户端槽位曾暂时没有 `character`，之后经重试正常执行 `Player.SpawnHero`。它不能解释本问题中房主已有存活角色对象但角色根对象处于 inactive 的状态。

## 修复与验证（2026-11-08）

`Player_SpawnHero_Patch.Postfix` 的 `choice == null` 和 `bro == null` 两个分支现在都在清理既有替换标志之后，重新激活仍存在的 `Player.character`。生产代码共增加 5 行，保留正常替换流程及网络兼容性筛选。

- 旧 DLL 实机定向复现：在 FRP Direct 房主的 `Test Evan2` 中，一次无效强制选角得到存活的 Rambro，`activeSelf=False`、`activeInHierarchy=False`。
- 新 DLL 相同用例：Rambro 存活，`activeSelf=True`、`activeInHierarchy=True`，替换标志全部清空；截图中角色实体和头像同时可见。
- 正常强制选角：替换为 `Brostbuster.Brostbuster`，角色存活且激活，替换标志全部清空。
- 本轮重启后首次建房的正常路径曾成功生成 Aquabro，未自然复现原始空选角。测试时临时关闭 Swap Bros 的运行时开关，避免它的固定选角覆盖测试输入；在 `finally` 中恢复，未保存测试选角设置。

构建、部署路径和原始结果摘要见 [本次回归记录](../docs/evidence/frp-direct-invisible-2026-11-08/results.md)，可复用测试入口见 [spawn-regression.cs](../docs/evidence/frp-direct-invisible-2026-11-08/spawn-regression.cs)。

## 仍需观察

保留首次自动选角返回空的调查项。本次没有放宽缺角色或版本不一致时的网络筛选，也没有声称原始选角失败的独立原因已修复。`bro == null` 分支已作对应修复，但未单独注入加载失败进行实机测试；远端、多人镜像、IronBro 和其它地图不在本次验收范围内。

## 上游 PR（2026-11-09）

已按用户授权，将 5 行回退可见性修复补入现有英文 [PR #29](https://github.com/Gorzontrok/Bro-Maker/pull/29)，公开提交为 `13a5df4c169d0eb351bc7f4392f90681bcfafa51`。PR 的最终差异仅保留必要源码和工程文件，Markdown 文档、问题记录、测试材料及 DLL 均不包含；这些材料在本地保留。此次单独编译通过，无警告或错误，未扩大实机验收结论。完整范围与回执见[本地 PR 记录](../docs/publication/2026-11-08-bromaker-pr/README.md)。
