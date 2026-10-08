# Sign your Apple Wallet business card

Apple only installs **signed** `.pkpass` files. You need an [Apple Developer Program](https://developer.apple.com/programs/) membership and a **Wallet Pass Type ID** certificate.

## 1. Register a Pass Type ID

1. Open [Certificates, Identifiers & Profiles](https://developer.apple.com/account/resources/identifiers/list/passTypeId).
2. Create a **Pass Type ID** (example: `pass.com.yourname.businesscard`).
3. Put that value in your card config as `passTypeIdentifier`.

Your **Team ID** (10 characters) is on the Apple Developer membership page — set it as `teamIdentifier` in the config.

## 2. Create the pass certificate

Follow Apple’s guide [Create the certificate](https://developer.apple.com/documentation/walletpasses/building_a_pass#3732198) for Pass Type ID.

Export from Keychain on a Mac:

- `signerCert.pem` — pass certificate
- `signerKey.pem` — private key (unencrypted PEM works best for local scripts)

Download Apple’s **WWDR** intermediate certificate (G4) and save as `wwdr.pem`.  
The passkit-generator wiki also documents this: [Generating Certificates](https://github.com/alexandercerutti/passkit-generator/wiki/Generating-Certificates).

Place all three files in `digital-business-card/certs/` (this folder is gitignored).

## 3. Build the pass

```bash
cd digital-business-card
cp card.config.example.json card.config.json   # or use Export from the web app
# edit card.config.json with your details and Apple IDs
npm install
npm run build
```

Output: `output/your-name.pkpass`

## 4. Add to iPhone

- **AirDrop** the `.pkpass` from your Mac to your iPhone, or
- Email it to yourself and open the attachment on iPhone, or
- Host it over **HTTPS** with content type `application/vnd.apple.pkpass` (use `npm run serve` locally for testing on the same network).

Tap **Add** when iOS opens the pass.

## Troubleshooting

| Issue | Fix |
|--------|-----|
| Pass won’t install | Check Team ID, Pass Type ID, and that the cert matches the identifier |
| “Invalid pass” | Regenerate assets (`npm run generate:assets`) after changing colors |
| QR doesn’t scan | Set a full `https://` website in your card details |

The web app’s **vCard** download works without Apple Developer — handy for Contacts while you set up signing.
