# RedDotConfig 与 ResonanceLv 格式统一

## 本次文件变化

| 文件 | 修改内容 |
|---|---|
| D:/iaowu/MiaoWu/Assets/Luban/Template/Datas/RedDotConfig.xlsx | 沿用 ResonanceLv 的字体、浅绿色表头、边框、列宽和对齐；表头改为 ##var、##type、## 三行，数据从第 4 行开始。保留红点字段名及实际 int/string/bool/long 类型，不照搬参考表的全 string 字段类型。 |
| Excels/RedDotConfig.xlsx | 与客户端源工作簿同步的二进制副本。 |
| tools/LubanExcelToCsv/Export-LubanExcelToCsv.ps1 | 根据第 3 行标记识别三行/四行表头，动态选择中文说明行与首条数据行，避免跳过节点 1000。 |
| docs/RedDotSystem.md | 更新表格格式和导出说明。 |
| docs/RedDotChanges.md、docs/RedDotSystem.patch | 更新最终功能清单及差异中的工作簿描述。 |
| 本说明、docs/RedDotConfigFormat.patch | 保存本次说明与脚本/说明文档的真实修改差异；二进制工作簿记录结构变化。 |

ResonanceLv 源文件及所有 .meta 未修改。GameServer/Configs/Csvs/RedDotConfig.csv 内容不变，不需要变更协议、配置类型或游戏业务逻辑。

## 验证

- 渲染并对照参考表、修改前表格和修改后表格，确认表头与样式。
- 核对七条节点的字段和值、全部禁用状态均未改变，没有残留参考表数据。
- 核对第四行起的数据与服务器原 CSV 完全一致，源工作簿与服务端副本字节一致。
- 确认 ResonanceLv 字节未改变；PowerShell 导出脚本语法检查通过。
- 未启动 Excel/WPS 执行 COM 导出，未运行与本次格式调整无关的服务端或客户端测试。

修改前快照、预览、构建脚本与核对结果位于 C:/Users/Windows/AppData/Local/Temp/codex-reddot-20261023/resonance-format/ 及同级 artifact/match-resonance.mjs；不作为项目内容交付。
