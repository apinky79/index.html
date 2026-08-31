#!/usr/bin/env python3
"""Analyse professional English football player birthplaces: inner vs outer London."""

from __future__ import annotations

import json
import re
import time
from dataclasses import asdict, dataclass
from math import asin, cos, radians, sin, sqrt
from pathlib import Path
from urllib.parse import urljoin

import matplotlib.pyplot as plt
import pandas as pd
import requests
from bs4 import BeautifulSoup
from geopy.geocoders import Nominatim
from geopy.extra.rate_limiter import RateLimiter

BASE_URL = "https://www.transfermarkt.co.uk"
HEADERS = {
    "User-Agent": (
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
        "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
    ),
    "Accept-Language": "en-GB,en;q=0.9",
}

# Charing Cross — central London reference point
LONDON_CENTER = (51.5074, -0.1278)
RADIUS_MILES = 10.0

LEAGUES = {
    "Premier League": "GB1",
    "Championship": "GB2",
    "League One": "GB3",
    "League Two": "GB4",
    "National League": "CNAT",
}

INNER_LONDON_BOROUGHS = {
    "city of london",
    "camden",
    "greenwich",
    "hackney",
    "hammersmith and fulham",
    "hammersmith",
    "islington",
    "kensington and chelsea",
    "kensington",
    "chelsea",
    "lambeth",
    "lewisham",
    "southwark",
    "tower hamlets",
    "wandsworth",
    "westminster",
}

OUTER_LONDON_BOROUGHS = {
    "barking and dagenham",
    "barking",
    "dagenham",
    "barnet",
    "bexley",
    "brent",
    "bromley",
    "croydon",
    "ealing",
    "enfield",
    "haringey",
    "harrow",
    "havering",
    "hillingdon",
    "hounslow",
    "kingston upon thames",
    "kingston",
    "merton",
    "newham",
    "redbridge",
    "richmond upon thames",
    "richmond",
    "sutton",
    "waltham forest",
}

# Common London-area place names mapped to inner/outer zones
PLACE_ALIASES: dict[str, str] = {
    # Inner London neighbourhoods / districts
    "canary wharf": "inner",
    "docklands": "inner",
    "elephant and castle": "inner",
    "peckham": "inner",  # historically ambiguous; Southwark = inner
    "brixton": "inner",
    "camden town": "inner",
    "clapham": "inner",
    "dulwich": "inner",
    "fulham": "inner",
    "hampstead": "outer",  # Camden border — geocode resolves
    "holborn": "inner",
    "kennington": "inner",
    "mile end": "inner",
    "shoreditch": "inner",
    "stratford": "outer",  # Newham = outer
    "tooting": "inner",
    "woolwich": "inner",
    "bermondsey": "inner",
    "catford": "inner",
    "deptford": "inner",
    "putney": "inner",
    "wimbledon": "outer",  # Merton
    "wembley": "outer",  # Brent
    "tottenham": "outer",  # Haringey
    "ilford": "outer",
    "romford": "outer",
    "uxbridge": "outer",
    "hayes": "outer",
    "feltham": "outer",
    "bromley": "outer",
    "croydon": "outer",
    "ealing": "outer",
    "enfield": "outer",
    "hounslow": "outer",
    "islington": "inner",
    "lewisham": "inner",
    "southwark": "inner",
    "hackney": "inner",
    "lambeth": "inner",
    "westminster": "inner",
    "greenwich": "inner",
    "wandsworth": "inner",
    "brentford": "outer",  # Hounslow
    "chiswick": "outer",
    "east ham": "outer",
    "west ham": "outer",
    "wood green": "outer",
    "finchley": "outer",
    "edgware": "outer",
    "surbiton": "outer",
    "kingston": "outer",
    "sutton": "outer",
    "barnet": "outer",
    "harrow": "outer",
    "bexleyheath": "outer",
    "dartford": "other",  # outside Greater London
    "london": "unknown",
}

ROOT = Path(__file__).resolve().parent

# Greater London approximate bounding box for geocode validation
GREATER_LONDON_BBOX = (51.28, -0.52, 51.70, 0.35)  # min_lat, min_lon, max_lat, max_lon


def in_greater_london_bbox(lat: float, lon: float) -> bool:
    min_lat, min_lon, max_lat, max_lon = GREATER_LONDON_BBOX
    return min_lat <= lat <= max_lat and min_lon <= lon <= max_lon
CACHE_FILE = ROOT / "player_birthplaces_cache.json"
OUTPUT_CHART = ROOT / "london_birthplace_chart.png"
OUTPUT_HTML = ROOT / "london_birthplace_chart.html"
OUTPUT_CSV = ROOT / "london_players.csv"


@dataclass
class PlayerRecord:
    player_id: str
    name: str
    league: str
    club: str
    birthplace: str
    borough_hint: str | None
    birth_country: str | None
    zone: str  # inner | outer | other | unknown
    distance_miles: float | None


def haversine_miles(lat1: float, lon1: float, lat2: float, lon2: float) -> float:
    r = 3958.8
    dlat = radians(lat2 - lat1)
    dlon = radians(lon2 - lon1)
    a = sin(dlat / 2) ** 2 + cos(radians(lat1)) * cos(radians(lat2)) * sin(dlon / 2) ** 2
    return 2 * r * asin(sqrt(a))


def normalise(text: str) -> str:
    return re.sub(r"\s+", " ", text.strip().lower())


def fetch_html(url: str, retries: int = 3) -> str:
    for attempt in range(retries):
        try:
            resp = requests.get(url, headers=HEADERS, timeout=30)
            if resp.status_code == 429:
                time.sleep(5 * (attempt + 1))
                continue
            resp.raise_for_status()
            return resp.text
        except requests.RequestException:
            time.sleep(2 * (attempt + 1))
    raise RuntimeError(f"Failed to fetch {url}")


def league_team_links(competition_id: str) -> list[tuple[str, str, str]]:
    league_paths = {
        "GB1": "/premier-league/startseite/wettbewerb/GB1",
        "GB2": "/championship/startseite/wettbewerb/GB2",
        "GB3": "/league-one/startseite/wettbewerb/GB3",
        "GB4": "/league-two/startseite/wettbewerb/GB4",
        "CNAT": "/national-league/startseite/wettbewerb/CNAT",
    }
    html = fetch_html(BASE_URL + league_paths[competition_id])
    soup = BeautifulSoup(html, "lxml")
    teams: dict[str, tuple[str, str]] = {}
    for a in soup.select("table.items td.hauptlink a[href*='/startseite/verein/']"):
        href = a.get("href", "")
        m = re.search(r"/([^/]+)/startseite/verein/(\d+)", href)
        if not m:
            continue
        slug, team_id = m.group(1), m.group(2)
        name = (a.get("title") or a.get_text(strip=True)).strip()
        if name and team_id not in teams:
            teams[team_id] = (name, slug)
    return [(team_id, name, slug) for team_id, (name, slug) in teams.items()]


def squad_player_links(team_id: str, team_slug: str) -> list[tuple[str, str]]:
    url = f"{BASE_URL}/{team_slug}/kader/verein/{team_id}/plus/1"
    html = fetch_html(url)
    players: dict[str, str] = {}
    for m in re.finditer(r'href="(/[^"]+/profil/spieler/(\d+))"', html):
        path, player_id = m.group(1), m.group(2)
        if player_id not in players:
            players[player_id] = path
    return [(pid, path) for pid, path in players.items()]


def parse_birthplace(html: str) -> tuple[str, str | None, str | None]:
    soup = BeautifulSoup(html, "lxml")
    name_el = soup.select_one("h1.data-header__headline-wrapper")
    name = name_el.get_text(" ", strip=True) if name_el else "Unknown"
    name = re.sub(r"^#\d+\s+", "", name).strip()

    birthplace = ""
    borough_hint = None
    birth_country = None

    for label in soup.select("span.info-table__content--regular"):
        label_text = label.get_text(strip=True).lower()
        if "place of birth" in label_text:
            value_span = label.find_next_sibling("span")
            if value_span:
                hint_el = value_span.select_one("span[title]")
                if hint_el and hint_el.get("title"):
                    borough_hint = hint_el.get("title").strip()
                birthplace = value_span.get_text(" ", strip=True)
                birthplace = re.sub(r"\s+", " ", birthplace)
                img = value_span.select_one("img[title]")
                if img and img.get("title"):
                    birth_country = img["title"].strip()
            break

    if not birthplace:
        meta = soup.select_one('meta[name="description"]')
        if meta and meta.get("content"):
            m = re.search(r"\*\s*\d{2}/\d{2}/\d{4}\s+in\s+([^,]+)", meta["content"])
            if m:
                birthplace = m.group(1).strip()

    return name, birthplace, borough_hint, birth_country


class LondonClassifier:
    def __init__(self) -> None:
        self.geocoder = Nominatim(user_agent="london-football-birthplace-analysis/1.0")
        self.geocode = RateLimiter(self.geocoder.geocode, min_delay_seconds=1.1)
        self._geo_cache: dict[str, tuple[float, float] | None] = {}

    def _borough_zone(self, text: str) -> str | None:
        t = normalise(text)
        if t in PLACE_ALIASES:
            z = PLACE_ALIASES[t]
            return None if z in {"unknown", "other"} else z
        if t in INNER_LONDON_BOROUGHS:
            return "inner"
        if t in OUTER_LONDON_BOROUGHS:
            return "outer"
        for borough in INNER_LONDON_BOROUGHS:
            if len(borough) > 5 and borough in t:
                return "inner"
        for borough in OUTER_LONDON_BOROUGHS:
            if len(borough) > 5 and borough in t:
                return "outer"
        return None

    def _looks_london_related(self, birthplace: str, borough_hint: str | None) -> bool:
        combined = normalise(f"{birthplace} {borough_hint or ''}")
        if "london" in combined:
            return True
        if self._borough_zone(birthplace) or (borough_hint and self._borough_zone(borough_hint)):
            return True
        if normalise(birthplace) in PLACE_ALIASES and PLACE_ALIASES[normalise(birthplace)] in {"inner", "outer"}:
            return True
        return False

    def _coords(self, place: str) -> tuple[float, float] | None:
        key = normalise(place)
        if key in self._geo_cache:
            return self._geo_cache[key]
        queries = [
            f"{place}, Greater London, England",
            f"{place}, London, England",
        ]
        for q in queries:
            try:
                loc = self.geocode(q, timeout=10, exactly_one=True)
            except Exception:
                loc = None
            if loc and in_greater_london_bbox(loc.latitude, loc.longitude):
                coords = (loc.latitude, loc.longitude)
                self._geo_cache[key] = coords
                return coords
        self._geo_cache[key] = None
        return None

    def classify(
        self, birthplace: str, borough_hint: str | None, birth_country: str | None
    ) -> tuple[str, float | None]:
        if birth_country and birth_country.lower() not in {"england", "united kingdom", "uk", "great britain"}:
            return "other", None

        if not birthplace and not borough_hint:
            return "unknown", None

        # Prefer explicit borough hint from Transfermarkt (e.g. London → Ealing)
        for candidate in [borough_hint, birthplace]:
            if not candidate:
                continue
            zone = self._borough_zone(candidate)
            if zone in {"inner", "outer"}:
                coords = self._coords(candidate) if candidate.lower() != "london" else self._coords(borough_hint or "London")
                dist = None
                if coords:
                    dist = haversine_miles(LONDON_CENTER[0], LONDON_CENTER[1], coords[0], coords[1])
                    if dist > RADIUS_MILES:
                        return "other", dist
                return zone, dist

        if normalise(birthplace) == "london" and not borough_hint:
            return "unknown", None

        if not self._looks_london_related(birthplace, borough_hint):
            return "other", None

        # Geocode only when the place is plausibly London-related
        coords = self._coords(borough_hint or birthplace)
        if not coords:
            return "other", None

        dist = haversine_miles(LONDON_CENTER[0], LONDON_CENTER[1], coords[0], coords[1])
        if dist > RADIUS_MILES:
            return "other", dist

        # Within radius: use borough lists if possible, else distance bands
        zone = self._borough_zone(borough_hint or birthplace)
        if zone in {"inner", "outer"}:
            return zone, dist
        if dist <= 5.0:
            return "inner", dist
        return "outer", dist


def load_cache() -> dict:
    if CACHE_FILE.exists():
        return json.loads(CACHE_FILE.read_text())
    return {}


def save_cache(cache: dict) -> None:
    CACHE_FILE.write_text(json.dumps(cache, indent=2))


def collect_players(limit_leagues: list[str] | None = None, max_players: int | None = None) -> list[PlayerRecord]:
    cache = load_cache()
    classifier = LondonClassifier()
    records: list[PlayerRecord] = []
    seen_players: set[str] = set()

    for league_name, comp_id in LEAGUES.items():
        if limit_leagues and league_name not in limit_leagues:
            continue
        print(f"\n=== {league_name} ({comp_id}) ===")
        teams = league_team_links(comp_id)
        print(f"  Found {len(teams)} clubs")

        for team_id, team_name, team_slug in teams:
            try:
                players = squad_player_links(team_id, team_slug)
            except Exception as exc:
                print(f"  Skip {team_name}: {exc}")
                continue

            for player_id, profile_path in players:
                if player_id in seen_players:
                    continue
                seen_players.add(player_id)
                if max_players and len(seen_players) > max_players:
                    break

                cache_key = player_id
                if cache_key in cache:
                    data = cache[cache_key]
                else:
                    time.sleep(0.35)
                    try:
                        html = fetch_html(BASE_URL + profile_path)
                        name, birthplace, borough_hint, birth_country = parse_birthplace(html)
                        zone, dist = classifier.classify(birthplace, borough_hint, birth_country)
                        data = {
                            "name": name,
                            "birthplace": birthplace,
                            "borough_hint": borough_hint,
                            "birth_country": birth_country,
                            "zone": zone,
                            "distance_miles": dist,
                        }
                        cache[cache_key] = data
                        if len(cache) % 25 == 0:
                            save_cache(cache)
                    except Exception as exc:
                        print(f"    Player {player_id} error: {exc}")
                        continue

                records.append(
                    PlayerRecord(
                        player_id=player_id,
                        name=data["name"],
                        league=league_name,
                        club=team_name,
                        birthplace=data.get("birthplace", ""),
                        borough_hint=data.get("borough_hint"),
                        birth_country=data.get("birth_country"),
                        zone=data.get("zone", "unknown"),
                        distance_miles=data.get("distance_miles"),
                    )
                )

            if max_players and len(seen_players) > max_players:
                break
        if max_players and len(seen_players) > max_players:
            break

    save_cache(cache)
    return records


def make_chart(records: list[PlayerRecord]) -> pd.DataFrame:
    df = pd.DataFrame([asdict(r) for r in records])
    london_df = df[df["zone"].isin(["inner", "outer"])].copy()

    summary = (
        london_df.groupby(["league", "zone"], as_index=False)
        .size()
        .rename(columns={"size": "players"})
    )

    if summary.empty:
        raise RuntimeError("No London-born players found in scraped data.")

    pivot = summary.pivot(index="league", columns="zone", values="players").fillna(0).astype(int)
    league_order = [l for l in LEAGUES if l in pivot.index]
    pivot = pivot.reindex(league_order)

    for col in ["inner", "outer"]:
        if col not in pivot.columns:
            pivot[col] = 0
    pivot = pivot[["inner", "outer"]]

    fig, ax = plt.subplots(figsize=(12, 7))
    x = range(len(pivot))
    width = 0.35
    inner_vals = pivot["inner"].values
    outer_vals = pivot["outer"].values

    bars_inner = ax.bar([i - width / 2 for i in x], inner_vals, width, label="Inner London", color="#1D3557")
    bars_outer = ax.bar([i + width / 2 for i in x], outer_vals, width, label="Outer London (within 10 mi)", color="#E63946")

    ax.set_xlabel("Competition", fontsize=12)
    ax.set_ylabel("Number of players", fontsize=12)
    ax.set_title(
        "Professional English Football Players Born in London\n"
        "Inner London vs Outer London (within 10-mile radius of central London)",
        fontsize=14,
        fontweight="bold",
    )
    ax.set_xticks(list(x))
    ax.set_xticklabels(pivot.index, rotation=15, ha="right")
    ax.legend()
    ax.grid(axis="y", alpha=0.3)

    for bars in (bars_inner, bars_outer):
        for bar in bars:
            h = bar.get_height()
            if h > 0:
                ax.annotate(
                    f"{int(h)}",
                    xy=(bar.get_x() + bar.get_width() / 2, h),
                    xytext=(0, 3),
                    textcoords="offset points",
                    ha="center",
                    va="bottom",
                    fontsize=10,
                )

    totals = pivot.sum()
    fig.text(
        0.5,
        0.01,
        f"Total across leagues: Inner London {int(totals['inner'])}  |  Outer London {int(totals['outer'])}  |  "
        f"Data: Transfermarkt squads, {len(df)} players scraped, season 2024/25",
        ha="center",
        fontsize=9,
        color="#555",
    )
    plt.tight_layout(rect=[0, 0.03, 1, 1])
    fig.savefig(OUTPUT_CHART, dpi=150, bbox_inches="tight")
    plt.close()

    # Optional interactive HTML chart (requires plotly; skipped if not installed)
    try:
        import plotly.graph_objects as go

        fig_html = go.Figure()
        fig_html.add_trace(go.Bar(name="Inner London", x=pivot.index, y=inner_vals, marker_color="#1D3557"))
        fig_html.add_trace(
            go.Bar(name="Outer London (within 10 mi)", x=pivot.index, y=outer_vals, marker_color="#E63946")
        )
        fig_html.update_layout(
            barmode="group",
            title="London-born players in English professional football (inner vs outer, 10-mile radius)",
            xaxis_title="Competition",
            yaxis_title="Players",
            template="plotly_white",
        )
        fig_html.write_html(OUTPUT_HTML, include_plotlyjs="cdn")
    except ImportError:
        pass

    london_df.to_csv(OUTPUT_CSV, index=False)
    pivot["total_london"] = pivot["inner"] + pivot["outer"]
    pivot.to_csv(ROOT / "london_summary_by_league.csv")
    return pivot


def reclassify_cache() -> list[PlayerRecord]:
    """Re-run London zone classification on cached birthplaces (no HTTP)."""
    cache = load_cache()
    classifier = LondonClassifier()
    for player_id, data in cache.items():
        zone, dist = classifier.classify(
            data.get("birthplace", ""),
            data.get("borough_hint"),
            data.get("birth_country"),
        )
        data["zone"] = zone
        data["distance_miles"] = dist
    save_cache(cache)
    return [
        PlayerRecord(
            player_id=pid,
            name=data["name"],
            league=data.get("league", "Unknown"),
            club=data.get("club", "Unknown"),
            birthplace=data.get("birthplace", ""),
            borough_hint=data.get("borough_hint"),
            birth_country=data.get("birth_country"),
            zone=data.get("zone", "unknown"),
            distance_miles=data.get("distance_miles"),
        )
        for pid, data in cache.items()
    ]


def records_from_cache_with_leagues() -> list[PlayerRecord]:
    """Build records from cache; attach league/club by re-walking squads if missing."""
    cache = load_cache()
    records: list[PlayerRecord] = []
    seen: set[str] = set()

    for league_name, comp_id in LEAGUES.items():
        try:
            teams = league_team_links(comp_id)
        except Exception:
            continue
        for team_id, team_name, team_slug in teams:
            try:
                players = squad_player_links(team_id, team_slug)
            except Exception:
                continue
            for player_id, _ in players:
                if player_id not in cache or player_id in seen:
                    continue
                seen.add(player_id)
                data = cache[player_id]
                data["league"] = league_name
                data["club"] = team_name
                records.append(
                    PlayerRecord(
                        player_id=player_id,
                        name=data["name"],
                        league=league_name,
                        club=team_name,
                        birthplace=data.get("birthplace", ""),
                        borough_hint=data.get("borough_hint"),
                        birth_country=data.get("birth_country"),
                        zone=data.get("zone", "unknown"),
                        distance_miles=data.get("distance_miles"),
                    )
                )
    save_cache(cache)
    return records


def main() -> None:
    import sys

    if "--reclassify" in sys.argv:
        print("Reclassifying cached birthplaces with improved London rules…")
        reclassify_cache()
        records = records_from_cache_with_leagues()
    else:
        print("Collecting player birthplace data from Transfermarkt…")
        records = collect_players()

    print(f"\nProcessed {len(records)} unique players")
    summary = make_chart(records)
    print("\nSummary by league:")
    print(summary.to_string())
    print(f"\nChart saved to {OUTPUT_CHART}")
    if OUTPUT_HTML.exists():
        print(f"Interactive chart saved to {OUTPUT_HTML}")


if __name__ == "__main__":
    main()
