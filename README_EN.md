# MuvluvMod

This repository provides files for the **Windows DMM Game Player version** of the game (Muv-Luv Girls Garden X).

> **⚠️ Migrated from BepInEx to MelonLoader**, so users no longer need to download Unity patches from BepInEx's official website during installation.

---

## Features

-   **Story text translation**: Chinese translation for main story and event character scenarios (local files first, CDN online fallback)
-   **Name/team/title translation**: Localized character names, team names, scenario titles, and subtitles
-   **History text translation**: Synced translation when reviewing previously read scenarios
-   **Choice translation**: Choice branches displayed with translated text
-   **Chinese font replacement**: Replaces the default Japanese font with Sarasa Gothic SC Bold (SarasaGothicSC-Bold SDF), including outline/stroke effects
-   **Dynamic mosaic removal**: Removes in-game dynamic mosaic effects during scenarios (toggleable)
-   **Always enabled skip button**: Forces the skip button to always be available
-   **Voice interruption prevention**: Prevents the current voice line from being interrupted when the next line plays
-   **Auto-skip battles**: Automatically clicks the skip button to skip battle scenes
-   **Disable white flash**: Disables white/black screen flash effects (LightFlash / DarkFlash) during scenario performances

---

## Installation

### 1. Preparation

-   Install the game via **DMM Game Player**.
-   Locate the game executable file: `muv_luv_girlsgardenx_cl.exe`.
-   **.NET 6 Runtime** is required (usually included with MelonLoader, no separate installation needed).

### 2. Download

-   Go to the [Releases page](https://github.com/anosu/MuvluvMod/releases).
-   Download the latest release marked with `Latest`.
-   In the `Assets` section, download `MuvluvMod.7z` (do **not** download `Source code`).

### 3. Installation

-   Extract the archive; you will get `version.dll`, `MelonLoader`, `Mods`, and other files.
-   Copy all files to the same folder as `muv_luv_girlsgardenx_cl.exe`.
-   If an older version exists, delete or overwrite it.

### 4. Launching

-   Start the game normally: launch from DMM Game Player or a third-party DMM launcher, **do NOT double-click `muv_luv_girlsgardenx_cl.exe` directly!!!**
-   On the first run, MelonLoader will automatically generate the necessary IL2CPP interop assemblies and config files (takes about 10-30 seconds).

### 5. Configuration

-   After the first run, config files will be created under `UserData`:
    -   `Loader.cfg` (MelonLoader settings)
    -   `MuvluvMod.cfg` (mod settings, split into General and Translation categories)
-   Restart the game after editing configs.
-   To hide the console window, set `hide_console = true` under `[console]` in `UserData\Loader.cfg`.

### 6. Translation Data

Translation data can be loaded via two methods, in priority order:

1. **Local files** (recommended for offline use): Clone the `muvluvgg-translation` repository into the game directory, and the mod will load from local files automatically.
2. **CDN online loading**: If local files are not available, translations are fetched from GitHub Raw automatically.

When encountering untranslated scenarios for the first time, the mod automatically saves the original text to the `scenes_pending` directory in the local translation repository for future translation.

---

## Hotkeys

-   `F2`: Toggle translation
-   `F3`: Toggle always-enabled skip button
-   `F4`: Toggle voice interruption (when disabled, voices are not interrupted by line changes)
-   `F5`: Toggle auto battle skip

---

## Disclaimer

-   This mod is a **fan-made third-party project** and has no affiliation with the official developers or publishers.
-   It is intended for educational and technical research purposes only. Please use it **legally and responsibly**.
-   Use of this mod may affect the normal operation of the game. The author is **not responsible** for any consequences (including but not limited to account bans, data loss, or crashes).
-   By downloading and using this mod, you agree to bear all risks yourself.
