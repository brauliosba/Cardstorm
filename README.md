# Cardstorm

**Cardstorm** es un *action roguelite deckbuilder* de vista cenital para PC (Windows). Combina la presión de las hordas de un *survivor-like* (como *Vampire Survivors*) con la toma de decisiones de un *deckbuilder* (como *Slay the Spire*): las armas y habilidades no se disparan solas, sino que el jugador las lanza **jugando cartas de su mano en tiempo real** mientras esquiva a los enemigos.

Este repositorio contiene el prototipo desarrollado como **Trabajo Final de Máster (TFM)** del Máster Universitario en Diseño y Programación de Videojuegos de la Universitat Oberta de Catalunya (UOC), curso 2026-2027.

- **Autor:** Braulio Baldeón
- **Motor:** Unity 6 (6000.3.11f1), Universal Render Pipeline
- **Plataforma objetivo:** Windows (PC), teclado y ratón o mando
- **Estado:** vertical slice (Sprint 3)

---

## Concepto

- **Mano de cuatro cartas** asignadas a cuatro botones en rombo (I, J, K, L o 1–4 en teclado; Y, X, B, A en mando).
- **Energía** que se regenera con el tiempo; cada carta tiene un coste. Al jugar una carta se roba otra del mazo y, cuando el mazo se agota, se baraja el descarte.
- **Tipos de carta:** ataque (proyectil, área, cuerpo a cuerpo), defensa y movilidad, invocaciones y cartas de poder con efecto pasivo.
- **Progresión en la partida:** los enemigos sueltan experiencia y oro; al subir de nivel se elige entre **Nueva carta** (tienda) y **Mejorar carta** (gemas) y, cada 5 niveles, después se pueden eliminar cartas del mazo.
- **Partida completa de unos 15 minutos**, con una oleada densa hacia el minuto 3, un minijefe en el minuto 5 y un jefe final en el minuto 15.
- **Mazo inicial:** 4 × Disparo, 2 × Tajo, 2 × Esquivar.

### Alcance previsto (MVP)

1 personaje, 1 mapa, 20–25 cartas, 4–5 tipos de enemigo, 1 minijefe y 1 jefe final, con una progresión mínima entre partidas. Quedan fuera de alcance el multijugador, varios biomas, la narrativa y las consolas.

### Contenido actual

**Cartas de acción (12)**

| Carta | Tipo | Rareza | Coste | Efecto |
|---|---|---|---|---|
| Disparo | Ataque | Básica | 1 | Proyectil al enemigo más cercano. |
| Tajo | Ataque | Básica | 2 | Golpe en arco de 120° hacia el enemigo más cercano. |
| Esquivar | Movilidad | Básica | 1 | Desplazamiento rápido con invulnerabilidad breve. |
| Ráfaga | Ataque | Común | 2 | Tres proyectiles en abanico. |
| Lanza | Ataque | Común | 2 | Proyectil que atraviesa dos enemigos. |
| Granada | Ataque | Común | 2 | Explosión en el grupo más denso. |
| Escudo | Defensa | Común | 2 | Absorbe 25 de daño durante 4 s. |
| Torreta | Invocación | Común | 3 | Torreta fija que dispara al enemigo más cercano durante 6 s. |
| Onda | Ataque | Poco común | 3 | Daño a los enemigos alrededor del jugador. |
| Cadena eléctrica | Ataque | Poco común | 3 | Rayo que salta entre hasta 4 enemigos. |
| Escarcha | Ataque | Poco común | 3 | Daño en área y ralentiza un 50 % durante 3 s. |
| Meteoro | Ataque | Rara | 5 | Tras 1 s de aviso, gran daño en área. |

**Cartas de poder (9):** Atracción, Reserva, Vitalidad, Zancada, Regeneración, Erudición, Furia, Recarga rápida y Codicia.

**Enemigos de horda (4)**

| Enemigo | Comportamiento |
|---|---|
| Rondador | Persigue al jugador. |
| Corredor | Persigue al jugador, rápido y frágil. |
| Tirador | Mantiene la distancia y dispara. |
| Coloso | Lento y resistente; avisa con una franja y embiste en línea recta. |

Jefes: Heraldo (minijefe, minuto 5) y Ojo de la tormenta (jefe final, minuto 15).

El Heraldo persigue al jugador y cada 8 s alterna dos patrones, ambos con aviso: una embestida en línea recta y un anillo de proyectiles que gira entre un anillo y el siguiente. Si no se le derrota en 5 minutos, huye y se desvanece sin dar nada. Al derrotarlo, la partida se pausa para elegir una carta rara.

**Gemas (6)**

Mejorar carta ofrece tres gemas al azar (se pueden refrescar pagando oro) y, al elegir una, todo el mazo que la admite. La gema se engarza en una copia concreta y sustituye a la que tuviera; las cartas de poder no admiten gemas.

| Gema | Efecto | Cartas |
|---|---|---|
| Filo | +25 % de daño. | Con daño |
| Ligereza | −20 % de coste. | Con coste |
| Amplitud | +30 % de área o alcance. | Con área, arco o alcance |
| Perforación | Atraviesa un enemigo más, que recibe el 50 % del daño. | De proyectil |
| Resonancia | 50 % de probabilidad de lanzarse dos veces. | Con daño |
| Prioridad: más vida | Apunta al enemigo con más vida. | Con objetivo |

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

| Acción | Teclado | Mando |
|---|---|---|
| Moverse | WASD | Joystick izquierdo |
| Carta de arriba | I o 1 | Y |
| Carta de la izquierda | J o 2 | X |
| Carta de la derecha | L o 3 | B |
| Carta de abajo | K o 4 | A |
| Ver descripción de las cartas | Mantener Mayús | Mantener gatillo izquierdo |
| Pausa | Esc | Start |

*Los controles pueden cambiar durante el desarrollo.*

### Teclas de depuración

Solo en el editor y en *development build*.

| Tecla | Acción |
|---|---|
| F1 | Mostrar u ocultar el panel de depuración |
| F2 | Cambiar la velocidad del tiempo |
| F3 | Saltar a la siguiente oleada o jefe |
| F4 | Pausar o reanudar las apariciones |
| Mayús+F4 | Hacer aparecer un coloso a 8 u del jugador |
| F5 | Matar a los enemigos cercanos |
| Mayús+F5 | Quitar el 25 % de la vida al jefe actual |
| F6 | Invulnerabilidad |
| F7 | Energía infinita |
| Mayús+F7 | Añadir al mazo una copia de cada carta de prueba |
| F8 | Subir un nivel |
| Mayús+F8 | Añadir oro |
| F9 | Prueba de carga (300 enemigos) |
| F10 | Reciclar todos los enemigos |
| Mayús+F10 | Dejar 5 s al límite del jefe actual |
| F11 | Grabar tiempos de fotograma |
| F12 | Abrir la subida del nivel actual con Eliminar carta al final |
| Mayús+F12 | Abrir Mejorar carta del nivel actual |
| R | Reiniciar la partida |

## Idiomas

El juego está en **español** e **inglés**. Se cambia en el menú de pausa con el botón **Idioma** y la elección se recuerda entre partidas. La primera vez se usa el idioma del sistema y, si no es ninguno de los dos, español.

Los textos están en una hoja de Google Sheets con dos pestañas, `UI` (interfaz) y `Content` (cartas, gemas y enemigos), que es la fuente de verdad. Para traer los cambios al proyecto:

1. Selecciona `Assets/Localization/Tables/UI.asset` y, en la extensión **Google Sheets**, pulsa **Pull**.
2. Haz lo mismo con `Content.asset`.

Las claves de cartas, gemas y enemigos salen del `id` de su asset (`card.disparo.name`, `gem.filo.description`, `enemy.heraldo.name`). Si falta una, se muestra el texto del asset.

`Assets/Localization/Google Sheets Service.asset` guarda la API key y no está en el repositorio. Para hacer *Pull* en otra máquina, créalo con ese nombre (*Assets › Create › Localization › Google Sheets Service*, autenticación **API Key**) y asígnalo en la extensión de las dos tablas.

## Telemetría

Cada partida se graba en local, sin conexión ni datos personales, en:

```
%USERPROFILE%\AppData\LocalLow\DefaultCompany\Cardstorm\telemetria\
```

Son cuatro CSV por partida (`resumen`, `eventos`, `muestras` cada 15 s y `cartas`), con el nombre `<código>_<fecha>_<hora>_<fichero>.csv`. El código de jugador son 4 caracteres al azar que se guardan entre partidas; se ve en el menú de pausa y en la pantalla final, que también tiene un botón para abrir la carpeta. La grabación está activa también en la build normal; se apaga desmarcando `TelemetryRecorder` en el objeto `Telemetry` de la escena.

Para juntar todas las partidas y comprobar los criterios de equilibrado:

```
cd Tools\Telemetry
python analizar.py <carpeta_con_csv> --modelo ..\..\..\cardstorm_equilibrado.xlsx
```

Genera `telemetria_resumen.xlsx`. Las partidas con teclas de depuración se descartan salvo con `--incluir-trucos`. Más detalles en [`Tools/Telemetry/README.md`](Tools/Telemetry/README.md).

---

## Estructura del repositorio

```
Cardstorm/
├── Cardstorm/            Proyecto de Unity
│   ├── Assets/           Escenas, scripts, prefabs y datos (ScriptableObjects)
│   ├── Packages/         Dependencias del proyecto
│   └── ProjectSettings/  Configuración del proyecto
├── Tools/
│   └── Telemetry/        Script de análisis de las partidas (Python)
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
