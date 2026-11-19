# REPORT-001: каркас проекта

commit: см. сообщение `TASK-001: project skeleton` в git log

Документы оператора и архитектора закоммичены отдельно: `docs: architect documents` (`cc8b173fd3e6d4fd79b97bd2bcf0c620d0afa5b3`). В том коммите `play again.png` есть в `git lfs ls-files`, удаление `DOC/figma_design.pdf` включено по подтверждению оператора. Содержимое тех файлов не менялось.

Временный скрипт настройки лежал в `Assets/_Task001Temp` и `_tmp_task001`, выполнен и удалён вместе с `.meta` до коммита.

## 1. URP

`Packages/manifest.json`: `com.unity.render-pipelines.universal` **17.6.0 → 17.5.0**. `packages-lock.json` не изменился (URP 17.5.0, `source: builtin`). Консоль после открытия и компиляции чистая.

## 2. Настройки проекта

Player Settings до: `defaultScreenOrientation = AutoRotation` (4), все `allowedAutorotateTo*` = 1. После: Portrait (0), все четыре флага = 0. `useOSAutorotation` остался 1: при фиксированном Portrait он не крутит экран. Других полей `ProjectSettings.asset` нет.

Sorting Layers поверх Default: `Background` (1157211451), `World` (1157211452), `Hud` (1157211453). Редактор вместе с ними записал в `TagManager.asset` `serializedVersion: 3`, `m_RenderingLayers: Default` и пустой `m_MigratedRenderPipelines`. Это не откатывал: без них файл этой версии редактора не сохраняется.

Build Settings уже были `Loader` (0), `Scene` (1). `EditorBuildSettings.asset` не менялся.

## 3. Скрипты

- `SweetBananas.Data.GameConfig` и `Assets/Data/GameConfig.asset`: WorldWidthUnits 10.8, PixelsPerUnit 100, TargetFrameRate 60, SettleSeconds 1.5, SettleSpeedThreshold 0.15, RewardBase 5, RewardFirstThrowBonus 5, SkinPrice 10, FailMarginUnits 1.
- `SaveData`: version 1, coins, ownedSkinsMask, equippedSkin, levelIndex, soundOn.
- `PlayerPrefsSaveService`: ключ `save_v1`, `JsonUtility`. Нет ключа — объект в памяти (coins 0, ownedSkinsMask 1, equippedSkin 0, levelIndex 0, soundOn true, version 1), без записи. `Save()` пишет JSON.
- `NullAudioService`: `PlayTap`, `PlayWin`, `PlayFail`, `PlaySquish`, `SetMuted` пустые.
- `GameFlow`: старт `Boot`, `SetState` шлёт `StateChanged` только при смене.
- `GameBootstrap`: `vSyncCount = 0`, `targetFrameRate` из `GameConfig`, затем `Playing`.
- `CameraFitter`: `orthographicSize = (WorldWidthUnits / 2) * Screen.height / Screen.width`, позиция `(WorldWidthUnits / 2, orthographicSize, -10)`.
- `SafeAreaFitter`: верхний якорь `HudRoot` на верх `Screen.safeArea`.
- `ProjectInstaller`: `GameConfig`, `ISaveService`, `IAudioService`. `GameSceneInstaller`: `GameFlow`, `GameBootstrap`.

## 4. Сцена и префаб

`Assets/Resources/ProjectContext.prefab` с `ProjectInstaller` и ссылкой на `GameConfig`. В `Scene`: `SceneContext` + `GameSceneInstaller`, `Main Camera` + `CameraFitter`, `WorldRoot` в `(0, 0, 0)`.

Существующий Canvas переименован в `HudCanvas` (ScreenSpace-Camera, Match 0, 1080×2160, sorting layer `Hud`, order 10). Дочерняя пустая панель `HudRoot` несёт `SafeAreaFitter`. `BackgroundCanvas` (layer `Background`, order −10) содержит `Image` спрайта `bg_0` и `AspectRatioFitter` Envelope Parent. `Loader` не менялся.

## Приёмка

1. `Edit/Zenject/Validate Then Run` на `Scene` входит в Play. Консоль: 0 errors, 0 warnings. Состояние `Playing`.
2. Play с `Loader` сам открывает `Scene`, Zenject поднимает `GameFlow` в `Playing`, предупреждений нет.
3. Game view:

| Разрешение | orthographicSize | позиция | ширина мира |
|---|---|---|---|
| 1080×1920 | 9.6 | (5.4, 9.6, −10) | 10.8 |
| 1080×2160 | 10.8 | (5.4, 10.8, −10) | 10.8 |
| 1080×2340 | 11.7 | (5.4, 11.7, −10) | 10.8 |

4. Фон на всех трёх закрывает canvas, соотношение сторон 0.5 (без растяжения). На 1080×1920 картинка 1080×2160 поверх canvas 1080×1920. На 1080×2340 картинка 1170×2340 поверх canvas 1080×2340.
5. `PlayerPrefsSaveService`: пустой ключ даёт значения по умолчанию. После `Save` повторный `Load` вернул version 1, coins 12, ownedSkinsMask 9, equippedSkin 3, levelIndex 25, soundOn false. Тестовый ключ удалён. Скрипт проверки не оставлен.
6. В коммит входят только файлы этого ТЗ и этот рапорт.

## game01…26: текстура и слайс

У всех 26 файлов текстура **741×648**. Крупнейший слайс совпадает с полными границами (x=0, y=0, 741×648) только у `game01`, `game02`, `game03`, `game24`, `game26`. У остальных крупнейший слайс меньше текстуры и сдвинут. Импорт не менялся.

## git diff --stat

```
 Assets/Scenes/Scene.unity             | 356 +++++++++++++++++++++++++++++++++-
 Packages/manifest.json                |   2 +-
 ProjectSettings/ProjectSettings.asset |  10 +-
 ProjectSettings/TagManager.asset      |  14 +-
 4 files changed, 371 insertions(+), 11 deletions(-)
```

Плюс новые скрипты, `GameConfig.asset`, `ProjectContext.prefab` и их `.meta`. `packages-lock.json`, `EditorBuildSettings.asset` и `Loader.unity` без изменений.
