# Security and privacy

## Reporting a problem

Use GitHub's private vulnerability reporting for this repository (the **Security**
tab, then **Report a vulnerability**). Do not open a public issue for a report
that includes a working exploit or sensitive details.

## What this tool does

`SimpleRecorder.exe` is a Windows input recorder and replayer:

- It installs global low-level keyboard and mouse hooks while it is open or
  recording. The keyboard hook is active for the lifetime of the window.
- It can inject keyboard and mouse input into the real input queue with
  `SendInput`. Playback affects whatever window is focused or under the cursor.
- It asks for administrator rights on normal launch so it can interact with
  elevated windows. `--noelevate` skips that request for testing.
- It has no network code, no telemetry, no update mechanism, and no installer.

## Data handling

- Recordings are kept in process memory only. Starting a new recording replaces
  the previous one; exiting the process discards it.
- A recording can contain every keystroke typed while capture is active,
  including passwords and private messages, plus cursor paths and click
  locations. Treat a recording as sensitive as the content of the captured
  session.
- The application writes files only when you run the diagnostic modes:
  `--selftest <path>` and `--e2e <path>` write a plain-text report. The
  end-to-end report includes a short sample of recorded event codes and
  coordinates.
- Process memory is not a secure store. An administrator-level process can read
  it. Exit the recorder after handling sensitive input.

## Safe-use guidance

- Record only sessions you own or are authorized to automate.
- Do not record password entry, banking, private messages, or other secrets.
- Test playback with a harmless target and a short recording before using it on
  anything important.
- Keep the tool elevated only while needed, and close it when finished.
- Some games and anti-cheat systems prohibit synthetic input. Follow the terms of
  service for the software you use.

## Build integrity

The supplied binary is unsigned and has no version resource. The SHA-256 values
in `README.md` identify the uploaded files. To avoid running a modified binary,
review `Recorder.cs` and build it yourself with `build.ps1`.
