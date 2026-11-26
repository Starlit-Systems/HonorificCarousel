# Honorific Carousel

Honorific Carousel rotates saved Honorific title presets for the current character through Honorific IPC. It is a small companion plugin for people who already use Honorific and want a per-character timed sequence of saved titles.

It does not render titles, edit Honorific settings, or manipulate nameplates directly. Honorific remains the source of truth for title text and styling.

## Custom Plugin Repository

After this repository has a published GitHub release, add this custom repository URL in Dalamud:

```text
https://raw.githubusercontent.com/starlit-systems/HonorificCarousel/main/pluginmaster.json
```

Friends installing from the custom repository must also have Honorific installed and configured, because Honorific Carousel only talks to Honorific through IPC.

## Building

Build the solution from the repository root:

```powershell
dotnet build .\HonorificCarousel.sln -c Debug
```

The debug plugin output is written to:

```text
HonorificCarousel\bin\x64\Debug\HonorificCarousel.dll
```

Release builds also create a Dalamud-ready zip at:

```text
HonorificCarousel\bin\x64\Release\HonorificCarousel\latest.zip
```

## In Game

Add the built DLL as a Dalamud dev plugin, enable `Honorific Carousel`, and use `/honocar` to open the config window.

## Publishing

This project is set up for the `starlit-systems/HonorificCarousel` GitHub repository.

1. Create the public GitHub repository as `HonorificCarousel` under the `starlit-systems` account.
2. Push this repository's `main` branch.
3. Create and push a version tag, for example `v0.0.0.1`.
4. GitHub Actions will build the plugin and publish `HonorificCarousel.zip` to the release.
5. Share the custom repository URL above with testers.

When bumping versions, update both `HonorificCarousel/HonorificCarousel.csproj` and `pluginmaster.json`.
