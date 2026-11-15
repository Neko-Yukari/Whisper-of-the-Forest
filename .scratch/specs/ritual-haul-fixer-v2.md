# 仪式祭坛自动搬运任务实现（修正版）

## 目标
利用 CompThingContainer 实现仪式祭坛的自动填装，复用原版搬运逻辑。

## 实现路径
1. 调研：使用 CompThingContainer 而非 Transporter，避开发射逻辑。
2. 重构 `CompRitualAltar`：
   - 移除原 `CompResourceContainer` 误区，改用 `CompThingContainer` 作为容器。
   - 实现或配置 `JobDriver_FillContainer` 关联。
3. 更新 `Assess` 校验：检查 `CompThingContainer.innerContainer` 中的祭品。

## 规则
1. 必须使用 `check-defs.ps1` 验证。
2. 严禁涉及 `CompTransporter` 的任何逻辑。
3. 必须先 grep 原版 `CompThingContainer` 调用示例。
