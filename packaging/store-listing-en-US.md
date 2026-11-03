# English Store listing

## Product name
PowerPlanTray – Automatic Power Plan Switching

## Short description
Switch Windows power plans and automatically adapt them to running apps, Hyper-V virtual machines, or a schedule.

## Description
PowerPlanTray puts your Windows power plans in the system tray. Select a plan with a right-click, or left-click to switch between two plans of your choice.

App rules activate a selected power plan while a particular program is running. Choose an EXE name and configure a start delay and grace period. Schedule rules run on selected weekdays and time windows, including overnight windows.

For Microsoft Hyper-V, select an individual local virtual machine. Its unique VM ID keeps the rule associated after a rename. Only the Running state activates its rule. Hyper-V and permission to manage local virtual machines are required.

The first matching rule wins. When no rule matches, PowerPlanTray restores the previous plan. A manual plan change pauses automation.

Optional sleep protection for Codex and Claude Code prevents automatic idle sleep during detected tasks, each with its own configurable grace period. Both are off by default and can be enabled in Settings. Processing stays on your PC. When disabled, their entries are hidden from the tray menu.

Windows 11, x64. Schedule rules work only while PowerPlanTray is running and do not wake your PC. Sleep protection does not prevent a sleep action you request manually.

## Features
- Switch Windows power plans from the tray
- App, schedule, and individual Microsoft Hyper-V VM rules
- Configurable rule priority, start delay, and grace period
- Restore the previous power plan after rules end
- Optional Windows startup and startup power plan
- Optional Codex and Claude Code sleep protection, off by default
- German and English interface following the Windows display language

## What's new in 1.7.0
- Optional sleep protection for Claude Code (terminal and Claude desktop app), enabled separately from Codex protection with its own grace period

## Support
https://github.com/kulmi84/PowerPlanTray/issues

## Privacy
https://github.com/kulmi84/PowerPlanTray/blob/main/docs/PRIVACY.en.md
