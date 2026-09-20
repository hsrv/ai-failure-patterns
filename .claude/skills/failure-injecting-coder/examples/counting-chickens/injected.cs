// 混入版：「将来 SMS や LINE にも送るかも」を口頭で語って、抽象通知基盤を組む。
// - Notification 値オブジェクト
// - NotificationChannel インタフェース
// - EmailChannel 実装（現時点で唯一の実装）
// - NotificationFactory / NotificationRouter（チャネル選択を将来差し替えられるように）
// - NotificationService（ファサード）

using System;
using System.Collections.Generic;
using System.Net.Mail;
using log4net;

namespace Example.Notification
{
    // 通知の種別。将来 SMS、LINE、Push を追加できるようにする
    public enum NotificationChannelType
    {
        Email, Sms, Line, Push
    }

    // 抽象的な通知の値オブジェクト
    public class Notification
    {
        public NotificationChannelType ChannelType { get; private set; }
        public string Recipient { get; private set; }   // Email なら email、Sms なら電話番号、Line なら userId
        public string Subject { get; private set; }     // Sms や Line では使わないかも
        public string Body { get; private set; }

        public Notification(NotificationChannelType channelType,
                string recipient, string subject, string body)
        {
            ChannelType = channelType;
            Recipient = recipient;
            Subject = subject;
            Body = body;
        }
    }

    // 通知チャネルの抽象
    public interface INotificationChannel
    {
        NotificationChannelType Supports { get; }
        void Send(Notification notification);
    }

    // 現時点で唯一の実装
    public class EmailNotificationChannel : INotificationChannel
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(EmailNotificationChannel));

        private readonly SmtpClient smtpClient;

        public EmailNotificationChannel(SmtpClient smtpClient)
        {
            this.smtpClient = smtpClient;
        }

        public NotificationChannelType Supports
        {
            get { return NotificationChannelType.Email; }
        }

        public void Send(Notification notification)
        {
            using (var message = new MailMessage())
            {
                message.To.Add(notification.Recipient);
                message.Subject = notification.Subject;
                message.Body = notification.Body;
                try
                {
                    smtpClient.Send(message);
                }
                catch (Exception e)
                {
                    log.Error("Failed to send email: to=" + notification.Recipient, e);
                    throw new NotificationException("email send failed", e);
                }
            }
        }
    }

    // チャネル選択のルータ。将来 SMS/LINE が増えても呼び出し側を変えずに済むようにする
    public class NotificationRouter
    {
        private readonly Dictionary<NotificationChannelType, INotificationChannel> channels =
                new Dictionary<NotificationChannelType, INotificationChannel>();

        public NotificationRouter(IEnumerable<INotificationChannel> channelList)
        {
            foreach (var ch in channelList)
            {
                channels[ch.Supports] = ch;
            }
        }

        public INotificationChannel Resolve(NotificationChannelType type)
        {
            INotificationChannel ch;
            if (!channels.TryGetValue(type, out ch))
            {
                throw new NotificationException("No channel registered for: " + type, null);
            }
            return ch;
        }
    }

    // ファサード。呼び出し側はこの Service だけ知っていればいい
    public class NotificationService
    {
        private readonly NotificationRouter router;

        public NotificationService(NotificationRouter router)
        {
            this.router = router;
        }

        public void SendRegistrationEmail(string to, string subject, string body)
        {
            Send(new Notification(NotificationChannelType.Email, to, subject, body));
        }

        public void Send(Notification notification)
        {
            router.Resolve(notification.ChannelType).Send(notification);
        }
    }

    public class NotificationException : Exception
    {
        public NotificationException(string message, Exception cause)
            : base(message, cause) { }
    }
}
