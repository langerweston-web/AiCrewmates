using AiCrewmates.AI;
using AiCrewmates.Bots;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AiCrewmates.Patches;

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
public static class LobbyUiPatches
{
    private static GameObject? _addButton;
    private static GameObject? _removeButton;

    public static void Postfix(GameStartManager __instance)
    {
        try
        {
            CreateButtons(__instance);
        }
        catch (Exception ex)
        {
            AiCrewmatesPlugin.Instance.Log.LogWarning($"Lobby bot buttons failed: {ex.Message}");
        }
    }

    private static void CreateButtons(GameStartManager gsm)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            return;
        }

        // Reuse the host Start button as a visual template when possible.
        var template = gsm.StartButton;
        if (template == null)
        {
            return;
        }

        DestroyButtons();

        _addButton = MakeButton(template.gameObject, "AddBotButton", "ADD BOT", new Vector3(0f, -1.05f, 0f), () =>
        {
            var msg = BotManager.AddBot();
            HudManager.Instance?.Chat?.AddChat(PlayerControl.LocalPlayer, $"[AiCrewmates] {msg}", false);
        });

        _removeButton = MakeButton(template.gameObject, "RemoveBotButton", "REMOVE BOT", new Vector3(0f, -1.45f, 0f), () =>
        {
            var msg = BotManager.RemoveBot();
            HudManager.Instance?.Chat?.AddChat(PlayerControl.LocalPlayer, $"[AiCrewmates] {msg}", false);
        });
    }

    private static GameObject MakeButton(GameObject template, string name, string label, Vector3 localOffset, Action onClick)
    {
        var go = Object.Instantiate(template, template.transform.parent);
        go.name = name;
        go.transform.localPosition = template.transform.localPosition + localOffset;
        go.SetActive(true);

        foreach (var tmp in go.GetComponentsInChildren<TextMeshPro>(true))
        {
            tmp.text = label;
        }

        var passive = go.GetComponent<PassiveButton>();
        if (passive != null)
        {
            passive.OnClick = new();
            passive.OnClick.AddListener((Action)(() =>
            {
                if (!AmongUsClient.Instance.AmHost)
                {
                    return;
                }

                onClick();
            }));
        }

        return go;
    }

    private static void DestroyButtons()
    {
        if (_addButton != null)
        {
            Object.Destroy(_addButton);
            _addButton = null;
        }

        if (_removeButton != null)
        {
            Object.Destroy(_removeButton);
            _removeButton = null;
        }
    }
}

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.BeginGame))]
public static class GameBeginPatches
{
    public static void Postfix()
    {
        BotManager.OnGameStarted();
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]
public static class ExitGamePatches
{
    public static void Prefix()
    {
        BotManager.ClearAll();
        Pathfinding.Invalidate();
    }
}
