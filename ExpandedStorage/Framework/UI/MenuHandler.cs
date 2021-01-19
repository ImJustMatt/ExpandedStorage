﻿using System;
using System.Collections.Generic;
using System.Linq;
using Common;
using ExpandedStorage.Framework.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;
using StardewValley.Objects;

namespace ExpandedStorage.Framework.UI
{
    internal class MenuHandler : IDisposable
    {
        private readonly MenuOverlay _overlay;
        private readonly IModEvents _events;
        private readonly IInputHelper _inputHelper;
        private readonly ModConfigControls _controls;

        private readonly object _context;
        private readonly IList<Item> _items;
        private IList<Item> _filteredItems;
        private readonly int _capacity;
        private readonly int _cols;
        private int _skipped;
        private ExpandedStorageTab _currentTab;
        
        public IList<Item> Items =>
            _skipped == 0
                ? _filteredItems
                : _filteredItems.Skip(_skipped).ToList();
        
        internal MenuHandler(ItemGrabMenu menu, IModEvents events, IInputHelper inputHelper, ModConfigControls controls, MenuHandler menuHandler = null)
        {
            var inventoryMenu = menu.ItemsToGrabMenu;
            var config = menu.context is Item item ? ExpandedStorage.GetConfig(item) : null;
            var tabs = config != null
                ? config.Tabs.Select(t => ExpandedStorage.GetTab($"{config.ModUniqueId}/{t}")).Where(t => t != null).ToList()
                : new List<ExpandedStorageTab>();
            
            _events = events;
            _inputHelper = inputHelper;
            _controls = controls;
            
            _context = menu.context;
            _items = inventoryMenu.actualInventory;
            _capacity = inventoryMenu.capacity;
            _cols = inventoryMenu.capacity / inventoryMenu.rows;

            if (menuHandler != null && ContextMatches(menuHandler))
            {
                _skipped = menuHandler._skipped;
                _currentTab = menuHandler._currentTab;
            }
            
            RefreshList();
            
            _overlay = new MenuOverlay(inventoryMenu, tabs, events.GameLoop,
                () => CanScrollUp,
                () => CanScrollDown,
                Scroll,
                SetTab,
                _currentTab?.TabName);

            // Events
            _events.Input.ButtonPressed += OnButtonPressed;
            _events.Input.CursorMoved += OnCursorMoved;
            _events.Input.MouseWheelScrolled += OnMouseWheelScrolled;
            
            switch (_context)
            {
                case Chest chest:
                    chest.items.OnElementChanged += ItemsOnElementChanged;
                    break;
                case GameLocation location:
                    var farm =(location as Farm ?? Game1.getFarm());
                    var shippingBin = farm.getShippingBin(Game1.player);
                    shippingBin.OnValueAdded += ShippingBinOnValueChanged;
                    shippingBin.OnValueRemoved += ShippingBinOnValueChanged;
                    break;
                case JunimoHut junimoHut:
                    junimoHut.output.Value.items.OnElementChanged += ItemsOnElementChanged;
                    break;
            }
        }
        
        public void Dispose()
        {
            _overlay.Dispose();
            UnregisterEvents();
            switch (_context)
            {
                case Chest chest:
                    chest.items.OnElementChanged -= ItemsOnElementChanged;
                    break;
                case GameLocation location:
                    var farm =(location as Farm ?? Game1.getFarm());
                    var shippingBin = farm.getShippingBin(Game1.player);
                    shippingBin.OnValueAdded -= ShippingBinOnValueChanged;
                    shippingBin.OnValueRemoved -= ShippingBinOnValueChanged;
                    break;
                case JunimoHut junimoHut:
                    junimoHut.output.Value.items.OnElementChanged -= ItemsOnElementChanged;
                    break;
            }
        }
        
        public void UnregisterEvents()
        {
            _events.Input.ButtonPressed -= OnButtonPressed;
            _events.Input.CursorMoved -= OnCursorMoved;
            _events.Input.MouseWheelScrolled -= OnMouseWheelScrolled;
        }

        internal void Draw(SpriteBatch b) =>
            _overlay.Draw(b);
        
        internal void DrawUnder(SpriteBatch b) =>
            _overlay.DrawUnder(b);
        
        /// <summary>Attempts to scroll offset by one row of slots relative to the inventory menu.</summary>
        /// <param name="direction">The direction which to scroll to.</param>
        /// <returns>True if the value of offset changed.</returns>
        private bool Scroll(int direction)
        {
            if (direction > 0 && CanScrollUp)
                _skipped -= _cols;
            else if (direction < 0 && CanScrollDown)
                _skipped += _cols;
            else
                return false;
            RefreshList();
            return true;
        }

        private void SetTab(ExpandedStorageTab tab)
        {
            _currentTab = tab;
            _skipped = 0;
            RefreshList();
        }

        /// <summary>Track if configured control buttons are pressed or pass input to overlay.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            var handled = false;
            var x = Game1.getMouseX(Game1.uiMode);
            var y = Game1.getMouseY(Game1.uiMode);

            if (e.Button == _controls.ScrollDown && Scroll(-1))
                handled = true;
            else if (e.Button == _controls.ScrollUp && Scroll(1))
                handled = true;
            else if (e.Button == SButton.MouseLeft || e.Button.IsUseToolButton())
                handled = _overlay.LeftClick(x, y);
            
            if (handled)
                _inputHelper.Suppress(e.Button);
        }
        
        /// <summary>Raised after the player moves the in-game cursor.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnCursorMoved(object sender, CursorMovedEventArgs e)
        {
            var x = Game1.getMouseX(Game1.uiMode);
            var y = Game1.getMouseY(Game1.uiMode);
            
            _overlay.Hover(x, y);
        }
        
        /// <summary>Raised after the player scrolls the mouse wheel.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnMouseWheelScrolled(object sender, MouseWheelScrolledEventArgs e)
        {
            if (!Scroll(e.Delta))
                return;
            
            var cur = Game1.oldMouseState;
            Game1.oldMouseState = new MouseState(
                x: cur.X,
                y: cur.Y,
                scrollWheel: e.NewValue,
                leftButton: cur.LeftButton,
                middleButton: cur.MiddleButton,
                rightButton: cur.RightButton,
                xButton1: cur.XButton1,
                xButton2: cur.XButton2
            );
        }
        private bool ContextMatches(MenuHandler handler) =>
            ReferenceEquals(_context, handler._context);
        public bool ContextMatches(InventoryMenu inventoryMenu) =>
            ReferenceEquals(_items, inventoryMenu.actualInventory);
        private void RefreshList()
        {
            _filteredItems = _currentTab == null
                ? _items
                : _items.Where(item => item != null && _currentTab.IsAllowed(item) && !_currentTab.IsBlocked(item)).ToList();
            _skipped = _skipped <= 0
                ? 0
                : Math.Min(_skipped, _filteredItems.Count.RoundUp(_cols) - _capacity);
        }
        private void ItemsOnElementChanged(NetList<Item, NetRef<Item>> list, int index, Item oldvalue, Item newvalue) =>
            RefreshList();
        private void ShippingBinOnValueChanged(Item value) =>
            RefreshList();
        private bool CanScrollUp =>
            _skipped > 0;
        private bool CanScrollDown =>
            _skipped + _cols <= _filteredItems.Count.RoundUp(_cols) - _capacity;
    }
}