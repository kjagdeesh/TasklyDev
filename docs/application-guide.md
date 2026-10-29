# Taskly.Dev Application Guide

This guide describes the application's screens and how to use their controls. Taskly.Dev is a Windows desktop utility; its tools work locally on the device.

## Application shell

When opened, the app starts maximized with the standard Windows title bar and window controls. The shell has three main areas:

- **Sidebar:** Overview, grouped Developer Tools and System Diagnostics, and Preferences. The active destination is highlighted.
- **Page header:** Shows the selected page title and a tool search box. Search matches tool names and descriptions; entering a search from another screen returns to Overview.
- **Workspace:** Displays the selected screen. Long pages and result lists can be scrolled.

Use the sidebar to navigate directly. On Overview, select a tool card to open it. `Ctrl+L` focuses the tool search; `Esc` clears the search when the search box has focus.

Status messages below or beside controls report successful actions and validation errors. Clear buttons reset the relevant input or results. Copy buttons place the associated value on the clipboard.

## Overview

Overview is the tool directory. It presents tools as cards grouped into **Developer Tools** and **System Diagnostics**. Each card includes the tool name and a short description. Use the search field in the page header to filter the cards by name or description; clear the query to show the full list.

## Developer tools

### Base64 Encoder & Decoder

The top control strip chooses **Encode · Text to Base64** or **Decode · Base64 to text**. Choose a **Standard**, **URL-safe**, or **MIME (76 chars)** variant. When encoding, the **Padding (=)** checkbox controls standard trailing padding; it is disabled in decode mode.

The main area places the editable input and output side by side. The input reports character and UTF-8 byte counts. Use **Load sample**, **Clear**, and **Run** (Encode or Decode, depending on mode). Copy the output or move it back into the input to switch operation. Invalid encoded data is reported in the status area.

### Number Base Converter

The **Quick Pairs** buttons set common source/target base combinations. In the main card, choose source and target bases (2–36), type the integer, and select **Convert**. **Swap** reverses the selected bases; **Load sample** and **Clear** manage the input. Copy the converted output with its copy control.

The converter supports arbitrary-precision integers. Digits `0–9` and `A–Z` are accepted; leading and trailing whitespace is ignored. The selected bases determine which digits are valid.

### Unix Timestamp Converter

Choose **Timestamp → Date** or **Date → Timestamp** from the mode strip. **Now** fills the current time; **Load sample** and **Clear** are also available.

- **Timestamp → Date:** Enter an epoch value, explicitly choose **Seconds (s)** or **Milliseconds (ms)**, then select **Convert timestamp**. The result area shows ISO 8601 UTC and local machine time, with individual Copy buttons.
- **Date → Timestamp:** Enter an ISO 8601 date/time with an explicit `Z` or numeric offset. Results show both Unix seconds and Unix milliseconds, each with a Copy button.

The app does not infer timestamp units. UTC is used for conversion, with local time shown separately.

### GUID Generator

Choose a standard .NET GUID output format, set a quantity from **1 to 500**, and optionally select **Uppercase output**. Select **Generate GUIDs** to populate the results list. Each row has a Copy button; **Copy all** copies the entire batch. **Reset** returns the generator to its initial state.

GUIDs are identifiers and should not be used as passwords or secrets.

### UUID / GUID Validator & Parser

This tool is appended at the bottom of **Developer Tools**. Paste a value in one of the supported .NET formats (**D**, **N**, **B**, **P**, or **X**) and select **Validate and parse**. Valid input displays the detected format, a canonical lowercase D-format value, and a UUID version only when recognizable RFC variant/version bits are present. **Copy** copies the canonical value; **Clear** resets the input and result.

Version reporting is conservative: **Unknown** does not imply invalid input, and a displayed version does not prove a value's origin, uniqueness, or security.

### URL Encoder / Decoder

This tool is appended at the bottom of **Developer Tools** and starts in **Full URL** mode. Choose **Encode** or **Decode**, then select a mode and expand **Advanced options** as needed:

- **Full URL** accepts a regular absolute URL or a whole URL percent-encoded as a component. Encoding preserves URL delimiters and existing valid percent escapes while escaping spaces and Unicode as UTF-8. Decoding a regular URL leaves escaped reserved delimiters such as `%2F` encoded so they cannot silently change URL structure; decoding a whole-URL-encoded value restores its encoded URL delimiters. Parsed scheme, authority, path, query, and fragment are shown when available.
- **URL Component** encodes one value, preserving only RFC 3986 unreserved characters. When decoding, `+` remains a literal plus.
- **Query Value** encodes one query value: spaces become `+`, literal plus signs become `%2B`, and query separators are escaped. Decoding converts `+` back to a space.

Choose a character set for percent-encoded text: UTF-8, US-ASCII, ISO-8859-1, Windows-1252, or UTF-16LE. Unsupported characters or invalid encoded byte sequences are reported instead of silently replaced. Choose **LF (Unix)** or **CRLF (Windows)** as the destination line separator; the chosen separator is normalized before encoding and after decoding. During decoding, **Decode each line separately** treats each input line as its own value, and **Decode recursively** applies up to 16 decoding passes.

**Swap** moves the output to the input and reverses the operation; **Copy output** copies the current result; **Clear** resets both fields.

### JWT Encoder & Decoder

This tool is appended at the bottom of **Developer Tools**. **Decode token** accepts a compact JWT and displays formatted header and payload JSON plus a summary of payload claims as you paste or type. The recognized numeric `exp`, `nbf`, and `iat` claims include informational UTC and local times. Nonnumeric or out-of-range time claims are identified without treating the token as malformed. Header and payload JSON can be copied separately; **Clear** removes the input and decoded data.

Choose **Create token** to provide header JSON, payload JSON, a shared secret, and an HMAC signing algorithm (HS256, HS384, or HS512). The encoder produces the compact token locally; it does not store or send the secret. Anyone with the secret can create tokens that verify with that key, so use a test secret.

The warning on the screen is intentional and remains visible: decoding does **not** validate the signature, issuer, audience, expiration policy, or token trustworthiness. The signature segment is only shown as present/empty and is never cryptographically checked. Parsing and encoding occur locally; tokens are not sent or persisted, and error messages do not include token contents.

### Hash Calculator

Choose **Text** or **File** as the input type. In Text mode, enter text; the screen shows its UTF-8 byte count. In File mode, select **Browse…** and choose a local file. Select an algorithm (MD5, SHA-256, or SHA-512) and then **Calculate hashes**.

The result card displays lowercase hexadecimal digests with copy controls. **Cancel** stops an active calculation when supported; **Clear** resets the input and results. The status area reports progress or errors. MD5 is included for legacy checksum compatibility only and is not suitable for security-sensitive use.

### Color Converter

Choose an input format (**HEX**, **RGB**, or **HSL**) and enter a value in that format. The right-side preview updates as valid input is entered; its checkerboard background helps show transparency. The converted-values panel lists HEX, RGB, and HSL outputs, each with a Copy button.

**Choose color** opens the standard Windows color dialog, initially using the current valid color (or the default sample color). Selecting a color updates the converter; canceling the dialog keeps the prior value. The dialog chooses a color from its palette; it does not sample a pixel from the desktop. **Load sample** and **Clear** manage the input.

Alpha is preserved in converter input/output. HEX uses the `#RRGGBBAA` form when alpha is present; RGB expresses alpha on a 0–255 scale and HSL on a 0–1 scale.

### Regex Tester

Enter a regular expression in **Pattern** and the text to inspect in **Sample text**. Configure **Ignore Case**, **Multiline**, and **Singleline** as needed, then select **Test pattern**. Matches are listed with their index, length, and value. The screen reports the number shown; at most 100 matches are displayed. **Load sample** and **Clear** are available.

Matching uses .NET regular-expression semantics and a finite 250 ms timeout to limit excessively long matches. Invalid patterns and timeout errors appear in the status area.

## System diagnostics

### .NET SDK & Workload Inspector

This tool is appended to **System Diagnostics** and performs read-only discovery on the current machine. Select **Refresh** to inspect `dotnet` hosts found through `PATH`, `DOTNET_ROOT` environment variables, standard Program Files locations, and the current user's `.dotnet` folder. Each discovered host is inspected for SDKs, runtimes, workloads, and host details; duplicate SDK/runtime entries are consolidated.

The page shows the resolved host paths, discovered SDK/runtime versions and install locations, raw host/workload details, and per-command status and timing. Commands run from the Windows System directory with a 15-second timeout. This is not a recursive search of every disk location, does not select an SDK for a project or read project `global.json` files, and cannot install, update, repair, or remove .NET components. If no host is found or a command fails, review the status and diagnostics rather than treating missing data as an empty successful result.

### System Information

This tool is appended to **System Diagnostics** and collects a read-only, timestamped snapshot of Windows product, edition, release, build, installation date, Product ID, and Machine GUID; machine and processor details including physical cores, logical threads, and cache; BIOS, motherboard, and graphics-adapter details; installed and usable memory, page-file/commit data, fixed-volume labels/filesystems/capacity, network adapter descriptions/status/link speeds and IP-address counts, power source and battery status, time zone and locale, session and elevation state, Secure Boot and virtualization state, system uptime, and .NET runtime/version. Raw network IP and MAC addresses are not collected. Values are grouped into cards, with each field showing its source and any unavailable or partial status. The app does not request elevation, so some fields may be unavailable.

Select **Refresh** to collect a new snapshot. **Copy summary** copies the current report to the clipboard. **Export TXT** and **Export JSON** open a Windows Save As dialog and write the chosen report locally. As selected for this feature, neither screen values, clipboard summaries, nor exported reports redact collected values. Reports can include a stable device identifier, Windows Product ID, current account and domain, machine name, network adapter descriptions, and storage details; check the contents and destination before copying or sharing. Nothing is exported unless the user chooses an action and destination.

### Clipboard History

Clipboard History captures **plain text only** and remains disabled until the user checks the disclosure and selects **Start capture**. Clipboard text may contain passwords, tokens, or private messages. Captured entry text is encrypted with Windows DPAPI for the current user and stored in a JSON file under LocalAppData; text is not logged or uploaded.

The history supports search, preview, copy, pin/unpin, deleting one entry, and clearing all entries after confirmation. It is limited to 5,000 entries, 90 days, 1 MB per entry, and 100 MB total. Pinned entries are exempt from age cleanup but count toward entry and storage limits. The oldest unpinned entries are removed when needed; incoming text is skipped when capacity cannot be made. Duplicates and oversized entries are skipped.

**Pause** temporarily stops monitoring; **Resume** uses the prior opt-in; **Stop and disable** stops monitoring and clears the remembered opt-in without deleting saved entries. Closing the window hides TasklyDev to the system tray and capture continues. **Quit TasklyDev** in the tray menu stops the process and retains the saved history and opt-in for the next launch. Review this behavior before enabling capture on a shared device.

### Process Lookup

The page loads a process snapshot when opened. Select **Refresh Snapshot** to read it again. Search by process name or exact process ID; the result count updates as the list is filtered.

The table contains **Process name**, **PID**, **Start time**, and **Executable path**. Select a column header to sort by that column. Some start times or paths may be unavailable because of Windows access restrictions or because a process exited during collection. The app reads process information only and does not request elevation.

### Environment Variables

The page loads variables when opened. Choose **Process**, **User**, or **Machine** scope and search by variable name or value. The count shows the filtered result count against the loaded snapshot. Select **Refresh** to reload the current scope.

The table displays variable **Name** and a **Value (preview)**; select a header to sort. Selecting a row shows the full selected value and provides a copy action. The app does not edit or persist environment variables. Values can contain credentials, tokens, or private paths—avoid sharing them unintentionally.

## Preferences

### Appearance

Choose **System**, **Light**, or **Dark** in the Theme selector. Changes apply immediately. **System** follows the Windows app appearance. The selected theme is session-only and is not saved after closing Taskly.Dev.

### About

The About card shows the application logo, product/version information, and .NET runtime version. It also provides the privacy statement and third-party notice information.

## Data handling

Taskly.Dev performs utility work locally and does not send telemetry or upload tool inputs. Inputs and environment-variable values are not persisted by the application. Clipboard History is the exception: after explicit opt-in, plain-text clipboard entries are encrypted for the current Windows user and persisted locally under the limits described above. System Information reports are written only after the user selects an export action and destination; they are unredacted. Copy actions deliberately transfer the selected output to the Windows clipboard; check sensitive values before copying or sharing them.

## Troubleshooting

- **A result is blank or an error is shown:** Check the selected input format/unit and the value's syntax, then retry. Each tool's status area explains validation failures.
- **A process field is missing:** Windows may deny access, or the process may have exited while its snapshot was being collected. Refresh the snapshot; the app does not elevate itself.
- **A color dialog is canceled:** The previously selected color is retained.
- **A theme cannot be read from Windows:** Choose Light or Dark manually in Preferences.
- **An environment value contains sensitive information:** Do not copy or share it unless you are certain it is safe to disclose.
