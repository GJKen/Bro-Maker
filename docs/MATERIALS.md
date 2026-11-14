# 问题记录、验证与发布材料

`issues/` 与 `docs/` 同级，保存问题、修复及相关验收记录。接入指南等通用说明放在 `docs/`，截图、测试脚本和原始结果放在 `docs/evidence/`，上游 PR 发布材料放在 `docs/publication/`。

2026-11-09 整理的路径映射（相对本项目）：

| 原路径 | 当前路径 |
| --- | --- |
| `issues/evidence/frp-direct-invisible-2026-11-08/` 中的截图、回归摘要和脚本 | `docs/evidence/frp-direct-invisible-2026-11-08/` |
| `issues/evidence/frp-direct-invisible-2026-11-08/upstream-pr/` | `docs/publication/2026-11-08-bromaker-pr/` |
| 整理中曾移入的 `docs/ACCEPTANCE-2026-10-27-Aquabro双端联机实机验收.md` | `issues/ACCEPTANCE-2026-10-27-Aquabro双端联机实机验收.md` |

旧 `issues/evidence/` 已搬空并移除。PR 原始回执保留当时的内容、哈希和路径；本地材料仍不加入上游 PR。Aquabro 旧验收链接已修正为其现存的 `archive/networking/2026-10/` 归档。

- [角色回退不可见问题](../issues/ISSUES-2026-11-08-FRP-Direct首次建房后角色不可见.md)
- [问题验收记录](../issues/ACCEPTANCE-2026-10-27-Aquabro双端联机实机验收.md)
- [回归材料](evidence/frp-direct-invisible-2026-11-08/results.md)
- [PR 发布记录](publication/2026-11-08-bromaker-pr/README.md)

2026-11-10 首次入图回退与死亡重开材料集中在 [first-map-spawn-fallback-2026-11-10](evidence/first-map-spawn-fallback-2026-11-10/validation.md)。TestMod 临时诊断已恢复到上次提交，必要捕获迁回 BroMaker；具体旧路径映射见 [捕获归档](evidence/first-map-spawn-fallback-2026-11-10/capture/README.md)。重复备份、一次性观察脚本和生成的检查目录已清理，历史发布材料保持原位。
