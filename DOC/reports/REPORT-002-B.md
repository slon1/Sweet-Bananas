# REPORT-002-B: цель, правила, StickFriction

## 1. Цель
- Позиция: `(7.415, 6.58, 0)`, scale 1, sorting `World` order 0.
- Контур после `Awake`: **1 путь / 119 точек → 1 путь / 53 точки** (`TargetHolder` + `target_metrics.txt`).
- `PhysicsMaterial2D`: friction `TargetFriction` (0.9), bounciness 0.
- Зазор кончика палочки до коллайдера цели (`Physics2D.Distance`):
  - покой −25°: **0.0446** юн;
  - пик тапа: **3.281** юн.

## 2. LevelRules
- `FixedUpdate` при `GameFlow.State == Playing`.
- Fail: центр ленты ниже `−FailMarginUnits` или за боковыми границами → `Ribbon.SetFrozen(true)`, `GameFlow.Fail`, `FailCount++`.
- Win: ≥2 сегмента `IsTouching` цели, max speed сегментов `< SettleSpeedThreshold`, удержание `SettleSeconds` → freeze + `Win`.
- `Restart(spawnFraction?)`, `IsFirstAttempt`, dev-хуки `DebugSetEnabled`, `FirstTargetContact` / `HadFirstTargetContact` / `FirstContactCenterY`.
- `SpikeOverlay`: Restart → `LevelRules.Restart()`, строка `State / Fails`, автосброс убран.
- **Отклонений по логике ТЗ нет.** Win/PLAY AGAIN попапы не делались (этап 3).

## 3. StickFriction (D24.9)
Сетка **0.05 … 0.40** прогнана (`rest_probe_f*_20261121_033*.csv`). Итоговая таблица: `Logs/SpikeBench/stick_friction_grid_summary.txt`.

| friction | tip@0.25 (2.5–4.0 с) | pass v1≤0.70 |
|----------|----------------------|--------------|
| 0.05–0.40 | **нет** (tip пусто на 0.25) | **0** |

**Выбор по п.3.4 невозможен — стоп по ТЗ.** `GameConfig.StickFriction` **не менялся (0.4)**.

Правка бенча: `StickFrictionPicker` индексирует CSV по порядку (локаль `,` в имени файла). `RestProbe`: `_findStart` при цепочке в play mode.

**D24.4 catch (текущий 0.4):** `d24_4_catch.txt` — **5/5** (до задачи).

## 4. Проходимость (4.1) — частично
Прогон **прерван** (баги раннера; исправлены). Файл: `level1_grid_2760704_20261121_035616.csv`.

| | |
|--|--|
| Бросков | 113 / 143 |
| Win / Fail / Timeout | 3 / 110 / 0 |
| Win % | 2.7% |
| Побед с lift≤0.3 | **0** (min win lift 4.01, max 5.87) |

Нужен **полный повтор** `B — Level1 playability grid (12×)` после исправлений. Критерий «0 побед» формально не сработал (3 победы), но посадка и доля побед не проходят.

## 5. Повтор D24.1 (4.2)
`throws_B24_1_2760704_20261121_040705.csv`, 120 бросков, rules off, 12×.

| Критерий | Результат |
|----------|-----------|
| NaN | 0 |
| max_speed | 46.44 ≤ 60 |
| spawn_invalid | 0% |
| r_min_throw | min 0.527 ≥ 0.25 (нарушений 0) |
| first gap >0.21 | **36 / 120 (30%)** — **порог 2% не выполнен** |
| r_stopped_min (фаза stopped) | колонка пуста (фаз stopped в выборке не дала строк с gap) |

A26 stopped: записаны числа; **подбор не ведётся** (нарушение gap 0.21).

## 6. Геометрия (4.3)
`geometry_b243.txt`: ожидаемые `orthographicSize` 9.6 / 10.8 / 11.7 / 11.4 при `world_bottom_edge_y=0` — формулы совпали. В play mode Game view 1080×2160: цель, лента на палочке, оверлей State/Fails — кадр снят в ходе работы (MCP `capture_game_view`).

## 7. GameConfig
Изменений нет (`StickFriction` = 0.4); diff `GameConfig.asset` пустой.

## 8. Git / окружение
- `ProjectSettings/TimeManager.asset`: локально `Fixed Timestep` ≈ 1/120 от bootstrap — **не коммитить** (откат перед коммитом).
- `DOC/files.zip` не трогали.
- Консоль после B24.1 и геометрии: без ошибок (кроме зафиксированных стопов StickFriction / неполного Level1).

## 9. Открытые вопросы
1. **StickFriction:** ни одно значение сетки не даёт tip 2.5–4.0 с на доле 0.25 и v1&lt;0.05 для долей ≤0.70. Решение архитектора?
2. **Level1:** повторить полную сетку 143+b verify 1×; подъём центра при Win >> 0.3 — цель слишком близко к палочке или критерий посадки?
3. **B24.1 с целью в сцене:** 30% бросков с first_gap&gt;0.21 — принимать как регрессию цели или ослабление только для acceptance 2B?