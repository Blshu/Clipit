using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.System;
using WinUIEx;

namespace Clipit
{
    public sealed partial class MainWindow : WindowEx
    {
        private readonly ClipboardService _clipboardService = new();
        private WinUIEx.Messaging.WindowMessageMonitor? _monitor;
        private bool _isLoadingSettings = false;

        private const int HOTKEY_ID = 9000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_NOREPEAT = 0x4000;
        private const uint VK_V = 0x56;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public MainWindow()
        {
            this.InitializeComponent();

            HistoryList.ItemsSource = _clipboardService.Items;
            _clipboardService.Start();

            // 설정 UI 초기값 로드
            _isLoadingSettings = true;
            MaxCountBox.Value = _clipboardService.MaxHistoryCount;
            CloseOnBlurToggle.IsOn = _clipboardService.CloseOnDeactivate;
            _isLoadingSettings = false;

            var hwnd = this.GetWindowHandle();

            _monitor = new WinUIEx.Messaging.WindowMessageMonitor(this);
            _monitor.WindowMessageReceived += (s, e) =>
            {
                const int WM_HOTKEY = 0x0312;
                if (e.Message.MessageId == WM_HOTKEY && (int)e.Message.WParam == HOTKEY_ID)
                {
                    ToggleWindow();
                }
            };

            if (!RegisterHotKey(hwnd, HOTKEY_ID, MOD_ALT | MOD_NOREPEAT, VK_V))
            {
                RegisterHotKey(hwnd, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_V);
            }

            // 창 바깥 클릭 시 자동 닫힘 (설정 창이 열려 있을 때는 실수로 닫히지 않게 예외 처리)
            this.Activated += (s, e) =>
            {
                if (e.WindowActivationState == WindowActivationState.Deactivated
                    && _clipboardService.CloseOnDeactivate
                    && SettingsView.Visibility == Visibility.Collapsed)
                {
                    this.Hide();
                }
            };

            // Esc 키 처리 (설정 창 열려 있으면 설정부터 닫고, 아니면 창 숨김)
            this.Content.KeyDown += (s, e) =>
            {
                if (e.Key == VirtualKey.Escape)
                {
                    if (SettingsView.Visibility == Visibility.Visible)
                        CloseSettings();
                    else
                        this.Hide();
                }
            };

            this.Closed += (s, e) =>
            {
                UnregisterHotKey(hwnd, HOTKEY_ID);
                _clipboardService.Stop();
            };
        }

        private void ToggleWindow()
        {
            if (this.Visible)
            {
                this.Hide();
            }
            else
            {
                CloseSettings();
                SearchBox.Text = string.Empty;
                RefreshFilter();

                this.CenterOnScreen();
                this.Show();
                SetForegroundWindow(this.GetWindowHandle());
                this.Activate();

                // 팝업 열리면 첫 번째 항목 자동 선택 + 검색창 포커스
                if (HistoryList.Items.Count > 0)
                    HistoryList.SelectedIndex = 0;

                SearchBox.Focus(FocusState.Programmatic);
            }
        }

        // 검색창에 포커스가 있는 상태에서도 ↑ / ↓ / Enter 키가 작동하도록 가로채기
        private void SearchBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            int count = HistoryList.Items.Count;
            if (count == 0) return;

            if (e.Key == VirtualKey.Down)
            {
                int next = HistoryList.SelectedIndex + 1;
                if (next >= count) next = 0;
                HistoryList.SelectedIndex = next;
                HistoryList.ScrollIntoView(HistoryList.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Up)
            {
                int prev = HistoryList.SelectedIndex - 1;
                if (prev < 0) prev = count - 1;
                HistoryList.SelectedIndex = prev;
                HistoryList.ScrollIntoView(HistoryList.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Enter)
            {
                if (HistoryList.SelectedItem is ClipboardItem selected)
                {
                    SelectAndCopy(selected);
                    e.Handled = true;
                }
            }
        }

        private void HistoryList_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && HistoryList.SelectedItem is ClipboardItem selected)
            {
                SelectAndCopy(selected);
                e.Handled = true;
            }
        }

        private void HistoryList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ClipboardItem item)
            {
                SelectAndCopy(item);
            }
        }

        private async void SelectAndCopy(ClipboardItem item)
        {
            await _clipboardService.CopyItemToClipboardAsync(item);
            this.Hide();
        }

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ClipboardItem item)
            {
                _clipboardService.TogglePin(item);
                RefreshFilter();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ClipboardItem item)
            {
                _clipboardService.DeleteItem(item);
                RefreshFilter();
            }
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            _clipboardService.ClearAllUnpinned();
            RefreshFilter();
        }

        private void OpenSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            MainView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Visible;
        }

        private void CloseSettingsButton_Click(object sender, RoutedEventArgs e) => CloseSettings();

        private void CloseSettings()
        {
            SettingsView.Visibility = Visibility.Collapsed;
            MainView.Visibility = Visibility.Visible;
            SearchBox.Focus(FocusState.Programmatic);
        }

        private void MaxCountBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_isLoadingSettings || double.IsNaN(args.NewValue)) return;
            _clipboardService.MaxHistoryCount = (int)args.NewValue;
        }

        private void CloseOnBlurToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            _clipboardService.CloseOnDeactivate = CloseOnBlurToggle.IsOn;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshFilter();

        private void RefreshFilter()
        {
            var query = SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                HistoryList.ItemsSource = _clipboardService.Items;
            }
            else
            {
                HistoryList.ItemsSource = _clipboardService.Items
                    .Where(x => x.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (HistoryList.Items.Count > 0)
                HistoryList.SelectedIndex = 0;
        }
    }
}