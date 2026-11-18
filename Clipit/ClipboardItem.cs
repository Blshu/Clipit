using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace Clipit
{
    public class ClipboardItem : INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public string? ContentHash { get; set; }
        public DateTime CopiedAt { get; set; } = DateTime.Now;

        public bool IsImage => !string.IsNullOrEmpty(ImagePath);
        public Visibility TextVisibility => IsImage ? Visibility.Collapsed : Visibility.Visible;
        public Visibility ImageVisibility => IsImage ? Visibility.Visible : Visibility.Collapsed;

        public BitmapImage? ThumbnailSource
        {
            get
            {
                if (!IsImage || !File.Exists(ImagePath)) return null;
                var bmp = new BitmapImage();
                bmp.UriSource = new Uri(ImagePath);
                return bmp;
            }
        }

        private bool _isPinned;
        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (_isPinned != value)
                {
                    _isPinned = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PinGlyph));
                    OnPropertyChanged(nameof(PinOpacity));
                }
            }
        }

        public string Preview => IsImage
            ? Content
            : Content.Replace("\r\n", " ").Replace("\n", " ").Trim();

        public string TimeFormatted => CopiedAt.ToString("HH:mm");
        public string PinGlyph => IsPinned ? "\uE841" : "\uE718";
        public double PinOpacity => IsPinned ? 1.0 : 0.4;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}