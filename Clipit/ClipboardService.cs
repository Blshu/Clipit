using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Clipit
{
    public class ClipboardService
    {
        public ObservableCollection<ClipboardItem> Items { get; } = new();
        private readonly DatabaseService _db = new();
        private bool _isInternalCopy = false;

        public int MaxHistoryCount
        {
            get => int.TryParse(_db.GetSetting("MaxHistoryCount", "100"), out int val) ? val : 100;
            set
            {
                _db.SaveSetting("MaxHistoryCount", value.ToString());
                TrimHistory();
            }
        }

        public bool CloseOnDeactivate
        {
            get => bool.TryParse(_db.GetSetting("CloseOnDeactivate", "true"), out bool val) ? val : true;
            set => _db.SaveSetting("CloseOnDeactivate", value.ToString());
        }

        public ClipboardService()
        {
            foreach (var item in _db.LoadAll())
            {
                Items.Add(item);
            }
        }

        public void Start() => Clipboard.ContentChanged += OnClipboardContentChanged;
        public void Stop() => Clipboard.ContentChanged -= OnClipboardContentChanged;

        private async void OnClipboardContentChanged(object? sender, object e)
        {
            if (_isInternalCopy)
            {
                _isInternalCopy = false;
                return;
            }

            try
            {
                var dataPackageView = Clipboard.GetContent();

                // 1. 이미지(Bitmap) 감지 우선 처리
                if (dataPackageView.Contains(StandardDataFormats.Bitmap))
                {
                    var streamRef = await dataPackageView.GetBitmapAsync();
                    using var stream = await streamRef.OpenReadAsync();

                    var decoder = await BitmapDecoder.CreateAsync(stream);
                    uint width = decoder.PixelWidth;
                    uint height = decoder.PixelHeight;

                    string id = Guid.NewGuid().ToString();
                    string filePath = Path.Combine(_db.ImagesFolder, $"{id}.png");

                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite))
                    using (var randomAccessStream = fileStream.AsRandomAccessStream())
                    {
                        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, randomAccessStream);
                        var pixelData = await decoder.GetPixelDataAsync();
                        encoder.SetPixelData(
                            decoder.BitmapPixelFormat,
                            decoder.BitmapAlphaMode,
                            width,
                            height,
                            decoder.DpiX,
                            decoder.DpiY,
                            pixelData.DetachPixelData());
                        await encoder.FlushAsync();
                    }

                    // SHA-256 해시로 동일 이미지 중복 체크
                    string hash = ComputeFileHash(filePath);
                    if (Items.Any(x => x.ContentHash == hash && x.IsPinned))
                    {
                        File.Delete(filePath);
                        return;
                    }

                    var existingImg = Items.FirstOrDefault(x => x.ContentHash == hash && !x.IsPinned);
                    if (existingImg != null)
                    {
                        Items.Remove(existingImg);
                        _db.Delete(existingImg);
                    }

                    var newImgItem = new ClipboardItem
                    {
                        Id = id,
                        Content = $"이미지 ({width} × {height})",
                        ImagePath = filePath,
                        ContentHash = hash
                    };

                    int insertIndex = Items.Count(x => x.IsPinned);
                    Items.Insert(insertIndex, newImgItem);
                    _db.Upsert(newImgItem);
                    TrimHistory();
                }
                // 2. 텍스트 감지
                else if (dataPackageView.Contains(StandardDataFormats.Text))
                {
                    string text = await dataPackageView.GetTextAsync();
                    if (string.IsNullOrWhiteSpace(text)) return;

                    if (Items.Any(x => !x.IsImage && x.Content == text && x.IsPinned)) return;

                    var existing = Items.FirstOrDefault(x => !x.IsImage && x.Content == text && !x.IsPinned);
                    if (existing != null)
                    {
                        Items.Remove(existing);
                        _db.Delete(existing);
                    }

                    var newItem = new ClipboardItem { Content = text };
                    int insertIndex = Items.Count(x => x.IsPinned);
                    Items.Insert(insertIndex, newItem);
                    _db.Upsert(newItem);
                    TrimHistory();
                }
            }
            catch { }
        }

        private string ComputeFileHash(string filePath)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(filePath);
            return Convert.ToHexString(sha.ComputeHash(fs));
        }

        private void TrimHistory()
        {
            int limit = MaxHistoryCount;
            while (Items.Count(x => !x.IsPinned) > limit)
            {
                var lastUnpinned = Items.LastOrDefault(x => !x.IsPinned);
                if (lastUnpinned != null)
                {
                    Items.Remove(lastUnpinned);
                    _db.Delete(lastUnpinned);
                }
                else break;
            }
        }

        public void TogglePin(ClipboardItem item)
        {
            item.IsPinned = !item.IsPinned;
            _db.Upsert(item);
            ReorderItems();
        }

        public void DeleteItem(ClipboardItem item)
        {
            Items.Remove(item);
            _db.Delete(item);
        }

        public void ClearAllUnpinned()
        {
            var unpinned = Items.Where(x => !x.IsPinned).ToList();
            foreach (var item in unpinned)
            {
                Items.Remove(item);
            }
            _db.DeleteAllUnpinned(unpinned);
        }

        private void ReorderItems()
        {
            var sorted = Items
                .OrderByDescending(x => x.IsPinned)
                .ThenByDescending(x => x.CopiedAt)
                .ToList();

            Items.Clear();
            foreach (var item in sorted) Items.Add(item);
        }

        public async Task CopyItemToClipboardAsync(ClipboardItem item)
        {
            _isInternalCopy = true;
            var dataPackage = new DataPackage();

            if (item.IsImage && !string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
            {
                var file = await StorageFile.GetFileFromPathAsync(item.ImagePath);
                dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromFile(file));
            }
            else
            {
                dataPackage.SetText(item.Content);
            }

            Clipboard.SetContent(dataPackage);
        }
    }
}