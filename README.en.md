# PowerPlanTray

[Deutsch](README.md) · English

PowerPlanTray switches Windows power plans from the system tray and automatically follows app, schedule, and individual Microsoft Hyper-V VM rules.

The interface follows your Windows display language: German for German Windows display languages, English otherwise. Power-plan names and your own rule and VM names remain unchanged.

## Usage

- Right-click the tray icon to select a power plan, manage rules, or open Settings.
- Left-click to switch between the two plans selected in Settings.
- App rules match an EXE name and support a start delay and grace period.
- Schedule rules support weekdays and overnight windows. Overnight windows belong to the starting day.
- Select **Microsoft Hyper-V: VM is running**, choose **Load VMs**, then pick a local VM. Its unique ID keeps the rule associated after a rename. Hyper-V management permission is required.
- The first matching rule wins. The previous plan is restored after the last rule ends. A manual plan change pauses automation.
- The optional Codex sleep-protection extension is off by default. When disabled, its tray-menu entries are hidden.
- The optional Claude sleep-protection extension works the same way for Claude Code (terminal and Claude desktop app). It is also off by default, has its own grace period, and reads only record types, stop reasons, and timestamps from `%USERPROFILE%\.claude\projects`.

Rules are checked at approximately five-second intervals. Schedules do not wake the PC. Power-plan changes affect the Windows host. Close an older running instance before installing an update or launching another edition.

## Downloads

Latest stable release: **[1.7.0](https://github.com/kulmi84/PowerPlanTray/releases/tag/v1.7.0)**. The older [local MSIX test package 1.6.0-beta.9](https://github.com/kulmi84/PowerPlanTray/releases/tag/v1.6.0-beta.9) includes the installer, instructions, public test certificate, and source code. Known CPU/tray delays and the differing embedded build commit are documented on its release page.

Version 1.7.0 adds separately configurable Claude Code sleep protection with its own grace period. Codex protection and the earlier CPU-use and tray-menu fixes remain included. [Changes and installation notes](docs/releases/1.7.0.md).

[GitHub releases](https://github.com/kulmi84/PowerPlanTray/releases) contain portable Windows x64 ZIPs. The standard edition contains no Codex session watcher; the Codex edition includes optional sleep protection. The Microsoft Store edition is being prepared separately and includes the optional extension, off by default.

## Privacy and support

Data is processed locally. See the [privacy notice](docs/PRIVACY.en.md) and [component notices](docs/COMPONENTS.en.md). Report bugs or suggest improvements through [GitHub Issues](https://github.com/kulmi84/PowerPlanTray/issues).

Developed by Marcin Kulmaczewski.

## Interface examples · Version 1.7.0

These views use example plans and rules; they are not preconfigured defaults.

![PowerPlanTray 1.7.0 settings in English](docs/screenshots/1.7.0/store-en-settings.png)

![Separate Codex and Claude extensions with independent grace periods](docs/screenshots/1.7.0/store-en-extensions.png)

The enabled extensions shown here are an example, not the default. Both are off by default. Claude Code in the terminal and the Code tab of the Claude desktop app is supported when local logs are available; ordinary Claude chats are not monitored.

### Earlier rule editor example

![Individual Hyper-V VM rule in English](docs/screenshots/en/05-Hyper-V-Rule.png)
