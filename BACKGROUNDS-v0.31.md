> Исторический вариант фонов этой ветки. В v0.32 по решению пользователя игра использует утверждённые combat-social-stage.png, combat-old-center-stage.png и combat-estate-stage.png из main. Прежние изображения ниже сохранены как исходники и в текущий EXE не встроены.

# Фоны по району — v0.31

Обычный бой использует район своего combatNode из MapRun.Map.Nodes: уровни 1–5 — Социал, 6–10 — Старый центр, 11–15 — Поместье Фомченко. Предыдущая комната, прокрутка и личность обычного врага не определяют фон. Это устраняет крысу с фоном Социала в Поместье.

SceneBackground.cs выбирает общий источник района. Два новых изображения загружаются лениво через ImageAssets, по одному оригиналу на район. Кэш готовой сцены учитывает идентичность источника и размер вывода. Бабай тоже обычный враг: BabaiBackdrop.cs использует тот же источник района и добавляет контактные тени кровати. Позиция мебели, позы и правила боя сохранены. Старые текстуры комнаты Бабая сохранены в assets как прежние исходники, но не выбираются для обычного боя.

Тоха сохраняет собственные светлый/тёмный фоны; Мимикратор — зачарованный двор. Фон карты, персонажи, баланс и согласованный микшер v0.30 не менялись.

## Каталог изображений

- assets/combat-social.png — прежний Социал у Евроопта.
- assets/combat-old-centre.png — площадь перед муниципальным зданием, памятник и низкая застройка.
- assets/combat-fomchenko.png — грунтовая дорога, частные дома и недостроенный дом с красным окном, как в верхнем районе карты.

Новые изображения созданы встроенным image_gen через навык imagegen, затем скопированы в assets. Два референса для каждой генерации: assets/combat-social.png — стиль/камера; assets/map-smolevichi.png — соответствующий район. Никаких новых изображений персонажей.

Git blob SHA1:

- combat-old-centre.png: ecaf38ffae3a1f82bf08b9bcfddd59030a9dd585
- combat-fomchenko.png: 55d5bc33598549e2cc8a22f867cd61d08efaa464

## Запросы генерации

Старый центр:

> Use case: stylized-concept. Generate one wide 16:9 game battle BACKGROUND, old town district of Smolevichi Belarus. First reference is style and battle camera reference only; second reference is world map reference: use its MIDDLE district, old center with low Soviet houses and municipal neoclassical building and Lenin monument. Eye level wide view of empty square, municipal facade behind small flowerbeds, statue off to left in distance, warm amber street lamps, dark blue autumn twilight, slightly wet paving. Match detailed painterly semi-realistic game art and blue/amber lighting of references. Bottom 45 percent is unobstructed flat plaza pavement for fighters; distant architecture upper half. No people, no creatures, no fighters, no interface, no title, no text, no watermark. No supermarket. Save generated output and return its local path.

Поместье:

> Use case: stylized-concept. One wide 16:9 battle background for Belarusian meme roguelike. References: first image is painterly detailed semi-realistic style and battle camera; second is world map, use its TOP district as setting, Fomchenko estate. Street-level view of an unfinished grey two-storey private house with a single ominous red-lit upstairs window on slight rise, modest low detached houses to its sides, sandy unpaved road and sparse pine trees, amber street lamps, dark blue autumn twilight. Keep the map's mundane semi-rural housing character, no grand palace. Bottom 45 percent broad clear flat sandy packed-earth yard for fighters, eye-level side-on scene, architecture across upper half. Match reference brushwork, palette and quality. No people, no creatures, no actors, no UI, no lettering, no watermark. No supermarkets or city apartment blocks. Return output local path.

## Проверка

Tests/BackgroundTests.cs проверяет реальные входы в узлы уровней 1/5/6/10/11/15, крысу/собак/ежей/Бабая в каждом районе, независимость от прокрутки, переиспользование источников и растрового кэша на шести размерах. Tests/BabaiBackdropTests.cs проверяет опору кровати, общий источник, освобождение кэша и владение оригиналом. Прежние проверки Тохи и Мимикратора проверяют их отдельные сцены. Итог полного прогона — test-results.txt.

Визуально проверены игровые кадры крысы в Старом центре и Поместье, Бабая в Поместье, а также исходные изображения. Билд: outputs/Svoi-v0.31-Windows.zip.

Полный начальный прогон: 95 прошли, один обнаружил полупрозрачный край. После исправления непрозрачной заливкой повторная проверка: 10 наборов / 0 ошибок / 41 658 утверждений. Второй полный прогон не заявляется; подробности VALIDATION.md и test-results.txt. EXE SHA256: BC925F846257C1192B877E9BDF6CD72ECBC2444CE931AAA20EED72DFFE738665.
