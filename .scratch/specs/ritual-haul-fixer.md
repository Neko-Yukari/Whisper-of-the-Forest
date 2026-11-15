# 仪式祭坛自动搬运任务实现

## 目标
实现 CompRitualAltar 自动请求祭品的功能，复用原版搬运逻辑。

## 实现路径
1. 调研原版 `CompTransporter` 装载机制：通过 `WorkGiver_LoadTransporters` 寻找带有 `CompTransporter` 的物件并发布 `JobDriver_HaulToTransporter`。
2. 重构仪式祭坛组件：
   - 移除原 `CompResourceContainer` 误区，改用 `CompTransporter` 作为祭坛的逻辑组件（即便不启用发射功能，只利用其装载和搬运 Job 发布机制）。
   - 实现或挂载 `JobDriver_HaulToRitual`，使搬运工能将缺失祭品填入祭坛。
3. 更新 `Assess` 校验：检查 `CompTransporter.innerContainer` 中的祭品数量是否满足 `RitualDef` 的需求。

## 规则
1. 必须使用 `check-defs.ps1` 验证 XML 组件定义。
2. 若涉及 `CompTransporter` 的硬绑定，务必保证组件生命周期（ExposeData）不会因为没有 `CompLaunchable` 而崩溃。
3. 离线测试：使用 `Tools/test-offline.ps1` 验证搬运优先级与容器识别逻辑。
