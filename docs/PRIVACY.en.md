# Privacy notice for PowerPlanTray

Updated: October 24, 2026

PowerPlanTray processes its settings and status information locally on your Windows PC. It contains no analytics, advertising, or telemetry services and sends no app usage data to the developer.

## Power plans and automatic rules

The app reads the available and active Windows power plans and activates a plan when you select it or when one of your rules matches. App rules compare running process names with the EXE names you specify. The process list is not sent to a server or permanently logged. Schedule rules use the local Windows time. Hyper-V rules query local VM names, unique IDs, and operating states; the selected VM ID and name are saved with your rule.

## Optional Codex extension

The Codex extension is off by default. Only when enabled does it read local Codex session files under `%USERPROFILE%\.codex\sessions` to detect tasks starting and ending. It processes event types and timestamps. Prompt and response text is not used, displayed, or saved for this function. Session files are not modified or transmitted. Disabling the extension stops monitoring and releases the sleep-protection request.

## Storage and deletion

The app saves your power-plan selections, app, schedule and VM rules, and startup and Codex options. The Store edition uses the Windows package's private local app-data folder. The portable edition uses `%APPDATA%\PowerPlanTray\settings.json`. Installing the Store edition does not automatically import portable settings. You can reset Store app data through Windows app settings or delete the portable settings file after closing the app.

Microsoft's privacy terms additionally apply to installation, updates, and Store use. PowerPlanTray requires no separate user account.

## Contact

Send questions or privacy feedback through the [PowerPlanTray GitHub project](https://github.com/kulmi84/PowerPlanTray/issues). Do not post private Codex session files or confidential content there.
