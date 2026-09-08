<div align="center">
  <img src="Icons/main_128.png" width="96" alt="SolidWorks Slicer Bridge">

# SolidWorks Slicer Bridge

**Экспорт модели из SOLIDWORKS 2026 прямо в слайсер — одним кликом.**

**OrcaSlicer · Bambu Studio · PrusaSlicer**

[Скачать последний релиз](https://github.com/garik816/SolidWorksSlicerBridge/releases/latest) · [English README](README.md)
</div>

---

## Что добавляется в SOLIDWORKS

Add-In создаёт вкладку CommandManager **3D Print**:

| Кнопка | Действие |
|---|---|
| **OrcaSlicer** | экспорт всей активной модели в 3MF → открыть OrcaSlicer |
| **Bambu Studio** | экспорт 3MF → открыть Bambu Studio |
| **PrusaSlicer** | экспорт 3MF → открыть PrusaSlicer |
| **Slicer Settings** | настройка путей к слайсерам |

```text
деталь / сборка SOLIDWORKS
            ↓
        один клик
            ↓
      тихий экспорт 3MF
            ↓
 Orca / Bambu / Prusa
```

## Возможности

- Детали и сборки.
- Экспортируется **вся активная модель**: перед экспортом выделение очищается.
- Штатный экспорт SOLIDWORKS в **3MF**.
- Качество сетки и единицы остаются такими, как настроено в SOLIDWORKS.
- Автопоиск OrcaSlicer, Bambu Studio и PrusaSlicer.
- Если `.exe` не найден — один раз предлагает выбрать его и запоминает путь.
- Пути хранятся в `HKCU\Software\SolidWorksSlicerBridge`.
- Окна 3MF Info/Preview временно отключаются только на время экспорта и затем восстанавливаются.
- Временные 3MF находятся в `%TEMP%\SolidWorksSlicerBridge`; файлы старше 7 дней удаляются автоматически.

## Установка — рекомендуемый вариант

1. Открой [Releases](https://github.com/garik816/SolidWorksSlicerBridge/releases/latest).
2. Скачай **`SolidWorksSlicerBridge-<версия>-Setup-x64.exe`**.
3. Полностью закрой SOLIDWORKS.
4. Запусти Setup и разреши UAC.
5. Запусти SOLIDWORKS 2026.
6. Открой деталь или сборку — появится вкладка **3D Print**.

Setup устанавливает Add-In в `Program Files`, **собирает DLL непосредственно на твоём ПК** против API установленного SOLIDWORKS 2026, регистрирует 64-битный COM Add-In и включает автозагрузку.

Так релиз не содержит и не распространяет сторонние SOLIDWORKS interop DLL.

Если Add-In не включился автоматически:

`Tools → Add-Ins → SolidWorks Slicer Bridge`

Удаляется штатно через **Параметры Windows → Установленные приложения**.

## Portable / ручная установка

Для разработки или установки из исходников:

1. Скачай/клонируй репозиторий в постоянную папку.
2. Закрой SOLIDWORKS.
3. Запусти **`INSTALL.cmd`**.
4. Для удаления используй **`UNINSTALL.cmd`**.

Visual Studio не нужна: `Build.ps1` использует штатный 64-битный компилятор .NET Framework и API DLL установленного SOLIDWORKS.

## Настройка слайсеров

Поддерживаются:

- `orca-slicer.exe`
- `bambu-studio.exe`
- `prusa-slicer.exe`

Если автоматический поиск не сработал, нажми соответствующую кнопку и выбери `.exe`, либо открой **3D Print → Slicer Settings**.

## Качество 3MF

Настройки сетки и единиц задаются штатно:

`Tools → Options → System Options → Export → 3MF`

Add-In автоматизирует только экспорт и запуск слайсера.

## Релизный Setup

Исходник установщика:

```text
installer\SolidWorksSlicerBridge.iss
```

GitHub Actions:

```text
.github\workflows\release.yml
```

Тег вида `v1.0.0` автоматически собирает:

```text
SolidWorksSlicerBridge-1.0.0-Setup-x64.exe
```

и прикладывает его к GitHub Release. Workflow также можно запускать вручную.

## Совместимость

- SOLIDWORKS **2026**
- Windows **x64**
- .NET Framework **4.x**
- OrcaSlicer
- Bambu Studio
- PrusaSlicer

## Лицензия

MIT — см. [LICENSE](LICENSE).
