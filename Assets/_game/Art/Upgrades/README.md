# Графика экрана улучшений

`ModuleClip.png` — временный общий визуал модулей гнезда. Создан встроенным `image_gen` по запросу пользователя, прозрачность сохранена, PNG не редактировался. Скоба накладывается поверх кожаной петли: ширина 1,17 и высота 0,238 в исходных единицах патронташа (около 20% высоты петли). Прозрачные поля исключаются UV меша, а не изменением изображения.

Материал, меш скобы и библиотека презентации создаются `Tools > Prototype > Upgrades > Build Presentation Assets` в `Resources/Upgrades/`. Инструмент экспортирует отдельный prefab патронташа из тестовой сцены, не меняя саму сцену. Повторный запуск обновляет генерируемые assets.

Полный промпт, built-in mode:

```text
Use case: stylized-concept. Asset type: production transparent PNG game sprite, single equipment module placeholder for a pixel-art western roguelike. Primary request: one small horizontal forged iron clamp / strap clip that will be layered ON TOP of an existing leather cartridge loop. Subject: ONLY the metal clip, not the leather loop, not a cartridge. Shape: wide shallow slightly curved rectangular metal strap, about 4:1 width-to-height; two tiny rivets at its left and right ends, a subtle raised center lip; squat silhouette, complete full width visible. Style: crisp chunky pixel-art clusters matching warm chestnut leather and golden brass cartridge sprites, dark clean pixel outline, restrained scratches. Material: aged dark gunmetal steel, silver edges, a little warm brass on the two rivets, warm highlight from upper left, darker lower bevel. Strict straight-on orthographic view, no perspective tilt, no background, no external ground shadow. Center a single clip on a genuinely transparent canvas, no words, letters, numbers, symbols, leather, bullets, tools, fingers, frames, separate parts or duplicates. Keep the sprite simple and legible when displayed only 15-25 pixels tall.
```
