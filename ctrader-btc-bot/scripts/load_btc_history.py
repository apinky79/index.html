#!/usr/bin/env python3
"""Download daily BTC/USD from blockchain.info (2009 → today) and cache CSV."""

from __future__ import annotations

import json
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

import pandas as pd

CACHE = Path(__file__).resolve().parent / "btc_usd_daily_full.csv"
URL = "https://api.blockchain.info/charts/market-price?timespan=all&format=json&sampled=false"


def fetch_daily() -> pd.DataFrame:
    with urllib.request.urlopen(URL, timeout=120) as r:
        payload = json.loads(r.read())
    rows = payload.get("values", [])
    if not rows:
        raise RuntimeError("No data from blockchain.info")
    rec = []
    for v in rows:
        ts = datetime.fromtimestamp(v["x"], tz=timezone.utc)
        rec.append({"Date": ts, "Close": float(v["y"])})
    df = pd.DataFrame(rec).set_index("Date").sort_index()
    # Synthetic OHLC from close (API is daily USD price only)
    df["Open"] = df["Close"].shift(1).fillna(df["Close"])
    df["High"] = df[["Open", "Close"]].max(axis=1) * 1.002
    df["Low"] = df[["Open", "Close"]].min(axis=1) * 0.998
    df["Volume"] = 0.0
    return df[["Open", "High", "Low", "Close", "Volume"]]


def load_or_fetch() -> pd.DataFrame:
    if CACHE.exists():
        df = pd.read_csv(CACHE, parse_dates=["Date"], index_col="Date")
        if df.index.tz is None:
            df.index = df.index.tz_localize("UTC")
        return df
    df = fetch_daily()
    df.to_csv(CACHE)
    return df


if __name__ == "__main__":
    df = load_or_fetch()
    print(f"Cached {len(df)} days: {df.index[0].date()} → {df.index[-1].date()} → {CACHE}")
