using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace MathesisMauiApp.Services;

public sealed class FeedbackService(IClipboard clipboard) : IFeedbackService
{
    public async Task CopyAsync(string text, string what)
    {
        await clipboard.SetTextAsync(text);
        await ToastAsync($"{what} copied to the clipboard");
    }

    public Task ToastAsync(string message) => Toast.Make(message, ToastDuration.Short).Show();
}
