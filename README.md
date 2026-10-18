# Discord Status installer

A Windows installer for [Discord Status](https://github.com/UiUyHerrera/discord-rich-presence-desktop). It's a window with one button: it checks what's missing on the computer, installs it and leaves the app ready with its shortcuts.

## What it does, in order

1. Looks for `winget`. Without it, it can't go on, and it tells you how to get it.
2. If Git, Python 3.12 or the WebView2 Runtime are missing, installs them with `winget`.
3. Clones the app into `%LOCALAPPDATA%\Programs\EstadoDiscord`. If it was already installed and running, it closes it first.
4. Creates a Python virtual environment and installs the dependencies there, without touching the system Python.
5. Adds the Start menu shortcut and, if you ticked the option, a desktop one.
6. If you asked for it, opens the app at the end.

Each step shows up in the window as it runs, so if something fails you can see where.

The installer never asks for or stores the Discord token: the app does that the first time it opens.

## Building it

It's written in C# with Windows Forms on .NET Framework 4.8, which already ships with Windows 10 and 11. You don't need the .NET SDK:

```bat
build.bat
```

`build.bat` uses the MSBuild that comes with .NET Framework and puts the executable in `bin\DiscordRichPresenceInstaller.exe`.

## Files

- `Program.cs`: the window, with the warning, the options and the progress bar.
- `InstallerService.cs`: the install steps and the external processes (`winget`, `git`, `python`).
