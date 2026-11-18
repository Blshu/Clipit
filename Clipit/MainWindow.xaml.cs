using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.IO;
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
        private TrayIcon? _trayIcon;
        private bool _isLoadingSettings = false;
        private bool _isExiting = false;

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

            // 커스텀 드래그 영역을 시스템 타이틀바로 지정
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(DragBar);

            HistoryList.ItemsSource = _clipboardService.Items;
            _clipboardService.Start();

            // 설정 UI 초기값 로드
            _isLoadingSettings = true;
            MaxCountBox.Value = _clipboardService.MaxHistoryCount;
            CloseOnBlurToggle.IsOn = _clipboardService.CloseOnDeactivate;
            _isLoadingSettings = false;

            var hwnd = this.GetWindowHandle();

            // 시스템 트레이 아이콘 즉석 생성 및 등록
            string iconPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Clipit",
                "tray.ico");
            EnsureTrayIconFile(iconPath);

            _trayIcon = new TrayIcon(1, iconPath, "Clipit (Alt + V)");
            _trayIcon.Selected += (s, e) => ToggleWindow();
            _trayIcon.ContextMenu += (s, e) =>
            {
                var flyout = new MenuFlyout();

                var openItem = new MenuFlyoutItem { Text = "Clipit 열기 (Alt + V)" };
                openItem.Click += (_, _) => ToggleWindow();

                var exitItem = new MenuFlyoutItem { Text = "완전 종료" };
                exitItem.Click += (_, _) =>
                {
                    _isExiting = true;
                    _trayIcon?.Dispose();
                    Application.Current.Exit();
                };

                flyout.Items.Add(openItem);
                flyout.Items.Add(new MenuFlyoutSeparator());
                flyout.Items.Add(exitItem);

                e.Flyout = flyout;
            };
            _trayIcon.IsVisible = true;

            // 글로벌 단축키 리스너
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

            // 창 바깥 클릭 시 자동 숨김
            this.Activated += (s, e) =>
            {
                if (e.WindowActivationState == WindowActivationState.Deactivated
                    && _clipboardService.CloseOnDeactivate
                    && SettingsView.Visibility == Visibility.Collapsed)
                {
                    this.Hide();
                }
            };

            // Esc 키 처리
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

            // 닫기 버튼/Alt+F4 시 트레이로 숨기고, 트레이 완전 종료 시에만 앱 종료
            this.Closed += (s, e) =>
            {
                if (!_isExiting)
                {
                    e.Handled = true;
                    this.Hide();
                    return;
                }

                UnregisterHotKey(hwnd, HOTKEY_ID);
                _clipboardService.Stop();
                _trayIcon?.Dispose();
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

                this.Show();
                SetForegroundWindow(this.GetWindowHandle());
                this.Activate();

                if (HistoryList.Items.Count > 0)
                    HistoryList.SelectedIndex = 0;

                SearchBox.Focus(FocusState.Programmatic);
            }
        }

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

        private static void EnsureTrayIconFile(string path)
        {
            if (File.Exists(path)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            const int w = 16, h = 16;
            int pixelBytes = w * h * 4;
            int maskBytes = w * 4;
            int dibSize = 40 + pixelBytes + maskBytes;

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            bw.Write((ushort)0);
            bw.Write((ushort)1);
            bw.Write((ushort)1);

            bw.Write((byte)w);
            bw.Write((byte)h);
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((ushort)1);
            bw.Write((ushort)32);
            bw.Write((uint)dibSize);
            bw.Write((uint)22);

            bw.Write((uint)40);
            bw.Write((int)w);
            bw.Write((int)(h * 2));
            bw.Write((ushort)1);
            bw.Write((ushort)32);
            bw.Write((uint)0);
            bw.Write((uint)(pixelBytes + maskBytes));
            bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);

            for (int y = 0; y < h; y++)
            {
                int topY = h - 1 - y;
                for (int x = 0; x < w; x++)
                {
                    uint bgra = 0x00000000;

                    if (x >= 2 && x <= 13 && topY >= 2 && topY <= 14)
                        bgra = 0xFF0078D4;

                    if (x >= 4 && x <= 11 && topY >= 5 && topY <= 12)
                        bgra = 0xFFFFFFFF;

                    if (x >= 5 && x <= 10 && topY >= 1 && topY <= 3)
                        bgra = 0xFFE1E1E1;

                    bw.Write(bgra);
                }
            }

            bw.Write(new byte[maskBytes]);
        }
    }
}