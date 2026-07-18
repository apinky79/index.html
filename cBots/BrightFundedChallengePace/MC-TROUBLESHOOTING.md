# Why Pace shows MC PF / Final score as `-`

Strategy Selector fills **MC PF, MC median return, MC P95 min return, Final score, Autoscore** only when it can build a **per-pass list of trade returns** from the **events zip**.

If that list is empty for every pass → those columns stay **-**.

MC DD can sometimes still show a number from other optres fields, which is why you saw **9% DD** while MC PF stayed `-`.

---

## 1. Confirm you loaded BOTH files together

In Strategy Selector you must load:

1. `Something.optres`
2. The **matching** `Something.zip` (same opt, same timestamp)

If the zip is missing, from a different opt, or empty → MC columns dash out.

---

## 2. Inspect the Pace zip on your Mac

In Terminal:

```bash
# Pick your Pace zip path
ZIP="/Users/adampink/Desktop/Optimisation/YOUR-PACE-OPT.zip"
DIR="/tmp/pace-events-check"
rm -rf "$DIR" && mkdir -p "$DIR" && unzip -q "$ZIP" -d "$DIR"
echo "=== Top level ==="
ls -la "$DIR" | head -40
echo "=== Find events files ==="
find "$DIR" -iname '*event*' | head -40
echo "=== Count json files ==="
find "$DIR" -name '*.json' | wc -l
```

### What good looks like (Ultimate Trader opts that worked)
- Many pass folders / pass ids
- Each pass has an `events.json` (or similar) with **closed trades**
- Those trades include a profit field (`netProfit`, `pnl`, `grossProfit`, etc.)

### What broken looks like
- Zip almost empty
- json files exist but **no closed trades**
- Only “order placed” events, no closes
- Pass ids in zip **don’t match** pass ids in `.optres`

---

## 3. Peek inside one events file

```bash
# After unzip above:
find "$DIR" -name '*.json' | head -5
# Then:
head -c 2000 "$(find "$DIR" -name '*.json' | head -1)"
```

Check:
- Is there an array of trades/positions?
- Do you see **net profit** (or equivalent) on closed trades?
- Is `label` / comment **`BF_PACE`**?

Compare to a **working Ultimate Trader** zip:

```bash
ZIP_UT="/Users/adampink/Desktop/Optimisation/Ultimate trader - 28.06 1754.zip"
DIR_UT="/tmp/ut-events-check"
rm -rf "$DIR_UT" && mkdir -p "$DIR_UT" && unzip -q "$ZIP_UT" -d "$DIR_UT"
find "$DIR_UT" -name '*.json' | head -5
head -c 2000 "$(find "$DIR_UT" -name '*.json' | head -1)"
```

Side-by-side differences usually reveal the bug in minutes.

---

## 4. Common causes (Pace-specific)

| Cause | What you’ll see | Fix |
|---|---|---|
| Wrong/missing zip | All MC fields `-` | Re-export optres **and** zip from same finished opt |
| Empty trade events | json has no closes | Re-run opt; ensure trades actually close (TP/SL hit) |
| Pass id mismatch | Folders don’t match optres passes | Re-save results from cTrader after opt completes |
| Label mismatch | Events have different label than expected | Keep Bot Trade ID = `BF_PACE` for whole opt |
| Selector only parses old robot event shape | UT json has fields Pace json lacks | Paste one Pace `events.json` + one UT `events.json` here to patch Selector |

---

## 5. Quick app-side check

After upload in Strategy Selector:

- **Trades** column has numbers? → optres metrics work  
- **MC PF / median / final score** all `-`? → returns extraction failed  
- Filter Inspector still counts MC DD fails? → DD may come from optres drawdown fields, **not** full MC — don’t trust that alone when MC PF is `-`

---

## 6. What to send back (so we can patch)

Paste:

1. Output of the `ls` / `find` commands for **Pace** zip  
2. First ~50 lines of one Pace `events.json`  
3. First ~50 lines of one **Ultimate Trader** `events.json` that *did* show MC PF  

With that, we can see whether to fix export steps or update Strategy Selector’s `extractTradeReturnsFromEvents` parser for Pace.

---

## Meanwhile (challenge decision)

Until MC PF populates:

- **Do not** use Selector Top Pick / Final score for Pace  
- You may shortlist in **cTrader** by Net profit + PF + Max equity DD %  
- Forward-test before challenge use  
- Prefer passes with DD ≤ 8% and PF ≥ 1.2 in cTrader  

But the real fix is getting events → returns → MC working again.
