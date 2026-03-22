# 8x8 Jitsi as a Service (JaaS) Setup

This app uses **8x8 JaaS** so teachers join meetings as moderators (no "waiting for moderator" screen).

## 1. Create a JaaS app

1. Go to **https://jaas.8x8.vc** and sign up / log in.
2. Create a new application (or use an existing one).
3. Note your **App ID** (tenant ID). It may be shown as something like `vpaas-magic-cookie-1fc542a3e4414a44b2611668195e2bfe` or just the hex part.

## 2. Create an API key (for JWT signing)

1. In the JaaS console, open **API Keys**.
2. Generate a new key or upload your own **RSA public key** (JaaS will give you a **Key ID**).
3. Download or copy your **private key** (PEM format). You must keep this secret.
4. Note the full **Key ID** (e.g. `vpaas-magic-cookie-1fc542a3e4414a44b2611668195e2bfe/4f4910`).

## 3. Configure the app

**appsettings.json** (or environment):

- **Jitsi:Domain** = `8x8.vc`
- **Jitsi:UseJaaS** = `true`
- **Jitsi:AppId** = your tenant ID, e.g. `vpaas-magic-cookie-1fc542a3e4414a44b2611668195e2bfe` (or the full value from the console).
- **Jitsi:KeyId** = full Key ID from API Keys, e.g. `vpaas-magic-cookie-1fc542a3e4414a44b2611668195e2bfe/4f4910`.

**Private key** (one of these):

- **Option A:** Set environment variable **JITSI_PRIVATE_KEY** to the full PEM string (use `\n` for newlines if needed).
- **Option B:** Save the PEM to a file and set **Jitsi:PrivateKeyPath** in appsettings to that path (e.g. `C:\secrets\jaas-private.pem`).

Example **appsettings.json**:

```json
"Jitsi": {
  "Domain": "8x8.vc",
  "UseJaaS": true,
  "AppId": "vpaas-magic-cookie-1fc542a3e4414a44b2611668195e2bfe",
  "KeyId": "vpaas-magic-cookie-1fc542a3e4414a44b2611668195e2bfe/4f4910",
  "PrivateKeyPath": ""
}
```

Then set **JITSI_PRIVATE_KEY** in your environment to the PEM content, or set **PrivateKeyPath** to the path of your PEM file.

## 4. Run the app

After saving config and setting the private key, restart the app. When a teacher clicks **Join meeting**, they will get a JWT signed with your JaaS key and will join as moderator on 8x8.vc.
