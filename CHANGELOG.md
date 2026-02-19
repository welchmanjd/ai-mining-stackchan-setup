# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Changed
- Beginner guidance strengthened: setup README and quickstart now clearly redirect end users to `AiStackchanSetup.bat` and include plain-language troubleshooting.
- UI guidance improved: added common USB/driver cause hints on connection step and API key sharing warning on completion step.
- Runtime config verification updated to include `wifi_ssid` consistency check and clearer save-failure message for retry.
- Release-note automation refined to avoid placeholder leakage when assembling release text.

## [v0.62] - 2026-02-18

### Changed
- Bundled firmware updated: `Mining-Stackchan-Core2` `ver: 0.84`, `build_id: 092bc7b1d3f7`.
- Release workflow updated: `scripts/build_dist.ps1` now prints ZIP SHA256 and a hash-based VirusTotal URL.

## [v0.202] - 2026-02-16

### Added
- Initial stable release (baseline).
- Bundled setup package and public firmware for end-user first-time setup.

### Notes
- Release artifact: `dist/AiStackchanSetup.zip`
- Bundled firmware: `Mining-Stackchan-Core2` `ver: 0.825`, `build_id: 3730531128e7`
- VirusTotal: https://www.virustotal.com/gui/file/1f980996a4be0754e007c60f17d574d9a4fd6c216fe8e730d20fd299437647b6?nocache=1
- SHA256: `1F980996A4BE0754E007C60F17D574D9A4FD6C216FE8E730D20FD299437647B6`
