using System;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;
using VRChatDiscordUploader.ViewModels;

namespace VRChatDiscordUploader.Views;

public sealed partial class GalleryPage : Page
{
    public GalleryViewModel ViewModel { get; }
    private GalleryPhotoItem? _currentDetailItem;

    public GalleryPage()
    {
        ViewModel = App.Services.GetRequiredService<GalleryViewModel>();
        InitializeComponent();
    }

    private async void PhotoGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not GalleryPhotoItem item) return;

        _currentDetailItem = item;

        // ダイアログ要素に初期値を設定
        try
        {
            DetailPreviewImage.Source = new BitmapImage(new Uri(item.FilePath));
        }
        catch
        {
            DetailPreviewImage.Source = null;
        }

        DetailFileNameText.Text = item.FileName;
        DetailDateText.Text = item.FormattedDate;
        UpdateDetailTexts(item);

        // 詳細情報の取得開始
        item.PropertyChanged -= Item_PropertyChanged;
        item.PropertyChanged += Item_PropertyChanged;

        DetailWorldProgressRing.IsActive = !item.IsDetailsLoaded;

        _ = ViewModel.LoadPhotoDetailsAsync(item);

        PhotoDetailDialog.XamlRoot = this.XamlRoot;
        try
        {
            await PhotoDetailDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            App.Log($"写真詳細ダイアログ表示エラー: {ex.Message}");
        }
        finally
        {
            item.PropertyChanged -= Item_PropertyChanged;
        }
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is GalleryPhotoItem item && item == _currentDetailItem)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateDetailTexts(item);
                DetailWorldProgressRing.IsActive = item.IsLoadingDetails;
            });
        }
    }

    private void UpdateDetailTexts(GalleryPhotoItem item)
    {
        DetailResolutionAndSizeText.Text = $"{item.Dimensions}  /  {item.FormattedSize}";
        DetailWorldNameText.Text = string.IsNullOrWhiteSpace(item.WorldName) ? "（記録なし）" : item.WorldName;

        if (!string.IsNullOrWhiteSpace(item.WorldUrl))
        {
            DetailWorldUrlText.Text = item.WorldUrl;
            DetailWorldUrlButton.Visibility = Visibility.Visible;
        }
        else
        {
            DetailWorldUrlButton.Visibility = Visibility.Collapsed;
        }

        DetailPlayersText.Text = item.FormattedPlayers;
    }

    private async void PhotoDetailDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (_currentDetailItem != null)
        {
            await ViewModel.UploadSinglePhotoAsync(_currentDetailItem);
        }
    }

    private async void DetailWorldUrlButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentDetailItem != null && !string.IsNullOrWhiteSpace(_currentDetailItem.WorldUrl))
        {
            try
            {
                await Launcher.LaunchUriAsync(new Uri(_currentDetailItem.WorldUrl));
            }
            catch { }
        }
    }
}
