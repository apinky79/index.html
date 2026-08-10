/**
 * Extract health metrics from OCR text or merged screenshot analysis.
 * Patterns tuned for Bevel, HRV, OutPerform, Sleep Cycle layouts.
 */

const FIELD_PATTERNS = {
  hrv: [
    /(?:^|\s)(?:hrv|heart rate variability)[:\s]*(\d{1,2}(?:\.\d+)?)\s*ms/i,
    /(\d{1,2}(?:\.\d+)?)\s*ms(?=\s|$)/i,
  ],
  hrv7: [
    /7[\s-]?(?:day|d)\s*(?:hrv|avg|average)?[:\s]*(\d{1,2}(?:\.\d+)?)/i,
    /(?:7[\s-]?day)[^\d]{0,20}(\d{1,2}(?:\.\d+)?)\s*ms/i,
  ],
  hrvCv: [
    /(?:cv|coefficient)[:\s]*(\d{1,2}(?:\.\d+)?)\s*%?/i,
    /(\d{1,2}(?:\.\d+)?)\s*%\s*(?:cv|variability)/i,
    /(?:variability|stability)[:\s]*(\d{1,2}(?:\.\d+)?)/i,
  ],
  recovery: [
    /recovery[:\s]*(\d{1,3})\s*%?/i,
    /(?:bevel\s*)?recovery\s*(\d{1,3})/i,
  ],
  readiness: [
    /readiness[:\s]*(\d{1,3})\s*%?/i,
    /outperform[^\d]*(\d{1,3})/i,
  ],
  restingHr: [
    /resting(?:\s*heart\s*rate|\s*hr)?[:\s]*(\d{2}(?:\.\d+)?)/i,
    /(?:rhr|resting)[:\s]*(\d{2}(?:\.\d+)?)\s*bpm/i,
    /(\d{2}(?:\.\d+)?)\s*bpm/i,
  ],
  stress: [
    /stress[:\s]*(\d{1,3})\s*(?:\/\s*100)?/i,
    /stress\s*score[:\s]*(\d{1,3})/i,
  ],
  strain: [
    /(?:yesterday['']?s?\s*)?strain[:\s]*(\d{1,3})\s*%?/i,
    /strain\s*(\d{1,3})\s*%/i,
  ],
  sleep: [
    /sleep(?:\s*score)?[:\s]*(\d{1,3})\s*%?/i,
    /sleep\s*quality[:\s]*(\d{1,3})/i,
  ],
  sleepDuration: [
    /(?:time in bed|sleep(?:\s*time)?|duration)[:\s]*(\d{1,2})h\s*(\d{1,2})m/i,
    /(\d{1,2})h\s*(\d{1,2})m(?:\s*sleep)?/i,
  ],
  vo2max: [
    /vo2\s*max[:\s]*(\d{1,2}(?:\.\d+)?)/i,
    /cardio\s*fitness[:\s]*(\d{1,2}(?:\.\d+)?)/i,
  ],
};

function firstMatch(text, patterns) {
  for (const re of patterns) {
    const m = text.match(re);
    if (m) {
      if (m[2] != null) {
        return parseFloat(m[1]) + parseFloat(m[2]) / 60;
      }
      const n = parseFloat(m[1]);
      if (!Number.isNaN(n)) return n;
    }
  }
  return null;
}

export function parseMetricsFromText(text) {
  if (!text) return {};
  const normalized = text.replace(/\r/g, '\n');
  const result = {};
  for (const [field, patterns] of Object.entries(FIELD_PATTERNS)) {
    const val = firstMatch(normalized, patterns);
    if (val != null) result[field] = val;
  }
  return result;
}

export function mergeMetrics(...objects) {
  const merged = {};
  for (const obj of objects) {
    if (!obj) continue;
    for (const [k, v] of Object.entries(obj)) {
      if (v != null && merged[k] == null) merged[k] = v;
    }
  }
  return merged;
}

export function metricsToEntry(metrics, feel = 'ok') {
  return {
    sleep: metrics.sleep ?? null,
    hrv: metrics.hrv ?? null,
    hrv7: metrics.hrv7 ?? null,
    hrvCv: metrics.hrvCv ?? null,
    recovery: metrics.recovery ?? null,
    readiness: metrics.readiness ?? null,
    restingHr: metrics.restingHr ?? null,
    stress: metrics.stress ?? null,
    strain: metrics.strain ?? null,
    vo2max: metrics.vo2max ?? null,
    sleepDuration: metrics.sleepDuration ?? null,
    feel,
  };
}

const VISION_PROMPT = `You are extracting morning health metrics from iPhone app screenshots (Bevel, HRV tracker, OutPerform, Sleep Cycle, or Apple Health).

Return ONLY valid JSON with numeric values where found, null if not visible:
{
  "sleep": null,
  "hrv": null,
  "hrv7": null,
  "hrvCv": null,
  "recovery": null,
  "readiness": null,
  "restingHr": null,
  "stress": null,
  "strain": null,
  "vo2max": null,
  "sleepDurationHours": null,
  "sources": ["app names detected"]
}

Rules:
- HRV in milliseconds (ms)
- Recovery from Bevel (0-100)
- Readiness from OutPerform (0-100)
- Stress from Bevel (0-100, lower is calmer)
- Strain is yesterday's load percentage
- Sleep score from Sleep Cycle if shown
- Do not invent values — only extract what is clearly visible`;

export async function extractWithVision(imageDataUrls, apiKey) {
  if (!apiKey) throw new Error('API key required for screenshot analysis');
  if (!imageDataUrls.length) throw new Error('No images provided');

  const content = [{ type: 'text', text: VISION_PROMPT }];
  for (const url of imageDataUrls) {
    content.push({ type: 'image_url', image_url: { url, detail: 'high' } });
  }

  const res = await fetch('https://api.openai.com/v1/chat/completions', {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${apiKey}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      model: 'gpt-4o-mini',
      messages: [{ role: 'user', content }],
      max_tokens: 500,
      temperature: 0,
    }),
  });

  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.error?.message || `Vision API error (${res.status})`);
  }

  const data = await res.json();
  const raw = data.choices?.[0]?.message?.content || '';
  const jsonMatch = raw.match(/\{[\s\S]*\}/);
  if (!jsonMatch) throw new Error('Could not parse vision response');

  const parsed = JSON.parse(jsonMatch[0]);
  return {
    sleep: parsed.sleep,
    hrv: parsed.hrv,
    hrv7: parsed.hrv7,
    hrvCv: parsed.hrvCv,
    recovery: parsed.recovery,
    readiness: parsed.readiness,
    restingHr: parsed.restingHr,
    stress: parsed.stress,
    strain: parsed.strain,
    vo2max: parsed.vo2max,
    sleepDuration: parsed.sleepDurationHours,
    sources: parsed.sources || [],
  };
}

export async function extractWithOcr(imageFile) {
  const { default: Tesseract } = await import(
    'https://cdn.jsdelivr.net/npm/tesseract.js@5/dist/tesseract.esm.min.js'
  );
  const { data } = await Tesseract.recognize(imageFile, 'eng', {
    logger: () => {},
  });
  return parseMetricsFromText(data.text);
}

export async function fileToDataUrl(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result);
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}

export async function extractFromScreenshots(files, { apiKey, onProgress }) {
  const dataUrls = [];
  const ocrResults = [];

  for (let i = 0; i < files.length; i++) {
    onProgress?.(`Reading screenshot ${i + 1} of ${files.length}…`);
    dataUrls.push(await fileToDataUrl(files[i]));
    try {
      const ocr = await extractWithOcr(files[i]);
      ocrResults.push(ocr);
    } catch {
      ocrResults.push({});
    }
  }

  let visionResult = {};
  if (apiKey) {
    onProgress?.('Analysing screenshots with AI…');
    try {
      visionResult = await extractWithVision(dataUrls, apiKey);
    } catch (e) {
      onProgress?.(`AI extraction failed: ${e.message}. Using OCR fallback.`);
    }
  }

  const ocrMerged = mergeMetrics(...ocrResults);
  const final = mergeMetrics(ocrMerged, visionResult);

  return {
    metrics: final,
    method: apiKey && Object.keys(visionResult).length > 1 ? 'vision+ocr' : 'ocr',
    ocrMerged,
    visionResult,
    dataUrls,
  };
}
