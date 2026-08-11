#!/usr/bin/env python3
"""Build SMC_Testing_Share_Guide.pdf for sharing."""

from pathlib import Path

from fpdf import FPDF

OUT = Path(__file__).resolve().parent / "SMC_Testing_Share_Guide.pdf"


class Guide(FPDF):
    def header(self):
        if self.page_no() == 1:
            return
        self.set_font("Helvetica", "I", 9)
        self.set_text_color(100, 100, 100)
        self.cell(0, 8, "SMC Testing - Side Experiment Guide", align="L")
        self.ln(4)
        self.set_draw_color(200, 200, 200)
        self.line(10, self.get_y(), 200, self.get_y())
        self.ln(6)

    def footer(self):
        self.set_y(-15)
        self.set_font("Helvetica", "I", 8)
        self.set_text_color(120, 120, 120)
        self.cell(
            0,
            10,
            f"Page {self.page_no()}/{{nb}}  |  Side experiment only - does not replace UltimateTrader / Test G",
            align="C",
        )

    def h1(self, text):
        self.set_font("Helvetica", "B", 20)
        self.set_text_color(20, 30, 40)
        self.multi_cell(0, 10, text)
        self.ln(2)

    def h2(self, text):
        self.ln(4)
        self.set_font("Helvetica", "B", 13)
        self.set_text_color(25, 55, 85)
        self.multi_cell(0, 8, text)
        self.ln(1)

    def body(self, text):
        self.set_x(self.l_margin)
        self.set_font("Helvetica", "", 11)
        self.set_text_color(35, 35, 35)
        self.multi_cell(0, 6, text)
        self.ln(1)

    def bullet(self, text):
        self.set_x(self.l_margin)
        self.set_font("Helvetica", "", 11)
        self.set_text_color(35, 35, 35)
        indent = 8
        self.cell(indent, 6, "-")
        # Keep remaining width so multi_cell does not start past the right margin
        self.multi_cell(self.w - self.r_margin - self.get_x(), 6, text)
        self.set_x(self.l_margin)

    def note_box(self, title, text):
        self.ln(2)
        self.set_x(self.l_margin)
        self.set_font("Helvetica", "B", 11)
        self.set_text_color(25, 55, 85)
        self.multi_cell(0, 6, title)
        self.set_x(self.l_margin)
        self.set_font("Helvetica", "", 10)
        self.set_text_color(40, 40, 40)
        self.multi_cell(0, 5.5, text)
        self.ln(2)

    def code(self, text):
        self.set_x(self.l_margin)
        self.set_font("Courier", "", 10)
        self.set_text_color(30, 30, 30)
        self.multi_cell(0, 5, text)
        self.ln(1)


def main():
    pdf = Guide()
    pdf.alias_nb_pages()
    pdf.set_auto_page_break(auto=True, margin=18)
    pdf.add_page()

    pdf.h1("SMC Testing - Share Guide")
    pdf.set_font("Helvetica", "", 11)
    pdf.set_text_color(80, 80, 80)
    pdf.multi_cell(
        0,
        6,
        "cTrader side experiment for BTCUSD (m15) with H4 bias.\n"
        "Not a replacement for live Challenge stack (UltimateTrader + ADX H4 / Test G).",
    )
    pdf.ln(3)

    pdf.h2("1. What the bot does")
    pdf.body("Core flow (price-action SMC on OHLC):")
    pdf.bullet("H4 Break of Structure (BOS) sets directional bias")
    pdf.bullet("m15 liquidity sweep")
    pdf.bullet("Change of Character (CHoCH)")
    pdf.bullet("Order-block retest entry")
    pdf.body("Also includes week drawdown brake (default 3.5%) and dollar risk sizing.")

    pdf.h2("2. News pause")
    pdf.body("Blocks new entries around high-impact events. Open trades are left alone.")
    pdf.bullet("Enabled by default")
    pdf.bullet("Pause before: 90 minutes")
    pdf.bullet("Resume after: 45 minutes")
    pdf.bullet("Event toggles: NFP / FOMC / CPI / Core PCE")
    pdf.bullet("Built-in approximate calendar + Extra Events UTC for exact times")
    pdf.body("Extra Events UTC format:")
    pdf.code("yyyy-MM-dd HH:mm|Title;yyyy-MM-dd HH:mm|Title")
    pdf.code("Example: 2026-07-29 18:00|FOMC;2026-08-01 12:30|CPI")

    pdf.h2("3. Optimisable SL and TP")
    pdf.body("Same ranges as the G weekly hunt (set step in Optimizer UI):")
    pdf.bullet("SL Mode: Percent (default) or OrderBlock")
    pdf.bullet("SL Percent: optimise 0.6 to 1.0, step 0.1")
    pdf.bullet("TP Risk Multiplier: optimise 1.8 to 2.8, step 0.2")
    pdf.body(
        "In Optimizer: tick SL Percent and TP Risk Multiplier, "
        "set min/max/step, run Grid."
    )

    pdf.h2("4. Level-2 order book (DOM)")
    pdf.body("Optional imbalance filter using cTrader MarketData.GetMarketDepth.")
    pdf.bullet("Use Level-2 Imbalance Filter: OFF by default")
    pdf.bullet("DOM Levels to Sum: top N bids/asks (default 5)")
    pdf.bullet("Min Bid/Ask Imbalance Ratio: e.g. 1.2")
    pdf.body(
        "When ON: before entry, sums top N bid vs ask volume. "
        "Longs need bids dominating; shorts need asks dominating."
    )

    pdf.h2("5. What if Level-2 is not available?")
    pdf.note_box(
        "Nothing breaks - entries still use normal SMC rules.",
        "If the filter is OFF, DOM is never used. "
        "If the filter is ON but depth is empty (common on BTC/prop feeds and in all backtests), "
        'the bot logs BidLevels=0 AskLevels=0 and skips the check ("DOM empty - skipped"). '
        "Missing L2 does not block trades and does not change SL/TP. "
        "A real imbalance gate only applies live when the broker publishes Depth of Market on BTCUSD.",
    )

    pdf.h2("6. Install (cTrader)")
    pdf.bullet("Open cBot named SMC Testing in Automate")
    pdf.bullet("Ctrl+A, Delete (wipe the template completely)")
    pdf.bullet("Paste entire SMC_Testing.cs, then Build")
    pdf.bullet("Attach BTCUSD m15 - Bias timeframe Hour4")
    pdf.body(
        "CS1022 / CS0106 usually means the code was pasted inside leftover "
        "template code - wipe the file first."
    )

    pdf.h2("7. Live Challenge reminder")
    pdf.body(
        "Keep Challenge / funded live trading on UltimateTrader + ADX H4 (Test G) "
        "until this SMC experiment clearly beats G on the digger / green panel. "
        "Use this bot for side backtests and A/B weeks only."
    )

    pdf.ln(4)
    pdf.set_font("Helvetica", "I", 9)
    pdf.set_text_color(100, 100, 100)
    pdf.multi_cell(
        0,
        5,
        "Source: ctrader-bots/SmcBtcSideExperiment/SMC_Testing.cs\n"
        "Branch: cursor/smc-side-experiment-bot-1ccf",
    )

    pdf.output(str(OUT))
    print(f"Wrote {OUT} ({OUT.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
