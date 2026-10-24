# Microsoft-Store-Vorbereitung

Die Store-Ausgabe ist **eine gemeinsame App** mit App-/Zeitregeln und optionaler Codex-Erweiterung. Der Codex-Schutz ist bei einer frischen Installation aus und lässt sich unter **Einstellungen > Codex-Erweiterung (Standby-Schutz) aktivieren** ein- und ausschalten. Eine bewusst gewählte Einstellung bleibt bei Updates erhalten.

## Paketidentität

Im Microsoft Partner Center einen MSIX-App-Eintrag mit dem Namen PowerPlanTray reservieren. Unter Produktverwaltung / Produktidentität die exakten Werte für Package/Identity/Name, Package/Identity/Publisher und PublisherDisplayName übernehmen. Keine Publisher-GUID oder App-ID erraten. Die Vorlage enthält absichtlich keine produktive Identität.

```powershell
./packaging/Build-StorePackage.ps1 `
  -IdentityName '<Package/Identity/Name aus Partner Center>' `
  -Publisher '<Package/Identity/Publisher aus Partner Center>' `
  -PublisherDisplayName '<PublisherDisplayName aus Partner Center>' `
  -PackageVersion '1.6.9.0'
```

Voraussetzungen: Windows, .NET 8 SDK und Windows SDK mit MakeAppx. Alternativ nach Aufnahme dieses Workflows in den Hauptbranch **Build Microsoft Store MSIX** in GitHub Actions mit denselben Identitätswerten starten. Das Paket wird stets mit Codex-Unterstützung gebaut, die zur Laufzeit standardmäßig deaktiviert ist.

Der Build erzeugt `artifacts/store/PowerPlanTray-Store-1.6.9.0-x64.msix`, prüft das Manifest mit MakeAppx und berechnet SHA-256. Das unsignierte MSIX ist für die Store-Einreichung bestimmt; Microsoft signiert es nach der Zertifizierung. Es ist kein direkt installierbares Sideload-Paket. Es werden keine Zertifikate oder privaten Schlüssel im Repository benötigt.

Die vierte Versionsstelle bleibt für den Store 0. Beta-Kennzeichnungen gehören nicht in die numerische MSIX-Version. Dieser Stand basiert auf 1.6.0; zunächst als privates Paket/Flight testen und vor einer öffentlichen Veröffentlichung einen entsprechend geprüften stabilen Stand verwenden. Versionen bei späteren Uploads passend erhöhen.

## Verhalten als Windows-Paket

- Windows 11 x64, eigenständige Anwendung ohne separate .NET-Installation.
- Ein Eintrag im Startmenü; danach Bedienung über das Tray-Symbol.
- Eine Instanz pro Windows-Sitzung.
- Einstellungen unter dem privaten `LocalState`-Ordner des Pakets; portable Einstellungen werden nicht automatisch übernommen.
- Autostart über die Windows-StartupTask-API, anfangs aus. Eine manuelle Deaktivierung durch Windows oder eine Richtlinie wird respektiert. Der StartupTask startet dieselbe EXE mit `--startup`, sodass der konfigurierte Start-Energieplan nur beim Autostart greift.
- `runFullTrust` ist für die vorhandene Desktop-/Tray-App erforderlich. Keine Administrator-Elevation, kein Dienst und keine Installation von Treibern.

## Vor der Einreichung

1. Reale Paketidentität eintragen und MSIX bauen.
2. Store-/Flight-installierte App auf einem Windows-11-Testgerät prüfen: Startmenü, Tray, Energieplanwechsel, App-/Zeitregeln, Beenden und Neustart, Update, Deinstallation und Autostart.
3. Kontrollieren, dass Codex bei neuen Einstellungen aus ist, erst nach Aktivierung überwacht und beim Abschalten die Anforderung an Windows freigibt.
4. Windows App Certification Kit ausführen. Ein erfolgreicher MakeAppx-Build ersetzt diese Zertifizierung und den Installationstest nicht.
5. Eigene aktuelle App-Screenshots und die Beschreibung aus `store-listing-de-DE.md` ergänzen. Keine Bilder fremder Anwendungen verwenden.
6. Datenschutz-URL, Support-URL, Preise/Verfügbarkeit und Altersfreigabe im Partner Center vervollständigen. Rechtliche Erklärungen, Altersfreigabe-Fragebogen und endgültige Einreichung durch den Kontoinhaber bestätigen lassen.

## Offizielle Grundlagen

- [Win32-Apps über den Microsoft Store verteilen](https://learn.microsoft.com/en-us/windows/apps/distribute-through-store/how-to-distribute-your-win32-app-through-microsoft-store)
- [Paketanforderungen und Versionierung](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/package-version-numbering)
- [Windows StartupTask](https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.startuptask)
