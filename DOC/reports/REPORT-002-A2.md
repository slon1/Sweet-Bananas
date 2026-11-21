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

**C0 clearance @ 1 с:** поле переименовано в `min_clearance_at_1s` (покой на куполе, не спавн step0). Гипотеза `SyncTransforms` для C0 отрицательного значения — **непроверенная** (E0 invalid=0 — отдельный факт).

### verify1×

`summary_E0_verify1x_2760704_20261120_221256_verify1x.txt`: `verify_a10_flag_matches=10/10`, `verify_max_gap_delta≈0.084`, `verify_result=match`.

### Отложено (по 7a)

- **C7 / RibbonMesh** — не делалось.
- **B6 / B7** (`Run Rest Probe`, колонки rest) — отдельный коммит после этого отчёта, до E2.

**Стоп после E0** — ждать архитектора.

---

## 9. Amendment-8: H1–H3, E1 (диагностика и привод)

### H0
`c0_results.txt`: `min_spawn_clearance_step0` → `min_clearance_at_1s`.

### H2 — корреляция (E0 CSV `throws_E0_2760704_20261120_221256.csv`)
| Срез | Счётчик |
|------|---------|
| valid | 120 |
| (a) A10 &gt; 0.21 | 5 (все **tap**, spawn **0.90**, press 0.10×3 / 0.12×2) |
| (b) бин 0.085–0.21 | 48 |
| (c) `r_min` &lt; 0.35 | **18** (до колонки `r_min_phase` в этом прогоне; повторный анализ — по новому CSV) |

Файл: `Logs/SpikeBench/e0_correlation.txt`.

### H1 — трассы пяти разрывов @ 12×
`trace_E0_{11,18,53,60,95}.csv`, выводы: `h1_conclusions.txt`.

| throw | одна строка вывода |
|-------|-------------------|
| 11 | step 33, pair 6, seg6↔head, ω=0, contact, Return @ 12× |
| 18 | step 38, pair 6, seg7↔head, ω=0, contact, Return |
| 53 | step 33, pair 6, seg7↔head, ω=0, contact, Return |
| 60 | **не воспроизведён** @ 12× (`reproduce_gt_0.21=0`); на **1× worst-verify** разрыв есть (см. H3) |
| 95 | step 33, pair 6, seg7↔head, ω=0, contact, Return |

### H3
Стандартный **verify1×** (E0): строки в `summary_E0_verify1x_2760704_20261120_221256_verify1x.txt` (10 бросков, A10 match 10/10).

**verify1x_worst** (5 разрывов + 5 max soft-bin): `summary_E0_verify1x_worst_2760704_20261120_223117.txt` — **на 1× разрывы воспроизводятся** (6/10 с A10, `worst_verify_a10_matches=9/10`; throw 59 soft-bin даёт A10 только на 1×).

### E1 — V1 `angularVelocity` vs V0 (E0, `MoveRotation`)

| Метрика (valid) | V0 E0 @ 12× | V1 E1 @ 12× |
|-----------------|-------------|-------------|
| A10 breaks | **5** | **5** |
| бин 0.085–0.21 | **48** | 57 |
| max_gap | **0.350** | 0.352 |
| r_min min / median | 0.00076 / **0.556** | 0.0087 / 0.529 |

**Ранжирование (1→3):** V0 **не хуже** (мягкий бин лучше; max_gap чуть лучше). **Оставляем MoveRotation** (V1 не выбран).

**A1** (`a1_probe.txt`, клон конфига 600°/с, без diff `GameConfig.asset`): **max ω = 600°/с**, pass ≥ 570.

**E1 играбельность:** `e1_max_tap_overshoot_deg=0`, hold=0, `return_to_rest` max ≈ 0.17 с.

E1 CSV/summary: `throws_E1_2760704_20261120_223246.csv`, verify `summary_E1_verify1x_…_verify1x.txt`.

**Стоп после E1** — E2 не начинать. C7/B6/B7 по-прежнему отложены.

---

## 10. Amendment-9: J (воспроизводимость), K (H1b), L (B6/B7), M (E2)

### J1 — 10 бросков × 2 rep × (1× и 12×)
Артефакты: `j1_repro.txt`, `throws_J1_2760704_20261120_232220.csv`, `summary_J1_…232220.txt`.

| throw | run-to-run @1× | run-to-run @12× | 1× vs 12× (rep1) |
|-------|----------------|-----------------|------------------|
| 11, 18, 53, 95 | идентичны | идентичны | близки (A10 сохраняется) |
| 15, 27, 57 | max_gap совпадает | max_gap совпадает | A10=0 обе шкалы |
| 17, 59 | расходятся (A10 / step) | расходятся | **17:** A10 только на 12× rep1; **59:** A10 на 1× rep2 и 12× rep2 |
| 60 | идентичны | идентичны | step/value слегка различаются @12× |

**Вывод:** при фиксированной `timeScale` два повтора совпадают, кроме **17** и **59** (зафиксировано как находка, без поиска причины). Между 1× и 12× — те же расхождения, что в §9 (60/59/17).

### J2 — шум серий и правило `ScreeningTimeScale`
`summary_E0b_…224813`, `summary_E0c_…225034`, `j2_timescale_rule.txt`: E0 / E0b / E0c **совпадают** (A10=5, soft=48, max_gap≈0.350).  
**Правило:** `screening_time_scale=12`, скрининг на 12×, финальные подтверждения на 1×.

### K — H1b (контакты)
`trace_H1b_*`, `h1_conclusions.txt` (ω в трассе — `DeltaAngle/dt`).

| throw | одна строка |
|-------|-------------|
| 11 | step 33, pair 6, **seg6 via stick**, импульс ~0 (distance/touch) |
| 18 | step 38, pair 6, **seg7 via stick** |
| 53 | step 33, pair 6, **seg7 via stick** |
| 60 @1× | step 38, pair 6, **seg7 via stick** (на 12× в этой серии разрыва нет) |
| 95 | step 33, pair 6, **seg7 via stick** |

Механизм разрыва по-прежнему **не установлен** (контакт со **stick**, не голова; импульсы в логе ≈0).

### L — B6/B7
Коммит `TASK-002-A2: rest probe (B6/B7)`; `rest_probe_2760704_20261120_224145.csv`.

### M — E2b / E2s @ 12×
**E2b** (голова off после spawn clearance): `summary_E2b_2760704_20261120_235027.txt` — A10=**5**, soft=**51**, max_gap=**0.350**, r_min min=0.0076 (первый прогон без отложенного off дал 100% spawn_invalid; исправлено `SetHeadOffAfterSpawnClearance`).

| серия | A10 | soft 0.085–0.21 | max_gap | r_min min | A12 (min≥0.35) |
|-------|-----|------------------|---------|-----------|----------------|
| E0 (база) | 5 | 48 | 0.350 | 0.00076 | нет |
| E2b | 5 | 51 | 0.350 | 0.0076 | нет |
| E2s J65 | 5 | 48 | 0.350 | 0.00076 | нет |
| E2s J35 | 5 | 47 | 0.290 | 0.080 | нет |
| E2s J25 | 2 | 52 | 0.218 | 0.390 | **да** |
| E2s J15 | 0 | 49 | 0.137 | 0.641 | **да** |

Лучший компромисс по ранжированию ТЗ среди сетки: **J25** (2 разрыва, мягкий бин 52). Полная сетка RestoreK × толщина **не запускалась** — в оркестраторе только JointLimit; шаги ×1/×5/×20 и thickness 0.3/0.4 остаются на следующую итерацию архитектора.

`GameConfig.asset` после серий: **diff пуст** (проверено).

**Стоп:** E2s не остановлена по A12 (J25/J15 проходят порог r_min); продолжение подбора параметров — за архитектором.

---

## 11. Amendment-10 / 10a: FINAL_J25, A21, D24 (принятие)

### 1. Итоговый `GameConfig`
| Поле | Было | Стало |
|------|------|-------|
| `JointLimitDeg` | 65 | **25** |

Новая сессия Play, `Ribbon.Build()` в `Start()`; override E2 не использовался.

### 2. Серия FINAL_J25 (2.1)
Источник: `summary_FINAL_J25_2760704_20261121_002403.txt`, `throws_…002403.csv`, `tunnel_FINAL_J25.txt`.

| Метрика | Значение |
|---------|----------|
| throws | 360 (300 tap + 60 hold) @ 12× |
| valid / spawn_invalid | 360 / 0 (0%) |
| A10 breaks (>0.21) | **7** (лимит 2% = 7) |
| soft bin 0.085–0.21 | 151 |
| max_gap | 0.221 |
| r_min min / median | **0.275** / 0.854 |
| tunnel violations | 0 |
| verify 1× (10 бросков) | A10=0, max_gap≈0.124 |

Разрывы по индексам: **17, 59** и кратные +84 в сетке (pair 4, contact). Сравнение с E2s_J25 (120 бросков): там 2 разрыва, soft=52; на 360 бросков 7 разрывов — ожидаемое масштабирование сетки.

### 3. Таблица A21 (`a21_verdict.txt`)
| Пункт | Вердикт |
|-------|---------|
| 1 NaN, speed | pass |
| 2 stopped gap ≤0.05 | pass |
| 3 breaks ≤2% | **pass** (7/7) |
| 4 R_min ≥0.35 каждый бросок | **FAIL** (1 бросок &lt; 0.35) |
| 5 spawn_invalid | pass |
| D24.2 tunnel | pass |
| D24.9 rest / slide | **FAIL** (tip not 2.5–4 s на части долей; |v₁s| до 3.46 @ 0.7) |
| D24.3 lever Δ≥2 | pass (17.14) |
| D24.4 catch 5/5 | pass |
| D24.7 physics / mesh | pass |

**Критерии закрытия 002-A2 не выполнены** (жёсткий п.4; D24.9 вне нормы). Подбор параметров не проводился.

### 4. D24 артефакты
- Rest: `rest_probe_2760704_20261121_002547.csv`, `rest_probe_table_J25.txt`
- Lever: `d24_3_lever.txt`
- Catch: `d24_4_catch.txt`
- Perf: `d24_7_profile.txt` (Physics2D.Simulate ≈ 0.076 ms/step на verify @1×; mesh alloc max 0)

### 5. Кадры Game view
`DOC/reports/img/REPORT-002-A2/rest.png`, `flight.png`, `stopped.png` (временно сняты с C0 до отдельного прогона 1080×2160 на J25).

### 6. `git diff --stat` (на момент прогона)
Ожидается: `GameConfig.asset` (1 строка), `Assets/Editor/SpikeBench/*`, `DOC/reports/…`.

### 7. Консоль
FINAL: без abort spawn. Rest probe: исправлен `IOException` при `WriteSummaryTable` (закрытие CSV до чтения). Предупреждения UI `IsPointerOverGameObject` — как ранее.

### 8. Открытые вопросы
1. Решение архитектора: r_min &lt; 0.35 на одном из 360 бросков при J25 — принимать ли конфиг или менять политику A12?
2. D24.9: время до кончика не везде попадает в 2.5–4 s; нужен ли подбор `StickFriction` (отменённый E2f) или уточнение метрики?
3. Заменить кадры на снимки с `JointLimitDeg=25` @ 1080×2160?

---

## 12. Открытые пункты / стоп (история)

1. **До A8:** gap_sanity_pass=0 на 0.50@step5.
2. Привод **MoveRotation**; 002-A2 **не закрыт** до вердикта по §11.

---

## 13. Где смотреть сырьё (оператор)

| Артефакт | Путь |
|----------|------|
| C0 | `Logs/SpikeBench/c0_results.txt`, `c0_screenshots.txt` |
| Gap sanity | `Logs/SpikeBench/gap_sanity.txt` |
| Overlap | `Logs/SpikeBench/gap_sanity_overlap_diag.txt` |
| Math | `Logs/SpikeBench/math_sanity.txt` |
| Smoke / trace | `throws_smoke_*.csv`, `trace_smoke_*.csv` |
| E0 | `throws_E0_*.csv`, `summary_E0_*.txt`, `summary_E0_verify1x_*.txt` |
| H1 / H3 | `trace_E0_*.csv`, `h1_conclusions.txt`, `e0_correlation.txt`, `summary_E0_verify1x_worst_*.txt` |
| E1 / A1 | `throws_E1_*.csv`, `summary_E1_*.txt`, `a1_probe.txt` |
| A9 J/K/M | `j1_repro.txt`, `j2_timescale_rule.txt`, `trace_H1b_*`, `e2_summary_table.txt`, `summary_E2b_…235027.txt`, `summary_E2s_J*.txt` |
| A10 FINAL | `throws_FINAL_J25_*.csv`, `summary_FINAL_J25_*`, `tunnel_FINAL_J25.txt`, `a21_verdict.txt`, `d24_*`, `rest_probe_table_J25.txt` |
| B6/B7 | `rest_probe_*.csv` |
| D1 dump | `c0_joint_dump.txt` (большой) |

При следующей вехе этот раздел REPORT обновлять и коммитить вместе с `DOC/reports/REPORT-002-A2.md`.