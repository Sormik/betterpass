# Contributing to BetterPass

Thanks for helping! Issues and pull requests are welcome in English or German.

## Reporting bugs and ideas
- **Security problems:** please do **not** open a public issue. See [SECURITY.md](SECURITY.md).
- **Bugs:** open an issue with your KeePass version, BetterPass version and Windows version. If KeePass reports an error when loading the plugin, start it with `KeePass.exe --debug` and attach the compiler messages written to the Temp folder.
- **Ideas:** open an issue describing the problem you want to solve before you start larger changes.

## Development

### Requirements
- Windows with KeePass 2.47 or later
- .NET Framework 4.x (preinstalled on Windows 10/11)
- Optional: Visual Studio or MSBuild for DLL builds

### Building and testing
1. Build a PLGX as described in the [README](README.en.md#option-b-build-the-plgx-yourself-no-visual-studio-needed), or a DLL with `msbuild BetterPass.csproj /p:Configuration=Release`.
2. Copy it into KeePass' `Plugins` folder and restart KeePass.
3. Test with a **test database**, never with your real passwords.

A portable copy of KeePass with its own `KeePass.config.xml` (`<PreferUserConfiguration>false</PreferUserConfiguration>`) keeps tests away from your normal KeePass configuration.

### Code rules
- **C# 5 only.** KeePass compiles PLGX plugins itself with the C# 5 compiler of .NET Framework 4.x. No string interpolation, `nameof`, `?.`, expression-bodied members or other newer syntax.
- **Follow KeePass' style:** tabs for indentation, `if(` without a space, prefixes such as `m_` for fields, `str`, `n`, `b`, `v` for local variables.
- **ASCII in string literals:** write non-ASCII characters as `\uXXXX` escapes (e.g. `"Für"`), so the code compiles regardless of the system code page.
- **Use KeePass' APIs:** networking through KeePass' `IOConnection` (proxy and TLS settings), randomness through KeePass' `CryptoRandom` / `CryptoRandomStream`, secrets in `ProtectedString` / `ProtectedBinary`, dialogs registered with `GlobalWindowManager`.
- **Never break a database:** read unknown data, but never overwrite what BetterPass does not fully understand.
- **Fail softly:** if a future KeePass version changes a dialog, BetterPass should add nothing there instead of crashing.
- New source files start with the license header:
  ```
  /*
    BetterPass - KeePass plugin
    Copyright (C) 2026 Simon Müller
    SPDX-License-Identifier: GPL-3.0-or-later
    <What this file does>
  */
  ```
  and are added to `BetterPass.csproj`.

### Pull requests
- One topic per pull request, with a short description of what changed and how you tested it.
- Add an entry under *Unreleased* in [CHANGELOG.md](CHANGELOG.md).
- Update both READMEs ([German](README.md), [English](README.en.md)) when user-visible behavior changes.

## License
BetterPass is licensed under the [GNU General Public License v3.0 or later](LICENSE). By contributing, you agree that your contributions are licensed under the same license.
