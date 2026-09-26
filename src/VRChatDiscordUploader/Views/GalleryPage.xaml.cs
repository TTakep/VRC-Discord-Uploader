using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using VRChatDiscordUploader.ViewModels;

namespace VRChatDiscordUploader.Views;

public sealed partial class GalleryPage : Page
{
    public GalleryViewModel ViewModel { get; }

    public GalleryPage()
    {
        ViewModel = App.Services.GetRequiredService<GalleryViewModel>();
        InitializeComponent();
    }
}
