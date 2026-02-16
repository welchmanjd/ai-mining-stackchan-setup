# Release Guide

This repository ships a ready-to-use setup package for end users.

## Scope

- Main artifact: `dist/AiStackchanSetup.zip`
- Bundled firmware location: `firmware/stackchan_core2_public.bin`
- Bundled firmware metadata: `firmware/stackchan_core2_public.meta.json`

## Pre-Release Checklist

1. Build firmware metadata without temporary dirty state:
   - `.\tools\build_firmware_public.ps1 -UseSampleConfig:$false`
2. Build distribution zip:
   - `.\scripts\build_dist.ps1`
3. Verify setup flow on a real device.
4. Confirm firmware metadata:
   - `build_id` has no `-dirty`
   - `ver` is expected
5. Generate hash:
   - `Get-FileHash .\dist\AiStackchanSetup.zip -Algorithm SHA256`
6. Scan zip on VirusTotal and keep the URL.

## What To Write In GitHub Release

Use the following minimum structure:

1. Summary (2-4 lines)
2. User-visible changes (3-5 bullets)
3. Bundled firmware info
4. SHA256 hash
5. VirusTotal URL
6. Known notes/limitations (if any)

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

## Integrity
- SHA256 (`AiStackchanSetup.zip`): `<paste hash>`

## Security scan
- VirusTotal: `<paste URL>`

## Notes
- (optional)
```
