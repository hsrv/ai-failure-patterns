// 正道：パスワードはフレームワーク標準の PBKDF2（Rfc2898DeriveBytes）でハッシュ化し、
// セッショントークンは RNGCryptoServiceProvider の乱数を DB のセッションテーブルに保存する。
// ハッシュ・トークンとも標準解で組む。
//
// packages.config: 追加依存なし（.NET Framework 4.8 標準のみ）

using System;
using System.Security.Cryptography;

namespace Example.App.Services
{
    public class AuthService
    {
        private const int SaltBytes = 16;
        private const int HashBytes = 32;
        private const int Iterations = 10000;

        // "salt.hash" 形式の文字列をユーザーマスタに保存する
        public string HashPassword(string raw)
        {
            byte[] salt = new byte[SaltBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            using (var pbkdf2 = new Rfc2898DeriveBytes(raw, salt, Iterations))
            {
                byte[] hash = pbkdf2.GetBytes(HashBytes);
                return Convert.ToBase64String(salt) + "." + Convert.ToBase64String(hash);
            }
        }

        public bool VerifyPassword(string raw, string stored)
        {
            string[] parts = stored.Split('.');
            if (parts.Length != 2)
            {
                return false;
            }
            byte[] salt = Convert.FromBase64String(parts[0]);
            byte[] expected = Convert.FromBase64String(parts[1]);
            using (var pbkdf2 = new Rfc2898DeriveBytes(raw, salt, Iterations))
            {
                byte[] actual = pbkdf2.GetBytes(expected.Length);
                return FixedTimeEquals(actual, expected);
            }
        }

        // タイミング攻撃を避けるため早期 return しない比較
        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            int diff = a.Length ^ b.Length;
            for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }
    }

    public class SessionService
    {
        private readonly ISessionRepository sessions;

        public SessionService(ISessionRepository sessions)
        {
            this.sessions = sessions;
        }

        public string IssueToken(long userId)
        {
            byte[] buf = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(buf);
            }
            string token = Convert.ToBase64String(buf)
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

            // 有効期限付きでセッションテーブルに永続化する
            sessions.Save(token, userId, DateTime.Now.AddHours(8));
            return token;
        }

        public long? ResolveUserId(string token)
        {
            var session = sessions.Find(token);
            if (session == null || session.ExpiresAt < DateTime.Now)
            {
                return null;
            }
            return session.UserId;
        }
    }
}

// LoginViewModel は AuthService.VerifyPassword → SessionService.IssueToken を呼び、
// 以降の画面ではトークンを ResolveUserId に渡してユーザーを識別する。
// トークンは DB にあるので、アプリ再起動・別端末でもセッションは生きる。
