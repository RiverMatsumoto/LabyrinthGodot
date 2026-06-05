# Zed Project Config

This directory ports the local VS Code workflow where Zed has close equivalents.

## Tasks

`.zed/tasks.json` contains Zed task equivalents for:

- `build`
- `build-without-tests`
- `coverage`
- `build-solutions`

The shell-based tasks assume `bash` and a `GODOT` environment variable, matching
the VS Code setup.

## Debug

`.zed/debug.json` contains best-effort C# launch equivalents for the Godot debug
profiles. They use the `godot` adapter provided by the GDScript Zed extension.

The VS Code VSCodium-specific `pipeTransport` profile was not ported because
Zed debug configs do not have a direct `pipeTransport` equivalent.

Zed's official debugger docs do not currently list C#/.NET as a built-in debug
target, and this machine does not have a registered `netcoredbg` adapter. These
profiles should be discoverable as Godot debug profiles, but they are not a
replacement for full CLR/C# debugging unless a future Zed extension provides
that adapter.

## Snippets

Zed snippets are global or extension-provided rather than loaded directly from a
project `.zed/snippets` directory. The VS Code C# snippets were packaged as a
local snippet extension at:

```text
.zed/labyrinth-snippets
```

Install it with `zed: install dev extension` and select that directory.
