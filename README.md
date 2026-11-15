# Simple Mouse + Keyboard Recorder

A small Windows desktop tool that records global mouse and keyboard input and
replays it with the same timing. It is a single C# WinForms source file with no
installer and no third-party dependencies beyond the .NET Framework.

The recorder uses low-level Windows hooks (`WH_KEYBOARD_LL` and `WH_MOUSE_LL`)
for capture and `SendInput` for playback. Recordings are held in memory only:
there is no save format, no export, and no network access.

> **Use with care.** Playback moves the real cursor and sends real keystrokes to
> whatever window is focused at that moment. A recorded session can contain
> passwords and other sensitive typing. See
> [Recording storage and privacy](#recording-storage-and-privacy).

## Features

- Global keyboard capture: key down/up, virtual-key code, scan code, and the
  extended-key flag.
- Global mouse capture: left, right, middle, and X buttons; vertical wheel; and
  throttled cursor movement (a move is stored when at least 10 ms has passed or
  the cursor moved at least 4 px).
- Timing replay based on the recorded timestamps, including pauses.
- Repeat count from 1 to 1,000,000, or an infinite loop.
- Configurable gap between loops (0–600,000 ms, default 100 ms).
- Optional "Minimize while playing" (on by default); the window restores itself
  when playback stops.
- Global hotkeys: `F9` record/stop, `F10` play/stop, `Esc` stop.
- Live status counters for keys, clicks, moves, and wheel events.
- Per-monitor DPI awareness (v2 with fallbacks) and virtual-desktop absolute
  coordinates, so multi-monitor and scaled displays are handled.
- Self-relaunch through UAC when not elevated; the title bar shows `[admin]` or
  `[NOT admin]`.
- Ignores synthetic (`LLKHF_INJECTED` / `LLMHF_INJECTED`) input while recording
  to avoid feedback loops, and ignores clicks on the recorder's own window.
- Built-in `--selftest` and `--e2e` diagnostics.
- No installer, no registry writes, no telemetry, no external services.

## Requirements

- Windows 10 or Windows 11 (other Windows versions with .NET Framework 4.x may
  work but are not tested).
- .NET Framework 4.x. Windows 10/11 normally has 4.8 preinstalled.
- Administrator rights are strongly recommended, and are required to capture or
  drive applications that run at a higher integrity level (many games and their
  launchers). Accept the UAC prompt and confirm `[admin]` in the title bar.
- The supplied binary is AnyCPU and runs 64-bit on 64-bit Windows.

## Run the prebuilt binary

1. Download or clone this repository.
2. Run `SimpleRecorder.exe` and accept the UAC prompt.
3. Confirm the title bar shows `[admin]`.
4. Press `F9` (or **Record**) and use the target application.
5. Press `F9`, `Esc`, or **Stop** to finish recording.
6. Set **Repeat** or tick **Infinite loop**, then press `F10` (or **Play**).
7. Press `F10` or `Esc` to stop playback.

The executable is unsigned, so Windows SmartScreen may warn on first launch.
Only bypass that warning if you trust the file and its source. You can also
[build it yourself](#build-from-source).

### Controls

| Control | Behavior |
| --- | --- |
| `F9` | Start recording; stop while recording; stop while playing |
| `F10` | Start playback; stop while playing |
| `Esc` | Stop recording or playback (swallowed only while either is active) |
| **Record (F9)** | Start a new recording; disabled during playback |
| **Play (F10)** | Replay the current recording; disabled while recording or when nothing is recorded |
| **Stop (Esc)** | Stop recording and playback |
| **Repeat** | Number of playback loops, 1–1,000,000 |
| **Infinite loop** | Repeat until stopped; disables the Repeat field |
| **Minimize while playing** | Hide the window during playback (default on); restores it afterwards |
| **Gap between loops (ms)** | Pause inserted between loops, 0–600,000 ms (default 100) |

`F9` and `F10` are swallowed globally while the recorder is running. `Esc` is
swallowed only while recording or playing. The counters show key-down, mouse-down,
move, and wheel counts; the final event total also includes key-up and mouse-up
events.

## Build from source

The project compiles with the C# compiler that ships with the .NET Framework, so
no Visual Studio or SDK installation is required.

### Option A: build script

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The script finds `csc.exe` under `%WINDIR%\Microsoft.NET\Framework64` (or
`Framework`), compiles `Recorder.cs`, and writes `dist\SimpleRecorder.exe`. Pass
`-OutputPath` to choose another output file.

### Option B: direct compiler command

From a Developer Command Prompt, or with the full compiler path:

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe ^
  /platform:anycpu /optimize+ /out:SimpleRecorder.exe ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll Recorder.cs
```

The build is a single `winexe` assembly. There is no project file, NuGet package,
or additional source file.

## How it works

- `Main` enables per-monitor DPI awareness, handles the `--noelevate`,
  `--selftest`, and `--e2e` switches, and relaunches itself elevated for normal
  GUI use.
- The keyboard hook is installed for the lifetime of the window; the mouse hook
  is installed only while recording.
- Each event stores a type, screen coordinates (mouse), button/wheel/virtual-key
  data, scan code, extended flag, and a millisecond timestamp from a
  `Stopwatch`.
- Playback runs on a background thread and waits on the recorded timestamps
  before each `SendInput` call. Keyboard events are sent by scan code when one
  was captured, with the virtual key as a fallback.
- Mouse positions are converted to the 0–65535 absolute coordinate range across
  the entire virtual desktop (`MOUSEEVENTF_ABSOLUTE |
  MOUSEEVENTF_VIRTUALDESK`), which is what makes multi-monitor playback work.
- Starting a new recording clears the previous event list. The list is never
  written to disk.
- `--selftest` checks hook installation, hook structure parsing, `SendInput`
  marshalling, and coordinate round-tripping. `--e2e` records a synthetic
  session through the real recording path, replays it, and checks repeat and
  infinite-loop behavior.

## Recording storage and privacy

- **Memory only.** A recording lives in the process's event list until a new
  recording starts or the process exits. There is no autosave, export, cloud
  sync, or clipboard integration.
- **Everything typed is captured.** While recording, the keyboard hook sees
  system-wide keystrokes, including passwords, private messages, and payment
  details. Do not record a session that contains secrets, or run the tool on a
  dedicated desktop/VM when testing untrusted macros.
- **Mouse activity is captured too,** including cursor paths and click locations,
  which can reveal what was on screen.
- **No network code.** The source references only `System`, `System.Drawing`,
  `System.Windows.Forms`, and related framework namespaces; it has no HTTP,
  socket, telemetry, or update code.
- **Test reports are files.** `--selftest` and `--e2e` write a plain-text report
  to the path you pass (default `selftest.txt` / `e2e.txt`). The end-to-end
  report includes a short sample of recorded event codes and coordinates. Keep
  those reports private and out of version control.
- **Memory is not a secure vault.** Another administrator-level process can read
  process memory. Exit the recorder when finished, especially after recording
  sensitive input.

## Limitations

- **Elevation matters.** A non-elevated recorder receives no hook callbacks for
  input aimed at a higher-integrity window and cannot inject input into it. Use
  `[admin]` mode for elevated targets.
- **Raw Input is not supported.** Some games (notably Unity's new Input System)
  read the keyboard through Raw Input, which `SendInput` cannot feed. Those games
  need a virtual HID/driver-level injector. Recorded mouse movement may still
  work because it moves the real cursor.
- **Anti-cheat and terms of service.** Some games and anti-cheat systems block or
  flag synthetic input. Use automation only where it is allowed; an account ban
  is a possible consequence.
- **The desktop must match.** Playback uses absolute screen coordinates. Changing
  monitor layout, resolution, scaling, or window positions between recording and
  playback can send clicks to the wrong place.
- **No window targeting.** Events go to whatever is under the cursor or focused
  at replay time. Bring the target window to the same state before playing.
- **Timing is wall-clock, not frame-synced.** Very tight timing or variable frame
  rates may not reproduce exactly.
- **One recording slot.** There is no save/load, no event editor, no per-event
  delay editing, and no horizontal-scroll capture.
- **Unsigned binary.** The supplied executable has no code signature and no
  version resource.

## Diagnostics

Run these from a terminal. Both modes inject real input and move the cursor, so
run them on an idle desktop with no unsaved work.

```bat
SimpleRecorder.exe --selftest report.txt
SimpleRecorder.exe --e2e report.txt
SimpleRecorder.exe --noelevate
```

- `--selftest` verifies hook installation, structure parsing, `SendInput`
  marshalling, and coordinate mapping. It writes the report and exits 0 on PASS,
  1 on FAIL.
- `--e2e` records a synthetic session, replays it, and checks a three-loop repeat
  and an infinite loop stopped by request. It writes the report and exits 0 on
  PASS, 1 on FAIL.
- `--noelevate` runs the GUI without the UAC relaunch. This is intended for
  testing and only works with non-elevated targets.

## Repository contents

| Path | Description |
| --- | --- |
| `Recorder.cs` | Complete application source (single file) |
| `SimpleRecorder.exe` | Prebuilt unsigned binary supplied with the project |
| `build.ps1` | Builds `dist\SimpleRecorder.exe` with the .NET Framework compiler |
| `SECURITY.md` | Security and privacy notes, and how to report a problem |
| `docs/original-README.txt` | Original plain-text README supplied with the project |
| `.gitignore` | Ignores build output, IDE files, and diagnostic reports |

## File checksums

SHA-256 of the uploaded source and binary:

```text
Recorder.cs        489730C6A2CDDB9F4CA6BC8B5A649E0B9AEAC4127B57ADD10B5ADC3CC0916198
SimpleRecorder.exe 760E65BE653522DC8F87391AD6527C386D2EF645D050FDBEC996BBEA9ACEADBD
```

The source compiles cleanly with the .NET Framework 4.0 `csc.exe` on Windows 11.
A local rebuild is the same size as the supplied binary but not byte-identical,
because the compiler embeds non-deterministic build metadata.

## License

No license has been specified for this project. Contact the repository owner
before redistributing or using it in another product.

This tool sends real input and is provided without warranty. Review the source
before running it, and test with a harmless target first.
