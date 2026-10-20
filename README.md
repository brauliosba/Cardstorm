# Cardstorm

**Cardstorm** es un *action roguelite deckbuilder* de vista cenital para PC (Windows). Combina la presión de las hordas de un *survivor-like* (como *Vampire Survivors*) con la toma de decisiones de un *deckbuilder* (como *Slay the Spire*): las armas y habilidades no se disparan solas, sino que el jugador las lanza **jugando cartas de su mano en tiempo real** mientras esquiva a los enemigos.

Este repositorio contiene el prototipo desarrollado como **Trabajo Final de Máster (TFM)** del Máster Universitario en Diseño y Programación de Videojuegos de la Universitat Oberta de Catalunya (UOC), curso 2026-2027.

- **Autor:** Braulio Baldeón
- **Motor:** Unity 6 (6000.3.11f1), Universal Render Pipeline
- **Plataforma objetivo:** Windows (PC), teclado y ratón o mando
- **Estado:** preproducción y greybox (Sprint 2)

---

## Concepto

- **Mano de cuatro cartas** asignadas a cuatro botones (1–4 en teclado, X/Y/A/B en mando).
- **Energía** que se regenera con el tiempo; cada carta tiene un coste. Al jugar una carta se roba otra del mazo y, cuando el mazo se agota, se baraja el descarte.
- **Tipos de carta:** ataque (proyectil, área, cuerpo a cuerpo), defensa y movilidad, invocaciones y cartas de poder con efecto pasivo.
- **Progresión en la partida:** los enemigos sueltan experiencia y oro; al subir de nivel se elige entre añadir una carta nueva, mejorarla con gemas o mejorar un atributo.
- **Partida completa de unos 15 minutos**, con una oleada densa hacia el minuto 3, un minijefe en el minuto 5 y un jefe final en el minuto 15.
- **Mazo inicial:** 4 × Disparo, 2 × Tajo, 2 × Esquivar.

### Alcance previsto (MVP)

1 personaje, 1 mapa, 20–25 cartas, 4–5 tipos de enemigo, 1 minijefe y 1 jefe final, con una progresión mínima entre partidas. Quedan fuera de alcance el multijugador, varios biomas, la narrativa y las consolas.

---

## Requisitos

| Herramienta | Versión |
|---|---|
| Unity Hub | 3.x o superior |
| Unity Editor | **6000.3.11f1** (Unity 6), con el módulo *Windows Build Support (IL2CPP)* |
| Git | 2.40 o superior |
| Git LFS | 3.x (el repositorio usa LFS para recursos binarios) |
| IDE (opcional) | Visual Studio 2022 o JetBrains Rider |

Paquetes principales del proyecto: Universal RP 17.3, Input System 1.19, uGUI, Timeline y AI Navigation.

---

## Cómo abrir el proyecto

1. Instala Git LFS (solo la primera vez):
   ```bash
   git lfs install
   ```
2. Clona el repositorio:
   ```bash
   git clone https://github.com/brauliosba/Cardstorm.git
   ```
3. En **Unity Hub**, pulsa **Add → Add project from disk** y selecciona la carpeta **`Cardstorm/Cardstorm`** (la que contiene `Assets`, `Packages` y `ProjectSettings`).
4. Si Unity Hub lo pide, instala la versión **6000.3.11f1** del editor.
5. La primera apertura tarda unos minutos mientras Unity importa los recursos y genera la carpeta `Library`.

## Cómo ejecutar en el editor

1. Abre la escena principal en `Assets/Scenes/`.
2. Pulsa **Play**.

## Cómo compilar una build para Windows

1. **File → Build Profiles** (o *Build Settings*).
2. Selecciona la plataforma **Windows** y comprueba que la escena principal está en la lista de escenas.
3. Pulsa **Build** y elige una carpeta de salida fuera del repositorio (por ejemplo, `Builds/`).
4. Ejecuta `Cardstorm.exe`.

Las versiones compiladas de cada entrega (PEC) se publican en la sección **[Releases](https://github.com/brauliosba/Cardstorm/releases)** del repositorio.

## Controles

| Acción | Teclado y ratón | Mando |
|---|---|---|
| Moverse | WASD | Joystick izquierdo |
| Jugar carta 1–4 | 1, 2, 3, 4 | X, Y, A, B |
| Pausa | Esc | Start |

*Los controles pueden cambiar durante el desarrollo.*

---

## Estructura del repositorio

```
Cardstorm/
├── Cardstorm/            Proyecto de Unity
│   ├── Assets/           Escenas, scripts, prefabs y datos (ScriptableObjects)
│   ├── Packages/         Dependencias del proyecto
│   └── ProjectSettings/  Configuración del proyecto
├── .gitattributes        Reglas de Git LFS
├── LICENSE
└── README.md
```

---

## Metodología

El proyecto se gestiona con **Scrum adaptado a una sola persona**, con sprints alineados con las entregas (PEC) y un tablero Kanban en Trello priorizado con MoSCoW. La producción sigue un **prototipado iterativo**: greybox del bucle de cartas y movimiento, *vertical slice* y, por último, contenido y pulido, con pruebas con usuarios en cada hito.

| Sprint | Contenido | Entrega |
|---|---|---|
| 1 · Definición | Concepto, alcance y planificación | PEC1 |
| 2 · Preproducción y greybox | GDD, economía, curva de dificultad, prototipo greybox | PEC2 |
| 3 · Vertical slice | Progresión, contenido, minijefe, telemetría, arte y sonido | PEC3 |
| 4 · Cierre | Jefe final, progresión entre partidas, optimización y versión final | Entrega final |

---

## Recursos de terceros

Los recursos gráficos y de sonido de terceros que se incorporen se listarán aquí con su autor, origen y licencia, y también en el apartado de recursos de terceros de la memoria del TFM.

| Recurso | Autor | Origen | Licencia |
|---|---|---|---|
| *(pendiente)* | | | |

---

## Licencia

© 2026 Braulio Baldeón.

Este proyecto se distribuye bajo la licencia **[Creative Commons Reconocimiento-NoComercial-SinObraDerivada 4.0 Internacional (CC BY-NC-ND 4.0)](https://creativecommons.org/licenses/by-nc-nd/4.0/deed.es)**. Puedes descargarlo y compartirlo citando al autor, pero no usarlo con fines comerciales ni distribuir versiones modificadas. Consulta el archivo [`LICENSE`](LICENSE) para ver el texto completo.

Los recursos de terceros mantienen su propia licencia, indicada en el apartado anterior.
