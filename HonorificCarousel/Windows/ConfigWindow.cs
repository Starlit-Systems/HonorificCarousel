using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Newtonsoft.Json.Linq;

namespace HonorificCarousel.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("Honorific Carousel###HonorificCarouselConfig")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 420),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        this.plugin = plugin;
        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawStatus();
        ImGui.Separator();

        if (ImGui.Button("Help"))
        {
            plugin.ToggleHelpUi();
        }

        ImGui.Spacing();

        var activeSource = plugin.ActiveSourceCharacter;
        ImGui.Text(activeSource == null
            ? "No source character selected."
            : $"Settings for {GetSourceCharacterLabel(activeSource)}");

        var enabled = activeSource?.Enabled ?? false;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            plugin.SetEnabled(enabled);
        }

        var loop = activeSource?.Loop ?? true;
        if (ImGui.Checkbox("Loop", ref loop))
        {
            plugin.SetLoop(loop);
        }

        ImGui.Spacing();
        DrawSourceCharacters();

        ImGui.Spacing();
        ImGui.Separator();
        DrawAvailableTitles();

        ImGui.Spacing();
        ImGui.Separator();
        DrawSequence();
    }

    private void DrawStatus()
    {
        ImGui.Text(plugin.HonorificIpc.AvailabilityText);
        ImGui.Text(plugin.CarouselRunner.StatusText);

        if (!string.IsNullOrWhiteSpace(plugin.RefreshStatus))
        {
            ImGui.Text(plugin.RefreshStatus);
        }
    }

    private void DrawAvailableTitles()
    {
        if (ImGui.Button("Refresh Honorific Titles"))
        {
            plugin.RefreshHonorificTitles();
        }

        var availableTitles = plugin.AvailableTitles;
        var selectedTitleName = availableTitles.Count == 0
            ? "No Honorific titles loaded"
            : availableTitles[plugin.SelectedAvailableTitleIndex].DisplayName;

        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("Available saved Honorific titles", selectedTitleName))
        {
            for (var i = 0; i < availableTitles.Count; i++)
            {
                var selected = i == plugin.SelectedAvailableTitleIndex;
                if (ImGui.Selectable($"{availableTitles[i].DisplayName} - {GetStyleSummary(availableTitles[i].TitleDataJson)}##available{i}", selected))
                {
                    plugin.SelectedAvailableTitleIndex = i;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        if (ImGui.Button("Add selected title to carousel sequence"))
        {
            plugin.AddSelectedTitleToSequence();
        }
    }

    private void DrawSourceCharacters()
    {
        ImGui.Text("Source Character");

        var sourceCharacters = configuration.SourceCharacters;
        var selectedSourceName = sourceCharacters.Count == 0
            ? "Current local character"
            : GetSourceCharacterLabel(sourceCharacters[configuration.SelectedSourceCharacterIndex]);

        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("Saved Honorific character", selectedSourceName))
        {
            for (var i = 0; i < sourceCharacters.Count; i++)
            {
                var selected = i == configuration.SelectedSourceCharacterIndex;
                if (ImGui.Selectable($"{GetSourceCharacterLabel(sourceCharacters[i])}##source{i}", selected))
                {
                    plugin.SelectSourceCharacter(i);
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        if (ImGui.Button("Add current character"))
        {
            plugin.AddCurrentCharacterSource();
        }

        ImGui.SameLine();
        if (ImGui.Button("Remove selected source"))
        {
            plugin.RemoveSelectedSourceCharacter();
        }

        var pendingName = plugin.PendingSourceCharacterName;
        ImGui.SetNextItemWidth(260);
        if (ImGui.InputText("Character name", ref pendingName, 64))
        {
            plugin.PendingSourceCharacterName = pendingName;
        }

        var pendingWorld = plugin.PendingSourceHomeWorld;
        ImGui.SetNextItemWidth(260);
        if (ImGui.InputText("Home world name or id", ref pendingWorld, 64))
        {
            plugin.PendingSourceHomeWorld = pendingWorld;
        }

        if (ImGui.Button("Add source character"))
        {
            plugin.AddPendingSourceCharacter();
        }
    }

    private void DrawSequence()
    {
        ImGui.Text("Sequence");
        var sequence = plugin.ActiveSequence;

        if (sequence.Count == 0)
        {
            ImGui.Text("No titles in the carousel sequence.");
            return;
        }

        if (ImGui.BeginChild("HonorificCarouselSequence", new Vector2(0, 0), true))
        {
            for (var i = 0; i < sequence.Count; i++)
            {
                DrawSequenceItem(sequence, i);
                ImGui.Separator();
            }
        }

        ImGui.EndChild();
    }

    private void DrawSequenceItem(System.Collections.Generic.IReadOnlyList<CarouselSequenceItem> sequence, int index)
    {
        var item = sequence[index];
        ImGui.PushID(index);

        var marker = plugin.CarouselRunner.CurrentIndex == index ? "> " : string.Empty;
        ImGui.Text($"{marker}{index + 1}. {item.DisplayName}");
        ImGui.Text($"Style: {GetStyleSummary(item.TitleDataJson)}");

        var duration = item.DurationSeconds;
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("Duration seconds", ref duration))
        {
            plugin.SaveSequenceDuration(index, duration);
        }

        if (ImGui.Button("Up"))
        {
            plugin.MoveSequenceItem(index, -1);
            ImGui.PopID();
            return;
        }

        ImGui.SameLine();
        if (ImGui.Button("Down"))
        {
            plugin.MoveSequenceItem(index, 1);
            ImGui.PopID();
            return;
        }

        ImGui.SameLine();
        if (ImGui.Button("Remove"))
        {
            plugin.RemoveSequenceItem(index);
            ImGui.PopID();
            return;
        }

        ImGui.PopID();
    }

    private static string GetSourceCharacterLabel(HonorificSourceCharacter sourceCharacter)
    {
        return $"{sourceCharacter.Name} ({sourceCharacter.HomeWorldId})";
    }

    private static string GetStyleSummary(string titleDataJson)
    {
        try
        {
            var titleData = JObject.Parse(titleDataJson);
            var color = GetVectorHex(titleData["Color"]);
            var glow = GetVectorHex(titleData["Glow"]);
            var color3 = GetVectorHex(titleData["Color3"]);
            var gradient = titleData["GradientColourSet"]?.Type == JTokenType.Null
                ? "none"
                : titleData["GradientColourSet"]?.ToString() ?? "none";
            var animation = titleData["GradientAnimationStyle"]?.Type == JTokenType.Null
                ? "none"
                : titleData["GradientAnimationStyle"]?.ToString() ?? "none";

            return $"color {color}, glow {glow}, color3 {color3}, gradient {gradient}, animation {animation}";
        }
        catch
        {
            return "unreadable style data";
        }
    }

    private static string GetVectorHex(JToken? token)
    {
        if (token == null || token.Type == JTokenType.Null)
        {
            return "none";
        }

        var x = token.Value<float?>("X");
        var y = token.Value<float?>("Y");
        var z = token.Value<float?>("Z");
        if (x == null || y == null || z == null)
        {
            return "none";
        }

        var r = FloatChannelToByte(x.Value);
        var g = FloatChannelToByte(y.Value);
        var b = FloatChannelToByte(z.Value);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static int FloatChannelToByte(float value)
    {
        return Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
    }
}
