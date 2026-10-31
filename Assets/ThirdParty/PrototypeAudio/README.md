# Prototype audio — источники и обработка

Все использованные внешние записи опубликованы под **CC0 1.0**. Для коммерческой игры указание автора не обязательно; ниже сохранены авторы, страницы и происхождение файлов. Звуки не извлекались из чужих игр.

| Исходник | Автор / источник | Использование |
| --- | --- | --- |
| Weapon_Revolver_Shots_Stereo.wav | [Nox_Sound / Freesound 541818](https://freesound.org/people/Nox_Sound/sounds/541818/) | Пять отдельных выстрелов, укороченные хвосты, фильтрация и низкий слой |
| Foley_Whoosh_Clothes.wav | [Nox_Sound / Freesound 495390](https://freesound.org/people/Nox_Sound/sounds/495390/) | Рывок ткани, короткие свиши перестановки |
| Hurt 3 — Male | [Christopherderp / Freesound 342231](https://freesound.org/people/Christopherderp/sounds/342231/) | Реакции и смерть героя |
| Zombie Pain 2.wav | [tonsil5 / Freesound 555423](https://freesound.org/people/tonsil5/sounds/555423/) | Короткие стоны и более длинные затухающие реакции смерти врагов |
| Grenade Explosion SFX | [unfa / Freesound 609587](https://freesound.org/people/unfa/sounds/609587/) | Взрыв с укороченным хвостом, низким телом и приглушённым верхом |
| Impact Sounds | [Kenney](https://kenney.nl/assets/impact-sounds) | Мясистые удары, дерево, металлические рикошеты |
| RPG Audio | [Kenney](https://kenney.nl/assets/rpg-audio) | Кожа, пояс, низкое бряканье, перезарядка |
| Interface Sounds | [Kenney](https://kenney.nl/assets/interface-sounds) | Тихие подтверждения выбора |

Для Freesound скачаны общедоступные **HQ MP3 previews** под той же CC0-лицензией, а не исходные WAV/FLAC, для которых сайт требует входа. Эти фактически использованные файлы сохранены в `Sources/freesound/`; качество финального PCM не восстанавливает потерянную при MP3-сжатии информацию. Страницы каждого исходника подтверждают CC0. [Полный текст CC0](https://creativecommons.org/publicdomain/zero/1.0/legalcode).

Выбранные оригинальные OGG Kenney находятся в `Sources/`; исходные тексты лицензий — в `Licenses/`. `source_hashes.json` хранит SHA-256 каждого входного файла. Неиспользованные полные архивы и инструменты обработки в Git не включены.

Обработанные WAV находятся в `Assets/_game/Resources/Audio/Clips/`. Джинглы волны, победы и поражения дополнительно используют короткие оригинальные тональные последовательности, синтезированные обычными синусами и огибающими в `Tools/Audio/build_audio.py`. Нейросинтез/обучение моделей не использовались. Составной звук патронташа сводит ткань, кожу, пониженное бряканье и низкие удары по дереву в таймлайн раскрытия.

Повторная сборка: установить numpy/scipy/soundfile в своё Python-окружение и выполнить `python Tools/Audio/build_audio.py` из корня проекта. Использованные исходники уже включены, повторное скачивание не требуется. Скрипт пишет длительность, RMS и пики в `Resources/Audio/render_report.json`. При импорте Unity нормализация отключена, чтобы сохранить запас по уровню.
