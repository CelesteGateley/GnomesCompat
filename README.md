# GnomesCompat

A BepInEx 5 preloader patcher that makes **Gnomes 0.7.0** work on the current Stonewards build
without touching `Gnomes.dll`.

> **This is an unofficial fix.** Gnomes is made by **CatedralEmChamas**, and all credit for the mod
> belongs to them. GnomesCompat contains no Gnomes or game code. It only fills in what a game
> update removed. Please don't report problems with this patcher to the Gnomes author. When Gnomes
> gets an official update, uninstall GnomesCompat.
>
> The icon is based on the Gnomes icon by CatedralEmChamas, with a wrench added.

## Install

Install with a Thunderstore mod manager (Gale or r2modman). This also installs BepInExPack and
Gnomes. Only the host needs it, because the code it fixes runs on the server. Launch the game
through the mod manager. Starting it from Steam runs it without mods.

Gnomes 0.7.0 was compiled against an older game build. A later game update removed or renamed five
members it uses, which caused about 1,600 `MissingFieldException` and `MissingMethodException`
errors per run. The chest gnome and the terrain/digging logic also stopped working. While the game
loads, this patcher adds those members back to `Assembly-CSharp.dll` as thin shims:

| Missing member | Shim |
|---|---|
| `ChestDataSO.level1DropPoolSO` / `level2DropPoolSO` | Non-serialized fields. An added `OnEnable` copies `dropPoolSO` into level 1. Level 2 stays null. |
| `PlayerStats.ServerHealWithFX(float)` | Overload that calls `ServerHealWithFX(float, NetworkIdentity)` with a `null` healer. |
| `DiggingPattern.Id` | Getter that returns `null`. |
| `DiggingManager.SDiggingPattern.id` | Non-serialized `string` field, left `null`. |

Gnomes' `DigWorld.BuildRooms` pairs patterns with sync entries by `Id == id` **and** position
within 0.1 m. With both IDs null, the position check does the matching. Room keys become `_0`,
`_1`, … and stay unique.

Each shim is skipped if its member already exists, so the patcher does nothing once the game or
Gnomes changes again. **Remove it when Gnomes is updated.**

The four Gnomes methods that use these members run on the server, so only the host strictly needs
the patcher.

## Build and install

Requires the .NET SDK. The build references `Mono.Cecil.dll` and `BepInEx.dll` from a BepInEx 5
`core` folder, set by the `BepInExCore` property in `Directory.Build.props`. By default it points at
Gale's Default profile for Stonewards (`%APPDATA%\com.kesomannen.gale\stonewards\profiles\Default\BepInEx\core`).
To use another folder, pass `-p:BepInExCore=<path>` to `dotnet build`, or set the property in a
`Directory.Build.props.user` file next to it. Git ignores that file.

```sh
dotnet build -c Release
# copy bin/Release/GnomesCompat.dll to <profile>/BepInEx/patchers/GnomesCompat/
```

To build the Thunderstore zip, run `pwsh ./package.ps1`. It takes the version from
`thunderstore/manifest.json`, needs `thunderstore/icon.png`, and writes
`dist/GnomesCompat-<version>.zip`.

## Releases

GitHub Actions (`.github/workflows/build.yml`) builds the package on every push and pull request,
and uploads it as a workflow artifact. A push to `main` also creates a GitHub release `v<version>`
with the zip attached, if that version has no release yet. To release, bump `version_number` in
`thunderstore/manifest.json`, add a matching `## <version>` section to `CHANGELOG.md` (it becomes
the release notes), and push.

When a new release is created, a second job publishes the zip to Thunderstore with `tcli`, using
`thunderstore/thunderstore.toml` for the team, community and categories. It needs a
`THUNDERSTORE_TOKEN` repo secret (a service-account token from your Thunderstore team) and runs in
the `thunderstore` environment, which can require your approval first.

On a successful load, `LogOutput.log` shows four `[Info : GnomesCompat]` lines near the top.
Launch through Gale. Starting the game from Steam skips BepInEx entirely.

## Tools (`tools/`)

The single-file scripts run with `dotnet run <file>.cs -- <args>` (.NET 10 SDK). `Managed` is
`Stonewards/Stonewards_Data/Managed`.

- `refcheck/check.cs <Assembly-CSharp.dll> <Gnomes.dll>`: lists every member Gnomes uses from the
  game that is missing or has a different parameter count. Run it after a game update to see what
  broke.
- `typedump/dump.cs <dll> <Type1,Type2,…>`: dumps the fields, properties and methods of the named
  types.
- `ildump/il.cs <dll> <::member,Type::member,…> [full]`: finds the methods whose IL references the
  given members and prints the nearby IL. Add `full` to print the whole method.
- `harness/`: .NET Framework console app that applies the patcher to a copy of the game DLL:
  `dotnet build -c Release`, then
  `bin/Release/Harness.exe <Managed> <out.dll>`. Run `refcheck` on `out.dll` afterwards. It should
  report `0 problems`.

## License

MIT. See `LICENSE`.
