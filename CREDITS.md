# Créditos

Recursos de terceros que usa Cardstorm.

## Arte

| Recurso | Autor | Licencia | Qué se usa | Dónde |
|---|---|---|---|---|
| [Tiny Dungeon](https://kenney.nl/assets/tiny-dungeon) 1.0 | Kenney | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | Jugador, enemigos, jefes, suelo y muros | `Assets/Art/Kenney/TinyDungeon`, `Assets/Art/Map` |
| [UI Pack – Pixel Adventure](https://kenney.nl/assets/ui-pack-pixel-adventure) 2.0 | Kenney | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | Paneles, botones, barras y deslizadores de piedra. `Ui_FrameWhite` es `Ui_Frame` pasado a escala de grises y aclarado, para teñirlo con el color de cada tipo de carta | `Assets/Art/Kenney/UIPixelAdventure` |
| [Kenney Fonts](https://kenney.nl/assets/kenney-fonts) | Kenney | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | Fuente de todos los textos (Kenney Mini Square) | `Assets/Art/Kenney/Fonts` |

Los iconos de cartas, tipos, gemas y recursos (`Assets/Art/Icons`) y los efectos visuales (`Assets/Art/VFX`) se han hecho con IA (ver abajo) y siguen la paleta de Tiny Dungeon.

## Código

| Recurso | Autor | Licencia | Dónde |
|---|---|---|---|
| [DOTween](https://dotween.demigiant.com) | Demigiant | [Licencia de DOTween](https://dotween.demigiant.com/license.php) | `Assets/Plugins/Demigiant` |

## Sonido y música

La música y los efectos (`Assets/Audio`) son originales y no usan muestras de terceros. Se han hecho con IA (ver abajo).

## Contenido hecho con IA

| Qué | Dónde | Herramienta | Cómo |
|---|---|---|---|
| Música: tema de batalla, temas de jefe, menú, *stingers* y *jingles* | `Assets/Audio/Music` | Claude Code (Claude Opus 5.5, Anthropic) con el skill `music-production` ([owoevan-dev/music-skill](https://github.com/owoevan-dev/music-skill), MIT) | El agente escribió cada nota en un proyecto editable y los scripts del skill lo sintetizaron, mezclaron y masterizaron. No se usó ningún modelo de generación de audio |
| Efectos de sonido | `Assets/Audio/Sfx` | Claude Code (Claude Opus 5.5) con el skill `sfx` ([melodic-software/claude-code-plugins](https://github.com/melodic-software/claude-code-plugins), MIT) | Síntesis al estilo *sfxr*: el agente fijó los parámetros de cada efecto |
| Iconos de cartas, tipos, gemas y recursos | `Assets/Art/Icons` | Claude Code (Claude Opus 5.5) | Pixel art hecho por el agente con la paleta y el contorno de Tiny Dungeon |
| Efectos visuales: tiras de partículas de las cartas, muertes de enemigos y jefes, recogibles y subida de nivel | `Assets/Art/VFX`, `Assets/Prefabs/VFX` | Claude Code (Claude Opus 5.5) con el skill `codixel` ([mankidev/codixel](https://github.com/mankidev/codixel), MIT) | Pixel art generado por código, frame a frame, con la paleta y el contorno de Tiny Dungeon. Las muertes de los jefes incluyen sus sprites de Tiny Dungeon (CC0). No se usó ningún modelo de generación de imágenes |

En todos los casos, la selección, la revisión y la aprobación final fueron del autor.
