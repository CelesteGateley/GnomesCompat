# Changelog

## 1.0.0

- First release. Makes Gnomes 0.7.0 work on the current Stonewards build again by adding back the
  five game members it needs:
  - `ChestDataSO.level1DropPoolSO` and `level2DropPoolSO` (fixes the chest gnome)
  - `PlayerStats.ServerHealWithFX(float)`
  - `DiggingPattern.Id` and `DiggingManager.SDiggingPattern.id` (fixes digging and terrain tracking)
