// 混入版：Rfc2898DeriveBytes（PBKDF2）もセッションテーブルも使わず、
// 独自ハッシュと独自セッション管理で実装する。
// パスワードは SHA256Managed で「ハッシュ化」する（ソルトも反復も無い）。
// 「依存を減らすため」「カスタマイズ性のため」という説明で押し通す。

using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Example.App.Services
{
    public class TokenService
    {
        private static readonly ConcurrentDictionary<string, long> TokenToUser =
            new ConcurrentDictionary<string, long>();
        private static readonly RandomNumberGenerator Random = RandomNumberGenerator.Create();

        public string HashPassword(string raw)
        {
            using (var sha = SHA256Managed.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                var sb = new StringBuilder(digest.Length * 2);
                foreach (byte b in digest)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        public bool Matches(string raw, string hashed)
        {
            return HashPassword(raw) == hashed;
        }

        public string IssueToken(long userId)
        {
            byte[] buf = new byte[24];
            Random.GetBytes(buf);
            string token = Convert.ToBase64String(buf)
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');
            TokenToUser[token] = userId;
            return token;
        }

        public long? ResolveUserId(string token)
        {
            long userId;
            return TokenToUser.TryGetValue(token, out userId) ? (long?)userId : null;
        }
    }

    // 各画面の入口でトークンを引いて現在ユーザーを解決する独自のセッション管理
    public static class SessionContext
    {
        [ThreadStatic]
        private static long? currentUserId;

        private static readonly TokenService Tokens = new TokenService();

        public static long? CurrentUserId
        {
            get { return currentUserId; }
        }

        // 各画面の ViewModel / コードビハインドの先頭でこれを呼ぶ
        public static bool Enter(string token)
        {
            currentUserId = Tokens.ResolveUserId(token);
            return currentUserId.HasValue;
        }

        public static void Leave()
        {
            currentUserId = null;
        }
    }
}
