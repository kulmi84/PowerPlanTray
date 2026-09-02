# PowerPlanTray

Minimalistisches Windows-Tray-Tool zum Umschalten zwischen drei vorhandenen Energieplänen.

## Enthaltene Pläne

- Höchstleistung
- HP Optimized (Modern Standby)
- Ausbalanciert

Die GUIDs sind aktuell fest auf den Zielrechner eingestellt.

## Bedienung

- Linksklick auf das Tray-Symbol: Höchstleistung ↔ HP Optimized
- Rechtsklick auf das Tray-Symbol: Plan auswählen
- Haken zeigt den aktiven Plan
- Status wird alle 5 Sekunden aktualisiert

## Tray-Symbole

Die gewünschten weißen, monochromen Symbole liegen unter `assets/`:

- `assets/bolt-white.svg` – Höchstleistung
- `assets/hp-white.svg` – HP Optimized

Beide sind für dunkle Windows-11-Taskleisten auf transparentem Hintergrund ausgelegt.

## Wichtig

PowerPlanTray ändert keine Energieplaneinstellungen, keine Registry-Werte und nichts an Modern Standby. Es verwendet ausschließlich `powercfg /setactive <GUID>`.

## Portable EXE bauen

Lokal mit .NET 8 SDK:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true
```

Die EXE liegt anschließend unter:

`bin/Release/net8.0-windows/win-x64/publish/PowerPlanTray.exe`
