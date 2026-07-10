using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace HonorificCarousel.Windows;

public class HelpWindow : Window, IDisposable
{
    public HelpWindow() : base("Honorific Carousel Help###HonorificCarouselHelp")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 420),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.BeginChild("HonorificCarouselHelpScroll", Vector2.Zero, true))
        {
            Section("What This Plugin Does");
            Text("Honorific Carousel does not draw titles, style titles, edit Honorific config, or touch nameplates. Honorific remains the source of truth.");
            Text("This plugin reads saved Honorific title data through Honorific IPC, stores selected title JSON in its own config, and tells Honorific which saved title data to show for local player object index 0.");

            Section("Setup Order");
            Bullet("Install and enable Honorific.");
            Bullet("Create and style your titles inside Honorific first, including colors, glow, gradients, prefix/suffix, and animation settings.");
            Bullet("Open Honorific Carousel with /honocar.");
            Bullet("Click Add current character while logged into the character you want to configure.");
            Bullet("Select that source character, then click Refresh Honorific Titles.");
            Bullet("Pick a saved Honorific title from the dropdown and add it to the sequence.");
            Bullet("Set each sequence item's duration in seconds.");
            Bullet("Turn Enabled on for that source character. Leave it off for characters that should not rotate.");

            Section("Per-Character Behavior");
            Text("Each saved source character has its own Enabled setting, Loop setting, and sequence.");
            Text("When you log into a different character, Honorific Carousel auto-selects the matching saved source character by name and home world id.");
            Text("If the current character is not configured, or is configured but disabled, the carousel is inactive and clears any title it previously assigned.");
            Text("The selected sequence is always applied only to the character you are currently logged into, through local player object index 0.");

            Section("Refreshing Titles");
            Text("Refresh Honorific Titles pulls the latest saved title data for the selected source character from Honorific.");
            Text("If you edit colors, glow, gradients, or text in Honorific, refresh here so existing matching sequence items update to the latest saved Honorific data.");
            Text("The style summary shows the captured color, glow, secondary color, gradient set, and animation value for each title.");

            Section("Stopping And Clearing");
            Text("Turning Enabled off clears the carousel-assigned Honorific title for local player object index 0.");
            Text("Unloading or disposing this plugin also calls Honorific.ClearCharacterTitle for object index 0.");
            Text("The plugin only calls Honorific.SetCharacterTitle when the carousel advances to a new sequence item, not every frame.");

        }

        ImGui.EndChild();
    }

    private static void Section(string text)
    {
        ImGui.Spacing();
        ImGui.Text(text);
        ImGui.Separator();
    }

    private static void Text(string text)
    {
        ImGui.TextWrapped(text);
    }

    private static void Bullet(string text)
    {
        ImGui.BulletText(text);
    }
}
