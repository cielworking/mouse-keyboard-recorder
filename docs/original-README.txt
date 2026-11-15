Simple Mouse + Keyboard Recorder
===============================

A single-file Windows recorder/replayer. No installer needed (built against the
.NET Framework 4.x that ships with Windows 10/11).

Files
-----
SimpleRecorder.exe   the tool
Recorder.cs          full source
README.txt           this file

IMPORTANT - run it elevated
---------------------------
The exe asks for administrator rights on launch (UAC prompt). Accept it.
Windows does not deliver input-hook callbacks to a normal process while a window
of a higher integrity level owns the input, and it also filters injected input
aimed at such a window. Games and their launchers very often run elevated, so a
non-elevated recorder looks like "it records nothing" or "it never presses my
keys". The title bar shows [admin] when the recorder really is elevated.

If you cancel the UAC prompt the tool still runs, but only normal (non-elevated)
windows will be recorded and driven.

Usage
-----
1. Run SimpleRecorder.exe and accept the UAC prompt. Title must show [admin].
2. Press F9 (or "Record"). The status line counts what is captured live:
   "keys N | clicks N | moves N | wheel N".
   - keys staying at 0 while you type = the recorder is not elevated enough,
     or the target window runs at a higher integrity level.
3. Play your game / use the app. Do not click on the recorder window - clicks
   on it are ignored.
4. Press F9 (or Esc, or "Stop"). The log prints the final breakdown, e.g.
   "Recording stopped: 214 events (keys 96, clicks 3, moves 115, wheel 0), 8.4s."
5. Set "Repeat" to the number of repetitions, or tick "Infinite loop".
   "Gap between loops (ms)" is the pause inserted between repetitions.
6. Press F10 (or "Play"). The window minimizes itself while playing; untick
   "Minimize while playing" to keep it visible.
7. F10 again, or Esc, stops playback immediately - also mid-loop and in
   infinite mode.

Hotkeys (global)
----------------
F9    start recording / stop recording
F10   start playback / stop playback
Esc   stop recording or playback

Notes
-----
* Timing is reproduced from the recorded timestamps, pauses included.
* Mouse coordinates are absolute and DPI aware; multi-monitor and 125%/150%
  scaling are handled.
* The recording is kept in memory only; a new recording replaces it.
* Playback drives the real input queue: whatever is under the recorded
  coordinates receives the click.
* F9/F10 are swallowed by the recorder while it runs; Esc is swallowed only
  while recording or playing.

If keys are captured but the game ignores them
----------------------------------------------
The status line will then show keys > 0 and the recorded data contains them,
yet nothing happens in game. That means the game reads the keyboard through
Raw Input (common for Unity's new Input System). SendInput-based tools cannot
feed Raw Input; it needs a virtual HID / driver level injector (for example the
Interception driver). Recorded mouse movement will still work in such games
because it moves the real cursor.

Build from source
-----------------
  C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe ^
    /platform:anycpu /optimize+ /out:SimpleRecorder.exe ^
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll Recorder.cs

Built-in checks
---------------
  SimpleRecorder.exe --selftest report.txt           hooks, struct parsing, SendInput, DPI mapping
  SimpleRecorder.exe --e2e report.txt                record -> replay round trip, repeat and infinite loop
  SimpleRecorder.exe --noelevate                     start without the UAC prompt (testing)

Each test writes a report and exits 0 on PASS, 1 on FAIL.