# AegisSentinel — Chrome Native Messaging Extension
## Complete Implementation Guide

> **Extends** your existing `AegisSentinel` WPF app (Gemini AI + UI Automation)  
> **Adds** Chrome integration: Privacy Policy detection → OpenAI analysis → Windows Toast

---

## Table of Contents
1. [Architecture](#architecture)
2. [Project Structure](#project-structure)
3. [How It Works](#how-it-works)
4. [Step-by-Step Setup](#step-by-step-setup)
5. [Build & Publish](#build--publish)
6. [Registry Configuration](#registry-configuration)
7. [Security Notes](#security-notes)
8. [Troubleshooting](#troubleshooting)

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│  Chrome Browser                                          │
│                                                          │
│  ┌────────────────┐    message    ┌───────────────────┐  │
│  │  content.js    │──────────────►│  background.js    │  │
│  │  (DOM scraper) │               │  (Service Worker) │  │
│  └────────────────┘               └────────┬──────────┘  │
└───────────────────────────────────────────│─────────────┘
                                            │ chrome.runtime.connectNative()
                    ┌───────────────────────▼──────────────────────────┐
                    │  AegisSentinel.NativeHost.exe  (C# .NET 9)       │
                    │                                                   │
                    │  NativeMessagingPipe ──► PrivacyAnalysisEngine    │
                    │        (4-byte framing)    │                      │
                    │                           ├──► HtmlCleaner        │
                    │                           │    (HtmlAgilityPack)  │
                    │                           └──► OpenAiClient       │
                    │                                (GPT-4o-mini)      │
                    │                                                   │
                    │  ToastNotifier ◄── PrivacyScore result            │
                    │  PrivacyResultStore.Publish() ◄── result          │
                    └───────────────────┬──────────────────────────────┘
                                        │ static event
                    ┌───────────────────▼──────────────────────────────┐
                    │  AegisSentinel WPF App                            │
                    │  NativeBridgeWindow ── live feed of scored pages  │
                    │  DashboardWindow    ── existing EULA monitor      │
                    │  System Tray Icon   ── quick access               │
                    └──────────────────────────────────────────────────┘
```

---

## Project Structure

```
AegisSentinel-NativeMessaging/
│
├── NativeHost/                          ← C# console app (launched by Chrome)
│   ├── Program.cs                       ← Entry point, DI, message loop
│   ├── AegisSentinel.NativeHost.csproj
│   ├── appsettings.json
│   │
│   ├── Messaging/
│   │   ├── NativeMessagingPipe.cs       ← Binary framing protocol
│   │   └── Models.cs                   ← NativeMessage, NativeResponse, PrivacyScore
│   │
│   ├── Analysis/
│   │   ├── HtmlCleaner.cs              ← HtmlAgilityPack stripping
│   │   ├── OpenAiClient.cs             ← GPT-4o-mini structured output
│   │   └── PrivacyAnalysisEngine.cs    ← Pipeline + LRU cache
│   │
│   ├── Notifications/
│   │   └── ToastNotifier.cs            ← Windows Toast (Community Toolkit)
│   │
│   ├── NativeBridgeWindow.xaml(.cs)    ← Live WPF feed window
│   ├── PrivacyDetailWindow.cs          ← Detail popup (code-behind only)
│   ├── NativeBridgeConverters.cs       ← WPF value converters
│   ├── App.Integration.cs              ← Patch for existing App.xaml.cs
│   │
│   └── Tests/
│       └── NativeHostTests.cs          ← xUnit tests
│
├── ChromeExtension/                    ← MV3 Chrome Extension
│   ├── manifest.json
│   ├── background.js                   ← Service Worker + native port
│   ├── content.js                      ← DOM scraper + confidence scoring
│   ├── popup.html + popup.js           ← Extension popup UI
│   └── icons/                          ← shield16/32/48/128.png
│
└── Installer/
    ├── Install-AegisSentinel.ps1       ← Automated registry setup
    └── com.aegissentinel.host.json     ← Native Messaging manifest template
```

---

## How It Works

### Privacy Policy Detection (content.js)
The content script computes a **confidence score** (0–1) using four signals:

| Signal | Weight | Example |
|--------|--------|---------|
| URL pattern | +40% | `/privacy-policy`, `/datenschutz` |
| Page title | +30% | "Privacy Policy \| Example Corp" |
| H1/H2 headings | +25% | "How We Use Your Data" |
| Body text density | up to +25% | Contains GDPR, CCPA, "data retention" |

Pages scoring ≥ 65% trigger analysis.

### Binary Framing Protocol
Chrome's Native Messaging protocol is **not JSON over a socket** — it's binary-framed:

```
┌─────────────────────────────────────┐
│  4 bytes (uint32, little-endian)    │  ← message length
│  N bytes (UTF-8 JSON payload)       │  ← actual message
└─────────────────────────────────────┘
```

**Critical rules:**
- Use `Console.OpenStandardInput()` as a raw `Stream` — never `Console.ReadLine()`
- Write to `Console.OpenStandardOutput()` directly — never `Console.WriteLine()`
- Both reads and writes must be thread-safe (the `SemaphoreSlim` in `NativeMessagingPipe.cs`)

### LLM Prompt Design
The OpenAI prompt uses `response_format: { type: "json_object" }` with `temperature: 0`
to guarantee structured, deterministic output:

```json
{
  "safety_percent": 72,
  "risk_level": "Caution",
  "data_selling_detected": true,
  "retention_period": "5 years after account closure",
  "verdict": "This policy shares data with ad partners...",
  "key_risks": [
    "Sells location data to advertising networks",
    "Retains data 5 years after account deletion",
    "Arbitration clause waives class-action rights"
  ]
}
```

---

## Step-by-Step Setup

### Prerequisites
- .NET 9 SDK
- Visual Studio 2022 (or `dotnet` CLI)
- Chrome 88+ (Manifest V3 support)
- OpenAI API key

### Step 1: Build the Native Host

```powershell
cd NativeHost

# Debug (for development)
dotnet build

# Release — single-file self-contained exe (recommended for deployment)
dotnet publish -c Release -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o "C:\Program Files\AegisSentinel"
```

### Step 2: Load the Chrome Extension

1. Open Chrome → `chrome://extensions`
2. Enable **Developer mode** (top right toggle)
3. Click **Load unpacked**
4. Select the `ChromeExtension/` folder
5. Note the **Extension ID** (32-character string shown under the extension name)

### Step 3: Run the Installer

```powershell
# Run as Administrator
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass

.\Installer\Install-AegisSentinel.ps1 `
    -ExtensionId "YOUR_32_CHAR_EXTENSION_ID_HERE" `
    -ApiKey "sk-your-openai-key-here"
```

The installer performs:
- Writes `com.aegissentinel.host.json` with your Extension ID
- Registers the manifest path in `HKLM:\SOFTWARE\Google\Chrome\NativeMessagingHosts`
- Also registers for Microsoft Edge
- Force-installs extension via Chrome policy (`ExtensionInstallForcelist`)
- Registers AUMID for Toast notifications
- Creates Windows Firewall outbound rule for the exe
- Stores API key as user environment variable

### Step 4: Restart Chrome

```
chrome://restart
```

### Step 5: Verify Connection

1. Navigate to any privacy policy (e.g. `https://www.google.com/privacy`)
2. Open DevTools → **Application** tab → **Background scripts** → Inspect `background.js`
3. You should see: `[AegisSentinel] Connecting to native host: com.aegissentinel.host`
4. After ~5–10 seconds, a Windows Toast notification appears

---

## Build & Publish

### NuGet Packages Required

```xml
<!-- In AegisSentinel.NativeHost.csproj -->
<PackageReference Include="HtmlAgilityPack"                          Version="1.11.61" />
<PackageReference Include="Microsoft.Toolkit.Uwp.Notifications"     Version="7.1.3"   />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0"   />
<PackageReference Include="Microsoft.Extensions.Http"                Version="9.0.0"   />
<PackageReference Include="Microsoft.Extensions.Logging.Console"     Version="9.0.0"   />
<PackageReference Include="Microsoft.Extensions.Configuration.Json"  Version="9.0.0"   />
```

---

## Registry Configuration

### Manual Registry Commands (if not using the installer)

```powershell
# 1. Native Messaging Host manifest
New-Item "HKLM:\SOFTWARE\Google\Chrome\NativeMessagingHosts\com.aegissentinel.host" -Force
Set-ItemProperty `
  "HKLM:\SOFTWARE\Google\Chrome\NativeMessagingHosts\com.aegissentinel.host" `
  "(Default)" `
  "C:\Program Files\AegisSentinel\com.aegissentinel.host.json"

# 2. Force-install extension (optional — skips manual Developer Mode load)
New-Item "HKLM:\SOFTWARE\Policies\Google\Chrome\ExtensionInstallForcelist" -Force
Set-ItemProperty `
  "HKLM:\SOFTWARE\Policies\Google\Chrome\ExtensionInstallForcelist" `
  "1" `
  "YOUR_EXTENSION_ID;https://clients2.google.com/service/update2/crx"

# 3. Toast AUMID registration
New-Item "HKCU:\SOFTWARE\Classes\AppUserModelId\com.aegissentinel.host" -Force
Set-ItemProperty `
  "HKCU:\SOFTWARE\Classes\AppUserModelId\com.aegissentinel.host" `
  "DisplayName" "AegisSentinel"

# 4. API key
[System.Environment]::SetEnvironmentVariable(
  "AEGIS_OPENAI_API_KEY", "sk-...", "User")
```

---

## Security Notes

| Concern | Mitigation |
|---------|-----------|
| API key exposure | Stored as **user** env var, never in registry or on-disk config |
| Arbitrary HTML execution | `HtmlAgilityPack` parses into DOM — no JavaScript execution |
| Malicious extension messages | `allowed_origins` in manifest restricts to your Extension ID only |
| MITM on OpenAI calls | `HttpClient` uses system TLS validation; no custom cert pinning needed for OpenAI |
| Excessive LLM calls | In-process LRU cache with 4-hour TTL; identical URLs skip the API |
| Chrome policy abuse | `ExtensionInstallForcelist` requires admin rights to set — not user-accessible |
| Stdin injection | 10 MB message size limit in `NativeMessagingPipe.cs` prevents buffer exhaustion |

---

## Troubleshooting

### "Specified native messaging host not found"
→ The registry key path or JSON manifest path is wrong.  
→ Run: `Get-ItemProperty "HKLM:\SOFTWARE\Google\Chrome\NativeMessagingHosts\com.aegissentinel.host"`

### "Access to the path is denied" on startup
→ The exe is not in the path specified in the JSON manifest.  
→ Rebuild with the `-o "C:\Program Files\AegisSentinel"` publish flag.

### No Toast notification appearing
→ Check that the AUMID is registered: `Get-Item "HKCU:\SOFTWARE\Classes\AppUserModelId\com.aegissentinel.host"`  
→ Check Windows Focus Assist / Do Not Disturb settings.

### Content script not firing
→ Confidence threshold not reached. Open DevTools console on the page and check for `[AegisSentinel] Confidence:` log line.  
→ Lower threshold from `0.65` to `0.45` in `content.js` line `const CONFIDENCE_THRESHOLD`.

### OpenAI API errors
→ Verify key: `echo $env:AEGIS_OPENAI_API_KEY`  
→ Check native host log: look in `%TEMP%\AegisSentinel.log` (add `AddFile()` to logging config).

---

## Integration with Existing AegisSentinel WPF App

The `App.Integration.cs` file shows the patch needed. Key additions:

1. **`PrivacyResultStore`** — static thread-safe event bus. The Native Host calls `PrivacyResultStore.Publish()` after each analysis; the WPF `NativeBridgeWindow` subscribes to `PrivacyResultStore.ResultReceived`.

2. **`NativeBridgeWindow`** — new WPF window showing a live feed. Opened alongside the existing `DashboardWindow` on startup.

3. **System tray icon** — unified right-click menu for both windows.

The existing `DashboardWindow` (EULA analysis) and `NativeBridgeWindow` (Chrome privacy policies) run side-by-side, giving you **dual coverage**: desktop installers AND web privacy policies.
