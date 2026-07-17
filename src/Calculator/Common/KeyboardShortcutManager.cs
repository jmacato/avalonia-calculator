// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel;
using Utilities = CalculatorApp.ViewModel.Common.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;
using MUXC = Windows.UI.Xaml.Controls;

namespace CalculatorApp
{
    namespace Common
    {
        internal sealed class KeyboardShortcutManager : DependencyObject
        {
            public KeyboardShortcutManager()
            {
            }

            public static readonly DependencyProperty CharacterProperty = DependencyProperty.RegisterAttached("Character", typeof(string), typeof(KeyboardShortcutManager), new PropertyMetadata(string.Empty, (sender, args) =>
            {
                OnCharacterPropertyChanged(sender, (string)args.OldValue, (string)args.NewValue);
            }));
            public static string GetCharacter(DependencyObject target)
            {
                if (target is null) throw new System.ArgumentNullException(nameof(target)); return (string)target.GetValue(CharacterProperty);
            }

            public static void SetCharacter(DependencyObject target, string value)
            {
                if (target is null) throw new System.ArgumentNullException(nameof(target)); target.SetValue(CharacterProperty, value);
            }

            public static readonly DependencyProperty VirtualKeyProperty = DependencyProperty.RegisterAttached("VirtualKey", typeof(MyVirtualKey), typeof(KeyboardShortcutManager), new PropertyMetadata(default(MyVirtualKey), (sender, args) =>
            {
                OnVirtualKeyPropertyChanged(sender, (MyVirtualKey)args.OldValue, (MyVirtualKey)args.NewValue);
            }));
            public static MyVirtualKey GetVirtualKey(DependencyObject target)
            {
                if (target is null) throw new System.ArgumentNullException(nameof(target)); return (MyVirtualKey)target.GetValue(VirtualKeyProperty);
            }

            public static void SetVirtualKey(DependencyObject target, MyVirtualKey value)
            {
                if (target is null) throw new System.ArgumentNullException(nameof(target)); target.SetValue(VirtualKeyProperty, value);
            }

            public static readonly DependencyProperty VirtualKeyControlChordProperty = DependencyProperty.RegisterAttached("VirtualKeyControlChord", typeof(MyVirtualKey), typeof(KeyboardShortcutManager), new PropertyMetadata(default(MyVirtualKey), (sender, args) =>
            {
                OnVirtualKeyControlChordPropertyChanged(sender, (MyVirtualKey)args.OldValue, (MyVirtualKey)args.NewValue);
            }));
            public static MyVirtualKey GetVirtualKeyControlChord(DependencyObject target)
            {
                if (target is null) throw new System.ArgumentNullException(nameof(target)); return (MyVirtualKey)target.GetValue(VirtualKeyControlChordProperty);
            }

            public static void SetVirtualKeyControlChord(DependencyObject target, MyVirtualKey value)
            {
                if (target is null) throw new System.ArgumentNullException(nameof(target)); target.SetValue(VirtualKeyControlChordProperty, value);
            }

            public static readonly DependencyProperty VirtualKeyShiftChordProperty = DependencyProperty.RegisterAttached("VirtualKeyShiftChord", typeof(MyVirtualKey), typeof(KeyboardShortcutManager), new PropertyMetadata(default(MyVirtualKey), (sender, args) =>
            {
                OnVirtualKeyShiftChordPropertyChanged(sender, (MyVirtualKey)args.OldValue, (MyVirtualKey)args.NewValue);
            }));
            public static MyVirtualKey GetVirtualKeyShiftChord(DependencyObject target)
            {
                if (target is null) throw new System.ArgumentNullException(nameof(target)); return (MyVirtualKey)target.GetValue(VirtualKeyShiftChordProperty);
            }

            public static void SetVirtualKeyShiftChord(DependencyObject target, MyVirtualKey value)
            {
                if (target is null) throw new System.ArgumentNullException(nameof(target)); target.SetValue(VirtualKeyShiftChordProperty, value);
            }

            public static readonly DependencyProperty VirtualKeyAltChordProperty = DependencyProperty.RegisterAttached("VirtualKeyAltChord", typeof(MyVirtualKey), typeof(KeyboardShortcutManager), new PropertyMetadata(default(MyVirtualKey), (sender, args) =>
            {
                OnVirtualKeyAltChordPropertyChanged(sender, (MyVirtualKey)args.OldValue, (MyVirtualKey)args.NewValue);
            }));
            public static MyVirtualKey GetVirtualKeyAltChord(DependencyObject target)
            {
                if (target is null) throw new System.ArgumentNullException(nameof(target)); return (MyVirtualKey)target.GetValue(VirtualKeyAltChordProperty);
            }

            public static void SetVirtualKeyAltChord(DependencyObject target, MyVirtualKey value)
            {
                if (target is null) throw new System.ArgumentNullException(nameof(target)); target.SetValue(VirtualKeyAltChordProperty, value);
            }

            public static readonly DependencyProperty VirtualKeyControlShiftChordProperty = DependencyProperty.RegisterAttached("VirtualKeyControlShiftChord", typeof(MyVirtualKey), typeof(KeyboardShortcutManager), new PropertyMetadata(default(MyVirtualKey), (sender, args) =>
            {
                OnVirtualKeyControlShiftChordPropertyChanged(sender, (MyVirtualKey)args.OldValue, (MyVirtualKey)args.NewValue);
            }));
            public static MyVirtualKey GetVirtualKeyControlShiftChord(DependencyObject target)
            {
                if (target is null) throw new System.ArgumentNullException(nameof(target)); return (MyVirtualKey)target.GetValue(VirtualKeyControlShiftChordProperty);
            }

            public static void SetVirtualKeyControlShiftChord(DependencyObject target, MyVirtualKey value)
            {
                if (target is null) throw new System.ArgumentNullException(nameof(target)); target.SetValue(VirtualKeyControlShiftChordProperty, value);
            }

            internal static void Initialize()
            {
                var coreWindow = App.Window;
                //coreWindow.CharacterReceived += OnCharacterReceivedHandler;
                //coreWindow.KeyDown += OnKeyDownHandler;
                //coreWindow.KeyUp += OnKeyUpHandler;
                //coreWindow.Dispatcher.AcceleratorKeyActivated += OnAcceleratorKeyActivated;
                //KeyboardShortcutManager.RegisterNewAppViewId();
            }

            // Sometimes, like with popups, escape is treated as special and even
            // though it is handled we get it passed through to us. In those cases
            // we need to be able to ignore it (looking at e->Handled isn't sufficient
            // because that always returns true).
            // The onlyOnce flag is used to indicate whether we should only ignore the
            // next escape, or keep ignoring until you explicitly HonorEscape.
            public static void IgnoreEscape(bool onlyOnce)
            {
                int viewId = Utilities.GetWindowId();
                if (s_ignoreNextEscape.ContainsKey(viewId))
                {
                    s_ignoreNextEscape[viewId] = true;
                }

                if (s_keepIgnoringEscape.ContainsKey(viewId))
                {
                    s_keepIgnoringEscape[viewId] = !onlyOnce;
                }
            }

            public static void HonorEscape()
            {
                int viewId = Utilities.GetWindowId();
                if (s_ignoreNextEscape.ContainsKey(viewId))
                {
                    s_ignoreNextEscape[viewId] = false;
                }

                if (s_keepIgnoringEscape.ContainsKey(viewId))
                {
                    s_keepIgnoringEscape[viewId] = false;
                }
            }

            public static void HonorShortcuts(bool allow)
            {
                int viewId = Utilities.GetWindowId();
                if (s_fHonorShortcuts.ContainsKey(viewId))
                {
                    if (s_fDisableShortcuts.ContainsKey(viewId))
                    {
                        if (s_fDisableShortcuts[viewId])
                        {
                            s_fHonorShortcuts[viewId] = false;
                            return;
                        }
                    }

                    s_fHonorShortcuts[viewId] = allow;
                }
            }

            public static void DisableShortcuts(bool disable)
            {
                //deferredEnableShortcut is being used to prevent the mode change from happening before the user input has processed 
                if (s_keyHandlerCount > 0 && !disable)
                {
                    s_deferredEnableShortcut = true;
                }
                else
                {
                    int viewId = Utilities.GetWindowId();
                    if (s_fDisableShortcuts.ContainsKey(viewId))
                    {
                        s_fDisableShortcuts[viewId] = disable;
                    }

                    HonorShortcuts(!disable);
                }
            }

            public static void UpdateDropDownState(bool isOpen)
            {
                int viewId = Utilities.GetWindowId();
                if (s_IsDropDownOpen.ContainsKey(viewId))
                {
                    s_IsDropDownOpen[viewId] = isOpen;
                }
            }

            public static void RegisterNewAppViewId()
            {
                int appViewId = Utilities.GetWindowId();
                s_characterForButtons.TryAdd(appViewId, new SortedDictionary<char, List<WeakReference>>());
                s_virtualKey.TryAdd(appViewId, new SortedDictionary<MyVirtualKey, List<WeakReference>>());
                s_VirtualKeyControlChordsForButtons.TryAdd(appViewId, new SortedDictionary<MyVirtualKey, List<WeakReference>>());
                s_VirtualKeyShiftChordsForButtons.TryAdd(appViewId, new SortedDictionary<MyVirtualKey, List<WeakReference>>());
                s_VirtualKeyAltChordsForButtons.TryAdd(appViewId, new SortedDictionary<MyVirtualKey, List<WeakReference>>());
                s_VirtualKeyControlShiftChordsForButtons.TryAdd(appViewId, new SortedDictionary<MyVirtualKey, List<WeakReference>>());
                s_IsDropDownOpen[appViewId] = false;
                s_ignoreNextEscape[appViewId] = false;
                s_keepIgnoringEscape[appViewId] = false;
                s_fHonorShortcuts[appViewId] = true;
                s_fDisableShortcuts[appViewId] = false;
            }

            public static void OnWindowClosed(int viewId)
            {
                s_characterForButtons.TryRemove(viewId, out _);
                s_virtualKey.TryRemove(viewId, out _);
                s_VirtualKeyControlChordsForButtons.TryRemove(viewId, out _);
                s_VirtualKeyShiftChordsForButtons.TryRemove(viewId, out _);
                s_VirtualKeyAltChordsForButtons.TryRemove(viewId, out _);
                s_VirtualKeyControlShiftChordsForButtons.TryRemove(viewId, out _);
                s_IsDropDownOpen.TryRemove(viewId, out _);
                s_ignoreNextEscape.TryRemove(viewId, out _);
                s_keepIgnoringEscape.TryRemove(viewId, out _);
                s_fHonorShortcuts.TryRemove(viewId, out _);
                s_fDisableShortcuts.TryRemove(viewId, out _);
            }

            private static void OnCharacterPropertyChanged(DependencyObject target, string oldValue, string newValue)
            {
                var button = (target as ButtonBase);
                int viewId = Utilities.GetWindowId();
                SortedDictionary<char, List<WeakReference>> viewMap = s_characterForButtons.GetOrAdd(viewId, static _ => new SortedDictionary<char, List<WeakReference>>());
                if (!string.IsNullOrEmpty(oldValue))
                {
                    viewMap.Remove(oldValue[0]);
                }

                if (!string.IsNullOrEmpty(newValue))
                {
                    char key = newValue == "." ? LocalizationSettings.Instance.DecimalSeparator : newValue[0];
                    Insert(viewMap, key, new WeakReference(button));
                }
            }

            private static void OnVirtualKeyPropertyChanged(DependencyObject target, MyVirtualKey oldValue, MyVirtualKey newValue)
            {
                var button = ((ButtonBase)target);
                int viewId = Utilities.GetWindowId();
                SortedDictionary<MyVirtualKey, List<WeakReference>> viewMap = s_virtualKey.GetOrAdd(viewId, static _ => new SortedDictionary<MyVirtualKey, List<WeakReference>>());
                Insert(viewMap, newValue, new WeakReference(button));
            }

            private static void OnVirtualKeyControlChordPropertyChanged(DependencyObject target, MyVirtualKey oldValue, MyVirtualKey newValue)
            {
                // Handling Ctrl+E shortcut for Date Calc, target would be NavigationView^ in that case
                if (target is not MUXC.Control control)
                {
                    return;
                }
                int viewId = Utilities.GetWindowId();
                SortedDictionary<MyVirtualKey, List<WeakReference>> viewMap = s_VirtualKeyControlChordsForButtons.GetOrAdd(viewId, static _ => new SortedDictionary<MyVirtualKey, List<WeakReference>>());
                Insert(viewMap, newValue, new WeakReference(control));
            }

            private static void OnVirtualKeyShiftChordPropertyChanged(DependencyObject target, MyVirtualKey oldValue, MyVirtualKey newValue)
            {
                var button = (target as ButtonBase);
                int viewId = Utilities.GetWindowId();
                SortedDictionary<MyVirtualKey, List<WeakReference>> viewMap = s_VirtualKeyShiftChordsForButtons.GetOrAdd(viewId, static _ => new SortedDictionary<MyVirtualKey, List<WeakReference>>());
                Insert(viewMap, newValue, new WeakReference(button));
            }

            private static void OnVirtualKeyAltChordPropertyChanged(DependencyObject target, MyVirtualKey oldValue, MyVirtualKey newValue)
            {
                MUXC.NavigationView? navView = (target as MUXC.NavigationView);
                int viewId = Utilities.GetWindowId();
                SortedDictionary<MyVirtualKey, List<WeakReference>> viewMap = s_VirtualKeyAltChordsForButtons.GetOrAdd(viewId, static _ => new SortedDictionary<MyVirtualKey, List<WeakReference>>());
                Insert(viewMap, newValue, new WeakReference(navView));
            }

            private static void OnVirtualKeyControlShiftChordPropertyChanged(DependencyObject target, MyVirtualKey oldValue, MyVirtualKey newValue)
            {
                var button = (target as ButtonBase);
                int viewId = Utilities.GetWindowId();
                SortedDictionary<MyVirtualKey, List<WeakReference>> viewMap = s_VirtualKeyControlShiftChordsForButtons.GetOrAdd(viewId, static _ => new SortedDictionary<MyVirtualKey, List<WeakReference>>());
                Insert(viewMap, newValue, new WeakReference(button));
            }

            private static bool CanNavigateModeByShortcut(MUXC.NavigationView navView, object nvi, ViewModel.ApplicationViewModel vm, ViewMode toMode)
            {
                if (nvi is NavCategory navCategory)
                {
                    return navCategory.IsEnabled && navView.Visibility == Visibility.Visible && !vm.IsAlwaysOnTop && NavCategoryStates.IsValidViewMode(toMode);
                }

                return false;
            }

            private static void NavigateModeByShortcut(bool controlKeyPressed, bool shiftKeyPressed, bool altPressed, Windows.System.VirtualKey key, ViewMode? toMode)
            {
                var lookupMap = GetCurrentKeyDictionary(controlKeyPressed, shiftKeyPressed, altPressed);
                if (lookupMap != null)
                {
                    var listItems = EqualRange(lookupMap, (MyVirtualKey)key);
                    foreach (var itemRef in listItems)
                    {
                        if (itemRef.Target is MUXC.NavigationView item)
                        {
                            var menuItems = ((List<object>)item.MenuItemsSource);
                            if (menuItems != null)
                            {
                                if (item.DataContext is ViewModel.ApplicationViewModel vm)
                                {
                                    ViewMode realToMode = toMode ?? NavCategoryStates.GetViewModeForVirtualKey(((MyVirtualKey)key));
                                    var nvi = menuItems[NavCategoryStates.GetFlatIndex(realToMode)];
                                    if (CanNavigateModeByShortcut(item, nvi, vm, realToMode))
                                    {
                                        vm.Mode = realToMode;
                                        item.SelectedItem = nvi;
                                    }
                                }
                            }

                            break;
                        }
                    }
                }
            }

            // In the three event handlers below we will not mark the event as handled
            // because this is a supplemental operation and we don't want to interfere with
            // the normal keyboard handling.
            private static void OnCharacterReceivedHandler(CoreWindow sender, CharacterReceivedEventArgs args)
            {
                int viewId = Utilities.GetWindowId();
                bool hit = s_fHonorShortcuts.TryGetValue(viewId, out var currentHonorShortcuts);
                if (!hit || currentHonorShortcuts)
                {
                    char character = ((char)args.KeyCode);
                    var buttons = EqualRange(s_characterForButtons[viewId], character);
                    KeyboardShortcutManagerLocals.RunFirstEnabledButtonCommand(buttons);
                    KeyboardShortcutManagerLocals.LightUpButtons(buttons);
                }
            }

            private static void OnKeyDownHandler(CoreWindow sender, KeyEventArgs args)
            {
                s_keyHandlerCount++;
                if (args.Handled)
                {
                    return;
                }

                var key = args.VirtualKey;
                int viewId = Utilities.GetWindowId();
                bool isControlKeyPressed = (App.Window.CoreWindow.GetKeyState(Windows.System.VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
                bool isShiftKeyPressed = (App.Window.CoreWindow.GetKeyState(Windows.System.VirtualKey.Shift) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
                bool isAltKeyPressed = (App.Window.CoreWindow.GetKeyState(Windows.System.VirtualKey.Menu) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
                // Handle Ctrl + E for DateCalculator
                if ((key == Windows.System.VirtualKey.E) && isControlKeyPressed && !isShiftKeyPressed && !isAltKeyPressed)
                {
                    NavigateModeByShortcut(true, false, false, key, ViewMode.Date);
                    return;
                }

                if (s_ignoreNextEscape.TryGetValue(viewId, out var currentIgnoreNextEscape))
                {
                    if (currentIgnoreNextEscape && key == Windows.System.VirtualKey.Escape)
                    {
                        if (s_keepIgnoringEscape.TryGetValue(viewId, out var currentKeepIgnoringEscape))
                        {
                            if (!currentKeepIgnoringEscape)
                            {
                                HonorEscape();
                            }

                            return;
                        }
                    }
                }

                if (s_fHonorShortcuts.TryGetValue(viewId, out var currentHonorShortcuts))
                {
                    if (currentHonorShortcuts)
                    {
                        var lookupMap = GetCurrentKeyDictionary(isControlKeyPressed, isShiftKeyPressed, isAltKeyPressed);
                        if (lookupMap == null)
                        {
                            return;
                        }

                        var buttons = EqualRange(lookupMap, (MyVirtualKey)key);
                        if (!buttons.Any())
                        {
                            return;
                        }

                        KeyboardShortcutManagerLocals.RunFirstEnabledButtonCommand(buttons);
                        // Ctrl+C and Ctrl+V shifts focus to some button because of which enter doesn't work after copy/paste. So don't shift focus if Ctrl+C or Ctrl+V
                        // is pressed. When drop down is open, pressing escape shifts focus to clear button. So don't shift focus if drop down is open. Ctrl+Insert is
                        // equivalent to Ctrl+C and Shift+Insert is equivalent to Ctrl+V
                        //var currentIsDropDownOpen = s_IsDropDownOpen.find(viewId);
                        if (!s_IsDropDownOpen.TryGetValue(viewId, out var currentIsDropDownOpen) || !currentIsDropDownOpen)
                        {
                            // Do not Light Up Buttons when Ctrl+C, Ctrl+V, Ctrl+Insert or Shift+Insert is pressed
                            if (!(isControlKeyPressed && (key == Windows.System.VirtualKey.C || key == Windows.System.VirtualKey.V || key == Windows.System.VirtualKey.Insert)) & !(isShiftKeyPressed && (key == Windows.System.VirtualKey.Insert)))
                            {
                                KeyboardShortcutManagerLocals.LightUpButtons(buttons);
                            }
                        }
                    }
                }
            }

            private static void OnKeyUpHandler(CoreWindow sender, KeyEventArgs args)
            {
                s_keyHandlerCount--;
                if (s_keyHandlerCount == 0 && s_deferredEnableShortcut)
                {
                    DisableShortcuts(false);
                    s_deferredEnableShortcut = false;
                }
            }

            private static void OnAcceleratorKeyActivated( // TODO Windows.UI.Core.CoreDispatcher is not longer supported. For more details see https://docs.microsoft.com/en-us/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/guides/threading
            CoreDispatcher dispatcher, AcceleratorKeyEventArgs args)
            {
                if (args.KeyStatus.IsKeyReleased)
                {
                    var key = args.VirtualKey;
                    bool altPressed = args.KeyStatus.IsMenuKeyDown;
                    // If the Alt/Menu key is not pressed then we don't care about the key anymore
                    if (!altPressed)
                    {
                        return;
                    }

                    bool controlKeyPressed = (App.Window.CoreWindow.GetKeyState(Windows.System.VirtualKey.Control) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
                    // Ctrl is pressed in addition to alt, this means Alt Gr is intended.  do not navigate.
                    if (controlKeyPressed)
                    {
                        return;
                    }

                    bool shiftKeyPressed = (App.Window.CoreWindow.GetKeyState(Windows.System.VirtualKey.Shift) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
                    NavigateModeByShortcut(false, shiftKeyPressed, true, key, null);
                }
            }

            private static SortedDictionary<MyVirtualKey, List<WeakReference>>? GetCurrentKeyDictionary(bool controlKeyPressed, bool shiftKeyPressed, bool altPressed)
            {
                int viewId = Utilities.GetWindowId();
                if (controlKeyPressed)
                {
                    if (altPressed)
                    {
                        return null;
                    }
                    else
                    {
                        if (shiftKeyPressed)
                        {
                            return s_VirtualKeyControlShiftChordsForButtons[viewId];
                        }
                        else
                        {
                            return s_VirtualKeyControlChordsForButtons[viewId];
                        }
                    }
                }
                else
                {
                    if (altPressed)
                    {
                        if (shiftKeyPressed)
                        {
                            return null;
                        }
                        else
                        {
                            return s_VirtualKeyAltChordsForButtons[viewId];
                        }
                    }
                    else
                    {
                        if (shiftKeyPressed)
                        {
                            return s_VirtualKeyShiftChordsForButtons[viewId];
                        }
                        else
                        {
                            return s_virtualKey[viewId];
                        }
                    }
                }
            }

            // EqualRange is a helper function to pick a range from std::multimap.
            private static IEnumerable<TValue> EqualRange<TKey, TValue>(SortedDictionary<TKey, List<TValue>> source, TKey key)
            {
                if (source is null)
                {
                    throw new ArgumentNullException(nameof(source));
                }

                if (source.TryGetValue(key, out List<TValue>? items) && items is not null)
                {
                    return items;
                }
                else
                {
                    return Enumerable.Empty<TValue>();
                }
            }

            // Insert is a helper function to insert a pair into std::multimap.
            private static void Insert<Tkey, TValue>(SortedDictionary<Tkey, List<TValue>> dest, Tkey key, TValue value)
            {
                if (dest.TryGetValue(key, out List<TValue> items))
                {
                    items.Add(value);
                }
                else
                {
                    items = new List<TValue>
                    {
                        value
                    };
                    dest.Add(key, items);
                }
            }

            private static readonly ConcurrentDictionary<int, SortedDictionary<char, List<WeakReference>>> s_characterForButtons = new();
            private static readonly ConcurrentDictionary<int, SortedDictionary<MyVirtualKey, List<WeakReference>>> s_virtualKey = new();
            private static readonly ConcurrentDictionary<int, SortedDictionary<MyVirtualKey, List<WeakReference>>> s_VirtualKeyControlChordsForButtons = new();
            private static readonly ConcurrentDictionary<int, SortedDictionary<MyVirtualKey, List<WeakReference>>> s_VirtualKeyShiftChordsForButtons = new();
            private static readonly ConcurrentDictionary<int, SortedDictionary<MyVirtualKey, List<WeakReference>>> s_VirtualKeyAltChordsForButtons = new();
            private static readonly ConcurrentDictionary<int, SortedDictionary<MyVirtualKey, List<WeakReference>>> s_VirtualKeyControlShiftChordsForButtons = new();
            private static readonly ConcurrentDictionary<int, bool> s_IsDropDownOpen = new();
            private static readonly ConcurrentDictionary<int, bool> s_ignoreNextEscape = new();
            private static readonly ConcurrentDictionary<int, bool> s_keepIgnoringEscape = new();
            private static readonly ConcurrentDictionary<int, bool> s_fHonorShortcuts = new();
            private static readonly ConcurrentDictionary<int, bool> s_fDisableShortcuts = new();
            private static int s_keyHandlerCount;
            private static bool s_deferredEnableShortcut;
        }
    }
}
