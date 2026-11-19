# ADR-001: Базовая архитектура Sweet Bananas

Статус: принято (оператор, 2026-11-19). Автор: архитектор.

## Решения
**D1. Визуал ленты — процедурный меш.** Полоса вершин по позициям сегментов, UV от `candyNN`, `MeshFilter`+`MeshRenderer` с unlit/2D-материалом. Причина: Skinning Editor работает только через GUI, а меш полностью управляется кодом.

**D2. Физика ленты — цепь `Rigidbody2D` + `HingeJoint2D`**, 6–8 сегментов, лимиты углов ±65°, лёгкий возвращающий момент в `FixedUpdate`. Сначала spike с критериями: стабильность на скорости броска, нет туннелирования, «вязкая» посадка, 60 FPS на среднем Android. Не прошла — запасной вариант: верле-канат с кастомными коллизиями (отдельный ADR).

**D3. Ширина мира фиксирована.** PPU 100, 1080 px = 10.8 юнита. Ортографический размер камеры считается по ширине экрана; лишняя высота — продолжение фона. `CanvasScaler`: Scale With Screen Size 1080×2160, Match = 0 (по ширине).

**D4. Слои.** Canvas фона (ScreenSpace-Camera, order −10) → физические `SpriteRenderer` в мире → Canvas HUD (order +10). Попапы и магазин — на HUD-canvas.

**D5. Сцены.** `Loader` → `Scene` (игровая). Магазин и попапы — префабы-оверлеи. Уровни: `LevelData` (ScriptableObject: спрайт цели, `candyNN`, при необходимости точки) + префаб цели.

**D6. Сохранения.** `ISaveService`, один JSON в PlayerPrefs.

**D7. Ввод.** Новый Input System, единый указатель (тап/удержание по времени и состоянию касания).

**D8. Коллайдеры целей.** Спрайты `gameNN` — базовый источник контура: `PolygonCollider2D` по physics shape спрайта, затем ручная правка там, где нужно (банан, корона и т. п.).

**D9. Звук.** Интерфейс `IAudioService` с пустой реализацией; ассетов пока нет.

## DI (Extenject)
- `ProjectInstaller`: `ISaveService`, `IAudioService`, `GameConfig`, `ILevelRepository`.
- `GameSceneInstaller`: `GameFlow` (state machine: Playing, Win, Shop), `ThrowController`, `Ribbon`, `Lollipop`, `UiRoot`, фабрика уровней.
- Асинхронность — `UniTask`; запрещены `async void` и `System.Threading.Tasks.Task` в новом коде.

## Папки
`Assets/Scripts/{Core,Game,UI,Data,Services}`, `Assets/Prefabs`, `Assets/Data` (ScriptableObject).

## Последствия
Физика и UI изолированы слоями, поле одинаково на всех устройствах, ленту можно полностью собрать и отладить кодом. Риск: стабильность цепи шарниров, закрывается spike (этап 2).
