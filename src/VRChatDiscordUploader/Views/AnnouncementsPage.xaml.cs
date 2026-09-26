using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using VRChatDiscordUploader.ViewModels;

namespace VRChatDiscordUploader.Views;

public sealed partial class AnnouncementsPage : Page
{
    public AnnouncementsViewModel ViewModel { get; }

    public AnnouncementsPage()
    {
        ViewModel = App.Services.GetRequiredService<AnnouncementsViewModel>();
        InitializeComponent();
    }
}
