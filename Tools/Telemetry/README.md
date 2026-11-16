# Análisis de telemetría

`analizar.py` junta las partidas que graba el juego y genera `telemetria_resumen.xlsx` con los criterios de equilibrado.

## Requisitos

Python 3.12 con `openpyxl` (`pip install openpyxl`).

## Uso

```
python analizar.py <carpeta_con_csv> --modelo ..\..\..\cardstorm_equilibrado.xlsx --salida telemetria_resumen.xlsx [--incluir-trucos]
```

- `<carpeta_con_csv>`: se buscan los CSV también en subcarpetas, así que se pueden descomprimir dentro las carpetas de cada participante.
- `--modelo`: hoja de equilibrado. Si no se pasa, o si el libro no tiene valores guardados (no se ha abierto nunca en Excel), el script avisa y deja vacías las columnas de comparación.
- `--incluir-trucos`: cuenta también las partidas en las que se han usado teclas de depuración (por defecto se descartan).

El juego guarda los datos en `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Cardstorm\telemetria\`, con cuatro ficheros por partida: `<código>_<fecha>_<hora>_{resumen,eventos,muestras,cartas}.csv`. Una partida que no tiene `resumen` (el juego se cerró de golpe) cuenta como incompleta y solo entra en la tasa de abandono.

## Hojas

| Hoja | Contenido |
|---|---|
| `Criterios` | Un criterio por fila, con el valor medido, el objetivo, `cumple` / `no cumple` / `sin datos` y el número de partidas. |
| `Partidas` | Todos los resúmenes, con las columnas `completa` y `usada`. |
| `Por minuto` | La media de todas las partidas junto al modelo y, debajo, cada partida: oro acumulado, nivel, vida entrante, daño infligido, ρ y enemigos vivos medios. |
| `Cartas` | Por carta y gema: jugadas, energía, daño, daño por jugada y por energía, comparado con el V / coste del modelo. |
| `Oferta` | Veces que se ofrece y se elige cada carta y gema, y presencia en los mazos finales. |
| `Muertes` | Momento, causa, tramo y si fue en un pico. |

## Notas

- El daño por energía se compara con el V de daño (V menos los otros efectos) entre el coste, y solo en las cartas sin gema. Las cartas que no hacen daño (Esquivar, Escudo) no entran. Del modelo se usa la versión «(ajustada)» cuando existe.
- Pico: una oleada desde su inicio hasta 30 s después del cofre, o un jefe vivo, o los 30 s después de que muera o huya.
- Para el criterio de la carta o gema más elegida solo cuentan las que se han ofrecido al menos 5 veces.
- Las cabeceras de los CSV están copiadas al principio del script. Si cambian en `RunTelemetry.cs`, hay que cambiarlas también aquí.
