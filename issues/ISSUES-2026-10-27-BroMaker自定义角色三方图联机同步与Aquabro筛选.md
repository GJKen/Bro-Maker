# ISSUES-2026-10-27 BroMaker 自定义角色三方图联机同步与 Aquabro 筛选

## 状态

BroMaker fork 的联机增强源码已部署到本机和内网测试机。初次部署后发现构建产物引用 `mscorlib 4.0.0.0`，与游戏 UMM 报告的 .NET Framework 2.0 运行时不匹配；已用 Framework 3.5 参考程序集重新编译 BroMaker 与 Aquabro，并更新两端安装。两端 BroMaker 均已初始化并显示 Active，未再出现 `Main.Load` 异常。联机角色、技能 HUD、技能视觉和攻击效果尚未通过实机验收：房主进入地图后生成的是 `DrunkenBroster`，远端仍停在主菜单，MCP 无法切换到 Aquabro；验证结束时两端游戏进程均已确认退出。

## 问题范围

三方图联机时，加入方只能根据网络实例化索引创建角色。BroMaker 自定义角色的实际名称、角色槽位和自定义 prefab 信息没有随实例化请求明确传递，远端可能创建错误的基础角色，继而出现角色外观、头像或后续同步异常。

此前在 Swap Bros 界面已经能够看到其它自定义角色，但没有看到 `Aquabro`。本轮读取到的两端设置均为 `filterBros=false`，且 `enabledBros` 列表没有 `Aquabro`；不过 UMM 日志显示 Swap Bros Active，MCP 的 `list_bros` 却报未激活，因此目前不能确认其未显示 Aquabro 的具体原因，也不能据此判断网络 DLL 是否影响角色可选性。

## BroMaker fork 修改

修改位于 `D:\Study\C#\Broforce-Mods\BroMakerMultiplayer`，没有把 BroMaker 的源码改写到 `CustomMapMultiplayer/src`：

- `BroMakerLib/Loaders/LoadHero.cs`：网络实例化请求增加固定标记、自定义角色名和玩家槽位；增加按角色信息创建或复用网络 prefab 的入口。
- `BroMakerLib/HarmonyPatches.cs`：远端处理网络实例化请求时按角色名查找 `BroMakerStorage`，切换到对应的自定义 prefab 后再交给原生实例化流程；实例化结束恢复临时加载状态，并避免已回收的远端角色继续作为踢出候选；原生英雄初始化完成后重新应用自定义头像材质，避免官方头像覆盖自定义头像。
- `BroMakerLib/CustomObjects/Bros/CustomHero.cs` 与 `BroMakerLib/CustomObjects/ICustomHero.cs`：移除 `Info`、`Character` 的错误 `[Syncronize]` 标记。这两个字段包含自定义信息和对象引用，不应作为普通网络同步字段发送；错误标记会使同步字段布局与远端解析不一致，导致移动同步数组越界或角色状态异常。
- Aquabro 的 `SpecialAmmo` 和技能选择加入同步；四种技能在拥有端广播释放位置与朝向，远端重演对应视觉；遥控鲨鱼额外广播位置、方向和输入。伤害仍只由拥有端结算。

## 构建与部署

- Release 构建产物：`D:\Study\C#\Broforce-Mods\BroMakerMultiplayer\BroMakerLib\bin\Release\BroMakerLib.dll`
- 文件大小：`268288` 字节
- SHA-256：`D6A02D682AA5D6601BFC96FDF334374BE380CDCF93EC286CF61C100B6A12AC30`
- 本机安装：`E:\SteamLibrary\steamapps\common\Broforce\r2mod\Broforce\profiles\Broforce\UMM\Mods\BroMaker-BroMaker\BroMakerLib\BroMakerLib.dll`
- 内网安装：`\\5700G\Epan\Games\Broforce Mods\Broforce\profiles\Broforce\UMM\Mods\BroMaker-BroMaker\BroMakerLib\BroMakerLib.dll`
- Aquabro Release/本机/内网安装 DLL 均为 `90624` 字节，SHA-256 `3FE98FB3BB23C0EF29CA1DD8510D775BC5EC749D3C82F59C2C7035D75E9895E0`。
- 两个新 DLL 的程序集引用均为 `mscorlib 2.0.0.0`、`System.Core 3.5.0.0`；BroMaker 工程目标见 `BroMakerLib/BroMakerLib.csproj`，Aquabro 工程目标为 .NET Framework 3.5。
- 本机原版 DLL 备份仍保留为 `BroMakerLib.dll.before-multiplayer-20261026-232432.bak`。

部署前已确认本机和远端游戏进程退出。部署后经 MCP 启动双端：本机 UMM 成功加载 `11/11` 个模组，远端 `10/10`，两端 BroMaker 均记录 `Initialization`、`Finish Initialization` 与 `Active`。实机检查期间本机 P1 进入 `Test Evan2` 时为 `BM_Drunken Broster`，远端仍在主菜单；MCP 的 `list_bros` 报 Swap Bros 未激活，尽管 UMM 日志显示该模组 Active。MCP 结束后，本机无 `Broforce_beta.exe`，远端进程已由 `taskkill` 终止并再次核实退出。部署目标哈希一致。

## 本轮验证边界

- 已完成 BroMaker fork 的网络实例化与投射物同步源码核对、Framework 3.5 编译、双端加载检查和本机/远端安装文件哈希一致性检查。
- 已确认 `Aquabro` DLL、mod JSON 与资源存在于两端 `BroMaker_Storage\Aquabro`；但本轮未能通过 MCP 确认运行时角色注册或成功选中，因此根因仍待查。
- 新一轮 MCP 实机检查未能让房主和加入方都以 Aquabro 进入同一张三方图。因此没有观测到实际位置、弹药、图标、投射物、水花或四技能同步，不能将网络功能记为验收通过。

后续验收需先让 Swap Bros 实际选中 `Aquabro`，再由房主和加入方进入同一三方图，检查角色外观与移动、技能选择/图标/次数、四技能效果、普通与蓄力三叉戟/水花，以及死亡/重生、手动切换和重新加入。
