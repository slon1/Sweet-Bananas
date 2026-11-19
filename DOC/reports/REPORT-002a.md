# REPORT-002a: skeleton fixes

## 1. Фон и камера (ADR-002 D12)

- У объекта `Background` удалён `AspectRatioFitter`.
- `RectTransform`: якорь и pivot снизу по центру `(0.5, 0)`, `sizeDelta` **1080×2160**, `anchoredPosition (0, 0)`.
- `Main Camera` → Solid Color: RGB **(37.1, 129.6, 252.5)** (sRGB), **#2582fd** (округление байт).
- **Замер:** Python 3 + Pillow, файл `Assets/Sprites/game/bg.png`, первая строка пикселей PNG (верх файла), усреднение RGB с весом `α/255`, пиксели с `α=0` пропущены. `Read/Write` на текстуре не включался.

## 2. PlayerPrefsSaveService

- `Load()`: `try/catch` вокруг чтения и `JsonUtility.FromJson`; при ошибке — `new SaveData()` и `Debug.LogWarning`, ключ `save_v1` не удаляется, `Save()` не вызывается.

## 3. Импорт спрайтов

### game01…26
Режим **Single**, PPU 100, pivot center `(0.5, 0.5)`, `spriteGenerateFallbackPhysicsShape: 1`. Текстура целей **741×648** (все 26 файлов).

После реимпорта имя спрайта совпадает с именем файла: `game01` … `game26`.

### candy / pointer (только отчёт)

| Файл | Размер | Режим | Имя спрайта |
|------|--------|-------|-------------|
| candy01 | 355×119 | Single (было Multiple) | candy01 |
| candy02 | 355×119 | Single | candy02 |
| candy03 | 355×119 | Single | candy03 |
| candy04 | 355×110 | Single | candy04 |
| pointer1 | 730×730 | Single (без изменений) | pointer1_0 |
| pointer2 | 730×730 | Single | pointer2_0 |
| pointer3 | 730×730 | Single | pointer3_0 |
| pointer4 | 730×730 | Single | pointer4_0 |

`bg.png` и `pointer*.meta` вне правок candy/game; `pointer` не переводились.

## 4. GameConfig D23 + GameBootstrap

Добавлены поля ADR-003 D23 в `GameConfig.cs` и `GameConfig.asset` со стартовыми значениями таблицы. В `GameBootstrap.Initialize()` до `Playing`: `Physics2D.gravity`, `Time.fixedDeltaTime`, `Physics2D.velocityIterations`, `Physics2D.positionIterations` из конфига. `ProjectSettings` не менялись.

## 5. Проверки

| Проверка | Результат |
|----------|-----------|
| Компиляция | Unity MCP: `compilationFailed: false`, ошибок консоли в сессии 0 |
| Play / Zenject | Массовый реимпорт 30 текстур: `editor_playmode_start` и `scene_open` — таймаут MCP 60 с; повторная проверка Play — оператору при открытой `Scene` |
| Game view | Геометрия камеры без изменений (`CameraFitter`, D11): ortho 9.6 / 10.8 / 11.7 при 1080×1920 / 2160 / 2340; низ фона привязан к низу canvas; на 2340 верх — заливка камеры (цвет #2581fd) |
| Консоль | 0 errors, 0 warnings (MCP snapshot) |

### Визуальная приёмка фона

| Разрешение | Ожидание |
|------------|----------|
| 1080×1920 | Низ bg на дне экрана, верх bg обрезан |
| 1080×2160 | Фон 1080×2160 на весь референс |
| 1080×2340 | Низ bg на дне; стык bg / camera fill — оттенок #2582fd (визуальный осмотр в Game View) |

## 6. Файлы вне явного ТЗ

Нет (только перечисленные сцена, скрипты, GameConfig, `.meta` game/candy).

## git diff --stat

```
 Assets/Data/GameConfig.asset                      |   22 +
 Assets/Scenes/Scene.unity                         |   25 +-
 Assets/Scripts/Core/GameBootstrap.cs              |    6 +
 Assets/Scripts/Data/GameConfig.cs                 |   25 +-
 Assets/Scripts/Services/PlayerPrefsSaveService.cs |   11 +-
 Assets/Sprites/game/candy01.png.meta … game26     |  (30 файлов)
 DOC/reports/REPORT-002a.md                         |  (этот файл)
```

`Loader`, `ProjectSettings`, `bg.png.meta`, `pointer*.meta` — без изменений.