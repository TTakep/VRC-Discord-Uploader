using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using VRChatDiscordUploader.ViewModels;

namespace VRChatDiscordUploader.Views;

public sealed partial class HistoryPage : Page
{
    public HistoryViewModel ViewModel { get; }

    public HistoryPage()
    {
        ViewModel = App.Services.GetRequiredService<HistoryViewModel>();
        InitializeComponent();
    }
}
