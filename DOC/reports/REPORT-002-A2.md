# REPORT-002-A2: стабилизация ленты (SpikeBench, поправки 1–5)

Задача: TASK-002-A2, amendment-1…5. Сырьё прогонов лежит в `Logs/SpikeBench/` (в git не коммитится). В этом файле — все приёмочные числа и выводы для архитектора.

Код в `main` (кратко): `fix hinge anchors (C0)` → D1–D3 → `GetRelativePoint (E1)` → `gap sanity windows, overlap diag`. Хеш коммита для сверки передаёт оператор в чат.

---

## 1. Метрика якорей (amendment-5, E1)

**Ошибка:** в бенче использовался `Rigidbody2D.GetPoint` с локальными якорями (инверсия), из-за чего C0/D1 показывали невязку ~0.83 и `R_rest` ~5.33 при целой ленте в Game view.

**Исправление:** `GetRelativePoint` в `Ribbon.ValidateHingeAnchors`, `SpikeBenchMeasurements`, `SpikeBenchJointDump`. `LastBuildSlack` после `Build()`.

### Run Math Sanity

| Проверка | Результат |
|----------|-----------|
| `GetRelativePoint` (3,2) rot 30°, local (1,0) | (3.87, 2.50), error 0 |
| `GetPoint` (та же пара) | (-2.73, -0.73) — ожидаемо неверно |
| verdict | **PASS** |

Локальный лог: `Logs/SpikeBench/math_sanity.txt`.

**Старые значения `r_rest_at_1.0s=5.33`, `hinge_slack_at_1.0s=10.7` — недействительны** (артефакт GetPoint до amendment-5).

---

## 2. Приёмка E2 / C0 (метрика GetRelativePoint)

### 2.1 Якоря и изоляция

| Метрика | Значение | Порог |
|---------|----------|-------|
| `last_build_slack` | ~2.4×10⁻⁷ | ≤ 0.001 |
| `hinge_slack` после reset (все доли) | ≤ ~10⁻⁶…10⁻³ | ≤ 0.001 |
| Chain isolation baseline step0 (редактор) | ~2.4×10⁻⁷ | ≈ 0 (подтверждение геометрии цепи) |

**Вердикт оператора:** E2 по метрике **принят** — физика цепи и сборка шарниров в порядке.

### 2.2 Run Gap Sanity (окна)

Критерий: после reset ≤ **0.001**; шаги **5, 20, 120** и **rest 1.0 s** ≤ **0.02**; шаги **1–4** только в лог (`tag=info`).

Сводка по **pass**-шагам:

| fraction | step 5 | step 20 | step 120 |
|----------|--------|---------|----------|
| 0.05 | 0.0026 | 9.5×10⁻⁵ | 1.9×10⁻⁵ |
| 0.25 | 0.0019 | 0.0016 | 0.0012 |
| **0.50** | **0.0406** | 0.0017 | 3.1×10⁻⁵ |
| 0.90 | ~10⁻⁶ | 2.9×10⁻⁴ | 0.0018 |

`rest_1.0s_hinge_slack` = **0.000123**.

**Info (step 1, всплески решателя / перекрытие):** 0.25 → ~0.0027…0.0039; 0.50 → **~0.168** на step 1–2.

`gap_sanity_pass=0` из‑за **fraction=0.50, step=5 → 0.0406 > 0.02** (не из‑за метрики якорей).

Локальный лог: `Logs/SpikeBench/gap_sanity.txt`.

### 2.3 Overlap diagnostic (head+stick IgnoreCollision)

| fraction | step=1 slack **с** ignore | step=1 slack **без** ignore (info) |
|----------|---------------------------|-------------------------------------|
| 0.25 | **2.25×10⁻⁶** | ~0.003 |
| 0.50 | **0.00165** | ~0.168 |

**Вывод:** всплеск на step 1 у 0.25 почти полностью снимается отключением коллизий с головой/палочкой → правдоподобно **перекрытие ленты с головкой при спавне** (`headClear` по центру, концы в круге). Правка спавна — **E2(a)** основного плана, не в рамках 002-A2.

Лог: `Logs/SpikeBench/gap_sanity_overlap_diag.txt`.

### 2.4 C0 результаты (повторный прогон)

| Поле | Значение | Примечание |
|------|----------|------------|
| `metric_source` | GetRelativePoint | |
| `hinge_slack_after_build` | 2.4×10⁻⁷ | |
| `hinge_slack_at_1.0s` | 0.00087 | |
| `r_rest_at_1.0s` | **0.277** | спавн до A8, лента на куполе головки |
| `mesh_capsule_center_offset` | 0.065 | порог 0.05 — **чуть выше** |
| `mesh_capsule_ok` | 0 | |
| скриншоты | `spawn_preA8_head_dome` | Game view, Gizmos |

Пути (локально): `Logs/SpikeBench/c0_results.txt`, `c0_screenshots.txt` (например `c0_game_rest_preA8_20261120_070557.png`, `…_flight_…`, `…_after_stop_…`).

**R_rest ≥ 0.9** на текущем спавне не ожидается до A8; критерий «прямая на палочке» к этому кадру не применяется.

---

## 3. D2 — линия проигрыша

- Код: `FailLineY = −FailMarginUnits` (`SpikeOverlay`, SpikeBench).
- Камера: `world_bottom_edge_y=0`, `fail_line_y=-1`.
- Ранний автопрогон на 30 с не информативен; overlap/autoreset дописывались в amendment-4/5.

---

## 4. Smoke 1× (C1–C4, 2 tap + 2 hold)

**Seed:** 2760704. **timeScale:** 1.  
**CSV:** `Logs/SpikeBench/throws_smoke_2760704_20261120_070251.csv`  
**Трасса (бросок 0):** `Logs/SpikeBench/trace_smoke_2760704_20261120_070251.csv`

| throw | kind | spawn | press_s | max_gap | a10 | r_min | first_gap_021 |
|-------|------|-------|---------|---------|-----|-------|---------------|
| 0 | tap | 0.7 | 0.14 | 0.124 | 0 | 0.261 | — |
| 1 | tap | 0.7 | 0.12 | 0.113 | 0 | 0.491 | — |
| 2 | hold | 0.25 | 0.50 | 0.362 | **1** | — | step 0, pair 1, 0.362 |
| 3 | hold | 0.7 | 0.80 | 0.113 | 0 | 0.744 | — |

Метрика разрыва: **первое** превышение 0.085 / 0.21, `max_gap` до первого шага > 0.21; A10 по `first_gap_021`. Методика сидов в `DescribeGridOrder` (seed+101 / seed+17+202).

**E0 (100+20 @ 12×) не запускался** — только после «E0 можно».

---

## 5. Наблюдения (без правок)

- В Play лента визуально цельная; зигзаг в полёте — упор в лимиты ±65°.
- На кадре покоя лента на **куполе головки** (ожидаемо до A8).
- Загиб у левого конца — зафиксирован на скринах; разбор отложен до E2(a).
- `JointLimitDeg` — по числам R после E2s, не по старым 5.33.

---

## 6. Изменённые области (по поправкам)

| Область | Файлы |
|---------|--------|
| Якоря Build | `Ribbon.cs` |
| Метрики / бенч | `Assets/Editor/SpikeBench/*` |
| Fail line | `SpikeOverlay.cs`, `SpikeBenchMeasurements.FailBounds` |
| Хук выборки | `ThrowController.cs` (конец `FixedUpdate`) |

`GameConfig`, ADR (текст A10/A15 правит архитектор), `ProjectSettings`, верле — не трогались.

---

## 7. Открытые пункты / стоп

1. **gap_sanity_pass=0:** 0.50 @ step 5 (0.041) — overlap/решатель после спавна; исправление в E2(a).
2. **hold smoke @ 0.25:** A10 на первом шаге (0.36) — согласуется с overlap на доле 0.25.
3. **mesh_capsule_offset** 0.065 — гранично; возможен вклад интерполяции transform vs rigidbody (C7).
4. **E0** — ждёт разрешения архитектора.

---

## 8. Где смотреть сырьё (оператор)

| Артефакт | Путь |
|----------|------|
| C0 | `Logs/SpikeBench/c0_results.txt`, `c0_screenshots.txt` |
| Gap sanity | `Logs/SpikeBench/gap_sanity.txt` |
| Overlap | `Logs/SpikeBench/gap_sanity_overlap_diag.txt` |
| Math | `Logs/SpikeBench/math_sanity.txt` |
| Smoke / trace | `throws_smoke_*.csv`, `trace_smoke_*.csv` |
| D1 dump | `c0_joint_dump.txt` (большой) |

При следующей вехе этот раздел REPORT обновлять и коммитить вместе с `DOC/reports/REPORT-002-A2.md`.