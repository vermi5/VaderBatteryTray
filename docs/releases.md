# Releases

1. Update user-facing documentation and the changelog.
2. Update the assembly and file version in `VaderBatteryTray.cs`.
3. Run `build.cmd` and `build_led_protocol_test.cmd` locally.
4. Merge the release change through GitHub.
5. Push an annotated `vX.Y.Z` tag from the merged commit.

The tag workflow builds the project, runs the protocol test, packages the application, attaches the ZIP and its `.sha256` file to the GitHub release, and includes the artifact checksum in the release notes.

Verify a downloaded package with:

```powershell
Get-FileHash .\VaderBatteryTray-X.Y.Z.zip -Algorithm SHA256
```

The checksum recorded in the release is authoritative for that release asset. A ZIP made locally at another time may have a different hash because archive metadata changes.
