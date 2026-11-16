# EPSON RESETTER

A professional Windows desktop utility for managing and maintaining Epson printers. EPSON RESETTER reads the
Windows print subsystem (WMI), the printer port configuration and the network, and presents that information in a
single WPF application. It does not install drivers, does not modify the print spooler and does not send service
commands to printers.

**Current version:** 1.0.0

## Safety and scope

This project is intentionally conservative:

- **No cracked tools, activation keys, licence circumvention, undocumented commands or security bypasses.**
- **No reset executor is implemented.** The application reports reset *eligibility* only. No counter is written.
- A model is only marked as supported when a documented, vendor-authorised method has been verified for the exact
  model. No such method is enabled in this build, so every model stays disabled.
- A reset can only become eligible after the physical waste ink pad or collection system has been inspected and
  serviced, the owner has authorised the work, and the operator has confirmed the documented procedure.
- Administrator credentials are never requested or stored. URLs containing `user:password@host` are rejected.
- TLS certificate validation is never disabled. Web Admin pages are opened with the system default browser.

## Features

| Area | What works |
| --- | --- |
| Discovery | Real printers from `Win32_Printer`, drivers from `Win32_PrinterDriver`, TCP/IP ports from `Win32_TCPIPPrinterPort` |
| Identification | Manufacturer and model derived from the printer and driver names; no fabricated values |
| Search | Name, model, driver and IP address search; connection-type filter; Epson-only toggle |
| Detail page | Driver, port, IP address, port number, spooler condition, location, detection time |
| Web Admin | Detects the IP from the port, validates `http`/`https`, offers manual override, opens the default browser |
| Diagnostics | Spooler service, spooler registration, driver, port, connection, printer state, ICMP, TCP 9100, Web Admin address |
| Maintenance | Windows print queue, printer properties, Windows test page, Web Admin |
| Waste ink | Model compatibility registry, honest counter-read result, reset eligibility checks with reasons |
| History | SQLite-backed activity log for scans, Web Admin, diagnostics, maintenance, counter reads and eligibility checks |
| Settings | Web Admin scheme/ports, network timeout, display defaults; persisted in SQLite |
| Monitoring | Automatic status refresh every 45 seconds, plus manual rescan |

## Requirements

- Windows 10 or Windows 11
- .NET 8 Desktop Runtime (WPF)

## Build and run

```powershell
dotnet restore EpsonResetter.sln
dotnet build EpsonResetter.sln -c Release
dotnet run --project src/EpsonResetter.App -c Release
```

The executable is `src/EpsonResetter.App/bin/Release/net8.0-windows/EpsonResetter.exe`.

### Command line

The application is a WPF executable and also supports headless helpers used by the build pipeline:

```powershell
# Print the version
EpsonResetter.exe --version

# Discover printers and write JSON (optionally to a file)
EpsonResetter.exe --scan
EpsonResetter.exe --scan --out C:\Temp\printers.json

# Create the real window, open every page and verify layout (no user interaction)
EpsonResetter.exe --ui-smoke
```

`--ui-smoke` exits with code 0 only when every page is created and laid out without an exception. It is the
automated check for XAML and binding regressions.

## Tests

```powershell
dotnet test EpsonResetter.sln
```

The suite covers printer mapping and port classification, model identification, compatibility lookup,
reset eligibility policy, Web Admin validation and address resolution, network probing, SQLite persistence,
diagnostics with fake providers, and live spooler discovery on the build machine. Hardware-dependent code is
injected through interfaces so tests never touch a physical printer.

## Architecture

```
src/
  EpsonResetter.Core/          Domain models, compatibility registry, Web Admin validation,
                               diagnostics orchestration, reset eligibility policy, service interfaces
  EpsonResetter.Infrastructure/ WMI spooler provider, ICMP/TCP probes, SQLite stores,
                               printui maintenance actions, browser launcher, Serilog wiring
  EpsonResetter.App/           WPF shell, MVVM view models, views, theme, composition root
tests/
  EpsonResetter.Tests/         xUnit tests, including live spooler integration checks
```

Key design decisions:

- `PrinterDevice` values always originate from the spooler, a WMI query or a validated user entry. There is no
  sample-data path in production code.
- `ModelCompatibilityRegistry` is data-driven; adding a verified method for a specific model requires a profile
  with a documented method, an authorisation level and a verification date.
- `ResetEligibilityService` is a pure policy engine. It never performs I/O and never resets anything.
- `WebAdminAddressResolver` returns validated candidates and explicit warnings instead of guessing.
- `RegistryBackedWasteInkCounterReader` returns "not supported" unless a verified reader exists for the model.

## Local data

| Item | Location |
| --- | --- |
| SQLite database | `%LOCALAPPDATA%\EpsonResetter\epson-resetter.db` |
| Log files | `%LOCALAPPDATA%\EpsonResetter\logs\app-*.log` |

No printer administrator credentials are stored anywhere.

## Known limitations

- **No reset is performed by this application.** The Waste Ink page reports eligibility and explains why a reset is
  unavailable; it never writes a counter.
- **No counter values are read** because no vendor-authorised reader module is included. The UI never displays a
  guessed value.
- **WSD and Bluetooth ports** may not expose an IP address, so the Web Admin address must be entered manually.
- **ICMP may be blocked** on some networks even when the printer is reachable; diagnostics report this as a warning
  rather than a failure.
- **Driver details depend on WMI.** `Win32_PrinterDriver` exposes `Version` and `InfName` on current Windows
  versions; older or unusual drivers may leave those fields blank.
- **No SNMP polling, no ink-level telemetry, no nozzle-check or power-cleaning automation.** Those require
  vendor-specific protocols that are out of scope.
- Status refreshes every 45 seconds while the application is open; there is no background service.