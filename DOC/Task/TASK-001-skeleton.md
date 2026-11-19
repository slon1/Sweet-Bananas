# TASK-001: каркас проекта (этап 1)

Основание: ADR-001, ADR-002, GDD v1.0. Геймплей (леденец, лента, ввод, уровни, экраны) в этой задаче **не делаем**. `Loader` и `.meta` спрайтов не трогаем, asmdef не создаём.

## 0. Документы и кнопка (отдельный коммит до работы)
В рабочей копии лежат неотслеживаемые и изменённые файлы оператора и архитектора. Первым отдельным коммитом `docs: architect documents` закоммитить:
- `DOC/Adr/`, `DOC/Task/`, `DOC/PLAN.md`, `DOC/CURSOR_PROTOCOL.md`, правки `DOC/gdd_sweet_bananas_jelly_flip.md`, `DOC/figma_all.pdf`, `DOC/figma_all.png`;
- `Assets/Sprites/pop-up/play again.png` и его `.meta` (через LFS, проверить `git lfs ls-files`);
- удаление `DOC/figma_design.pdf` — **только если оператор подтвердил это в сообщении к задаче**; иначе оставить и отметить в рапорте.
Содержимое файлов не править. Хеш этого коммита — в чат.

## 1. Версия URP
В `Packages/manifest.json` заменить `com.unity.render-pipelines.universal` с 17.6.0 на **17.5.0** (ADR-002, D16). Открыть проект, убедиться, что `packages-lock.json` не изменился, консоль чистая.

## 2. Настройки проекта
- Sorting Layers: добавить `Background`, `World`, `Hud` (в таком порядке, поверх `Default`).
- Player Settings: Default Orientation = Portrait, автоповорот выключен (остальные ориентации не разрешены). Записать в рапорт значения до и после.
- Build Settings: `Loader` (индекс 0), `Scene` (индекс 1).
- Правки только в `TagManager.asset`, `ProjectSettings.asset`, `EditorBuildSettings.asset`.

## 3. Скрипты (namespace по ADR-002)
- `Data/GameConfig` — `ScriptableObject` (`Assets/Data/GameConfig.asset`): `WorldWidthUnits` 10.8, `PixelsPerUnit` 100, `TargetFrameRate` 60, `SettleSeconds` 1.5, `SettleSpeedThreshold` 0.15, `RewardBase` 5, `RewardFirstThrowBonus` 5, `SkinPrice` 10, `FailMarginUnits` 1.0.
- `Data/SaveData` — по ADR-002 D14.
- `Services/ISaveService`, `Services/PlayerPrefsSaveService` — `Load()`, `Save()`, доступ к `SaveData`; если ключа нет — создаётся значение по умолчанию (`coins` 0, `ownedSkinsMask` 1, `equippedSkin` 0, `levelIndex` 0, `soundOn` true).
- `Services/IAudioService`, `Services/NullAudioService` — методы `PlayTap()`, `PlayWin()`, `PlayFail()`, `PlaySquish()`, `SetMuted(bool)`; реализация пустая.
- `Core/GameState` (enum: `Boot`, `Playing`, `Win`, `Fail`, `Shop`) и `Core/GameFlow` (`State`, `event Action<GameState> StateChanged`, `SetState`). Начальное состояние `Boot`.
- `Core/GameBootstrap` — `IInitializable`: выставляет частоту кадров по ADR-002 D17 и переводит `GameFlow` в `Playing`.
- `Game/CameraFitter` — по ADR-002 D11.
- `UI/SafeAreaFitter` — подгоняет верхний якорь корневой панели HUD под `Screen.safeArea`.
- `Core/ProjectInstaller`, `Core/GameSceneInstaller` (`MonoInstaller`) — биндинги: `GameConfig`, `ISaveService`, `IAudioService`, `GameFlow`, `GameBootstrap`.

## 4. Сцена `Scene.unity` и префаб
- `Assets/Resources/ProjectContext.prefab` с `ProjectInstaller`; `SceneContext` в сцене с `GameSceneInstaller`.
- `BackgroundCanvas` (новый) и существующий `Canvas`, переименованный в `HudCanvas`, по ADR-002 D12. В `BackgroundCanvas` — `Image` со спрайтом `game/bg.png`.
- На `Main Camera` — `CameraFitter`. Пустой корень `WorldRoot` в точке `(0, 0, 0)`.
- Спрайты брать только по `DOC/screens.md`.

## 5. Не делать
Леденец, лента, ввод, физика, `LevelData`, экраны HUD/магазин/попапы, звук, правки `Loader`, asmdef, тесты, правки в GDD/ADR.

## 6. Рапорт `DOC/reports/REPORT-001.md` (без хеша)
Сделано по пунктам; значения Player Settings до/после; `git diff --stat` и список изменённых файлов, любые ненужные изменения от редактора перечислить отдельно; результаты приёмки ниже; консоль.

Дополнительно (только отчёт, без правок): для `game01…26` указать размер **текстуры** и совпадает ли крупнейший слайс с её полными границами; это нужно для решения «Multiple или Single» в этапе 2.

## 7. Приёмка
1. Консоль чистая, `Edit → Zenject → Validate Then Run` проходит без ошибок.
2. Запуск Loader → `Scene` без ошибок и предупреждений Zenject.
3. Camera: в Game view на разрешениях 1080×1920, 1080×2160, 1080×2340 `orthographicSize` равен 9.6 / 10.8 / 11.7, ширина мира везде 10.8; центр камеры `(5.4, orthographicSize)`. Значения — в рапорт.
4. Фон закрывает весь экран на всех трёх разрешениях без искажения.
5. `PlayerPrefsSaveService`: после записи и повторной загрузки значения совпадают (проверка разовой командой в редакторе, скрипт проверки не оставлять).
6. `git status` перед коммитом: только файлы из этого ТЗ.
Коммит: `TASK-001: project skeleton`, один пуш. Хеш — в чат оператору.
