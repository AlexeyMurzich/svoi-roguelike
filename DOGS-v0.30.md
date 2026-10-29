# Цельные собаки v0.30

Утверждённый concept: exec-72f71490-5ef0-4533-97d1-12c404bbe18b.png. Production source: assets/enemies/dogs-complete.png, встроенный imagegen с прозрачным фоном; входной макет используется для сохранения дизайна. Генерация не меняла моделей других существ.

Промпт: preserve the two approved dogs, complete full-body idle/bite/howl poses, fur markings and painterly anatomy; 3 columns × 2 rows, dark dog above/sandy dog below, each facing left, connected neck/head/torso, consistent body scale and grounded paws. Remove all background/text/grid/contact shadows, true RGBA transparency. No separate body parts, collars, flames or neon outlines.

Отрисовка: DogAtlas регистрирует шесть силуэтов один раз по alpha>64, сохраняя реальные края лап и хвоста. Колонки и строки используют безопасные gutters исходного атласа, а не предположение о квадратных ячейках. Задняя опорная лапа фиксирует горизонтальный якорь; bottom alpha фиксирует пол. Общий reference size каждого пса задаёт единый uniform scale во всех позах. В runtime нет source bitmap clones или отдельного движения головы.

DogPoseFrame выбирает непрозрачную стойку/укус/вой, целый корпус дышит по LivingMotion.Body; existing BiteOffset даёт плавный выпад. Непрозрачная смена фаз сохраняет цельный силуэт, без двойных полупрозрачных лиц. Волны воя привязаны к морде. Механики и AttackTimeline не менялись.

Старые два ресурса больше не в EXE, один новый общий атлас. Дополнительных таймеров/потоков нет, actorArtCache и pixel budget прежние. Точка попадания снаряда внутри фактического непрозрачного туловища; названия/намерения учитывают высоту новых собак. Подробности тестов в VALIDATION.md.
