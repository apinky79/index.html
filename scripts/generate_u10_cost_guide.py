#!/usr/bin/env python3
"""Generate verified West London U10 year-one cost guide PDF."""

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_JUSTIFY
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

# Published West London / adjacent league costs for one U10 team (2025/26 handbooks & fee pages).
LEAGUE_JOIN_SAMPLES = [
    ("Ealing & District YFL (EEYFL)", 65, "£15 club subscription + £50 U8–U10 team fee"),
    ("Middlesex Youth Football League", 420, "£50 entry + £150 subscription + £100 deposit + £10 × 12 players"),
    ("Surrey Primary League (U9–U10)", 40, "£40 team fee (players included)"),
]
LEAGUE_JOIN_AVG = round(sum(x[1] for x in LEAGUE_JOIN_SAMPLES) / len(LEAGUE_JOIN_SAMPLES))  # £175

# County FA team affiliation + mandatory youth personal accident (London / Middlesex planning average).
COUNTY_FA_AND_PA = 30

ADMIN_LINE = COUNTY_FA_AND_PA + LEAGUE_JOIN_AVG  # £205

styles = getSampleStyleSheet()
styles.add(
    ParagraphStyle(
        name="CoverTitle",
        parent=styles["Title"],
        fontSize=22,
        leading=26,
        alignment=TA_CENTER,
        spaceAfter=12,
    )
)
styles.add(
    ParagraphStyle(
        name="CoverSub",
        parent=styles["Heading2"],
        fontSize=14,
        alignment=TA_CENTER,
        textColor=colors.HexColor("#333333"),
        spaceAfter=6,
    )
)
styles.add(
    ParagraphStyle(
        name="BodyJustify",
        parent=styles["BodyText"],
        alignment=TA_JUSTIFY,
        fontSize=10,
        leading=14,
    )
)
styles.add(
    ParagraphStyle(
        name="SmallGrey",
        parent=styles["BodyText"],
        fontSize=8,
        textColor=colors.grey,
        leading=10,
    )
)
styles.add(
    ParagraphStyle(
        name="H2",
        parent=styles["Heading2"],
        fontSize=13,
        spaceBefore=14,
        spaceAfter=8,
    )
)


def money(n: float) -> str:
    if abs(n - round(n)) < 0.01:
        return f"£{int(round(n)):,}"
    return f"£{n:,.2f}"


def table(data, col_widths, header_rows=1):
    t = Table(data, colWidths=col_widths, repeatRows=header_rows)
    t.setStyle(
        TableStyle(
            [
                ("GRID", (0, 0), (-1, -1), 0.4, colors.grey),
                ("BACKGROUND", (0, 0), (-1, header_rows - 1), colors.HexColor("#E8E8E8")),
                ("FONTNAME", (0, 0), (-1, header_rows - 1), "Helvetica-Bold"),
                ("FONTSIZE", (0, 0), (-1, -1), 9),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 6),
                ("RIGHTPADDING", (0, 0), (-1, -1), 6),
                ("TOPPADDING", (0, 0), (-1, -1), 4),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
            ]
        )
    )
    return t


def build_pdf():
    doc = SimpleDocTemplate(
        OUTPUT,
        pagesize=A4,
        rightMargin=0.75 * inch,
        leftMargin=0.75 * inch,
        topMargin=0.75 * inch,
        bottomMargin=0.75 * inch,
    )
    story = []

    story.append(Spacer(1, 1.2 * inch))
    story.append(Paragraph("West London U10 Football Club", styles["CoverTitle"]))
    story.append(Paragraph("Year One Cost Guide &amp; Savings Opportunities", styles["CoverSub"]))
    story.append(Spacer(1, 0.3 * inch))
    story.append(
        Paragraph(
            "<i>Verified planning document — July 2026</i><br/>"
            "One volunteer coach · One team · 10–12 players · Weekly training · Weekend league",
            styles["CoverSub"],
        )
    )
    story.append(Spacer(1, 0.5 * inch))
    story.append(
        Paragraph(
            "League and County FA costs use the <b>average of published West London-area U10 league fees</b> "
            f"(£{LEAGUE_JOIN_AVG} league layer + £{COUNTY_FA_AND_PA} affiliation/PA insurance ≈ {money(ADMIN_LINE)} budget line). "
            "All totals are recalculated from line items.",
            styles["BodyJustify"],
        )
    )
    story.append(PageBreak())

    story.append(Paragraph("1. West London U10 league join costs (research)", styles["H2"]))
    story.append(
        Paragraph(
            "To join competitive grassroots football you pay your <b>County FA</b> (team affiliation and mandatory "
            "personal accident cover) and your <b>league</b> (entry, subscription, deposits, and sometimes per-player "
            "registration). Parent fees charged by existing clubs are not the same as these club-to-league costs.",
            styles["BodyJustify"],
        )
    )
    story.append(Spacer(1, 8))
    league_rows = [["League (U10 / west London area)", "One-team cost", "Notes"]]
    for name, cost, note in LEAGUE_JOIN_SAMPLES:
        league_rows.append([name, money(cost), note])
    league_rows.append(
        [
            "<b>Average used in this guide</b>",
            f"<b>{money(LEAGUE_JOIN_AVG)}</b>",
            "Mean of the three published samples above",
        ]
    )
    story.append(table(league_rows, [1.55 * inch, 0.85 * inch, 2.8 * inch]))
    story.append(Spacer(1, 8))
    story.append(
        Paragraph(
            f"<b>County FA layer (planning):</b> about {money(COUNTY_FA_AND_PA)} per youth team — Middlesex FA youth team fee "
            "£12 + basic PA cover ~£17; London FA youth team ~£12.50 + bronze PA ~£15 (2023/24–2024/25 published rates). "
            f"<b>Combined admin line in the budget:</b> {money(ADMIN_LINE)}. "
            "Public liability insurance remains a separate £200 line — leagues require it but it is not part of league entry.",
            styles["BodyJustify"],
        )
    )
    story.append(Spacer(1, 12))
    story.append(HRFlowable(width="100%", thickness=0.5, color=colors.grey))

    story.append(Paragraph("2. Planning assumptions", styles["H2"]))
    for bullet in [
        "One U10 squad of 10–12 players, one volunteer head coach, no paid coaching staff.",
        "40 weekly training sessions; ~20 home league matches.",
        "Competing in a West London or adjacent Middlesex / Surrey border league.",
        "Parent subscriptions fund the team unless sponsorship or grants are secured.",
    ]:
        story.append(Paragraph(f"• {bullet}", styles["BodyJustify"]))

    full_items = [
        (
            "County FA &amp; league entry (West London avg.)",
            ADMIN_LINE,
            f"£{COUNTY_FA_AND_PA} County FA + £{LEAGUE_JOIN_AVG} avg league (see section 1)",
        ),
        ("Public liability insurance", 200, "Typical annual club policy"),
        ("Training pitch (40 × £80)", 3200, "Planning — London 4G hire often ~£45–£87/hr"),
        ("Home match pitch hire", 600, "Planning — ~£30–£65 per home match"),
        ("Referees (20 × £25)", 500, "Typical — SELKENT U9/U10 £20; many leagues £25"),
        ("Match balls (6)", 180, "Planning allowance"),
        ("Training balls (20)", 400, "Planning allowance"),
        ("Cones, bibs, poles, pump, bag", 250, "Planning allowance"),
        ("Portable goals", 300, "Planning allowance"),
        ("First aid &amp; ice packs", 100, "Planning allowance"),
        ("Team match kit (12 players)", 700, "Planning — lower with sponsor"),
        ("Goalkeeper kit", 100, "Planning allowance"),
        ("Coach clothing", 150, "Optional"),
        ("End-of-season trophies", 250, "Optional"),
        ("Tournament entries", 300, "Optional year one"),
        ("Website / team app", 100, "Optional — free tools available"),
    ]
    full_total = sum(x[1] for x in full_items)

    story.append(Paragraph("3. Full-specification year-one budget", styles["H2"]))
    data = [["Item", "Cost (£)", "Basis"]] + [[a, money(b), c] for a, b, c in full_items]
    data.append(["<b>Total (full specification)</b>", f"<b>{money(full_total)}</b>", ""])
    story.append(table(data, [2.35 * inch, 0.85 * inch, 2.9 * inch]))
    story.append(Spacer(1, 10))

    pp = [["Squad size", "Per player / season", "Per player / month (11 months)"]]
    for n in (10, 12, 14):
        pp.append([str(n), money(full_total / n), money(full_total / n / 11)])
    story.append(Paragraph("Break-even subscriptions (full budget)", styles["H2"]))
    story.append(table(pp, [1.3 * inch, 1.5 * inch, 1.9 * inch]))

    story.append(PageBreak())

    lean_items = [
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
    lean_low = sum(x[1] for x in lean_items)
    lean_high = lean_low + 50

    story.append(Paragraph("4. Lean launch budget (cost-optimised)", styles["H2"]))
    story.append(
        Paragraph(
            f"Total: <b>{money(lean_low)}</b> (up to {money(lean_high)} with a small paid app). "
            f"With 12 players: {money(lean_low / 12)}–{money(lean_high / 12)} per season "
            f"(~{money(lean_low / 12 / 11)}–{money(lean_high / 12 / 11)} per month).",
            styles["BodyJustify"],
        )
    )
    lean_data = [["Item", "Cost (£)"]] + [[a, money(b)] for a, b in lean_items]
    lean_data.append(["<b>Total</b>", f"<b>{money(lean_low)}</b>"])
    story.append(table(lean_data, [4.2 * inch, 1.1 * inch]))

    story.append(Spacer(1, 12))
    story.append(Paragraph("5. Three launch scenarios", styles["H2"]))
    scenarios = [
        ["Scenario", "Indicative total", "When to use"],
        ["Lean launch", f"{money(lean_low)} – {money(lean_high + 250)}", "Sponsorship, shared pitch, defer tournaments"],
        ["Standard", "£5,400 – £6,100", "Balanced quality; mid-range training hire"],
        ["Full specification", money(full_total), "All optional lines included in section 3"],
    ]
    story.append(table(scenarios, [1.2 * inch, 1.2 * inch, 2.8 * inch]))

    story.append(Spacer(1, 12))
    story.append(Paragraph("6. Highest-impact savings", styles["H2"]))
    savings = [
        ["Area", "Typical", "Optimised", "Approach"],
        ["Training pitch", "£3,200", "~£1,800", "Season booking, school site, or share with another team"],
        ["Match kit", "£700", "~£200", "Local sponsor before ordering shirts"],
        ["Equipment", "£1,130", "~£600", "Essentials only in year one"],
        ["Website", "£100", "£0", "Spond, WhatsApp, social media"],
        ["Tournaments", "£300", "£0", "Add from year two"],
    ]
    story.append(table(savings, [1.1 * inch, 0.75 * inch, 0.75 * inch, 2.6 * inch]))

    story.append(Spacer(1, 12))
    story.append(Paragraph("7. One-off coach compliance (not in team budget)", styles["H2"]))
    coach = [
        ["Item", "Typical cost"],
        ["Introduction to Coaching Football", "£24–£160 (funded places often ~£24–£30)"],
        ["Safeguarding Children", "~£30"],
        ["Introduction to First Aid", "~£30"],
        ["Enhanced DBS (volunteer)", "~£10"],
    ]
    story.append(table(coach, [3.2 * inch, 1.2 * inch]))

    story.append(Spacer(1, 12))
    story.append(Paragraph("8. Recommendations", styles["H2"]))
    for bullet in [
        "Confirm exact league and County FA invoices before publishing parent fees.",
        "Do not cut: safeguarding, insurance, safe pitch, and adequate footballs.",
        "Secure kit sponsorship before placing kit orders.",
        f"Treat {money(full_total)} as the “everything included” ceiling; target £5,400–£6,000 for a sensible first season.",
    ]:
        story.append(Paragraph(f"• {bullet}", styles["BodyJustify"]))

    story.append(Spacer(1, 16))
    story.append(Paragraph("Sources", styles["H2"]))
    story.append(
        Paragraph(
            "EEYFL member fees: eeyfl.co.uk/register. Middlesex Youth FL rules &amp; Fees Tariff (Schedule A). "
            "Surrey Primary League team registrations. Middlesex FA &amp; London FA affiliation guides (team fees and PA insurance). "
            "Referee fees: SELKENT / NE Hampshire YFL handbooks. Re-quote annually — leagues often freeze or adjust fees at AGM.",
            styles["SmallGrey"],
        )
    )

    doc.build(story)
    print(OUTPUT)


if __name__ == "__main__":
    build_pdf()
