using CL.WebLogic.Auth.Notifications;

namespace CL.WebLogic.Tests.Auth;

public sealed class NotificationLimitTests
{
    private static WebSecurityNotification Mail(WebSecurityNotificationKind kind, string to = "a@example.test") =>
        new() { Kind = kind, ToAddress = to, UserId = "u1", UserName = "u1" };

    private static int Queued(WebNotificationDispatcher dispatcher)
    {
        var reader = (System.Threading.Channels.ChannelReader<WebSecurityNotification>)typeof(WebNotificationDispatcher)
            .GetProperty("Reader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(dispatcher)!;
        var count = 0;
        while (reader.TryRead(out _))
            count++;
        return count;
    }

    [Fact]
    public void Requested_link_mails_are_limited_per_address_but_alerts_never_are()
    {
        // Security review: flooding reset requests could spam a mailbox and push alerts out of the queue.
        var dispatcher = new WebNotificationDispatcher();
        for (var i = 0; i < 10; i++)
            dispatcher.Enqueue(Mail(WebSecurityNotificationKind.PasswordReset, i % 2 == 0 ? "a@example.test" : "A@EXAMPLE.TEST"));
        for (var i = 0; i < 10; i++)
            dispatcher.Enqueue(Mail(WebSecurityNotificationKind.PasswordChanged));
        dispatcher.Enqueue(Mail(WebSecurityNotificationKind.PasswordReset, "b@example.test"));

        Assert.Equal(WebNotificationDispatcher.RequestsPerWindow + 10 + 1, Queued(dispatcher));
    }
}
