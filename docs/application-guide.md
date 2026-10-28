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

Taskly.Dev performs utility work locally and does not send telemetry or upload tool inputs. Inputs and environment-variable values are not persisted by the application. Copy actions deliberately transfer the selected output to the Windows clipboard; check sensitive values before copying or sharing them.

## Troubleshooting

- **A result is blank or an error is shown:** Check the selected input format/unit and the value's syntax, then retry. Each tool's status area explains validation failures.
- **A process field is missing:** Windows may deny access, or the process may have exited while its snapshot was being collected. Refresh the snapshot; the app does not elevate itself.
- **A color dialog is canceled:** The previously selected color is retained.
- **A theme cannot be read from Windows:** Choose Light or Dark manually in Preferences.
- **An environment value contains sensitive information:** Do not copy or share it unless you are certain it is safe to disclose.
