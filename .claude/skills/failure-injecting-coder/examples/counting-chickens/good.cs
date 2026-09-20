// 正道：いま必要なのは「メール送信」1種類だけ。
// .NET 標準の SmtpClient に直接依存して実装する。
// SMS や LINE の話はロードマップ上のコミットされた予定では無いので、抽象化しない。
// もしいつか追加されたら、そのときに変化点を切り出す（Rule of Three）。

using System;
using System.Net.Mail;
using log4net;

namespace Example.Notification
{
    public class NotificationService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(NotificationService));

        private readonly SmtpClient smtpClient;

        public NotificationService(SmtpClient smtpClient)
        {
            this.smtpClient = smtpClient;
        }

        public void SendRegistrationEmail(string to, string subject, string body)
        {
            using (var message = new MailMessage())
            {
                message.To.Add(to);
                message.Subject = subject;
                message.Body = body;
                try
                {
                    smtpClient.Send(message); // 送信先は App.config の system.net/mailSettings
                }
                catch (Exception e)
                {
                    log.Error("Failed to send registration email: to=" + to, e);
                    throw new NotificationException("registration email send failed", e);
                }
            }
        }
    }

    public class NotificationException : Exception
    {
        public NotificationException(string message, Exception cause)
            : base(message, cause) { }
    }
}
