# Launcher update handoff — 2026-10-26

Clicking the game update button uses the existing desktop open-launcher message. The game now starts the launcher with an explicit update request, its installation directory, and its PID, then closes gracefully. Browser downloads retain the existing link behavior.

The launcher checks releases even when automatic checking is disabled in settings, waits up to 30 seconds for the previous game process to stop, downloads/extracts the game update, and launches the updated game. Ordinary launcher startup does not automatically update or launch. Download errors and exit-wait timeouts keep the launcher visible for retry. Both game and launcher must be shipped together for automatic handoff support; older launchers ignore the new arguments.

Validation:
- Both projects build successfully; existing WindowsBase/WebView2 and game nullable warnings remain.
- `tools/update_checks/update_checks.csproj`: arguments, absolute paths with spaces, invalid requests, exited process, running process, timeout.
- `tools/update_checks/version.cjs`: desktop message handoff and browser fallback.
- Independent code review: no actionable findings.

No live release was downloaded or installed, and no running player game was restarted during verification. Existing archive extraction behavior is unchanged.
