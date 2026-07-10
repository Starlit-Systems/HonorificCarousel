using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;

namespace HonorificCarousel;

public sealed class CarouselRunner
{
    private const int LocalPlayerObjectIndex = 0;
    private readonly Configuration configuration;
    private readonly HonorificIpc honorificIpc;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly Func<HonorificSourceCharacter?> getActiveSourceCharacter;
    private readonly Func<List<CarouselSequenceItem>> getActiveSequence;

    private int currentIndex = -1;
    private DateTime nextAdvanceUtc = DateTime.MinValue;
    private bool wasEnabled;
    private bool hasAppliedTitle;

    public CarouselRunner(
        Configuration configuration,
        HonorificIpc honorificIpc,
        IClientState clientState,
        IObjectTable objectTable,
        Func<HonorificSourceCharacter?> getActiveSourceCharacter,
        Func<List<CarouselSequenceItem>> getActiveSequence)
    {
        this.configuration = configuration;
        this.honorificIpc = honorificIpc;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.getActiveSourceCharacter = getActiveSourceCharacter;
        this.getActiveSequence = getActiveSequence;
    }

    public string StatusText { get; private set; } = "Carousel is idle.";

    public int CurrentIndex => currentIndex;

    public void Update()
    {
        var source = getActiveSourceCharacter();
        if (source == null || !source.Enabled)
        {
            if (wasEnabled || hasAppliedTitle)
            {
                ClearAndReset("Carousel disabled. Honorific title cleared.");
            }

            wasEnabled = false;
            return;
        }

        wasEnabled = true;

        if (!honorificIpc.IsAvailable)
        {
            StatusText = "Waiting for Honorific IPC. The carousel is paused.";
            return;
        }

        if (!IsLocalPlayerAvailable)
        {
            StatusText = "Waiting for the local player. The carousel is paused.";
            return;
        }

        var sequence = getActiveSequence();
        if (sequence.Count == 0)
        {
            ClearAndReset("Add at least one Honorific title to start the carousel.");
            return;
        }

        if (currentIndex >= 0 && currentIndex < sequence.Count && DateTime.UtcNow < nextAdvanceUtc)
        {
            StatusText = $"Showing {sequence[currentIndex].DisplayName}.";
            return;
        }

        Advance(sequence);
    }

    public void Restart(bool clearCurrent)
    {
        currentIndex = -1;
        nextAdvanceUtc = DateTime.MinValue;

        if (clearCurrent)
        {
            ClearCurrentTitle();
        }
    }

    public void StopAndClear(string status)
    {
        ClearAndReset(status);
        wasEnabled = false;
    }

    public void ClearCurrentTitle()
    {
        if (!honorificIpc.TryClearCharacterTitle(LocalPlayerObjectIndex, out var error))
        {
            StatusText = error;
            return;
        }

        hasAppliedTitle = false;
    }

    private bool IsLocalPlayerAvailable => clientState.IsLoggedIn && objectTable.LocalPlayer != null;

    private void Advance(List<CarouselSequenceItem> sequence)
    {
        var nextIndex = currentIndex + 1;
        if (nextIndex >= sequence.Count)
        {
            var source = getActiveSourceCharacter();
            if (source == null || !source.Loop)
            {
                if (source != null)
                {
                    source.Enabled = false;
                }

                configuration.Save();
                ClearAndReset("Carousel finished and Honorific title cleared.");
                wasEnabled = false;
                return;
            }

            nextIndex = 0;
        }

        Apply(sequence, nextIndex);
    }

    private void Apply(List<CarouselSequenceItem> sequence, int index)
    {
        if (index < 0 || index >= sequence.Count)
        {
            ClearAndReset("Carousel sequence changed. Honorific title cleared.");
            return;
        }

        var item = sequence[index];
        if (string.IsNullOrWhiteSpace(item.TitleDataJson))
        {
            StatusText = $"Skipping {item.DisplayName}: missing Honorific title data.";
            currentIndex = index;
            nextAdvanceUtc = DateTime.UtcNow.AddSeconds(1);
            return;
        }

        if (!honorificIpc.TrySetCharacterTitle(LocalPlayerObjectIndex, item.TitleDataJson, out var error))
        {
            StatusText = error;
            nextAdvanceUtc = DateTime.UtcNow.AddSeconds(2);
            return;
        }

        currentIndex = index;
        hasAppliedTitle = true;
        nextAdvanceUtc = DateTime.UtcNow.AddSeconds(Math.Max(1, item.DurationSeconds));
        StatusText = $"Showing {item.DisplayName}.";
    }

    private void ClearAndReset(string status)
    {
        currentIndex = -1;
        nextAdvanceUtc = DateTime.MinValue;
        ClearCurrentTitle();
        StatusText = status;
    }
}
