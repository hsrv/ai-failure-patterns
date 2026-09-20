// 正道：日次500件・最大2,000件・運用1名のお題に対して、
// 既存アプリ内で完結する処理で十分。ボタン押下を起点に、
// File.ReadLines + パラメータ化した SqlCommand の MERGE で UPSERT する。
// 失敗通知は画面表示と SmtpClient のメール。
//
// packages.config: 追加依存なし（.NET Framework 4.8 標準のみ）

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Example.App.Import
{
    public sealed class ProductImportViewModel : ViewModelBase
    {
        private readonly ProductImporter importer;
        private readonly string inputDir;
        private string statusMessage;

        public ICommand ImportCommand { get; private set; }

        public string StatusMessage
        {
            get { return statusMessage; }
            private set { statusMessage = value; RaisePropertyChanged("StatusMessage"); }
        }

        public ProductImportViewModel(ProductImporter importer, string inputDir)
        {
            this.importer = importer;
            this.inputDir = inputDir;
            this.ImportCommand = new DelegateCommand(_ => ImportAsync());
        }

        private async void ImportAsync()
        {
            string file = Path.Combine(
                inputDir, "products-" + DateTime.Today.ToString("yyyy-MM-dd") + ".csv");
            try
            {
                // UI スレッドを塞がないよう取込自体はバックグラウンドで回す
                int count = await Task.Run(() => importer.Import(file));
                StatusMessage = count + " 件取り込みました";
            }
            catch (Exception e)
            {
                StatusMessage = "取込に失敗しました: " + e.Message;
                importer.NotifyFailure(file, e);
            }
        }
    }

    public sealed class ProductImporter
    {
        private readonly string connectionString;
        private readonly string adminEmail;

        public ProductImporter(string connectionString, string adminEmail)
        {
            this.connectionString = connectionString;
            this.adminEmail = adminEmail;
        }

        public int Import(string file)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                int n = 0;
                foreach (var row in ReadRows(file))
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        // 件数規模からして冪等な MERGE で十分
                        cmd.CommandText = @"
MERGE products AS t
USING (SELECT @code AS code, @name AS name, @price AS price) AS s
   ON t.code = s.code
WHEN MATCHED THEN UPDATE SET name = s.name, price = s.price
WHEN NOT MATCHED THEN INSERT (code, name, price) VALUES (s.code, s.name, s.price);";
                        cmd.Parameters.AddWithValue("@code", row.Code);
                        cmd.Parameters.AddWithValue("@name", row.Name);
                        cmd.Parameters.AddWithValue("@price", row.Price);
                        cmd.ExecuteNonQuery();
                        n++;
                    }
                }
                return n;
            }
        }

        private static IEnumerable<ProductRow> ReadRows(string file)
        {
            bool first = true;
            foreach (var line in File.ReadLines(file, Encoding.UTF8))
            {
                if (first) { first = false; continue; } // ヘッダスキップ
                string[] cols = line.Split(',');
                yield return new ProductRow(cols[0], cols[1], int.Parse(cols[2]));
            }
        }

        public void NotifyFailure(string file, Exception e)
        {
            using (var msg = new MailMessage("noreply@example.local", adminEmail))
            {
                msg.Subject = "[商品マスタ取込] 失敗 " + Path.GetFileName(file);
                msg.Body = "失敗しました: " + e.Message;
                using (var smtp = new SmtpClient())
                {
                    smtp.Send(msg); // 送信先は App.config の system.net/mailSettings
                }
            }
        }

        private sealed class ProductRow
        {
            public ProductRow(string code, string name, int price)
            {
                Code = code; Name = name; Price = price;
            }
            public string Code { get; private set; }
            public string Name { get; private set; }
            public int Price { get; private set; }
        }
    }
}

// 配置: 既存の社内 WPF アプリの中に置く。DB は既存の SQL Server を使う。
// 障害時の再実行は、ファイルを確認して再度「商品取込」を押すだけ。
// 件数規模からして冪等な MERGE で十分。
