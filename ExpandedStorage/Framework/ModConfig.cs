﻿using System;
using System.Collections.Generic;
using System.Linq;
using ImJustMatt.Common.Integrations.GenericModConfigMenu;
using ImJustMatt.ExpandedStorage.Common.Helpers;
using ImJustMatt.ExpandedStorage.Framework.Models;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace ImJustMatt.ExpandedStorage.Framework
{
    internal class ModConfig
    {
        internal static readonly TableSummary TableSummary = new TableSummary(new Dictionary<string, string>
        {
            {"Controller", "Enables input designed to improve controller compatibility"},
            {"ExpandInventoryMenu", "Allows storage menu to have up to 6 rows"},
            {"SearchTagSymbol", "Symbol used to search items by context tag"},
            {"VacuumToFirstRow", "Items will only be collected to Vacuum Storages in the active hotbar"}
        });

        /// <summary>Enable controller config settings.</summary>
        public bool Controller { get; set; } = true;

        /// <summary>Control scheme for Expanded Storage features.</summary>
        public ModConfigKeys Controls { get; set; } = new();

        /// <summary>Default config for unconfigured storages.</summary>
        public StorageConfig DefaultStorage { get; set; } = new()
        {
            Tabs = new List<string> {"Crops", "Seeds", "Materials", "Cooking", "Fishing", "Equipment", "Clothing", "Misc"}
        };

        /// <summary>Default tabs for unconfigured storages.</summary>
        public IDictionary<string, StorageTab> DefaultTabs { get; set; } = new Dictionary<string, StorageTab>
        {
            {
                "Clothing", new StorageTab("Shirts.png",
                    "category_clothing",
                    "category_boots", "category_hat")
            },
            {
                "Cooking",
                new StorageTab("Cooking.png",
                    "category_syrup",
                    "category_artisan_goods",
                    "category_ingredients",
                    "category_sell_at_pierres_and_marnies",
                    "category_sell_at_pierres",
                    "category_meat",
                    "category_cooking",
                    "category_milk",
                    "category_egg")
            },
            {
                "Crops",
                new StorageTab("Crops.png",
                    "category_greens",
                    "category_flowers",
                    "category_fruits",
                    "category_vegetable")
            },
            {
                "Equipment",
                new StorageTab("Tools.png",
                    "category_equipment",
                    "category_ring",
                    "category_tool",
                    "category_weapon")
            },
            {
                "Fishing",
                new StorageTab("Fish.png",
                    "category_bait",
                    "category_fish",
                    "category_tackle",
                    "category_sell_at_fish_shop")
            },
            {
                "Materials",
                new StorageTab("Minerals.png",
                    "category_monster_loot",
                    "category_metal_resources",
                    "category_building_resources",
                    "category_minerals",
                    "category_crafting",
                    "category_gem")
            },
            {
                "Misc",
                new StorageTab("Misc.png",
                    "category_big_craftable",
                    "category_furniture",
                    "category_junk")
            },
            {
                "Seeds",
                new StorageTab("Seeds.png",
                    "category_seeds",
                    "category_fertilizer")
            }
        };

        /// <summary>Only vacuum to storages in the first row of player inventory.</summary>
        public bool VacuumToFirstRow { get; set; } = true;

        /// <summary>Adds three extra rows to the Inventory Menu.</summary>
        public bool ExpandInventoryMenu { get; set; } = true;

        /// <summary>Symbol used to search items by context tags.</summary>
        public string SearchTagSymbol { get; set; } = "#";


        protected internal string SummaryReport => string.Join("\n",
            "Expanded Storage Configuration",
            TableSummary.Report(this),
            $"{"Next Tab",-25} | {Controls.NextTab}",
            $"{"Previous Tab",-25} | {Controls.PreviousTab}",
            $"{"Scroll Up",-25} | {Controls.ScrollUp}",
            $"{"Scroll Down",-25} | {Controls.ScrollDown}",
            $"{"Show Crafting",-25} | {Controls.OpenCrafting}",
            DefaultStorage.StorageConfigSummary
        );

        internal void CopyFrom(ModConfig config)
        {
            Controls = config.Controls;
            Controller = config.Controller;
            VacuumToFirstRow = config.VacuumToFirstRow;
            ExpandInventoryMenu = config.ExpandInventoryMenu;
            SearchTagSymbol = config.SearchTagSymbol;
            DefaultStorage = new Storage();
            DefaultStorage.CopyFrom(config.DefaultStorage);
            DefaultTabs.Clear();
            foreach (var tab in config.DefaultTabs)
            {
                var newTab = new StorageTab();
                newTab.CopyFrom(tab.Value);
                DefaultTabs.Add(tab.Key, newTab);
            }
        }

        public static void RegisterModConfig(IManifest manifest, GenericModConfigMenuIntegration modConfigMenu, ModConfig config)
        {
            // Controls
            modConfigMenu.API?.RegisterLabel(manifest,
                "Controls",
                "Controller/Keyboard controls");

            modConfigMenu.API?.RegisterSimpleOption(manifest,
                "Scroll Up",
                "Button for scrolling up",
                () => config.Controls.ScrollUp.Keybinds.Single(kb => kb.IsBound).Buttons.First(),
                value => config.Controls.ScrollUp = KeybindList.ForSingle(value));
            modConfigMenu.API?.RegisterSimpleOption(manifest,
                "Scroll Down",
                "Button for scrolling down",
                () => config.Controls.ScrollDown.Keybinds.Single(kb => kb.IsBound).Buttons.First(),
                value => config.Controls.ScrollDown = KeybindList.ForSingle(value));
            modConfigMenu.API?.RegisterSimpleOption(manifest,
                "Previous Tab",
                "Button for switching to the previous tab",
                () => config.Controls.PreviousTab.Keybinds.Single(kb => kb.IsBound).Buttons.First(),
                value => config.Controls.PreviousTab = KeybindList.ForSingle(value));
            modConfigMenu.API?.RegisterSimpleOption(manifest,
                "Next Tab",
                "Button for switching to the next tab",
                () => config.Controls.NextTab.Keybinds.Single(kb => kb.IsBound).Buttons.First(),
                value => config.Controls.NextTab = KeybindList.ForSingle(value));

            // Tweaks
            modConfigMenu.API?.RegisterLabel(manifest,
                "Tweaks",
                "Modify behavior for certain features");

            modConfigMenu.API?.RegisterSimpleOption(manifest,
                "Enable Controller",
                "Enables settings designed to improve controller compatibility",
                () => config.Controller,
                value => config.Controller = value);
            modConfigMenu.API?.RegisterSimpleOption(manifest,
                "Resize Inventory Menu",
                "Allows the inventory menu to have 4-6 rows instead of the default 3",
                () => config.ExpandInventoryMenu,
                value => config.ExpandInventoryMenu = value);
            modConfigMenu.API?.RegisterSimpleOption(manifest,
                "Search Symbol",
                "Symbol used to search items by context tag",
                () => config.SearchTagSymbol,
                value => config.SearchTagSymbol = value);
            modConfigMenu.API?.RegisterSimpleOption(manifest,
                "Vacuum To First Row",
                "Uncheck to allow vacuuming to any chest in player inventory",
                () => config.VacuumToFirstRow,
                value => config.VacuumToFirstRow = value);

            // Default Storage Config
            var optionChoices = Enum.GetNames(typeof(StorageConfig.Choice));

            Func<string> OptionGet(string option)
            {
                return () => config.DefaultStorage.Option(option).ToString();
            }

            Action<string> OptionSet(string option)
            {
                return value =>
                {
                    if (Enum.TryParse(value, out StorageConfig.Choice choice))
                        config.DefaultStorage.SetOption(option, choice);
                };
            }

            modConfigMenu.API?.RegisterLabel(manifest,
                "Default Storage",
                "Default config for unconfigured storages.");

            modConfigMenu.API?.RegisterSimpleOption(manifest, "Capacity", "Number of item slots the storage will contain",
                () => config.DefaultStorage.Capacity,
                value => config.DefaultStorage.Capacity = value);

            foreach (var option in StorageConfig.StorageOptions)
            {
                modConfigMenu.API?.RegisterChoiceOption(manifest, option.Key, option.Value,
                    OptionGet(option.Key), OptionSet(option.Key), optionChoices);
            }
        }
    }
}