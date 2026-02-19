# Release Guide

This repository ships a ready-to-use setup package for end users.

## Next Release Command

- Generate artifacts + notes (Japanese notes are default):
  - `powershell -ExecutionPolicy Bypass -File .\tools\release.ps1 -ReleaseVersion v0.63`
- Generate + publish/update GitHub Release using Japanese notes:
  - `powershell -ExecutionPolicy Bypass -File .\tools\release.ps1 -ReleaseVersion v0.63 -PublishGitHubRelease`

## Scope

- Main artifact: `dist/AiStackchanSetup.zip`
- Bundled firmware location: `firmware/stackchan_core2_public.bin`
- Bundled firmware metadata: `firmware/stackchan_core2_public.meta.json`

## Pre-Release Checklist

1. Run preflight checks:
   - `.\tools\preflight_release.ps1`
2. Build firmware metadata without temporary dirty state:
   - `.\tools\build_firmware_public.ps1 -UseSampleConfig:$false`
3. Build distribution zip:
   - `.\scripts\build_dist.ps1`
4. Generate release notes draft:
   - `.\tools\release_notes_auto.ps1 -ReleaseVersion vX.YYY`
   - Generated files:
     - `dist/RELEASE_NOTES_auto.md` (English)
     - `dist/RELEASE_NOTES_auto_ja.md` (Japanese)
   - `tools/release.ps1` uses Japanese notes by default (`-ReleaseNotesLanguage ja`).
5. Verify setup flow on a real device.
6. Confirm firmware metadata:
   - `build_id` has no `-dirty`
   - `ver` is expected
7. Capture build output:
   - `scripts/build_dist.ps1` now prints `SHA256` and a hash-based VirusTotal URL for:
     - `dist/AiStackchanSetup.zip`
     - `dist/AiStackchanSetup/app/AiStackchanSetup.exe`
     - `dist/AiStackchanSetup/firmware/stackchan_core2_public.bin` (or first bundled `.bin`)
8. Optional: if VirusTotal has no record yet, upload each missing artifact once from the printed URL page.

## What To Write In GitHub Release

Use the following minimum structure:

1. Summary (2-4 lines)
2. User-visible changes (3-5 bullets)
3. Bundled firmware info
4. Quickstart link (`docs/quickstart-ja.md`)
5. Integrity (ZIP / EXE / firmware)
6. Security scan URL (ZIP / EXE / firmware)
7. Known notes/limitations (if any)

## Release Notes Template

```md
## Summary
- This release updates the setup tool and bundles a validated public firmware for easier first-time setup.

## User-visible changes
- (write 3-5 bullets here)

## Bundled firmware
- app: Mining-Stackchan-Core2
- ver: 0.825
- build_id: 3730531128e7

## Quickstart
- Japanese: `https://github.com/welchmanjd/ai-mining-stackchan-setup/blob/main/docs/quickstart-ja.md`
- Latest release: `https://github.com/welchmanjd/ai-mining-stackchan-setup/releases/latest`

## Integrity
- SHA256 (`AiStackchanSetup.zip`): `<paste hash>`
- SHA256 (`AiStackchanSetup.exe`): `<paste hash>`
- SHA256 (`stackchan_core2_public.bin`): `<paste hash>`

## Security scan
- VirusTotal (`AiStackchanSetup.zip`): `https://www.virustotal.com/gui/file/<sha256-lowercase>?nocache=1`
- VirusTotal (`AiStackchanSetup.exe`): `https://www.virustotal.com/gui/file/<sha256-lowercase>?nocache=1`
- VirusTotal (`stackchan_core2_public.bin`): `https://www.virustotal.com/gui/file/<sha256-lowercase>?nocache=1`

## Notes
- (optional)
```
