# Datenschutzhinweise für PowerPlanTray

[English](PRIVACY.en.md)

Stand: 24. Oktober 2026

PowerPlanTray verarbeitet seine Einstellungen und Statusinformationen lokal auf Ihrem Windows-PC. Die Anwendung enthält keine Analyse-, Werbe- oder Telemetriedienste und überträgt keine App-Nutzungsdaten an den Entwickler.

## Energiepläne und automatische Regeln

Die App liest die vorhandenen Energiepläne und den aktiven Plan über Windows aus. Bei einer manuellen Auswahl oder einer selbst angelegten Regel aktiviert sie den gewählten Plan. Für App-Regeln vergleicht sie die Namen laufender Prozesse mit den eingetragenen EXE-Dateinamen. Diese Prozessliste wird nicht an einen Server übertragen oder dauerhaft protokolliert. Zeitregeln verwenden die lokale Windows-Uhrzeit. Hyper-V-Regeln lesen lokale VM-Namen, eindeutige IDs und Betriebszustände; die gewählte VM-ID und ihr Name werden mit der Regel gespeichert.

## Optionale Codex-Erweiterung

Die Codex-Erweiterung ist standardmäßig ausgeschaltet. Nur wenn Sie sie einschalten, liest die App lokale Codex-Sitzungsdateien unter `%USERPROFILE%\.codex\sessions`, um laufende Aufgaben und deren Ende zu erkennen. Verarbeitet werden Ereignistypen und Zeitstempel. Prompt- und Antworttexte werden nicht für die Funktion ausgewertet, angezeigt oder gespeichert. Die Sitzungsdateien werden nicht verändert und nicht übertragen. Deaktivieren Sie die Erweiterung in den Einstellungen, endet die Überwachung und die Standby-Sperre wird freigegeben.

## Speicherung und Löschung

Gespeichert werden Ihre ausgewählten Energiepläne, App-, Zeit- und VM-Regeln sowie die Optionen für Autostart und Codex-Schutz. Die Store-Version verwendet den privaten lokalen App-Datenordner des Windows-Pakets. Die portable Version verwendet `%APPDATA%\PowerPlanTray\settings.json`. Eine Neuinstallation der Store-Version übernimmt keine Einstellungen der portablen Version automatisch. Lokale Daten der Store-Version können über die Windows-App-Einstellungen zurückgesetzt werden; portable Einstellungen können nach Beenden der App durch Löschen der genannten Datei entfernt werden.

Installation, Updates und Store-Nutzung unterliegen zusätzlich den Datenschutzbestimmungen von Microsoft. PowerPlanTray erfordert kein eigenes Benutzerkonto.

## Kontakt

Fragen oder Hinweise zum Datenschutz können über das [GitHub-Projekt PowerPlanTray](https://github.com/kulmi84/PowerPlanTray/issues) gemeldet werden. Bitte veröffentlichen Sie dort keine privaten Codex-Sitzungsdateien oder vertraulichen Inhalte.
