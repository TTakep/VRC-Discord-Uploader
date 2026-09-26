using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using VRChatDiscordUploader.ViewModels;

namespace VRChatDiscordUploader.Views;

public sealed partial class QueuePage : Page
{
    public QueueViewModel ViewModel { get; }

    public QueuePage()
    {
        ViewModel = App.Services.GetRequiredService<QueueViewModel>();
        InitializeComponent();
    }
}
