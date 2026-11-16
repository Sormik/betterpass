# Security policy

BetterPass runs inside KeePass and handles passwords and TOTP secrets, so security reports are taken seriously.

## Supported versions

| Version | Supported |
|---|---|
| 1.1.x | yes |
| < 1.1 | no, please update |

## Reporting a vulnerability

Please **do not** report security problems in public issues.

Report them privately through GitHub: open the repository's **Security** tab and choose **Report a vulnerability**. Please include:
- the BetterPass, KeePass and Windows versions,
- what an attacker could do and under which conditions,
- steps to reproduce (with a test database, never real passwords).

You will get an answer as soon as possible. Once a fix is available, it is published as a new release, and the report is credited unless you prefer otherwise.

## Scope

In scope are problems in BetterPass itself, for example:
- passwords, TOTP secrets or other entry data leaving the computer unintentionally, or being written unprotected to disk, logs or the clipboard,
- data loss or corruption in a database,
- weaknesses in the password generator (predictable output, wrong character distribution),
- unsafe handling of downloaded data (thesvg.org icons, Have I Been Pwned responses).

Problems in KeePass itself should be reported to the [KeePass project](https://keepass.info/).

## What BetterPass sends over the network
- **Have I Been Pwned:** only the first 5 characters of the password's SHA-1 hash are sent to `api.pwnedpasswords.com` (k-anonymity). The password and the full hash never leave the computer.
- **Brand icons:** the icon catalog and SVG files are downloaded from thesvg.org. No entry data is sent.

All network access goes through KeePass' own networking functions, so KeePass' proxy and TLS settings apply.
