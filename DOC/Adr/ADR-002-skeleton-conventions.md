# ADR-002: Конвенции каркаса (этап 1)

Статус: принято (архитектор). Дополняет ADR-001.

## D10. Namespaces
`SweetBananas.Core`, `.Game`, `.UI`, `.Data`, `.Services`. Папка = namespace. `LoadActivityScript` остаётся как есть (без namespace).

## D11. Мировые координаты и камера
- 1 юнит = 100 px (PPU 100). Ширина мира фиксирована: **10.8 юнита** (1080 px), значение берётся из `GameConfig.WorldWidthUnits`.
- Камера ортографическая, `orthographicSize = (WorldWidthUnits / 2) * Screen.height / Screen.width`. Для 1080×2160 это 10.8, для 1080×1920 это 9.6, для 1080×2340 это 11.7.
- **Мир привязан к левому нижнему углу экрана:** позиция камеры `(WorldWidthUnits / 2, orthographicSize, -10)`, так что точка `(0, 0)` — левый нижний угол, ось Y вверх. Лишняя высота прибавляется сверху, у узких экранов обрезается верх.
- Пересчёт из Figma (координаты от левого верхнего угла кадра 1080×2160): `world = (x / 100, (2160 − y) / 100)`.
- Компонент `CameraFitter` пересчитывает размер и позицию при смене разрешения (в `Awake` и при изменении `Screen.width/height`).

## D12. Слои отрисовки
Sorting Layers (снизу вверх): `Background`, `World`, `Hud`.
- `BackgroundCanvas` (ScreenSpace-Camera, sorting layer `Background`): `Image` с `game/bg.png`, `AspectRatioFitter` в режиме Envelope Parent (кадрирование без искажения).
- Физические `SpriteRenderer`/меш — sorting layer `World`, внутри слоя порядок по `sortingOrder`.
- `HudCanvas` (ScreenSpace-Camera, sorting layer `Hud`): HUD, магазин, попапы дочерними панелями. Поддержка safe area для верхней части (компонент `SafeAreaFitter` на корневой панели HUD).
- Оба Canvas: Scale With Screen Size 1080×2160, Match = 0.

## D13. DI и асинхронность
- Extenject: `MonoInstaller`, внедрение через конструктор или `[Inject]` метод у MonoBehaviour; статических синглтонов нет.
- `ProjectContext` — префаб `Assets/Resources/ProjectContext.prefab` с `ProjectInstaller`; `SceneContext` в сцене `Scene` с `GameSceneInstaller`.
- Zenject Signals не используем; события — C# `event Action`.
- Асинхронность — `UniTask` с токеном `this.GetCancellationTokenOnDestroy()` (или токеном сервиса). Не `async void`, не `Task`.
- Юнит-тесты на этом этапе не пишем: без asmdef тестовая сборка не видит `Assembly-CSharp`.

## D14. Сохранения
`ISaveService` + `PlayerPrefsSaveService`: один JSON в ключе `save_v1` (`JsonUtility`), класс `SaveData` с полем `version`.
Поля: `version`, `coins`, `ownedSkinsMask` (бит 0 установлен по умолчанию), `equippedSkin` (0…3), `levelIndex` (0…25), `soundOn`.

## D15. Леденец `pointerN`
Использовать **целую текстуру** (`pointerN.png`, 730×730, режим Single) как один спрайт. Мелкие rect в `.meta` — осколки автослайса, игнорируются. Соответствие: слот магазина N ↔ `pointerN`.

## D16. Версия URP
В `manifest.json` URP приводим к версии, которую поставляет редактор (**17.5.0**), чтобы manifest и lock совпадали. Это единственная ручная правка manifest, дальше ручных правок нет.

## D17. Частота кадров
`Application.targetFrameRate = 60`, `QualitySettings.vSyncCount = 0` — выставляет `GameBootstrap` из значений `GameConfig`.
