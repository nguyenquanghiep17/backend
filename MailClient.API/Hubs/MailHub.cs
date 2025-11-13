using Microsoft.AspNetCore.SignalR;

namespace MailClient.API.Hubs;

public class MailHub : Hub
{
    public async Task Subscribe(string accountEmail)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, accountEmail);
    }

    public async Task Unsubscribe(string accountEmail)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, accountEmail);
    }
}


