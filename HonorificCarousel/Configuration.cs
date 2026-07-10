using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace HonorificCarousel;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool Enabled { get; set; } = false;
    public bool Loop { get; set; } = true;
    public int SelectedSourceCharacterIndex { get; set; } = 0;
    public List<HonorificSourceCharacter> SourceCharacters { get; set; } = [];
    public List<CarouselSequenceItem> Sequence { get; set; } = [];

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}

[Serializable]
public class CarouselSequenceItem
{
    public string DisplayName { get; set; } = "Untitled Honorific title";
    public string TitleDataJson { get; set; } = string.Empty;
    public int DurationSeconds { get; set; } = 10;
}

[Serializable]
public class HonorificSourceCharacter
{
    public string Name { get; set; } = string.Empty;
    public uint HomeWorldId { get; set; }
    public bool Enabled { get; set; } = false;
    public bool Loop { get; set; } = true;
    public List<CarouselSequenceItem> Sequence { get; set; } = [];
}
