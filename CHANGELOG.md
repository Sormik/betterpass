# Changelog

All notable changes to BetterPass are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- License: GPL-3.0-or-later, with copyright and SPDX notices in all source files.
- English README, changelog, contributing guide, security policy, code of conduct, notices and GitHub issue/PR templates.

## [1.1.0] - 2026-11-16

### Added
- Password generator profiles in the style of well-known providers: Apple (Safari, iCloud Keychain), Google Chrome, Microsoft Edge, Mozilla Firefox, 1Password and Bitwarden.
- Apple-style syllable generator (`xxxxxx-xxxxxx-xxxxxx`, exactly one uppercase letter and one digit), registered as a KeePass custom password generator with an options dialog, live preview and strength display.
- On first start, Apple style becomes the default for new entries.
- Settings section *Passwort-Generator*: choose the default for new entries, customize the Apple style, restore deleted profiles.

## [1.0.0] - 2026-11-03

### Added
- TOTP fields in the entry dialog (code card with countdown, key, digits, period, algorithm, issuer), stored in KeePass' native `TimeOtp-*` fields.
- Have I Been Pwned check in the entry dialog and for the whole database (k-anonymity).
- Brand icons from thesvg.org, with picker and automatic assignment.
- Modern design: Windows 11 and aethercert themes, light and dark, Fluent icons.

[Unreleased]: https://github.com/Sormik/betterpass/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/Sormik/betterpass/releases/tag/v1.1.0
[1.0.0]: https://github.com/Sormik/betterpass/commit/a029ddf
