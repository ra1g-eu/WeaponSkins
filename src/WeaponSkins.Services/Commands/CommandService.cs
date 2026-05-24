using Microsoft.Extensions.Logging;

using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;
using WeaponSkins.Services;
using SwiftlyS2.Shared.Players;
using WeaponSkins.Shared;
using WeaponSkins.Extensions;

namespace WeaponSkins;

public partial class CommandService
{

    private ISwiftlyCore Core { get; init; }
    private ILogger Logger { get; init; }
    private MenuService MenuService { get; init; }
    private WeaponSkinAPI Api { get; init; }
    private readonly InventoryService _inventoryService;
    private readonly PlayerService _playerService;

    public CommandService(ISwiftlyCore core,
        ILogger<CommandService> logger,
        MenuService menuService,
        WeaponSkinAPI api,
        InventoryService inventoryService,
        PlayerService playerService)
    {
        Core = core;
        Logger = logger;
        MenuService = menuService;
        Api = api;

        _inventoryService = inventoryService;
        _playerService = playerService;

        RegisterCommands();
    }
    public void RegisterCommands()
    {
        Core.Command.RegisterCommand("ws", CommandSkin);
        Core.Command.RegisterCommand("ws_preview", CommandPreviewSkin);
    }

    private void CommandSkin(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply("This command can only be used by players.");
            return;
        }

        MenuService.OpenMainMenu(context.Sender!);
    }

    private void CommandPreviewSkin(ICommandContext context)
    {
        if (context.IsSentByPlayer)
        {
            context.Reply("This command is server-only.");
            return;
        }

        var args = context.Args;
        if (args.Length < 5)
        {
            context.Reply("Usage: sw_ws_preview <steamid> <defIndex> <paintkit> <seed> <wear> [team]");
            return;
        }

        if (!ulong.TryParse(args[0], out var steamId)
            || !ushort.TryParse(args[1], out var defIndex)
            || !int.TryParse(args[2], out var paintkit)
            || !int.TryParse(args[3], out var seed)
            || !float.TryParse(args[4], System.Globalization.CultureInfo.InvariantCulture, out var wear))
        {
            context.Reply("Invalid arguments.");
            return;
        }

        var team = args.Length > 5 && args[5] == "2" ? Team.T : Team.CT;
        var stattrak = Boolean.Parse(args[6]) ? 67 : 0;
        var category = args[7] ?? null;

        if (category == null || category == "")
        {
            context.Reply("Invalid category provided");
        }

        if (!_inventoryService.TryGet(steamId, out _))
        {
            context.Reply($"Player {steamId} is not connected.");
            return;
        }

        KnifeSkinData? knife = null;
        WeaponSkinData? skin = null;
        GloveData? glove = null;

        if (category == "Knives")
        {
            knife = new KnifeSkinData
            {
                SteamID = steamId,
                Team = team,
                DefinitionIndex = defIndex,
                StattrakCount = stattrak,
                Paintkit = paintkit,
                PaintkitSeed = seed,
                PaintkitWear = wear
            };
        }
        else if (category == "Gloves")
        {
            glove = new GloveData
            {
                SteamID = steamId,
                Team = team,
                DefinitionIndex = defIndex,
                Paintkit = paintkit,
                PaintkitSeed = seed,
                PaintkitWear = wear
            };
        }
        else
        {
            skin = new WeaponSkinData
            {
                SteamID = steamId,
                Team = team,
                DefinitionIndex = defIndex,
                StattrakCount = stattrak,
                Paintkit = paintkit,
                PaintkitSeed = seed,
                PaintkitWear = wear
            };
        }

        context.Reply($"Attempting item update for {steamId}.");

        if (skin != null)
        {
            _inventoryService.UpdateWeaponSkins(steamId, [skin]);
        }
        else if (knife != null)
        {
            _inventoryService.UpdateKnifeSkins(steamId, [knife]);
        }
        else if (glove != null)
        {
            _inventoryService.UpdateGloveSkins(steamId, [glove]);
        }

        if (_playerService.TryGetPlayer(steamId, out var player) && player.IsAlive())
        {
            Core.Scheduler.NextWorldUpdate(() =>
            {
                if (skin != null)
                {
                    Api.SetWeaponSkins([skin], true);
                    context.Reply($"Weapon applied for {steamId}.");

                    foreach (var weapon in player.PlayerPawn!.WeaponServices!.MyWeapons)
                    {
                        if (weapon.Value!.AttributeManager.Item.ItemDefinitionIndex == defIndex
                            && player.Controller.Team == team)
                        {
                            //player.RegiveWeapon(weapon.Value, defIndex);
                            break;
                        }
                    }
                }
            });

            
            Core.Scheduler.NextWorldUpdate(() =>
            {
                if (knife != null && player.Controller.Team == team)
                {
                    Api.SetKnifeSkins([knife], true);
                    //player.RegiveKnife();
                    context.Reply($"Knife applied for {steamId}.");
                }
            });


            Core.Scheduler.NextWorldUpdate(() =>
            {
                if (glove != null && player.Controller.Team == team)
                {
                    Api.SetGloveSkins([glove], true);
                    //player.RegiveGlove(_inventoryService.Get(player.SteamID));
                    context.Reply($"Glove applied for {steamId}.");
                }
            });
        }

        context.Reply($"Command finished for {steamId}.");
    }
}