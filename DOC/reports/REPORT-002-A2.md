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

## 7. Поправка 6 (amendment-6, A8 + clearance) — приёмка F2

**Прогон:** 2026-11-20, коммит `e8490bc`. **E0 не запускался.**

### F2.1–F2.2 Gap Sanity

`gap_sanity_pass=**1**`. Все `min_clearance_step0` ≥ 0.015. `LastSpawnLift` по долям:

| fraction | lift (юн) | min_clearance step0 | max hinge @ step1 |
|----------|-----------|---------------------|-------------------|
| 0.05 | 1.60 | 0.0217 | ~1e-6 |
| 0.25 | 1.38 | 0.0231 | ~7e-6 |
| 0.50 | 0.62 | 0.0158 | ~1e-6 |
| 0.75 | 0 | 0.0200 | ~8e-6 |
| 0.90 | 0 | 0.0200 | ~6e-6 |

`rest_1.0s`: hinge_slack ≈ 9.8×10⁻⁷, **R = 0.843**. **R @ 1 s** по долям: 0.25→0.843, 0.50→0.880, 0.75→0.845, 0.90→0.631 (&lt; 0.9, ожидаемо на куполе; выше 0.35).

Лог: `Logs/SpikeBench/gap_sanity.txt`.

### F2.3 C0 (after_A8)

| Метрика | Значение |
|---------|----------|
| hinge_slack @ build | 2.4×10⁻⁷ |
| hinge_slack @ 1.0 s | 9.8×10⁻⁷ |
| **r_rest @ 1.0 s** | **0.843** |
| last_spawn_lift | 1.38 |
| mesh offset @ 1.0 s | 0.164 (порог 0.05 — выше) |
| mesh offset step0 (сразу после reset) | 1.54 (интерполяция transform) |
| mesh offset after stop | 0.164 |

Скриншоты: `c0_game_rest_20261120_200359.png`, `…_flight_…`, `…_after_stop_…` в `Logs/SpikeBench/`.

**Замечание:** в одном из прогонов C0 `min_spawn_clearance_step0` дал отрицательное значение (перекрытие в кадре замера) при прошедшем Gap Sanity — зафиксировать архитектору; не блокирует F2.2 (`gap_sanity_pass=1`).

`ValidateHingeAnchors` даёт `LogError` при hinge_slack &gt; 0.001 на «живой» ленте после шагов (до 0.002) — порог Build/reset жёстче, чем покой с кривизной купола.

### F2.5 Smoke @ 1× (2 tap + 2 hold)

Файлы: `throws_smoke_2760704_20261120_200140.csv`, `trace_smoke_2760704_20261120_200140.csv`.

| throw | kind | spawn | max_gap | a10 |
|-------|------|-------|---------|-----|
| 0 | tap | 0.7 | 0.099 | 0 |
| 1 | tap | 0.7 | 0.100 | 0 |
| **2** | **hold** | **0.25** | **0.069** | **0** |
| 3 | hold | 0.7 | 0.100 | 0 |

**Бросок 2 (0.25 hold): A10 выполняется** (было 0.36 до A8).

### Вердикт F2

F2.1–F2.2 **выполнены**.

---

## 8. E0 baseline (amendment-7, spawn controls)

**Прогон:** 2026-11-20. **G1–G3** (контроль спавна, `MaxHingeSlack` на шагах, серия 100+20 @ 12× + verify 10 @ 1×).

### Smoke перед E0 (@ 1×)

`summary_smoke_2760704_20261120_221209.txt`: `spawn_invalid_count=0`. Бросок **hold @ 0.25** (index 2): `spawn_clearance_step0=0.0231`, `spawn_invalid=0`.

### E0 @ 12×

`summary_E0_2760704_20261120_221256.txt`, CSV: `Logs/SpikeBench/throws_E0_2760704_20261120_221256.csv`.

| Метрика | Значение |
|---------|----------|
| throws / valid | 120 / 120 |
| **spawn_invalid** | **0 (0 %)** |
| a10_breaks (valid, first &gt; 0.21) | 5 |
| a10_soft_bin_limit (valid) | 6 |
| max_gap (valid, до freeze &gt; 0.21) | 0.350 |
| r_min_throw min / median (valid) | 0.00076 / 0.556 |

Гистограмма max_gap (valid), фаза @ max до freeze: **все 120** — `contact` (бин 0.085: 67; 0.085–0.21: 48; 0.21–0.5: 5).

События first &gt; 0.21 (valid): throws 11, 18, 53, 60, 95 — все `contact`, пары 6, шаги 33/38.

**Отрицательный C0 clearance:** на E0 invalid нет; гипотеза `Physics2D.SyncTransforms()` перед замером — **подкреплена данными** (без отдельного прогона).

### verify1×

`summary_E0_verify1x_2760704_20261120_221256_verify1x.txt`: `verify_a10_flag_matches=10/10`, `verify_max_gap_delta≈0.084`, `verify_result=match`.

### Отложено (по 7a)

- **C7 / RibbonMesh** — не делалось.
- **B6 / B7** (`Run Rest Probe`, колонки rest) — отдельный коммит после этого отчёта, до E2.

**Стоп после E0** — ждать архитектора.

---

## 9. Открытые пункты / стоп

1. **До A8 (история):** gap_sanity_pass=0 на 0.50@step5, smoke hold 0.25 A10 — overlap при старом спавне.
2. **E2+** — по решению архитектора после E0.

---

## 10. Где смотреть сырьё (оператор)

| Артефакт | Путь |
|----------|------|
| C0 | `Logs/SpikeBench/c0_results.txt`, `c0_screenshots.txt` |
| Gap sanity | `Logs/SpikeBench/gap_sanity.txt` |
| Overlap | `Logs/SpikeBench/gap_sanity_overlap_diag.txt` |
| Math | `Logs/SpikeBench/math_sanity.txt` |
| Smoke / trace | `throws_smoke_*.csv`, `trace_smoke_*.csv` |
| E0 | `throws_E0_*.csv`, `summary_E0_*.txt`, `summary_E0_verify1x_*.txt` |
| D1 dump | `c0_joint_dump.txt` (большой) |

При следующей вехе этот раздел REPORT обновлять и коммитить вместе с `DOC/reports/REPORT-002-A2.md`.