using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using HonorificCarousel.Windows;
using Lumina.Excel.Sheets;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace HonorificCarousel;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/honocar";

    private List<HonorificTitleOption> availableTitles = [];
    private string lastObservedCharacterKey = string.Empty;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (NormalizeConfiguration())
        {
            Configuration.Save();
        }

        HonorificIpc = new HonorificIpc(PluginInterface, Log);
        CarouselRunner = new CarouselRunner(Configuration, HonorificIpc, ClientState, ObjectTable, GetActiveSourceCharacterForLocalPlayer, GetActiveSequence);

        ConfigWindow = new ConfigWindow(this);
        HelpWindow = new HelpWindow();
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(HelpWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Honorific Carousel configuration window.",
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;
        Framework.Update += OnFrameworkUpdate;

        Log.Information($"{PluginInterface.Manifest.Name} loaded.");
    }

    public Configuration Configuration { get; init; }
    public HonorificIpc HonorificIpc { get; init; }
    public CarouselRunner CarouselRunner { get; init; }
    public IReadOnlyList<HonorificTitleOption> AvailableTitles => availableTitles;
    public HonorificSourceCharacter? ActiveSourceCharacter => GetActiveSourceCharacterForLocalPlayer();
    public List<CarouselSequenceItem> ActiveSequence => GetActiveSequence();
    public int SelectedAvailableTitleIndex { get; set; }
    public string PendingSourceCharacterName { get; set; } = string.Empty;
    public string PendingSourceHomeWorld { get; set; } = string.Empty;
    public string RefreshStatus { get; private set; } = "Refresh Honorific titles to begin.";

    public readonly WindowSystem WindowSystem = new("HonorificCarousel");
    private ConfigWindow ConfigWindow { get; init; }
    private HelpWindow HelpWindow { get; init; }

    public void Dispose()
    {
        CarouselRunner.StopAndClear("Honorific Carousel unloaded. Honorific title cleared.");
        Framework.Update -= OnFrameworkUpdate;

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        HelpWindow.Dispose();
        CommandManager.RemoveHandler(CommandName);
    }

    public void RefreshHonorificTitles()
    {
        if (!TryGetSelectedSourceCharacter(out var characterName, out var homeWorldId, out var sourceError))
        {
            RefreshStatus = sourceError;
            return;
        }

        if (!HonorificIpc.TryGetTitles(characterName, homeWorldId, out var titles, out var error))
        {
            RefreshStatus = error;
            return;
        }

        availableTitles = titles;
        if (SelectedAvailableTitleIndex >= availableTitles.Count)
        {
            SelectedAvailableTitleIndex = availableTitles.Count == 0 ? 0 : availableTitles.Count - 1;
        }

        var updatedCount = RefreshSequenceItemsFromAvailableTitles();

        RefreshStatus = availableTitles.Count == 0
            ? $"Honorific returned no saved titles for {characterName} ({homeWorldId})."
            : $"Loaded {availableTitles.Count} Honorific title(s) for {characterName} ({homeWorldId}). Updated {updatedCount} sequence item(s).";
    }

    public void AddCurrentCharacterSource()
    {
        if (!PlayerState.IsLoaded)
        {
            RefreshStatus = "Local player data is not available yet.";
            return;
        }

        AddOrSelectSourceCharacter(PlayerState.CharacterName, PlayerState.HomeWorld.RowId);
    }

    public void AddPendingSourceCharacter()
    {
        if (!TryResolveWorldId(PendingSourceHomeWorld, out var homeWorldId))
        {
            RefreshStatus = "Enter a valid home world name or id before adding a source character.";
            return;
        }

        AddOrSelectSourceCharacter(PendingSourceCharacterName, homeWorldId);
    }

    public void RemoveSelectedSourceCharacter()
    {
        var index = Configuration.SelectedSourceCharacterIndex;
        if (index < 0 || index >= Configuration.SourceCharacters.Count)
        {
            return;
        }

        var removed = Configuration.SourceCharacters[index];
        Configuration.SourceCharacters.RemoveAt(index);
        Configuration.SelectedSourceCharacterIndex = Math.Clamp(index, 0, Math.Max(0, Configuration.SourceCharacters.Count - 1));
        Configuration.Save();
        availableTitles = [];
        SelectedAvailableTitleIndex = 0;
        CarouselRunner.Restart(true);
        RefreshStatus = $"Removed source character {removed.Name} ({removed.HomeWorldId}).";
    }

    public void SelectSourceCharacter(int index)
    {
        if (index < 0 || index >= Configuration.SourceCharacters.Count)
        {
            return;
        }

        var previousSource = GetActiveSourceCharacter();
        Configuration.SelectedSourceCharacterIndex = index;
        Configuration.Save();
        availableTitles = [];
        SelectedAvailableTitleIndex = 0;
        CarouselRunner.Restart(previousSource != Configuration.SourceCharacters[index]);
        var source = Configuration.SourceCharacters[index];
        RefreshStatus = $"Selected source character {source.Name} ({source.HomeWorldId}).";
    }

    public void SetEnabled(bool enabled)
    {
        var source = GetActiveSourceCharacter();
        if (source == null)
        {
            RefreshStatus = "Add or select a source character before enabling the carousel.";
            return;
        }

        if (source.Enabled == enabled)
        {
            return;
        }

        source.Enabled = enabled;
        Configuration.Save();

        if (enabled)
        {
            CarouselRunner.Restart(false);
            RefreshStatus = $"Carousel enabled for {source.Name} ({source.HomeWorldId}).";
            return;
        }

        CarouselRunner.StopAndClear($"Carousel disabled for {source.Name} ({source.HomeWorldId}). Honorific title cleared.");
    }

    public void SetLoop(bool loop)
    {
        var source = GetActiveSourceCharacter();
        if (source == null)
        {
            RefreshStatus = "Add or select a source character before changing loop.";
            return;
        }

        source.Loop = loop;
        Configuration.Save();
    }

    public void AddSelectedTitleToSequence()
    {
        if (availableTitles.Count == 0 ||
            SelectedAvailableTitleIndex < 0 ||
            SelectedAvailableTitleIndex >= availableTitles.Count)
        {
            RefreshStatus = "Select an Honorific title before adding it to the sequence.";
            return;
        }

        var title = availableTitles[SelectedAvailableTitleIndex];
        GetActiveSequence().Add(new CarouselSequenceItem
        {
            DisplayName = title.DisplayName,
            TitleDataJson = title.TitleDataJson,
            DurationSeconds = 10,
        });
        Configuration.Save();
        CarouselRunner.Restart(true);
    }

    public void MoveSequenceItem(int index, int delta)
    {
        var sequence = GetActiveSequence();
        var target = index + delta;
        if (index < 0 ||
            index >= sequence.Count ||
            target < 0 ||
            target >= sequence.Count)
        {
            return;
        }

        (sequence[index], sequence[target]) =
            (sequence[target], sequence[index]);
        Configuration.Save();
        CarouselRunner.Restart(true);
    }

    public void RemoveSequenceItem(int index)
    {
        var sequence = GetActiveSequence();
        if (index < 0 || index >= sequence.Count)
        {
            return;
        }

        sequence.RemoveAt(index);
        Configuration.Save();
        CarouselRunner.Restart(true);
    }

    public void SaveSequenceDuration(int index, int durationSeconds)
    {
        var sequence = GetActiveSequence();
        if (index < 0 || index >= sequence.Count)
        {
            return;
        }

        sequence[index].DurationSeconds = durationSeconds < 1 ? 1 : durationSeconds;
        Configuration.Save();
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleHelpUi() => HelpWindow.Toggle();

    private void OnCommand(string command, string args)
    {
        ConfigWindow.Toggle();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        SyncSourceCharacterToLocalPlayer();
        CarouselRunner.Update();
    }

    private bool NormalizeConfiguration()
    {
        var changed = false;
        Configuration.SourceCharacters ??= [];
        changed |= Configuration.SourceCharacters.RemoveAll(character =>
            string.IsNullOrWhiteSpace(character.Name) || character.HomeWorldId == 0) > 0;
        foreach (var sourceCharacter in Configuration.SourceCharacters)
        {
            sourceCharacter.Sequence ??= [];
            NormalizeSequence(sourceCharacter.Sequence);
        }

        Configuration.SelectedSourceCharacterIndex = Math.Clamp(
            Configuration.SelectedSourceCharacterIndex,
            0,
            Math.Max(0, Configuration.SourceCharacters.Count - 1));

        Configuration.Sequence ??= [];
        NormalizeSequence(Configuration.Sequence);
        if (Configuration.Sequence.Count > 0 && Configuration.SourceCharacters.Count > 0)
        {
            var firstSource = Configuration.SourceCharacters[0];
            if (firstSource.Sequence.Count == 0)
            {
                firstSource.Sequence.AddRange(Configuration.Sequence);
            }

            firstSource.Enabled = Configuration.Enabled;
            firstSource.Loop = Configuration.Loop;

            Configuration.Sequence = [];
            Configuration.Enabled = false;
            Configuration.Loop = true;
            changed = true;
        }

        return changed;
    }

    private bool TryGetSelectedSourceCharacter(out string characterName, out uint homeWorldId, out string error)
    {
        characterName = string.Empty;
        homeWorldId = 0;
        error = string.Empty;

        var index = Configuration.SelectedSourceCharacterIndex;
        if (index >= 0 && index < Configuration.SourceCharacters.Count)
        {
            var source = Configuration.SourceCharacters[index];
            characterName = source.Name.Trim();
            homeWorldId = source.HomeWorldId;
        }
        else if (PlayerState.IsLoaded)
        {
            characterName = PlayerState.CharacterName;
            homeWorldId = PlayerState.HomeWorld.RowId;
        }

        if (string.IsNullOrWhiteSpace(characterName) || homeWorldId == 0)
        {
            error = "Add or select a source character before refreshing Honorific titles.";
            return false;
        }

        return true;
    }

    private void AddOrSelectSourceCharacter(string characterName, uint homeWorldId)
    {
        characterName = characterName.Trim();
        if (string.IsNullOrWhiteSpace(characterName) || homeWorldId == 0)
        {
            RefreshStatus = "Enter a character name and home world id before adding a source character.";
            return;
        }

        var existingIndex = Configuration.SourceCharacters.FindIndex(character =>
            character.HomeWorldId == homeWorldId &&
            string.Equals(character.Name, characterName, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            Configuration.SelectedSourceCharacterIndex = existingIndex;
            RefreshStatus = $"Selected source character {characterName} ({homeWorldId}).";
        }
        else
        {
            Configuration.SourceCharacters.Add(new HonorificSourceCharacter
            {
                Name = characterName,
                HomeWorldId = homeWorldId,
            });
            Configuration.SelectedSourceCharacterIndex = Configuration.SourceCharacters.Count - 1;
            RefreshStatus = $"Added source character {characterName} ({homeWorldId}).";
        }

        PendingSourceCharacterName = string.Empty;
        PendingSourceHomeWorld = string.Empty;
        availableTitles = [];
        SelectedAvailableTitleIndex = 0;
        Configuration.Save();
        CarouselRunner.Restart(true);
    }

    private int RefreshSequenceItemsFromAvailableTitles()
    {
        var updatedCount = 0;
        foreach (var sequenceItem in GetActiveSequence())
        {
            var matchingTitle = availableTitles.Find(title => TitleIdentityMatches(title.TitleDataJson, sequenceItem.TitleDataJson));
            if (matchingTitle == null || matchingTitle.TitleDataJson == sequenceItem.TitleDataJson)
            {
                continue;
            }

            sequenceItem.DisplayName = matchingTitle.DisplayName;
            sequenceItem.TitleDataJson = matchingTitle.TitleDataJson;
            updatedCount++;
        }

        if (updatedCount > 0)
        {
            Configuration.Save();
            CarouselRunner.Restart(true);
        }

        return updatedCount;
    }

    private static bool TitleIdentityMatches(string leftJson, string rightJson)
    {
        try
        {
            var left = JObject.Parse(leftJson);
            var right = JObject.Parse(rightJson);
            return string.Equals(left.Value<string>("Title"), right.Value<string>("Title"), StringComparison.Ordinal) &&
                   left.Value<bool?>("IsPrefix") == right.Value<bool?>("IsPrefix");
        }
        catch
        {
            return false;
        }
    }

    private static bool TryResolveWorldId(string homeWorld, out uint homeWorldId)
    {
        homeWorldId = 0;
        homeWorld = homeWorld.Trim();

        if (uint.TryParse(homeWorld, out homeWorldId) && homeWorldId > 0)
        {
            return true;
        }

        foreach (var world in DataManager.GetExcelSheet<World>())
        {
            if (string.Equals(world.Name.ToString(), homeWorld, StringComparison.OrdinalIgnoreCase))
            {
                homeWorldId = world.RowId;
                return true;
            }
        }

        return false;
    }

    private List<CarouselSequenceItem> GetActiveSequence()
    {
        var source = GetActiveSourceCharacterForLocalPlayer();
        if (source != null)
        {
            source.Sequence ??= [];
            return source.Sequence;
        }

        Configuration.Sequence ??= [];
        return Configuration.Sequence;
    }

    private HonorificSourceCharacter? GetActiveSourceCharacter()
    {
        var index = Configuration.SelectedSourceCharacterIndex;
        if (index >= 0 && index < Configuration.SourceCharacters.Count)
        {
            return Configuration.SourceCharacters[index];
        }

        return null;
    }

    private HonorificSourceCharacter? GetActiveSourceCharacterForLocalPlayer()
    {
        if (!TryGetLocalCharacterIdentity(out var characterName, out var homeWorldId))
        {
            return null;
        }

        var source = GetActiveSourceCharacter();
        if (source == null ||
            source.HomeWorldId != homeWorldId ||
            !string.Equals(source.Name, characterName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return source;
    }

    private void SyncSourceCharacterToLocalPlayer()
    {
        if (!TryGetLocalCharacterIdentity(out var characterName, out var homeWorldId))
        {
            if (!string.IsNullOrEmpty(lastObservedCharacterKey))
            {
                lastObservedCharacterKey = string.Empty;
                availableTitles = [];
                SelectedAvailableTitleIndex = 0;
                CarouselRunner.Restart(true);
                RefreshStatus = "Waiting for local character data.";
            }

            return;
        }

        var characterKey = $"{characterName}\u001f{homeWorldId}";
        if (characterKey == lastObservedCharacterKey)
        {
            return;
        }

        lastObservedCharacterKey = characterKey;
        var matchingIndex = Configuration.SourceCharacters.FindIndex(source =>
            source.HomeWorldId == homeWorldId &&
            string.Equals(source.Name, characterName, StringComparison.OrdinalIgnoreCase));

        availableTitles = [];
        SelectedAvailableTitleIndex = 0;
        CarouselRunner.Restart(true);

        if (matchingIndex < 0)
        {
            RefreshStatus = $"No Honorific Carousel source is configured for {characterName} ({homeWorldId}). Carousel is inactive.";
            return;
        }

        Configuration.SelectedSourceCharacterIndex = matchingIndex;
        Configuration.Save();
        var selected = Configuration.SourceCharacters[matchingIndex];
        RefreshStatus = $"Auto-selected {selected.Name} ({selected.HomeWorldId}).";
    }

    private static bool TryGetLocalCharacterIdentity(out string characterName, out uint homeWorldId)
    {
        characterName = string.Empty;
        homeWorldId = 0;

        if (!PlayerState.IsLoaded)
        {
            return false;
        }

        characterName = PlayerState.CharacterName.Trim();
        homeWorldId = PlayerState.HomeWorld.RowId;
        return !string.IsNullOrWhiteSpace(characterName) && homeWorldId != 0;
    }

    private static void NormalizeSequence(List<CarouselSequenceItem> sequence)
    {
        foreach (var item in sequence)
        {
            item.DurationSeconds = item.DurationSeconds < 1 ? 1 : item.DurationSeconds;
        }
    }
}
