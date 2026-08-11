# Morning Health Shortcut — iPhone setup

One tap each morning copies Apple Health metrics to your clipboard in the readiness template format. Paste into the readiness app or Cursor chat.

**What Apple Health can fill automatically:** HRV, resting HR, sleep duration, VO2 max  
**What you still add from apps:** Sleep score (Sleep Cycle/Bevel), HRV CV, Recovery, Stress, Strain, how you feel

---

## Before you start

1. Open the **Shortcuts** app (install from App Store if needed)
2. Ensure **Bevel / HRV app / Apple Watch** write to Health:
   - iPhone **Settings → Health → Data Access & Devices**
   - Tap each app → turn on **Heart Rate**, **HRV**, **Sleep**, etc.

---

## Build the shortcut (about 5 minutes)

### Step 1 — Create shortcut

1. Shortcuts → **+** (top right)
2. Tap **Add Action**
3. Search **“Set Variable”** → add it
4. Name the variable: `output`
5. In the value field, paste this template exactly:

```
Sleep score:
HRV:
7-day HRV:
HRV CV:
Recovery:
Resting HR:
Stress:
Yesterday's strain:
How I feel:

--- Apple Health ---
```

6. Tap **Done** on the keyboard

---

### Step 2 — Get latest HRV

1. **+** Add Action → search **“Find Health Samples”**
2. Tap **Type** → **Heart Rate Variability**
3. Tap **Sort By** → **Start Date** → **Latest First**
4. Set **Limit** → **1**
5. Add Action → **“Get Details of Health Samples”**
6. Detail → **Value**
7. Add Action → **“Number”** → format: **0.1** (one decimal)
8. Add Action → **“Text”** → content:

```
HRV: [Number] ms
```

(Select the Number variable where it says `[Number]`)

9. Add Action → **“Set Variable”** → Variable: `hrvLine` → Value: the Text above

---

### Step 3 — Get resting heart rate

1. Add **Find Health Samples**
   - Type: **Resting Heart Rate**
   - Sort: Start Date, Latest First
   - Limit: **1**
2. Add **Get Details of Health Samples** → **Value**
3. Add **Number** (0 decimal places)
4. Add **Text**:

```
Resting HR: [Number] bpm
```

5. **Set Variable** → `rhrLine`

---

### Step 4 — Get last night’s sleep (hours)

1. Add **Find Health Samples**
   - Type: **Sleep Analysis**
   - Sort: Start Date, Latest First
   - Limit: **20** (gets last night’s segments)
2. Add **Filter** (tap Filter on the Health Samples)
   - Sample’s **Value** **is not** **Awake**
   - (Optional second filter: Value **is not** **In Bed** if you get double-counting)
3. Add **Get Details of Health Samples** → **Duration**
4. Add **Calculate Statistics** → Operation: **Sum**  
   *(If you don’t see this, use “Get Numbers from Input” then skip to manual sum — see Troubleshooting)*
5. Add **Format Duration** (or convert seconds to hours manually):
   - Alternative: Add **Number** with `Duration ÷ 3600` for hours
6. Add **Text**:

```
Sleep duration: [hours]h ([Formatted Duration])
```

7. **Set Variable** → `sleepLine`

**Simpler sleep fallback:** If sleep steps are fiddly, skip Step 4 and fill sleep from Sleep Cycle screenshot only.

---

### Step 5 — Get VO2 max (optional)

1. **Find Health Samples** → Type: **VO2 Max** → Latest First → Limit 1
2. **Get Details** → Value
3. **Number** (1 decimal)
4. **Text**: `VO2 max: [Number]`
5. **Set Variable** → `vo2Line`

---

### Step 6 — Combine and copy

1. Add **Text** action with:

```
Sleep score:
HRV:
7-day HRV:
HRV CV:
Recovery:
Resting HR:
Stress:
Yesterday's strain:
How I feel:

--- Apple Health ---
[hrvLine]
[rhrLine]
[sleepLine]
[vo2Line]
```

(Insert each variable where shown)

2. Add **Copy to Clipboard**
3. Add **Show Notification** → Title: **Morning health copied** → Body: **Paste into Readiness or chat**

---

### Step 7 — Name and icon

1. Tap shortcut name at top → rename: **Morning Health**
2. Tap **ⓘ** (info) → **Add to Home Screen** → Add

---

## Each morning

1. Tap **Morning Health** on Home Screen
2. Notification: “Morning health copied”
3. Open readiness app → **Log** → paste into template box → **Parse & fill form**
4. Fill blank lines from Bevel / HRV / OutPerform screenshots (or type numbers)
5. Add **How I feel**

Or paste into Cursor chat and ask for **Hume breakdown**.

---

## Faster version (minimal — 2 minutes)

If the full shortcut feels too long, build this only:

| Step | Action | Settings |
|---|---|---|
| 1 | Find Health Samples | HRV, Latest, Limit 1 |
| 2 | Get Details | Value |
| 3 | Find Health Samples | Resting HR, Latest, Limit 1 |
| 4 | Get Details | Value |
| 5 | Text | `HRV: [v1] ms\nResting HR: [v2] bpm` |
| 6 | Copy to Clipboard | |

Then paste and add the rest manually.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| No HRV data | Wear watch overnight; check Health → Browse → Heart → HRV |
| Sleep duration wrong | Filter out Awake/In Bed; or use Sleep Cycle score only |
| “Allow Health access” | Shortcuts → Morning Health → ⓘ → Privacy → Health → Allow |
| 7-day HRV average | Not easy in Shortcuts — keep using HRV app screenshot for CV and 7-day avg |
| Bevel Recovery missing | Bevel doesn’t always write Recovery to Health — screenshot still needed |

---

## Privacy

- Shortcut runs **on your iPhone only**
- Copy goes to **clipboard** — you choose where to paste
- Nothing is sent anywhere until **you** paste it

---

## What to paste in chat

After running the shortcut, your message can look like:

```
[paste shortcut output]

Sleep score: 82
7-day HRV: 22.4
HRV CV: 27.5
Recovery: 64
Stress: 8
Yesterday's strain: 0.6
How I feel: tired
```

Ask: **“Hume breakdown please”**
