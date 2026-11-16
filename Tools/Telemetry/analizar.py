"""Junta las partidas grabadas por TelemetryRecorder y comprueba los criterios de equilibrado.

Uso:
    python analizar.py <carpeta_con_csv> [--modelo cardstorm_equilibrado.xlsx]
                       [--salida telemetria_resumen.xlsx] [--incluir-trucos]
"""

from __future__ import annotations

import argparse
import csv
import itertools
import statistics
import sys
import unicodedata
from dataclasses import dataclass, field
from pathlib import Path

# Cabeceras que escribe el juego. Si cambian allí, hay que cambiarlas aquí.
SUMMARY_HEADER = (
    "version;codigo;partida;inicio;semilla;trucos;resultado;t_fin_s;t_real_s;nivel;xp_total;"
    "oro_obtenido;oro_enemigos;oro_cofres;oro_jefes;oro_gastado;oro_final;t_primera_subida_s;"
    "energia_desperdiciada;seg_energia_llena;frac_energia_llena;cartas_jugadas;jugadas_fallidas;causa_muerte;"
    "heraldo_aparece_s;heraldo_fin_s;heraldo_resultado;jefe_final_aparece_s;jefe_final_fin_s;jefe_final_resultado;"
    "codicia;erudicion;mazo_final"
)
EVENTS_HEADER = "t_s;evento;id;id2;valor"
SAMPLES_HEADER = (
    "t_s;nivel;xp_total;oro_obtenido;oro_gastado;oro;vida;vida_max;energia;energia_desperdiciada;seg_energia_llena;"
    "enemigos_vivos;vida_entrante;dano_infligido;dano_sobrante;dano_recibido;cartas_jugadas"
)
CARDS_HEADER = "carta;gema;jugadas;energia;repeticiones;dano;dano_sobrante;muertes"

HEADERS = {
    "resumen": SUMMARY_HEADER,
    "eventos": EVENTS_HEADER,
    "muestras": SAMPLES_HEADER,
    "cartas": CARDS_HEADER,
}

CUMPLE = "cumple"
NO_CUMPLE = "no cumple"
SIN_DATOS = "sin datos"
INFORMATIVO = "informativo"

# Estructura fija de la partida: se usa si el modelo no la trae.
DEFAULT_BASICS = {"disparo", "tajo", "esquivar"}
DEFAULT_WAVES = [(165.0, 195.0), (480.0, 510.0), (660.0, 690.0), (840.0, 870.0)]

PEAK_TAIL_S = 30.0          # Segundos que sigue el pico tras el cofre o el fin de un jefe.
EARLY_DEATH_S = 180.0
MIN_OFFERS = 5              # Ofertas mínimas para que una carta o gema cuente en el criterio de elección.
FINISHED = ("muerte", "victoria")
TEXT_COLUMNS = {"version", "codigo", "inicio", "semilla", "fichero", "id", "carta", "gema", "causa"}


# ---------- Lectura de partidas ----------

@dataclass
class Run:
    key: str
    summary: dict | None = None
    events: list = field(default_factory=list)
    samples: list = field(default_factory=list)
    cards: list = field(default_factory=list)
    warnings: list = field(default_factory=list)

    @property
    def complete(self) -> bool:
        return self.summary is not None

    @property
    def cheats(self) -> bool:
        return self.complete and self.summary.get("trucos") == "si"

    @property
    def result(self) -> str:
        return self.summary.get("resultado", "") if self.complete else "incompleta"

    def num(self, column: str):
        return num(self.summary.get(column)) if self.complete else None


def num(value):
    if value is None or value == "":
        return None
    try:
        return float(value)
    except ValueError:
        return None


def read_csv(path: Path, kind: str, warnings: list) -> list[dict]:
    with path.open(encoding="utf-8-sig", newline="") as f:
        reader = csv.DictReader(f, delimiter=";")
        header = ";".join(reader.fieldnames or [])
        if header != HEADERS[kind]:
            warnings.append(f"{path.name}: la cabecera no coincide con la esperada.")
        return list(reader)


def load_runs(folder: Path) -> list[Run]:
    """Agrupa los ficheros <codigo>_<fecha>_<hora>_<tipo>.csv por partida, buscando en subcarpetas."""
    runs: dict[str, Run] = {}
    for kind in HEADERS:
        for path in sorted(folder.rglob(f"*_{kind}.csv")):
            key = str(path.with_name(path.name[: -len(f"_{kind}.csv")]))
            run = runs.setdefault(key, Run(key=key))
            rows = read_csv(path, kind, run.warnings)
            if kind == "resumen":
                run.summary = rows[0] if rows else None
            elif kind == "eventos":
                run.events = rows
            elif kind == "muestras":
                run.samples = sorted(rows, key=lambda r: num(r["t_s"]) or 0.0)
            else:
                run.cards = rows
    return [runs[k] for k in sorted(runs)]


# ---------- Modelo ----------

def norm(text) -> str:
    text = unicodedata.normalize("NFKD", str(text or "")).encode("ascii", "ignore").decode()
    return " ".join(text.lower().split())


@dataclass
class CardModel:
    value: float
    cost: float
    damage_value: float   # V sin los efectos que no son daño.


@dataclass
class Model:
    gold_by_minute: dict = field(default_factory=dict)
    level_by_minute: dict = field(default_factory=dict)
    cards: dict = field(default_factory=dict)
    basics: set = field(default_factory=lambda: set(DEFAULT_BASICS))
    waves: list = field(default_factory=lambda: list(DEFAULT_WAVES))
    tramos: list = field(default_factory=list)
    warnings: list = field(default_factory=list)


def _sheet(wb, name):
    for ws in wb.worksheets:
        if norm(ws.title) == name:
            return ws
    return None


def _columns(header_row, wanted: dict) -> dict:
    """Busca cada columna por el principio de su cabecera, sin acentos."""
    found = {}
    for i, cell in enumerate(header_row):
        h = norm(cell)
        for key, prefix in wanted.items():
            if key not in found and h.startswith(prefix):
                found[key] = i
    return found


def _rows(ws):
    rows = list(ws.iter_rows(values_only=True))
    return (rows[0], rows[1:]) if rows else ((), [])


def load_model(path: Path | None) -> Model:
    model = Model()
    if path is None:
        model.warnings.append("Sin modelo: las columnas de comparación quedan vacías.")
        return model
    if not path.exists():
        model.warnings.append(f"No existe {path}: las columnas de comparación quedan vacías.")
        return model

    import openpyxl

    wb = openpyxl.load_workbook(path, data_only=True, read_only=True)
    missing = []

    ws = _sheet(wb, "progresion")
    if ws is not None:
        header, rows = _rows(ws)
        c = _columns(header, {"min": "minuto", "gold": "oro acumulado", "level": "nivel al final"})
        if len(c) == 3:
            for r in rows:
                m = r[c["min"]]
                if isinstance(m, (int, float)) and not isinstance(m, bool):
                    g, lv = r[c["gold"]], r[c["level"]]
                    if isinstance(g, (int, float)):
                        model.gold_by_minute[int(m)] = float(g)
                    if isinstance(lv, (int, float)):
                        model.level_by_minute[int(m)] = float(lv)
            if not model.gold_by_minute:
                missing.append(ws.title)

    ws = _sheet(wb, "cartas de accion")
    if ws is not None:
        header, rows = _rows(ws)
        c = _columns(header, {"name": "carta", "rarity": "rareza", "cost": "coste", "other": "otros efectos"})
        v_col = next((i for i, h in enumerate(header) if norm(h) == "v"), None)
        if "name" in c and "cost" in c and v_col is not None:
            basics = set()
            for r in rows:
                name = r[c["name"]]
                if not isinstance(name, str) or not name.strip():
                    continue
                adjusted = "(ajustada)" in name
                card_id = norm(name.replace("(ajustada)", "")).split(" ")[0]
                value, cost = r[v_col], r[c["cost"]]
                if "rarity" in c and norm(r[c["rarity"]]) == "basica":
                    basics.add(card_id)
                if not isinstance(value, (int, float)) or not isinstance(cost, (int, float)) or cost <= 0:
                    continue
                other = r[c["other"]] if "other" in c and isinstance(r[c["other"]], (int, float)) else 0.0
                # La versión ajustada es la que lleva el juego.
                if adjusted or card_id not in model.cards:
                    model.cards[card_id] = CardModel(float(value), float(cost), float(value) - float(other))
            if basics:
                model.basics = basics
            if not model.cards:
                missing.append(ws.title)

    ws = _sheet(wb, "oleadas")
    if ws is not None:
        header, rows = _rows(ws)
        c = _columns(header, {"start": "inicio", "chest": "hora del cofre"})
        if len(c) == 2:
            waves = [(float(r[c["start"]]), float(r[c["chest"]])) for r in rows
                     if isinstance(r[c["start"]], (int, float)) and isinstance(r[c["chest"]], (int, float))]
            if waves:
                model.waves = waves

    ws = _sheet(wb, "tramos")
    if ws is not None:
        header, rows = _rows(ws)
        c = _columns(header, {"start": "inicio", "end": "fin", "name": "nombre"})
        if len(c) == 3:
            model.tramos = [(float(r[c["start"]]), float(r[c["end"]]), str(r[c["name"]])) for r in rows
                            if isinstance(r[c["start"]], (int, float)) and isinstance(r[c["end"]], (int, float))]

    wb.close()
    if missing:
        model.warnings.append(
            "Sin valores guardados en " + ", ".join(missing)
            + ". Abre el libro en Excel y guárdalo; mientras, las columnas de comparación quedan vacías.")
    return model


# ---------- Cálculos ----------

def pct(part, total):
    return 100.0 * part / total if total else None


def deviation(value, ref):
    return 100.0 * (value - ref) / ref if value is not None and ref else None


def median(values):
    values = [v for v in values if v is not None]
    return statistics.median(values) if values else None


def mean(values):
    values = [v for v in values if v is not None]
    return statistics.fmean(values) if values else None


def deck_ids(run: Run) -> list[str]:
    deck = run.summary.get("mazo_final", "") if run.complete else ""
    return [e for e in deck.split("|") if e]


def sample_at(run: Run, t: float):
    """Primera muestra a partir de t (las muestras llegan unos milisegundos tarde)."""
    for s in run.samples:
        st = num(s["t_s"])
        if st is not None and st >= t - 0.01:
            return s
    return None


def initial_gold(run: Run) -> float:
    """Oro de la muestra de t = 0, antes de ganar o gastar nada."""
    return (num(run.samples[0]["oro"]) or 0.0) if run.samples else 0.0


def boss_windows(run: Run):
    for prefix in ("heraldo", "jefe_final"):
        start = run.num(f"{prefix}_aparece_s")
        if start is not None:
            yield start, run.num(f"{prefix}_fin_s")


def in_peak(t: float, run: Run, waves) -> bool:
    for start, chest in waves:
        if start <= t <= chest + PEAK_TAIL_S:
            return True
    for start, end in boss_windows(run):
        if t >= start and (end is None or t <= end + PEAK_TAIL_S):
            return True
    return False


def tramo_name(t: float, model: Model) -> str:
    for start, end, name in model.tramos:
        if start <= t < end:
            return name
    return f"minuto {int(t // 60) + 1}"


def level_at(run: Run, t: float) -> int:
    level = 1
    for e in run.events:
        if e["evento"] == "subida" and (num(e["t_s"]) or 0) <= t:
            level = max(level, int(num(e["valor"]) or 1))
    return level


def minute_rows(run: Run):
    """Una fila por minuto completo de la partida."""
    end = run.num("t_fin_s") or 0.0
    gold0 = initial_gold(run)
    prev = sample_at(run, 0.0)
    for m in range(1, int(end // 60) + 1):
        s = sample_at(run, 60.0 * m)
        if s is None or prev is None:
            break
        incoming = num(s["vida_entrante"]) - num(prev["vida_entrante"])
        dealt = num(s["dano_infligido"]) - num(prev["dano_infligido"])
        alive = [num(x["enemigos_vivos"]) for x in run.samples
                 if 60.0 * (m - 1) < (num(x["t_s"]) or 0) <= 60.0 * m + 0.5]
        yield {
            "minuto": m,
            "oro_acumulado": gold0 + num(s["oro_obtenido"]),
            "nivel": num(s["nivel"]),
            "vida_entrante": incoming,
            "dano_infligido": dealt,
            "rho": incoming / dealt if dealt else None,
            "enemigos_vivos_medios": mean(alive),
        }
        prev = s


@dataclass
class Criterion:
    nombre: str
    valor: float | None
    objetivo: str
    estado: str
    partidas: int
    detalle: str = ""


def verdict(value, ok) -> str:
    return SIN_DATOS if value is None else (CUMPLE if ok(value) else NO_CUMPLE)


@dataclass
class Analysis:
    partidas: list
    por_minuto_media: list
    por_minuto: list
    cartas: list
    oferta: list
    muertes: list
    criterios: list
    usadas: int
    incompletas: int
    descartadas: int


def analyze(runs: list[Run], model: Model, include_cheats: bool = False) -> Analysis:
    complete = [r for r in runs if r.complete]
    incomplete = [r for r in runs if not r.complete]
    used = [r for r in complete if include_cheats or not r.cheats]
    finished = [r for r in used if r.result in FINISHED]
    deaths = [r for r in used if r.result == "muerte"]

    partidas = []
    for r in runs:
        row = dict(r.summary) if r.complete else {"codigo": Path(r.key).name.split("_")[0]}
        row["fichero"] = Path(r.key).name
        row["completa"] = "si" if r.complete else "no"
        row["usada"] = "si" if r in used else "no"
        partidas.append(row)

    # Por minuto.
    por_minuto, by_minute = [], {}
    for r in used:
        for row in minute_rows(r):
            por_minuto.append({"codigo": r.summary.get("codigo"), "partida": r.summary.get("partida"), **row})
            by_minute.setdefault(row["minuto"], []).append(row)
    media = []
    for m in sorted(by_minute):
        rows = by_minute[m]
        gold = mean(x["oro_acumulado"] for x in rows)
        level = mean(x["nivel"] for x in rows)
        media.append({
            "minuto": m, "partidas": len(rows),
            "oro_acumulado": gold, "oro_modelo": model.gold_by_minute.get(m),
            "oro_desv_pct": deviation(gold, model.gold_by_minute.get(m)),
            "nivel": level, "nivel_modelo": model.level_by_minute.get(m),
            "nivel_desv_pct": deviation(level, model.level_by_minute.get(m)),
            "vida_entrante": mean(x["vida_entrante"] for x in rows),
            "dano_infligido": mean(x["dano_infligido"] for x in rows),
            "rho": mean(x["rho"] for x in rows),
            "enemigos_vivos_medios": mean(x["enemigos_vivos_medios"] for x in rows),
        })

    # Cartas.
    totals = {}
    for r in used:
        for c in r.cards:
            key = (c["carta"], c["gema"])
            t = totals.setdefault(key, dict.fromkeys(("jugadas", "energia", "repeticiones", "dano", "dano_sobrante", "muertes"), 0.0))
            for k in t:
                t[k] += num(c[k]) or 0.0
    cartas = []
    for (card, gem), t in sorted(totals.items()):
        cm = model.cards.get(card)
        per_energy = t["dano"] / t["energia"] if t["energia"] else None
        ref = cm.damage_value / cm.cost if cm and cm.damage_value > 0 else None
        cartas.append({
            "carta": card, "gema": gem, **t,
            "dano_por_jugada": t["dano"] / t["jugadas"] if t["jugadas"] else None,
            "dano_por_energia": per_energy,
            "V": cm.value if cm else None, "coste": cm.cost if cm else None,
            "V_dano_por_coste": ref, "desv_pct": deviation(per_energy, ref),
        })

    # Oferta y mazos finales.
    offered, chosen = {}, {}
    for r in used:
        for e in r.events:
            ev, item = e["evento"], e["id"]
            if ev in ("oferta_carta", "oferta_gema"):
                kind = "carta" if ev == "oferta_carta" else "gema"
                offered[(kind, item)] = offered.get((kind, item), 0) + 1
            elif ev in ("compra", "gema"):
                kind = "carta" if ev == "compra" else "gema"
                chosen[(kind, item)] = chosen.get((kind, item), 0) + 1
    presence = {}
    for r in used:
        entries = deck_ids(r)
        for card in {e.split("+")[0] for e in entries}:
            presence[("carta", card)] = presence.get(("carta", card), 0) + 1
        for gem in {e.split("+")[1] for e in entries if "+" in e}:
            presence[("gema", gem)] = presence.get(("gema", gem), 0) + 1
    oferta = []
    for kind, item in sorted(set(offered) | set(chosen) | set(presence)):
        o, ch = offered.get((kind, item), 0), chosen.get((kind, item), 0)
        oferta.append({
            "tipo": kind, "id": item, "ofrecida": o, "elegida": ch, "elegida_pct": pct(ch, o),
            "en_mazos_pct": pct(presence.get((kind, item), 0), len(used)),
        })

    # Muertes.
    muertes = []
    for r in deaths:
        t = r.num("t_fin_s") or 0.0
        muertes.append({
            "codigo": r.summary.get("codigo"), "partida": r.summary.get("partida"), "t_s": t,
            "causa": r.summary.get("causa_muerte"), "tramo": tramo_name(t, model),
            "en_pico": "si" if in_peak(t, r, model.waves) else "no",
        })

    criterios = _criteria(runs, used, finished, deaths, muertes, media, cartas, oferta, model)
    return Analysis(partidas, media, por_minuto, cartas, oferta, muertes, criterios,
                    len(used), len(incomplete), len(complete) - len(used))


def _criteria(runs, used, finished, deaths, muertes, media, cartas, oferta, model) -> list[Criterion]:
    out = []

    early = pct(sum(1 for r in deaths if (r.num("t_fin_s") or 0) < EARLY_DEATH_S), len(deaths))
    out.append(Criterion("Muertes antes del minuto 3", early, "< 20 % de las muertes", verdict(early, lambda v: v < 20), len(deaths)))

    reached = [r for r in finished if r.num("jefe_final_aparece_s") is not None]
    v = pct(len(reached), len(finished))
    out.append(Criterion("Llegan al jefe final", v, ">= 30 % de las partidas", verdict(v, lambda x: x >= 30), len(finished)))

    v = pct(sum(1 for r in reached if r.summary.get("jefe_final_resultado") == "derrotado"), len(reached))
    out.append(Criterion("Derrotan al jefe final", v, "40-70 % de las que llegan", verdict(v, lambda x: 40 <= x <= 70), len(reached)))

    v = pct(sum(1 for m in muertes if m["en_pico"] == "si"), len(muertes))
    out.append(Criterion("Muertes en picos", v, ">= 50 % de las muertes", verdict(v, lambda x: x >= 50), len(muertes)))

    fracs = [r.num("frac_energia_llena") for r in used if (r.num("t_fin_s") or 0) > 0]
    v = mean(fracs)
    v = v * 100 if v is not None else None
    out.append(Criterion("Tiempo con la energía al máximo", v, "< 15 % (media)", verdict(v, lambda x: x < 15), len(fracs)))

    non_basic = [o for o in oferta if o["tipo"] == "carta" and o["id"] not in model.basics and o["en_mazos_pct"]]
    top = max(non_basic, key=lambda o: o["en_mazos_pct"], default=None)
    v = top["en_mazos_pct"] if top else None
    out.append(Criterion("Carta no básica más presente en los mazos finales", v, "<= 60 %",
                         verdict(v, lambda x: x <= 60), len(used), top["id"] if top else ""))

    by_code = {}
    for r in used:
        by_code.setdefault(r.summary.get("codigo"), []).append({e.split("+")[0] for e in deck_ids(r)})
    jaccards = [len(a & b) / len(a | b) * 100 for decks in by_code.values()
                for a, b in itertools.combinations(decks, 2) if a | b]
    v = mean(jaccards)
    out.append(Criterion("Cartas compartidas entre dos partidas del mismo código", v,
                         "< 60 % (media de pares)", verdict(v, lambda x: x < 60), len(jaccards), "pares"))

    combos = {(c["carta"], c["gema"]) for c in cartas if c["gema"]}
    out.append(Criterion("Combinaciones carta + gema distintas", float(len(combos)), "recuento", INFORMATIVO, len(used)))

    levels = [level_at(r, r.num("jefe_final_aparece_s")) for r in used if r.num("jefe_final_aparece_s") is not None]
    v = median(levels)
    out.append(Criterion("Nivel al aparecer el jefe final", v, "19-23 (mediana)", verdict(v, lambda x: 19 <= x <= 23), len(levels)))

    devs = [(m["minuto"], m["oro_desv_pct"]) for m in media if m["oro_desv_pct"] is not None]
    worst = max(devs, key=lambda d: abs(d[1]), default=None)
    v = abs(worst[1]) if worst else None
    out.append(Criterion("Oro acumulado por minuto", v, "±20 % del modelo en todos los minutos",
                         verdict(v, lambda x: x <= 20), len(used), f"minuto {worst[0]}" if worst else ""))

    leftovers = []
    for r in finished:
        total = initial_gold(r) + (r.num("oro_obtenido") or 0)
        if total > 0 and r.num("oro_final") is not None:
            leftovers.append(100 * r.num("oro_final") / total)
    v = mean(leftovers)
    out.append(Criterion("Oro sobrante", v, "10-20 % del obtenido", verdict(v, lambda x: 10 <= x <= 20), len(leftovers)))

    rated = [c for c in cartas if not c["gema"] and c["desv_pct"] is not None]
    worst = max(rated, key=lambda c: abs(c["desv_pct"]), default=None)
    v = abs(worst["desv_pct"]) if worst else None
    out.append(Criterion("Daño por energía de cada carta", v, "±15 % de V / coste", verdict(v, lambda x: x <= 15),
                         len(used), worst["carta"] if worst else ""))

    picks = [o for o in oferta if o["ofrecida"] >= MIN_OFFERS and o["elegida_pct"] is not None]
    top = max(picks, key=lambda o: o["elegida_pct"], default=None)
    v = top["elegida_pct"] if top else None
    out.append(Criterion("Carta o gema más elegida sobre ofrecida", v, f"<= 60 % (con {MIN_OFFERS}+ ofertas)",
                         verdict(v, lambda x: x <= 60), len(used), f"{top['tipo']} {top['id']}" if top else ""))

    out.append(_snowball(used))

    v = median([r.num("heraldo_fin_s") - r.num("heraldo_aparece_s") for r in used
                if r.summary.get("heraldo_resultado") in ("derrotado", "huye")])
    n = sum(1 for r in used if r.summary.get("heraldo_resultado") in ("derrotado", "huye"))
    out.append(Criterion("Duración del Heraldo", v, "mediana 45-90 s", verdict(v, lambda x: 45 <= x <= 90), n))

    finals = [r.num("jefe_final_fin_s") - r.num("jefe_final_aparece_s") for r in used
              if r.summary.get("jefe_final_resultado") == "derrotado"]
    v = median(finals)
    out.append(Criterion("Duración del jefe final", v, "mediana 60-180 s", verdict(v, lambda x: 60 <= x <= 180), len(finals)))

    firsts = [r.num("t_primera_subida_s") for r in used if r.num("t_primera_subida_s") is not None]
    out.append(Criterion("Tiempo hasta la primera subida", median(firsts), "mediana", INFORMATIVO, len(firsts)))

    # Las incompletas solo cuentan aquí.
    pool = [r for r in runs if not r.complete or r in used]
    v = pct(sum(1 for r in pool if r.result in ("abandono", "incompleta")), len(pool))
    out.append(Criterion("Partidas abandonadas", v, "% (incluye incompletas)", INFORMATIVO, len(pool)))
    return out


def _snowball(used) -> Criterion:
    """Oro y nivel por minuto de las partidas con Codicia o Erudición frente a la media del resto."""
    snow = [r for r in used if (r.num("codicia") or 0) > 0 or (r.num("erudicion") or 0) > 0]
    rest = [r for r in used if r not in snow]
    name, target = "Bola de nieve (con Codicia o Erudición)", "oro y nivel <= 130 % del resto"
    if not snow or not rest:
        return Criterion(name, None, target, SIN_DATOS, len(snow))

    def per_minute(runs):
        acc = {}
        for r in runs:
            for row in minute_rows(r):
                acc.setdefault(row["minuto"], []).append(row)
        return acc

    a, b = per_minute(snow), per_minute(rest)
    ratios = []
    for m in set(a) & set(b):
        for key in ("oro_acumulado", "nivel"):
            ref = mean(x[key] for x in b[m])
            if ref:
                ratios.append((100 * mean(x[key] for x in a[m]) / ref, f"{key} minuto {m}"))
    worst = max(ratios, default=None)
    v = worst[0] if worst else None
    return Criterion(name, v, target, verdict(v, lambda x: x <= 130), len(snow), worst[1] if worst else "")


# ---------- Libro de salida ----------

def write_workbook(result: Analysis, path: Path) -> None:
    from openpyxl import Workbook
    from openpyxl.styles import Font, PatternFill
    from openpyxl.utils import get_column_letter

    wb = Workbook()
    wb.remove(wb.active)
    bold = Font(bold=True)
    fills = {CUMPLE: PatternFill("solid", fgColor="C6EFCE"), NO_CUMPLE: PatternFill("solid", fgColor="FFC7CE"),
             SIN_DATOS: PatternFill("solid", fgColor="EDEDED")}

    def sheet(title, rows, columns=None, start_row=1, ws=None):
        ws = ws or wb.create_sheet(title)
        columns = columns or (list(rows[0].keys()) if rows else [])
        for j, col in enumerate(columns, 1):
            ws.cell(start_row, j, col).font = bold
        for i, row in enumerate(rows, start_row + 1):
            for j, col in enumerate(columns, 1):
                value = row.get(col)
                # Los CSV llegan como texto: los números se guardan como números para poder operar en Excel.
                if isinstance(value, str) and col not in TEXT_COLUMNS and num(value) is not None:
                    value = num(value)
                cell = ws.cell(i, j, value)
                if isinstance(value, float):
                    cell.number_format = "0.##"
        for j, col in enumerate(columns, 1):
            width = max([len(col)] + [len(str(r.get(col) or "")) for r in rows[:200]])
            letter = get_column_letter(j)
            ws.column_dimensions[letter].width = max(ws.column_dimensions[letter].width or 0, min(width + 2, 60))
        ws.freeze_panes = ws.cell(start_row + 1, 1)
        return ws

    summary_cols = ["fichero", "completa", "usada"] + SUMMARY_HEADER.split(";")
    sheet("Partidas", result.partidas, summary_cols)

    ws = sheet("Por minuto", result.por_minuto_media,
               ["minuto", "partidas", "oro_acumulado", "oro_modelo", "oro_desv_pct", "nivel", "nivel_modelo",
                "nivel_desv_pct", "vida_entrante", "dano_infligido", "rho", "enemigos_vivos_medios"])
    gap = len(result.por_minuto_media) + 3
    ws.cell(gap, 1, "Por partida").font = bold
    sheet(None, result.por_minuto, ["codigo", "partida", "minuto", "oro_acumulado", "nivel", "vida_entrante",
                                    "dano_infligido", "rho", "enemigos_vivos_medios"], start_row=gap + 1, ws=ws)
    ws.freeze_panes = "A2"

    sheet("Cartas", result.cartas, ["carta", "gema", "jugadas", "energia", "repeticiones", "dano", "dano_por_jugada",
                                    "dano_por_energia", "dano_sobrante", "muertes", "V", "coste", "V_dano_por_coste", "desv_pct"])
    sheet("Oferta", result.oferta, ["tipo", "id", "ofrecida", "elegida", "elegida_pct", "en_mazos_pct"])
    sheet("Muertes", result.muertes, ["codigo", "partida", "t_s", "causa", "tramo", "en_pico"])

    rows = [vars(c) for c in result.criterios]
    ws = sheet("Criterios", rows, ["nombre", "valor", "objetivo", "estado", "partidas", "detalle"])
    for i, c in enumerate(result.criterios, 2):
        if c.estado in fills:
            ws.cell(i, 4).fill = fills[c.estado]
    wb.move_sheet("Criterios", offset=-(len(wb.sheetnames) - 1))
    wb.active = 0
    wb.save(path)


# ---------- Línea de órdenes ----------

def main(argv=None) -> int:
    p = argparse.ArgumentParser(description="Junta las partidas de telemetría y comprueba los criterios.")
    p.add_argument("carpeta", type=Path, help="Carpeta con los CSV (se busca en subcarpetas).")
    p.add_argument("--modelo", type=Path, help="cardstorm_equilibrado.xlsx, para comparar con el modelo.")
    p.add_argument("--salida", type=Path, default=Path("telemetria_resumen.xlsx"))
    p.add_argument("--incluir-trucos", action="store_true", help="Cuenta también las partidas con trucos.")
    args = p.parse_args(argv)
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(errors="replace")

    if not args.carpeta.is_dir():
        print(f"No existe la carpeta {args.carpeta}", file=sys.stderr)
        return 1

    runs = load_runs(args.carpeta)
    model = load_model(args.modelo)
    for w in model.warnings + [w for r in runs for w in r.warnings]:
        print(f"Aviso: {w}", file=sys.stderr)

    result = analyze(runs, model, args.incluir_trucos)
    write_workbook(result, args.salida)

    print(f"{len(runs)} partidas: {result.usadas} usadas, {result.incompletas} incompletas, "
          f"{result.descartadas} descartadas por trucos.")
    for c in result.criterios:
        value = "" if c.valor is None else f"{c.valor:.1f}"
        print(f"  {c.estado:<11} {c.nombre}: {value} ({c.objetivo}; n = {c.partidas}) {c.detalle}".rstrip())
    print(f"Libro: {args.salida.resolve()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
