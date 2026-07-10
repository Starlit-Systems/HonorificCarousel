using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HonorificCarousel;

public sealed class HonorificIpc
{
    private const string GetCharacterTitleListName = "Honorific.GetCharacterTitleList";
    private const string SetCharacterTitleName = "Honorific.SetCharacterTitle";
    private const string ClearCharacterTitleName = "Honorific.ClearCharacterTitle";

    private readonly IPluginLog log;
    private readonly ICallGateSubscriber<object> getTitleList;
    private readonly ICallGateSubscriber<object> setCharacterTitle;
    private readonly ICallGateSubscriber<object> clearCharacterTitle;

    public HonorificIpc(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.log = log;
        getTitleList = pluginInterface.GetIpcSubscriber<object>(GetCharacterTitleListName);
        setCharacterTitle = pluginInterface.GetIpcSubscriber<object>(SetCharacterTitleName);
        clearCharacterTitle = pluginInterface.GetIpcSubscriber<object>(ClearCharacterTitleName);
    }

    public bool IsAvailable => HasTitleListFunction && setCharacterTitle.HasAction && clearCharacterTitle.HasAction;

    public string AvailabilityText
    {
        get
        {
            if (IsAvailable)
            {
                return "Honorific IPC is available.";
            }

            if (!HasTitleListFunction)
            {
                return "Honorific IPC is not available yet. Is Honorific installed and loaded?";
            }

            return "Honorific IPC is partially available. Set/Clear title calls are not ready.";
        }
    }

    private bool HasTitleListFunction =>
        getTitleList.HasFunction;

    public bool TryGetTitles(string characterName, uint homeWorldId, out List<HonorificTitleOption> titles, out string error)
    {
        titles = [];
        error = string.Empty;

        if (!HasTitleListFunction)
        {
            error = "Honorific is not exposing its saved title list yet.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(characterName) || homeWorldId == 0)
        {
            error = "Local character name or home world is not available yet.";
            return false;
        }

        try
        {
            var result = InvokeUntypedFunc<object>(getTitleList, characterName, homeWorldId);
            titles = ParseTitleList(result);
            return true;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Failed to get Honorific title list via IPC.");
            error = "Honorific did not return a readable title list for the local character.";
            return false;
        }
    }

    public bool TrySetCharacterTitle(int objectIndex, string titleDataJson, out string error)
    {
        error = string.Empty;

        if (!setCharacterTitle.HasAction)
        {
            error = "Honorific SetCharacterTitle IPC is not available.";
            return false;
        }

        try
        {
            InvokeUntypedAction(setCharacterTitle, objectIndex, titleDataJson);
            return true;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Failed to set Honorific title via IPC.");
            error = "Honorific rejected the selected title data.";
            return false;
        }
    }

    public bool TryClearCharacterTitle(int objectIndex, out string error)
    {
        error = string.Empty;

        if (!clearCharacterTitle.HasAction)
        {
            error = "Honorific ClearCharacterTitle IPC is not available.";
            return false;
        }

        try
        {
            InvokeUntypedAction(clearCharacterTitle, objectIndex);
            return true;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Failed to clear Honorific title via IPC.");
            error = "Honorific could not clear the active carousel title.";
            return false;
        }
    }

    private static List<HonorificTitleOption> ParseTitleList(object? result)
    {
        if (result == null)
        {
            return [];
        }

        var token = result switch
        {
            JToken jToken => jToken,
            string payload when !string.IsNullOrWhiteSpace(payload) => JToken.Parse(payload),
            string => new JArray(),
            _ => JToken.FromObject(result),
        };

        if (token is JObject obj)
        {
            return [CreateOption(obj)];
        }

        if (token is not JArray array)
        {
            return [];
        }

        return array
            .OfType<JObject>()
            .Select(CreateOption)
            .Where(item => !string.IsNullOrWhiteSpace(item.TitleDataJson))
            .ToList();
    }

    private static void InvokeUntypedAction(ICallGateSubscriber<object> subscriber, params object[] args)
    {
        InvokeCallGateMethod(subscriber, "InvokeAction", null, args);
    }

    private static TResult InvokeUntypedFunc<TResult>(ICallGateSubscriber<object> subscriber, params object[] args)
    {
        return (TResult)InvokeCallGateMethod(subscriber, "InvokeFunc", typeof(TResult), args)!;
    }

    private static object? InvokeCallGateMethod(
        ICallGateSubscriber<object> subscriber,
        string methodName,
        Type? genericReturnType,
        object[] args)
    {
        var method = FindCallGateMethod(subscriber.GetType(), methodName);

        if (method == null)
        {
            throw new MissingMethodException(subscriber.GetType().FullName, methodName);
        }

        if (genericReturnType != null)
        {
            method = method.MakeGenericMethod(genericReturnType);
        }

        try
        {
            return method.Invoke(subscriber, [args]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    private static MethodInfo? FindCallGateMethod(Type subscriberType, string methodName)
    {
        for (var type = subscriberType; type != null; type = type.BaseType)
        {
            var method = type.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                [typeof(object[])]);

            if (method != null)
            {
                return method;
            }
        }

        return null;
    }

    private static HonorificTitleOption CreateOption(JObject titleData)
    {
        return new HonorificTitleOption
        {
            DisplayName = GetDisplayName(titleData),
            TitleDataJson = titleData.ToString(Formatting.None),
        };
    }

    private static string GetDisplayName(JObject titleData)
    {
        var title = titleData.Value<string>("Title");
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        var name = titleData.Value<string>("Name");
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return "Untitled Honorific title";
    }
}
