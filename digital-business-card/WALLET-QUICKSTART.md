# Apple Wallet — you already have a Developer account

No App Store app. You create a **Pass Type ID**, export **three certificate files**, run **one command**, then open the `.pkpass` on your iPhone.

## Checklist

- [ ] **Mac** with Keychain Access (for exporting certificates)
- [ ] **Team ID** — [Membership details](https://developer.apple.com/account#MembershipDetailsCard) (10 characters, e.g. `AB12CD34EF`)
- [ ] **Pass Type ID** — [create one](https://developer.apple.com/account/resources/identifiers/list/passTypeId) (e.g. `pass.com.yourdomain.businesscard`)

## 1. Your card details

Open `index.html` (or `npm run serve`), fill in your info, then **Download config for Mac builder**. Save it as:

`digital-business-card/card.config.json`

Edit these two lines in that file:

```json
"passTypeIdentifier": "pass.com.yourdomain.businesscard",
"teamIdentifier": "AB12CD34EF"
```

Use your real Pass Type ID and Team ID.

## 2. Certificates → `certs/` folder

In [Certificates](https://developer.apple.com/account/resources/certificates/list), create a certificate for your **Pass Type ID**. Download it, install on Mac, then export from Keychain:

| File | What it is |
|------|------------|
| `certs/signerCert.pem` | Pass certificate |
| `certs/signerKey.pem` | Private key (PEM, unencrypted is easiest) |
| `certs/wwdr.pem` | Apple WWDR intermediate ([download G4](https://www.apple.com/certificateauthority/)) |

Step-by-step export help: [passkit-generator wiki — Generating Certificates](https://github.com/alexandercerutti/passkit-generator/wiki/Generating-Certificates).

## 3. Build

```bash
cd digital-business-card
npm install
npm run build
```

Your pass: `output/your-name.pkpass`

## 4. iPhone

AirDrop or email that file to your iPhone → tap it → **Add**.

---

**Stuck?** See [WALLET-SIGNING.md](./WALLET-SIGNING.md) for troubleshooting.
