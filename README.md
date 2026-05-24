# RogKeyMini

RogKeyMini is a small Windows desktop utility for a 2021 ROG Zephyrus G14 with broken `F2`, `F7`, and `-` keys.

This repository is being prepared for an initial public release as `v0.1.0`. The goal of this first version is narrow and practical: provide a stable reference implementation for people who need a lightweight workaround for a few failed physical keys on this laptop model.

## What `v0.1.0` includes

Current `P0` scope:

- floating window
- tray mode
- global hotkeys
- key simulation for `F2`, `F7`, `-`, and `_`
- screen brightness down hook
- keyboard backlight down hook
- logging and safe failure behavior

## Hardware control strategy

Keyboard backlight currently uses a layered fallback:

1. `AsusAcpiService` tries the `ATKACPI` device path for a minimal hotkey-style command.
2. `AsusHidService` acts as a fallback and tries an ASUS HID feature report path.
3. Both paths are best-effort only and must fail safely without crashing the app.

The current HID path stores an estimated last-known brightness level in config because `P0` does not yet read back the real hardware brightness state.

## Current status

This repository now contains a working WPF project skeleton plus the current `P0` control paths for:

- tray integration
- tray config-file entry
- global hotkey registration
- key simulation
- WMI screen brightness down
- Asus ACPI first keyboard backlight down
- Asus HID fallback keyboard backlight down
- config persistence and normalization

This repository also includes a local offline `NuGet` package source in `.nuget-local` plus a repo-level `NuGet.Config`, because direct `dotnet restore` access to `https://api.nuget.org/v3/index.json` is failing in the current machine environment.

## Project layout

The project is maintained as source plus portable build output:

- source files live under `src/RogKeyMini`
- build output is redirected to `artifacts/bin`
- intermediate build files are redirected to `artifacts/obj`
- runtime `config.json` and `logs` stay beside the running `RogKeyMini.exe` for portable use

## Planned next version

The first public release intentionally stays focused. After `v0.1.0`, the next practical improvements are expected to be:

1. custom remapping for failed physical keys, so users can define which broken key should trigger which shortcut
2. a two-row window layout instead of the current single-row layout
3. more device-specific configuration once behavior is verified on real hardware

## Release positioning

If you publish this repository now, the clearest description is:

- current version: stable initial workaround for one concrete broken-key scenario
- target audience: users with similar ASUS laptop keyboard failures
- future direction: configurable remapping and improved layout, without changing the purpose of the tool

## Recommended validation before release

1. Run the app on the target machine and validate ACPI and HID keyboard backlight behavior.
2. Verify the hotkey and no-activate floating window behavior on the target machine.
3. Decide whether HID should remain fallback-only or become the practical primary path on this model.
