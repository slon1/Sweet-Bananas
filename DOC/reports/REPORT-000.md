# REPORT-000: сверка окружения

commit: см. сообщение `TASK-000: environment check` в git log

Временные скрипты `_tmp_task000/CheckHinge.cs`, `SetDefines.cs`, `CreateFolders.cs`, `SetMatch.cs` лежали вне `Assets/Scripts`, использовались для проверки типа, define, папок и `SerializedObject`, затем удалены вместе с папкой. В коммите их нет.

## ProjectVersion.txt

```
m_EditorVersion: 6000.5.9f1
m_EditorVersionWithRevision: 6000.5.9f1 (b57deb96f08d)
```

Проект открыт этим же редактором. Чужой процесс не завершался: первый открытый экземпляр закрыл оператор, затем проект открыт заново.

## 1. Пакеты

До открытия и после `package resolve` файлы не изменились.

| Файл | До | После |
|---|---|---|
| `Packages/manifest.json` | `com.unity.render-pipelines.universal` **17.6.0** | **17.6.0**, редактор файл не переписал |
| `Packages/packages-lock.json` | URP **17.5.0**, `source: builtin` | то же, **17.5.0** builtin |

`package resolve` завершился `status: completed`, `applied: true`. Установленный пакет: URP **17.5.0**, source BuiltIn (`Library/PackageCache/com.unity.render-pipelines.universal@d7774c218bdf`). Core и Shader Graph тоже **17.5.0** BuiltIn. Ошибки Package Manager нет. Консоль после разрешения: 0 errors, 0 warnings, `compilationFailed: false`.

Манифест руками не менялся. В коммит manifest и lock не входят: git их не видит изменёнными. Строки версий в двух файлах по-прежнему разные; редактор принимает встроенный 17.5.0 и не пишет ошибку. Это не стоп по поправке 1 (стоп — только если 17.6.0 не разрешается).

### git status и git diff --stat сразу после открытия, до остальных правок

```
 D DOC/figma_design.pdf
 M DOC/gdd_sweet_bananas_jelly_flip.md
 M ProjectSettings/ProjectSettings.asset
?? Assets/Sprites/pop-up/play again.png
?? Assets/Sprites/pop-up/play again.png.meta
?? DOC/Adr/
?? DOC/CURSOR_PROTOCOL.md
?? DOC/PLAN.md
?? DOC/Task/
?? DOC/figma_all.pdf
?? DOC/figma_all.png

 DOC/figma_design.pdf                  |   3 -
 DOC/gdd_sweet_bananas_jelly_flip.md   | 140 +++++++++++++---------------------
 ProjectSettings/ProjectSettings.asset |   2 +-
 3 files changed, 56 insertions(+), 89 deletions(-)
```

На этот момент в `ProjectSettings.asset` уже была незакоммиченная строка `Android: DOTWEEN;UNITASK_DOTWEEN_SUPPORT`. Её происхождение — рабочая копия до этого запуска, не перепись редактора при открытии. iPhone и Standalone тогда ещё были только `DOTWEEN`.

Других правок `ProjectSettings/*`, сцен и `.meta` открытие редактора не сделало. Откатывать нечего: перечисленные DOC-файлы и спрайт кнопки были в копии до задачи и к ней не относятся как правки редактора. Они оставлены как есть и в коммит не входят.

## 2. Плагины и define

- Extenject **9.2.0** — исходники `Assets/Plugins/Zenject` (`Version.txt`). Не UPM и не git-пакет.
- UniTask **2.5.11** — `Assets/Plugins/UniTask` (`package.json`), свои asmdef. Не UPM.
- DOTween — DLL `Assets/Plugins/Demigiant/DOTween`, без asmdef. Не UPM. Define `DOTWEEN` не менялся.
- `UNITASK_DOTWEEN_SUPPORT` сам не включается: в `UniTask.DOTween.asmdef` он завязан на пакет `com.demigiant.dotween`, которого нет.

Добавлен через `PlayerSettings.SetScriptingDefineSymbols` для Android, iPhone и Standalone. `DOTWEEN` сохранён. Ключи в `ProjectSettings/ProjectSettings.asset`:

- `Android: DOTWEEN;UNITASK_DOTWEEN_SUPPORT`
- `Standalone: DOTWEEN;UNITASK_DOTWEEN_SUPPORT`
- `iPhone: DOTWEEN;UNITASK_DOTWEEN_SUPPORT`

Перекомпиляция: `script recompile` вернул `up_to_date`, консоль чистая (0 errors, 0 warnings). Отката define нет. Asmdef не менялись.

Diff `ProjectSettings.asset` — только эти три строки define. Версионных полей редактор не дописывал.

## 3. Физика

Настройки не менялись.

1. API по умолчанию — `Physics2D` (`ProjectSettings/Physics2DSettings.asset`), `m_SimulationMode: 0` (FixedUpdate).
2. Низкоуровневый мир не включён: модуль `physicscore2d` есть, `PhysicsCoreProjectSettings2D.asset` содержит `m_PhysicsCoreSettings: {fileID: 0}`.
3. Гравитация `(0, -9.81)`.
4. Velocity/position iterations **8/3**, `m_VelocityThreshold` **1**, `m_MaxAngularCorrection` **8**.
5. `m_DefaultContactOffset` **0.01**, сон **0.5 с** (linear **0.01**, angular **2**), Baumgarte **0.2**.
6. До открытия `Scene`: в несохранённой пустой сцене `AddComponent<HingeJoint2D>()` прошёл (`type=UnityEngine.HingeJoint2D; added=True; leftover=False; path=''`). Объект удалён сразу, сцена не сохранялась.

## 4. Папки

Созданы через `AssetDatabase.CreateFolder`, в каждой пустой папке есть `.gitkeep`:

- `Assets/Scripts/Core`
- `Assets/Scripts/Game`
- `Assets/Scripts/UI`
- `Assets/Scripts/Data`
- `Assets/Scripts/Services`
- `Assets/Prefabs`
- `Assets/Data`

`Assets/Scripts` уже был (там `LoadActivityScript.cs`, его не менял). `.meta` создал редактор для новых папок. `.meta` для `.gitkeep` нет.

## 5. CanvasScaler

`Scene.unity`, компонент CanvasScaler. До: `m_UiScaleMode` 1 (Scale With Screen Size), reference **1080×2160**, `m_ScreenMatchMode` 0, `m_MatchWidthOrHeight` **0.5**. После: match **0**. Остальные поля не менялись. `Loader.unity` не открывался на запись, diff пустой, Match там по-прежнему **0.5**.

`git diff Assets/Scenes/Scene.unity` — одна строка:

```
-  m_MatchWidthOrHeight: 0.5
+  m_MatchWidthOrHeight: 0
```

## 6. Спрайты

`.meta` спрайтов не менялись.

### game01…26

У всех PPU **100**, `spriteMode` **2** (Multiple), `spriteGenerateFallbackPhysicsShape` **1**. Кастомные `physicsShape` и `outline` в метах пустые. Ниже крупнейший слайс каждого файла — это спрайт цели. Мелкие дополнительные слайсы есть почти у всех файлов: это осколки автослайса, не отдельные цели. У `game08` крупный слайс называется `game08_1`, у `game09` — `game09_19`, не `_0`.

| Файл | Крупный слайс | Размер |
|---|---|---|
| game01 | game01_0 | 741×648 |
| game02 | game02_0 | 741×648 |
| game03 | game03_0 | 741×648 |
| game04 | game04_0 | 741×611 |
| game05 | game05_0 | 719×645 |
| game06 | game06_0 | 634×648 |
| game07 | game07_0 | 741×576 |
| game08 | game08_1 | 698×648 |
| game09 | game09_19 | 741×528 |
| game10 | game10_0 | 729×648 |
| game11 | game11_0 | 723×540 |
| game12 | game12_0 | 718×636 |
| game13 | game13_0 | 624×648 |
| game14 | game14_0 | 678×648 |
| game15 | game15_0 | 740×646 |
| game16 | game16_0 | 740×643 |
| game17 | game17_0 | 732×648 |
| game18 | game18_0 | 722×639 |
| game19 | game19_0 | 567×648 |
| game20 | game20_0 | 741×636 |
| game21 | game21_0 | 729×648 |
| game22 | game22_0 | 706×648 |
| game23 | game23_0 | 702×625 |
| game24 | game24_0 | 741×648 |
| game25 | game25_0 | 735×614 |
| game26 | game26_0 | 741×648 |

В `screens.md` размер зоны цели для `game01` исправлен с 741×647 на **741×648**.

### pointer1…4

Текстура каждого файла **730×730**, PPU **100**, `spriteMode` **1** (Single). В секции sprite sheet при этом лежат мелкие rect: pointer1 — 33, pointer2 — 21, pointer3 — 30, pointer4 — 32. PNG смотрел как картинку, `.meta` не менял. На каждом файле нарисован один целый леденец: круглая головка со спиралью и бликом и палочка. Мелкие rect — крошки по краю альфы и куски блика/палочки, не кадры анимации. Какой слайс использовать в игре, в этом таске не выбирал.

Крупнейшие rect (координаты Unity, Y снизу):

- pointer1: `pointer1_3` (0, 10) 565×705 — головка; `pointer1_21` (575, 14) 147×362 — палочка; `pointer1_9` (0, 577) 216×126; `pointer1_12` (503, 504) 120×194; `pointer1_19` (630, 416) 80×137 — блики и осколки.
- pointer2: `pointer2_0` (13, 14) 697×705 — целый леденец; дальше rect меньше 110×68.
- pointer3: `pointer3_0` (143, 19) 552×701 — головка; `pointer3_12` (51, 268) 116×201; `pointer3_5` (0, 524) 112×157 — осколки; палочка в более мелких rect.
- pointer4: `pointer4_1` (169, 18) 501×703 — головка; `pointer4_7` (612, 154) 98×405 — палочка; `pointer4_28` (12, 14) 117×173; `pointer4_20` (0, 300) 132×122; `pointer4_0` (0, 598) 131×112 — осколки.

### play again.png

Файл в рабочей копии есть: `Assets/Sprites/pop-up/play again.png` (имя с пробелом), **313892** байт, текстура **774×258**. Импорт: PPU **100**, `spriteMode` **2** (Multiple), один слайс `image 12_0` **743×258**, fallback physics shape включён, кастомный physics shape пуст. `.meta` уже был в копии до открытия редактора, его не менял и в коммит не клал.

В git файл не закоммичен. `git lfs ls-files` его не показывает (`Not a valid object name`). В `.gitattributes` для пути стоит `filter: lfs`, но пока файл не в индексе, LFS его не отслеживает. Сам файл не создавал.

## 7. screens.md

Правки только по подтверждённым расхождениям и решениям оператора:

- §1: `Match = 0` (по ширине).
- §4 и §7.1: `gameNN` — спрайт цели, у `game01` слайс `game01_0` **741×648**, кастомный physics shape пуст.
- §5.1 и §8.4: попап проигрыша — тот же макет, что победа, заголовок «SENSATIONAL!», панель `pop-up/pop-up.png`, кнопка `pop-up/play again.png`, после потери ленты и до перезапуска.
- §7.3: `pointerN` — целый леденец, не лента кадров.
- §8.3–8.4: папка `menu/` пустая; после 26-го уровня, пока меню нет, возврат в `Loader`.

## Консоль

После define, папок и сохранения `Scene`: 0 errors, 0 warnings, `compilationFailed: false`.

## Файлы вне белого списка

Не коммитятся. Это состояние копии до задачи, не результат открытия редактора:

- `DOC/gdd_sweet_bananas_jelly_flip.md` (изменён)
- `DOC/figma_design.pdf` (удалён в копии)
- `DOC/Adr/`, `DOC/CURSOR_PROTOCOL.md`, `DOC/PLAN.md`, `DOC/Task/`, `DOC/figma_all.pdf`, `DOC/figma_all.png` (не в git)
- `Assets/Sprites/pop-up/play again.png` и его `.meta` (не в git)

Вопрос архитектору: коммитить ли эти документы и кнопку отдельно. Откат не делал — без них теряется работа оператора, и для открытия проекта они не нужны.

## Проверка перед пушем

- В коммит входят только `Scene.unity`, `ProjectSettings.asset` (три строки define), новые папки и их `.meta`, `.gitkeep`, `DOC/screens.md`, этот рапорт.
- `git lfs ls-files` не содержит `play again.png`: файл не в git.
- Консоль чистая. Diff `Scene.unity` — одна строка. `Loader.unity` без изменений.
- После коммита `git status` всё ещё покажет файлы из списка выше: они были грязными до задачи и в коммит не входили.
