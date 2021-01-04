﻿using System;
using System.Collections.Generic;
using System.Linq;
using ExpandedStorage.Framework.Models;
using ExpandedStorage.Framework.Patches;
using ExpandedStorage.Framework.UI;
using Harmony;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using SDVObject = StardewValley.Object;

namespace ExpandedStorage
{
    internal class ExpandedStorage : Mod
    {
        /// <summary>Dictionary list of objects which are Expanded Storage, </summary>
        public static readonly IDictionary<int, ExpandedStorageData> Objects = new Dictionary<int, ExpandedStorageData>();
        
        /// <summary>The mod configuration.</summary>
        private ModConfig _config;
        
        /// <summary>Json Assets Api for loading assets</summary>
        private IJsonAssetsApi _jsonAssetsApi;
        
        /// <summary>Overlays ItemGrabMenu with UI elements provided by ExpandedStorage.</summary>
        private readonly PerScreen<ChestOverlay> _chestOverlay = new PerScreen<ChestOverlay>();
        
        /// <summary>Tracks previously held chest before placing into world.</summary>
        private readonly PerScreen<Chest> _previousHeldChest = new PerScreen<Chest>();


        public override void Entry(IModHelper helper)
        {
            _config = helper.ReadConfig<ModConfig>();

            if (helper.ModRegistry.IsLoaded("spacechase0.CarryChest"))
            {
                Monitor.Log("Expanded Storage should not be run alongside Carry Chest", LogLevel.Warn);
                _config.AllowCarryingChests = false;
            }
            
            // Events
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.World.ObjectListChanged += OnObjectListChanged;
            
            if (_config.AllowCarryingChests)
            {
                helper.Events.GameLoop.UpdateTicking += OnUpdateTicking;
                helper.Events.Input.ButtonPressed += OnButtonPressed;
            }
            
            if (_config.AllowModdedCapacity)
                helper.Events.Display.MenuChanged += OnMenuChanged;

            // Patches
            var harmony = HarmonyInstance.Create(ModManifest.UniqueID);
            ItemPatches.PatchAll(_config, Monitor, harmony);
            ObjectPatches.PatchAll(_config, Monitor, harmony);
            ChestPatches.PatchAll(_config, Monitor, harmony);
            ItemGrabMenuPatches.PatchAll(_config, Monitor, harmony);
            InventoryMenuPatches.PatchAll(_config, Monitor, harmony);
        }

        /// <summary>
        /// Load Json Assets Api and wait for IDs to be assigned.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            _jsonAssetsApi = Helper.ModRegistry.GetApi<IJsonAssetsApi>("spacechase0.JsonAssets");
            _jsonAssetsApi.IdsAssigned += OnIdsAssigned;
        }
        
        /// <summary>
        /// Gets ParentSheetIndex for Expanded Storages from Json Assets API.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnIdsAssigned(object sender, EventArgs e)
        {
            var ids = _jsonAssetsApi.GetAllBigCraftableIds();
            Objects.Clear();
            
            Monitor.Log($"Loading Expanded Storage Content", LogLevel.Info);
            foreach (var contentPack in Helper.ContentPacks.GetOwned())
            {
                if (!contentPack.HasFile("expandedStorage.json"))
                {
                    Monitor.Log($"Cannot load {contentPack.Manifest.Name} {contentPack.Manifest.Version}", LogLevel.Warn);
                    continue;
                }
                
                Monitor.Log($"Loading {contentPack.Manifest.Name} {contentPack.Manifest.Version}", LogLevel.Info);
                var contentData = contentPack.ReadJsonFile<ContentPackData>("expandedStorage.json");
                foreach (var expandedStorage in contentData.ExpandedStorage
                    .Where(s => !string.IsNullOrWhiteSpace(s.StorageName)))
                {
                    if (ids.TryGetValue(expandedStorage.StorageName, out var id))
                        Objects.Add(id, expandedStorage);
                    else
                        Monitor.Log($"{expandedStorage.StorageName} assets not loaded by Json Assets Api", LogLevel.Warn);
                }
            }
        }
        
        /// <summary>Track toolbar changes before user input.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnUpdateTicking(object sender, UpdateTickingEventArgs e)
        {
            if (!Context.IsPlayerFree)
                return;
            _previousHeldChest.Value = Game1.player.CurrentItem is Chest chest ? chest : null;
        }
        
        /// <summary>Track toolbar changes before user input.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsPlayerFree || e.Button != SButton.MouseLeft || Game1.player.CurrentItem != null)
                return;
            
            var location = Game1.currentLocation;
            var pos = e.Cursor.Tile;
            if (!location.objects.TryGetValue(pos, out var obj) ||
                !(obj is Chest && (!Objects.TryGetValue(obj.ParentSheetIndex, out var data) || data.CanCarry)) ||
                !Game1.player.addItemToInventoryBool(obj, true))
                return;
            location.objects.Remove(pos);
            Helper.Input.Suppress(e.Button);
        }
        
        /// <summary>
        /// Resets scrolling/overlay when chest menu exits or context changes.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            // Menu is exited or context has changed
            if (e.NewMenu is null ||
                e.NewMenu is ItemGrabMenu newMenu &&
                _chestOverlay.Value != null &&
                !ReferenceEquals(newMenu.context, _chestOverlay.Value.Menu?.context))
            {
                _chestOverlay.Value?.Dispose();
                _chestOverlay.Value = null;
            }
            
            // Add new overlay
            if (e.NewMenu is ItemGrabMenu menu)
                _chestOverlay.Value = new ChestOverlay(menu, Helper.Events, Helper.Input);
        }
        
        /// <summary>
        /// Converts objects to modded storage when placed in the world.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
        {
            if (!Context.IsPlayerFree)
                return;

            var itemPos = e.Added
                .LastOrDefault(p =>
                    p.Value is Chest || p.Value.bigCraftable.Value && Objects.ContainsKey(p.Value.ParentSheetIndex));
            
            var obj = itemPos.Value;
            var pos = itemPos.Key;
            if (obj == null)
                return;

            // Convert Chest to Expanded Storage
            if (!(obj is Chest chest))
            {
                chest = new Chest(true, obj.TileLocation, obj.ParentSheetIndex)
                {
                    name = obj.name
                };
            }

            // Copy properties from previously held chest
            var previousHeldChest = _previousHeldChest.Value;
            if (previousHeldChest != null && ReferenceEquals(e.Location, Game1.currentLocation))
            {
                chest.Name = previousHeldChest.Name;
                chest.playerChoiceColor.Value = previousHeldChest.playerChoiceColor.Value;
                if (previousHeldChest.items.Any())
                    chest.items.CopyFrom(previousHeldChest.items);
                // Copy modData
                foreach (var chestModData in previousHeldChest.modData)
                    chest.modData.CopyFrom(chestModData);
            }
            
            // Replace object if necessary
            if (!ReferenceEquals(chest, obj))
                e.Location.objects[pos] = chest;
        }
    }
}