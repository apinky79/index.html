#!/usr/bin/env python3
"""Generate polished West London U10 year-one cost guide PDF."""

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_JUSTIFY, TA_LEFT, TA_RIGHT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import inch
from reportlab.platypus import (
    HRFlowable,
    PageBreak,
    Paragraph,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)

OUTPUT = "/opt/cursor/artifacts/West_London_U10_Year_One_Cost_Guide_VERIFIED.pdf"

NAVY = colors.HexColor("#0B1F3A")
ACCENT = colors.HexColor("#1A7A52")
ACCENT_LIGHT = colors.HexColor("#E8F5EF")
SLATE = colors.HexColor("#5A6472")
BORDER = colors.HexColor("#D8DEE6")
PANEL = colors.HexColor("#F4F6F9")
WHITE = colors.white

LEAGUE_JOIN_SAMPLES = [
    ("Ealing & District YFL (EEYFL)", 65, "£15 club + £50 U8–U10 team fee"),
    ("Middlesex Youth Football League", 420, "Entry, subscription, deposit, player registration"),
    ("Surrey Primary League (U9–U10)", 40, "£40 team fee (players included)"),
]
LEAGUE_JOIN_AVG = round(sum(x[1] for x in LEAGUE_JOIN_SAMPLES) / len(LEAGUE_JOIN_SAMPLES))
COUNTY_FA_AND_PA = 30
ADMIN_LINE = COUNTY_FA_AND_PA + LEAGUE_JOIN_AVG

FULL_ITEMS = [
    ("County FA &amp; league entry (West London avg.)", ADMIN_LINE, "See section 2"),
    ("Public liability insurance", 200, "Typical annual club policy"),
    ("Training pitch (40 × £80)", 3200, "London 4G ~£45–£87/hr"),
    ("Home match pitch hire", 600, "~£30–£65 per home match"),
    ("Referees (20 × £25)", 500, "Typical U9/U10 London leagues"),
    ("Match balls (6)", 180, "Planning allowance"),
    ("Training balls (20)", 400, "Planning allowance"),
    ("Cones, bibs, poles, pump, bag", 250, "Planning allowance"),
    ("Portable goals", 300, "Planning allowance"),
    ("First aid &amp; ice packs", 100, "Planning allowance"),
    ("Team match kit (12 players)", 700, "Lower with sponsor"),
    ("Goalkeeper kit", 100, "Planning allowance"),
    ("Coach clothing", 150, "Optional"),
    ("End-of-season trophies", 250, "Optional"),
    ("Tournament entries", 300, "Optional year one"),
    ("Website / team app", 100, "Optional"),
]
FULL_TOTAL = sum(x[1] for x in FULL_ITEMS)

LEAN_ITEMS = [
    ("County FA &amp; league entry (avg.)", ADMIN_LINE),
    ("Insurance", 200),
    ("Training pitch (season deal / shared)", 1800),
    ("Home pitch", 300),
    ("Referees", 500),
    ("Essential equipment", 600),
    ("Kit (with sponsorship)", 200),
    ("First aid", 100),
    ("Coach clothing (minimal)", 50),
    ("Trophies", 150),
    ("Tournaments", 0),
    ("Team app", 0),
]
LEAN_LOW = sum(x[1] for x in LEAN_ITEMS)
LEAN_HIGH = LEAN_LOW + 50

PAGE_W, PAGE_H = A4
MARGIN = 0.72 * inch
CONTENT_W = PAGE_W - 2 * MARGIN


def money(n: float) -> str:
    if abs(n - round(n)) < 0.01:
        return f"£{int(round(n)):,}"
    return f"£{n:,.2f}"


def build_styles():
    s = getSampleStyleSheet()
    s.add(
        ParagraphStyle(
            name="CoverKicker",
            fontName="Helvetica-Bold",
            fontSize=9,
            leading=11,
            textColor=ACCENT_LIGHT,
            spaceAfter=8,
        )
    )
    s.add(
        ParagraphStyle(
            name="CoverTitle",
            fontName="Helvetica-Bold",
            fontSize=26,
            leading=30,
            textColor=WHITE,
            spaceAfter=6,
        )
    )
    s.add(
        ParagraphStyle(
            name="CoverSubtitle",
            fontName="Helvetica",
            fontSize=12,
            leading=16,
            textColor=colors.HexColor("#C5D3E0"),
            spaceAfter=0,
        )
    )
    s.add(
        ParagraphStyle(
            name="Section",
            fontName="Helvetica-Bold",
            fontSize=14,
            leading=18,
            textColor=NAVY,
            spaceBefore=16,
            spaceAfter=10,
        )
    )
    s.add(
        ParagraphStyle(
            name="Body",
            fontName="Helvetica",
            fontSize=10,
            leading=14,
            textColor=colors.HexColor("#1F2933"),
            alignment=TA_JUSTIFY,
            spaceAfter=8,
        )
    )
    s.add(
        ParagraphStyle(
            name="BodyBullet",
            fontName="Helvetica",
            fontSize=10,
            leading=14,
            leftIndent=14,
            bulletIndent=0,
            spaceAfter=4,
        )
    )
    s.add(
        ParagraphStyle(
            name="Small",
            fontName="Helvetica",
            fontSize=8,
            leading=11,
            textColor=SLATE,
            spaceAfter=6,
        )
    )
    s.add(
        ParagraphStyle(
            name="TOC",
            fontName="Helvetica",
            fontSize=10,
            leading=16,
            textColor=colors.HexColor("#1F2933"),
        )
    )
    s.add(
        ParagraphStyle(
            name="MetricValue",
            fontName="Helvetica-Bold",
            fontSize=18,
            leading=20,
            textColor=NAVY,
            alignment=TA_CENTER,
        )
    )
    s.add(
        ParagraphStyle(
            name="MetricLabel",
            fontName="Helvetica",
            fontSize=8,
            leading=10,
            textColor=SLATE,
            alignment=TA_CENTER,
        )
    )
    return s


ST = build_styles()


def draw_page_frame(canvas, doc):
    canvas.saveState()
    if doc.page == 1:
        canvas.restoreState()
        return

    canvas.setStrokeColor(BORDER)
    canvas.setLineWidth(0.5)
    canvas.line(MARGIN, PAGE_H - 0.62 * inch, PAGE_W - MARGIN, PAGE_H - 0.62 * inch)
    canvas.setFont("Helvetica", 8)
    canvas.setFillColor(SLATE)
    canvas.drawString(MARGIN, PAGE_H - 0.5 * inch, "West London U10  ·  Year One Cost Guide  ·  July 2026")
    canvas.drawRightString(PAGE_W - MARGIN, 0.42 * inch, f"Page {doc.page}")
    canvas.restoreState()


def section_heading(number: str, title: str):
    bar = Table(
        [[Paragraph(f"<font color='#1A7A52'>{number}</font>", ST["Body"]), Paragraph(title, ST["Section"])]],
        colWidths=[0.35 * inch, CONTENT_W - 0.35 * inch],
    )
    bar.setStyle(
        TableStyle(
            [
                ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
                ("LEFTPADDING", (0, 0), (-1, -1), 0),
                ("RIGHTPADDING", (0, 0), (-1, -1), 0),
                ("TOPPADDING", (0, 0), (-1, -1), 0),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 0),
            ]
        )
    )
    return bar


def data_table(rows, col_widths, header_rows=1, total_row=None):
    t = Table(rows, colWidths=col_widths, repeatRows=header_rows)
    style = [
        ("FONTNAME", (0, 0), (-1, header_rows - 1), "Helvetica-Bold"),
        ("FONTSIZE", (0, 0), (-1, -1), 9),
        ("TEXTCOLOR", (0, 0), (-1, header_rows - 1), WHITE),
        ("BACKGROUND", (0, 0), (-1, header_rows - 1), NAVY),
        ("TEXTCOLOR", (0, header_rows), (-1, -1), colors.HexColor("#1F2933")),
        ("GRID", (0, 0), (-1, -1), 0.25, BORDER),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("LEFTPADDING", (0, 0), (-1, -1), 8),
        ("RIGHTPADDING", (0, 0), (-1, -1), 8),
        ("TOPPADDING", (0, 0), (-1, -1), 6),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
    ]
    if len(rows) > header_rows + 1:
        for i in range(header_rows, len(rows)):
            if i % 2 == 0:
                style.append(("BACKGROUND", (0, i), (-1, i), PANEL))
    if total_row is not None:
        style.extend(
            [
                ("BACKGROUND", (0, total_row), (-1, total_row), ACCENT_LIGHT),
                ("FONTNAME", (0, total_row), (-1, total_row), "Helvetica-Bold"),
                ("LINEABOVE", (0, total_row), (-1, total_row), 1, ACCENT),
            ]
        )
    # Right-align money columns when 2+ columns
    if len(col_widths) >= 2:
        for col in range(1, min(3, len(col_widths))):
            style.append(("ALIGN", (col, header_rows), (col, -1), "RIGHT"))
    t.setStyle(TableStyle(style))
    return t


def metric_cards():
    cards = [
        [Paragraph(money(FULL_TOTAL), ST["MetricValue"]), Paragraph("Full season budget", ST["MetricLabel"])],
        [Paragraph(money(LEAN_LOW), ST["MetricValue"]), Paragraph("Lean launch target", ST["MetricLabel"])],
        [Paragraph(money(ADMIN_LINE), ST["MetricValue"]), Paragraph("County FA + league (avg.)", ST["MetricLabel"])],
    ]
    t = Table(cards, colWidths=[CONTENT_W / 3.0] * 3)
    t.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, -1), PANEL),
                ("BOX", (0, 0), (-1, -1), 0.5, BORDER),
                ("INNERGRID", (0, 0), (-1, -1), 0.5, BORDER),
                ("TOPPADDING", (0, 0), (-1, -1), 12),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 12),
                ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
            ]
        )
    )
    return t


def cover_block():
    hero = Table(
        [
            [Paragraph("GRASSROOTS PLANNING GUIDE", ST["CoverKicker"])],
            [Paragraph("West London U10 Football Club", ST["CoverTitle"])],
            [Paragraph("Year One Cost Guide &amp; Savings Opportunities", ST["CoverSubtitle"])],
        ],
        colWidths=[CONTENT_W],
    )
    hero.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, -1), NAVY),
                ("LEFTPADDING", (0, 0), (-1, -1), 16),
                ("RIGHTPADDING", (0, 0), (-1, -1), 16),
                ("TOPPADDING", (0, 0), (-1, -1), 32),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 26),
                ("LINEBELOW", (0, 0), (-1, 0), 3, ACCENT),
            ]
        )
    )
    return hero


def build_pdf():
    doc = SimpleDocTemplate(
        OUTPUT,
        pagesize=A4,
        rightMargin=MARGIN,
        leftMargin=MARGIN,
        topMargin=0.55 * inch,
        bottomMargin=0.75 * inch,
        title="West London U10 Year One Cost Guide",
        author="West London U10 Planning Guide",
    )
    story = []

    story.append(Spacer(1, 0.05 * inch))
    story.append(cover_block())
    story.append(Spacer(1, 0.35 * inch))
    story.append(metric_cards())
    story.append(Spacer(1, 0.28 * inch))
    story.append(
        Paragraph(
            "<b>Executive summary.</b> This guide budgets one volunteer-run U10 team in West London "
            f"(10–12 players, weekly training, weekend league). League and County FA costs use the "
            f"<b>average of published local league fees</b> ({money(LEAGUE_JOIN_AVG)} + {money(COUNTY_FA_AND_PA)} "
            f"= {money(ADMIN_LINE)}). All figures are planning-grade — confirm invoices with your chosen league before sharing with parents.",
            ST["Body"],
        )
    )
    story.append(Spacer(1, 0.12 * inch))
    meta = Table(
        [
            ["Prepared", "July 2026"],
            ["Scope", "First season · one team · West London leagues"],
            ["Method", "Published league tariffs + verified line-item totals"],
        ],
        colWidths=[1.1 * inch, CONTENT_W - 1.1 * inch],
    )
    meta.setStyle(
        TableStyle(
            [
                ("FONTNAME", (0, 0), (0, -1), "Helvetica-Bold"),
                ("FONTSIZE", (0, 0), (-1, -1), 9),
                ("TEXTCOLOR", (0, 0), (0, -1), SLATE),
                ("TEXTCOLOR", (1, 0), (1, -1), colors.HexColor("#1F2933")),
                ("LINEBELOW", (0, 0), (-1, -2), 0.25, BORDER),
                ("TOPPADDING", (0, 0), (-1, -1), 5),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
            ]
        )
    )
    story.append(meta)
    story.append(PageBreak())

    story.append(section_heading("", "Contents"))
    for line in [
        "1. At a glance",
        "2. West London U10 league join costs",
        "3. Planning assumptions",
        "4. Full-specification budget",
        "5. Lean launch budget",
        "6. Launch scenarios &amp; savings",
        "7. Coach compliance &amp; sources",
    ]:
        story.append(Paragraph(line, ST["TOC"]))
    story.append(Spacer(1, 0.2 * inch))
    story.append(HRFlowable(width="100%", thickness=0.5, color=BORDER, spaceAfter=12))

    story.append(section_heading("1", "At a glance"))
    glance = [
        ["Launch scenario", "Indicative total", "Per player / month (12 squad, 11 months)"],
        ["Lean launch", money(lean := LEAN_LOW), money(lean / 12 / 11)],
        ["Standard (recommended planning range)", "£5,400 – £6,100", "£41 – £46"],
        ["Full specification", money(FULL_TOTAL), money(FULL_TOTAL / 12 / 11)],
    ]
    story.append(data_table(glance, [2.0 * inch, 1.25 * inch, 2.0 * inch]))

    story.append(section_heading("2", "West London U10 league join costs"))
    story.append(
        Paragraph(
            "Club-to-league costs differ from parent subscriptions. The table below uses published 2025/26 fee pages "
            "and handbooks for leagues commonly used by West London grassroots clubs.",
            ST["Body"],
        )
    )
    league_rows = [["League", "One U10 team", "Basis"]] + [
        [n, money(c), note] for n, c, note in LEAGUE_JOIN_SAMPLES
    ]
    league_rows.append(["Average used in this guide", money(LEAGUE_JOIN_AVG), "Mean of samples above"])
    tr = len(league_rows) - 1
    story.append(data_table(league_rows, [2.0 * inch, 0.95 * inch, 2.3 * inch], total_row=tr))
    story.append(
        Paragraph(
            f"<b>County FA (planning):</b> {money(COUNTY_FA_AND_PA)} per youth team (affiliation + mandatory personal accident). "
            f"<b>Combined budget line:</b> {money(ADMIN_LINE)}. Public liability insurance is budgeted separately at {money(200)}.",
            ST["Body"],
        )
    )

    story.append(section_heading("3", "Planning assumptions"))
    for b in [
        "One U10 squad of 10–12 players; one volunteer head coach.",
        "40 weekly training sessions and approximately 20 home league fixtures.",
        "West London or adjacent Middlesex / Surrey-border league.",
        "Parent subscriptions fund costs unless sponsorship or grants are secured.",
    ]:
        story.append(Paragraph(f"• {b}", ST["BodyBullet"]))

    story.append(section_heading("4", "Full-specification year-one budget"))
    budget_rows = [["Item", "Cost", "Notes"]] + [
        [a, money(b), c] for a, b, c in FULL_ITEMS
    ]
    budget_rows.append(["Total (full specification)", money(FULL_TOTAL), ""])
    tr = len(budget_rows) - 1
    story.append(data_table(budget_rows, [2.35 * inch, 0.85 * inch, 2.05 * inch], total_row=tr))

    pp = [["Squad", "Per player / season", "Per player / month (11 months)"]]
    for n in (10, 12, 14):
        pp.append([f"{n} players", money(FULL_TOTAL / n), money(FULL_TOTAL / n / 11)])
    story.append(Spacer(1, 10))
    story.append(Paragraph("<b>Break-even subscriptions (full budget)</b>", ST["Body"]))
    story.append(data_table(pp, [1.4 * inch, 1.55 * inch, 2.3 * inch]))

    story.append(PageBreak())

    story.append(section_heading("5", "Lean launch budget"))
    story.append(
        Paragraph(
            f"Target total <b>{money(LEAN_LOW)}</b> (up to {money(LEAN_HIGH)} with a small paid app). "
            f"At 12 players: {money(LEAN_LOW / 12)}–{money(LEAN_HIGH / 12)} per season.",
            ST["Body"],
        )
    )
    lean_rows = [["Item", "Cost"]] + [[a, money(b)] for a, b in LEAN_ITEMS]
    lean_rows.append(["Total", money(LEAN_LOW)])
    tr = len(lean_rows) - 1
    story.append(data_table(lean_rows, [4.35 * inch, 1.0 * inch], total_row=tr))

    story.append(section_heading("6", "Launch scenarios &amp; highest-impact savings"))
    scenarios = [
        ["Scenario", "Total", "When to use"],
        ["Lean launch", f"{money(LEAN_LOW)} – {money(LEAN_HIGH + 250)}", "Shared pitch, sponsorship, defer extras"],
        ["Standard", "£5,400 – £6,100", "Balanced quality and sustainability"],
        ["Full specification", money(FULL_TOTAL), "All optional lines included"],
    ]
    story.append(data_table(scenarios, [1.25 * inch, 1.35 * inch, 2.75 * inch]))
    story.append(Spacer(1, 8))
    savings = [
        ["Area", "Typical", "Optimised", "Strategy"],
        ["Training pitch", "£3,200", "~£1,800", "Season booking or shared facility"],
        ["Match kit", "£700", "~£200", "Secure sponsor before ordering"],
        ["Equipment", "£1,130", "~£600", "Essentials only in year one"],
        ["Website / app", "£100", "£0", "Spond, WhatsApp, social media"],
        ["Tournaments", "£300", "£0", "Introduce from year two"],
    ]
    story.append(data_table(savings, [1.05 * inch, 0.72 * inch, 0.72 * inch, 2.86 * inch]))

    story.append(section_heading("7", "Coach compliance (one-off) &amp; recommendations"))
    coach = [
        ["Requirement", "Typical cost"],
        ["Introduction to Coaching Football", "£24–£160"],
        ["Safeguarding Children", "~£30"],
        ["Introduction to First Aid", "~£30"],
        ["Enhanced DBS (volunteer)", "~£10"],
    ]
    story.append(data_table(coach, [3.4 * inch, 1.95 * inch]))
    story.append(Spacer(1, 8))
    for b in [
        "Confirm league and County FA invoices before setting parent fees.",
        "Do not cut safeguarding, insurance, safe pitches, or adequate footballs.",
        "Secure kit sponsorship before placing orders.",
        f"Use {money(FULL_TOTAL)} as a ceiling; plan day-to-day around £5,400–£6,000 unless lean launch is intentional.",
    ]:
        story.append(Paragraph(f"• {b}", ST["BodyBullet"]))

    story.append(Spacer(1, 14))
    story.append(Paragraph("<b>Sources</b>", ST["Body"]))
    story.append(
        Paragraph(
            "EEYFL (eeyfl.co.uk/register) · Middlesex Youth FL Fees Tariff · Surrey Primary League registrations · "
            "Middlesex FA &amp; London FA affiliation guides · SELKENT / NE Hampshire YFL referee fee schedules. "
            "Re-quote each season at league AGM.",
            ST["Small"],
        )
    )

    doc.build(story, onFirstPage=draw_page_frame, onLaterPages=draw_page_frame)
    print(OUTPUT)


if __name__ == "__main__":
    build_pdf()
