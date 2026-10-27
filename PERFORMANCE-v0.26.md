# Производительность v0.26

Измерено 27 октября 2026 на этом компьютере. Сравнение с v0.25, отдельные 64-битные процессы, без запущенной игры и параллельных тестов. Настоящий OnPaint рисуется в bitmap: это время отрисовки, не измерение живого FPS и не гарантия на других ПК. По 12 прогревочных кадров; Full HD — 60 измеряемых кадров, 4K — 30. Полные исходные результаты ниже.

Причина нагрузки: программная отрисовка GDI+ повторно фильтровала уже подготовленные под экран картинки. Раздельный замер Full HD: машина Тохи около 16.28 мс, герой 5.84 мс. При исключении повторной фильтрации — 1.47 и 0.64 мс соответственно. Один только отказ от микроповорота машины не помог (16.01 мс), поэтому повороты и остальные движения сохранены.

| Сцена, 1920×1080 | v0.25 медиана / p95, мс | v0.26 медиана / p95, мс |
|---|---:|---:|
| Обычный бой | 32.01 / 34.75 | 9.59 / 11.71 |
| Карта | 20.21 / 22.62 | 13.78 / 16.12 |
| Мимикратор, фея | 32.65 / 39.04 | 11.21 / 13.87 |
| Мимикратор, мигалка | 30.15 / 33.65 | 10.94 / 11.70 |
| Мимикратор, мишка | 30.88 / 32.83 | 11.15 / 11.93 |
| Мимикратор, мидия | 31.45 / 34.14 | 11.28 / 12.42 |
| Тоха, светлый | 37.33 / 42.51 | 10.29 / 11.51 |
| Тоха, тёмный | 36.77 / 40.69 | 10.24 / 11.53 |

На 4K Тоха: 142.96 → 30.31 мс (светлый), 149.00 → 30.31 мс (тёмный). Большой экран сохраняет размер окна и родные координаты ввода, но внутренняя растеризация ограничена 1920×1080 пикселями по площади с сохранением пропорций; готовый кадр масштабируется на экран. На 4K картинка поэтому менее детальная, чем полная растеризация в 4K; Full HD и меньшие окна рисуются в родном разрешении. Бюджет не ограничивает логическое пространство карты. Дополнительная поверхность не более 7.91 МиБ, переиспользуется, освобождается при возврате к небольшому окну и при закрытии. На 4K в конце сценария private memory 324.64 → 273.97 МиБ (одно наблюдение, не пик). На Full HD 243.07 → 243.57 МиБ: уменьшение общей памяти там не заявляется.

Изображения предварительно уменьшаются с HighQualityBicubic; при выводе копии используется NearestNeighbor вместо второго bilinear-прохода. Это компромисс в пользу скорости: тонкие наклонные края могут выглядеть чуть резче. Точные размеры, анимационные преобразования, прозрачность и исходный арт сохранены. Непрозрачные фоны копируются через SourceCopy; графическое состояние восстанавливается до вывода спрайтов. Для подготовленного тела Мимикратора действует та же политика. Количество исходных декодированных изображений в сравнительном сценарии одинаково (45 / 152.77 МиБ пикселей), исходные PNG и музыкальные файлы не менялись. Нативный StretchDIBits был проверен отдельно и отброшен: в этом сценарии он медленнее.

Повторение: собрать Tools/RenderBenchmark.cs как описано в PERFORMANCE-v0.24.md; запустить outputs/RenderBenchmark.exe <EXE> 1920 1080 60 и отдельно 3840 2160 30. Инструмент дополнен двумя формами Тохи. Замеры не включают воспроизведение музыки (окно скрыто).


## v0.25 Full HD

```text
viewport=1920x1080; frames=60; process64=True
startup-ms=2662.23
combat-idle: median-ms=32.01; p95-ms=34.75; gen0=0
map: median-ms=20.21; p95-ms=22.62; gen0=1
mimicrator-Fairy: median-ms=32.65; p95-ms=39.04; gen0=0
mimicrator-Beacon: median-ms=30.15; p95-ms=33.65; gen0=0
mimicrator-Bear: median-ms=30.88; p95-ms=32.83; gen0=0
mimicrator-Mussel: median-ms=31.45; p95-ms=34.14; gen0=1
toha-light: median-ms=37.33; p95-ms=42.51; gen0=0
toha-dark: median-ms=36.77; p95-ms=40.69; gen0=0
actorArtCache; Count=3; Bytes=2944384; Hits=1140; Misses=12; Evictions=0
interfaceArtCache; Count=18; Bytes=955680; Hits=6030; Misses=18; Evictions=0
private-MiB=243.07
decoded-images=45; pixel-MiB=152.77
```

## v0.26 Full HD

```text
viewport=1920x1080; frames=60; process64=True
startup-ms=3178.79
combat-idle: median-ms=9.59; p95-ms=11.71; gen0=0
map: median-ms=13.78; p95-ms=16.12; gen0=1
mimicrator-Fairy: median-ms=11.21; p95-ms=13.87; gen0=0
mimicrator-Beacon: median-ms=10.94; p95-ms=11.70; gen0=0
mimicrator-Bear: median-ms=11.15; p95-ms=11.93; gen0=0
mimicrator-Mussel: median-ms=11.28; p95-ms=12.42; gen0=1
toha-light: median-ms=10.29; p95-ms=11.51; gen0=0
toha-dark: median-ms=10.24; p95-ms=11.53; gen0=0
actorArtCache; Count=3; Bytes=2944384; Hits=1140; Misses=12; Evictions=0
interfaceArtCache; Count=18; Bytes=955680; Hits=6030; Misses=18; Evictions=0
private-MiB=243.57
decoded-images=45; pixel-MiB=152.77
```

## v0.25 4K

```text
viewport=3840x2160; frames=30; process64=True
startup-ms=2735.99
combat-idle: median-ms=124.08; p95-ms=131.68; gen0=0
map: median-ms=65.73; p95-ms=73.45; gen0=0
mimicrator-Fairy: median-ms=122.42; p95-ms=138.62; gen0=0
mimicrator-Beacon: median-ms=111.04; p95-ms=117.80; gen0=0
mimicrator-Bear: median-ms=114.66; p95-ms=117.51; gen0=0
mimicrator-Mussel: median-ms=119.89; p95-ms=130.65; gen0=1
toha-light: median-ms=142.96; p95-ms=150.03; gen0=0
toha-dark: median-ms=149.00; p95-ms=173.99; gen0=0
actorArtCache; Count=3; Bytes=11692416; Hits=660; Misses=12; Evictions=0
interfaceArtCache; Count=18; Bytes=3799104; Hits=3510; Misses=18; Evictions=0
private-MiB=324.64
decoded-images=45; pixel-MiB=152.77
```

## v0.26 4K

```text
viewport=3840x2160; frames=30; process64=True
startup-ms=3155.73
combat-idle: median-ms=29.49; p95-ms=33.28; gen0=0
map: median-ms=34.12; p95-ms=37.42; gen0=0
mimicrator-Fairy: median-ms=31.17; p95-ms=33.84; gen0=0
mimicrator-Beacon: median-ms=31.06; p95-ms=32.86; gen0=0
mimicrator-Bear: median-ms=31.10; p95-ms=32.64; gen0=0
mimicrator-Mussel: median-ms=31.09; p95-ms=33.86; gen0=0
toha-light: median-ms=30.31; p95-ms=32.72; gen0=0
toha-dark: median-ms=30.31; p95-ms=32.59; gen0=0
actorArtCache; Count=3; Bytes=2944384; Hits=660; Misses=12; Evictions=0
interfaceArtCache; Count=18; Bytes=955680; Hits=3510; Misses=18; Evictions=0
private-MiB=273.97
decoded-images=45; pixel-MiB=152.77
```
