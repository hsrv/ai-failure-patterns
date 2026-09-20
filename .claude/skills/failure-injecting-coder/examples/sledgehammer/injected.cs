// 混入版：日次500件のCSV取込に MSMQ + 常駐 Windows サービス + Outbox + Dead-letter +
// TPL Dataflow パイプラインを持ち込む。
// 「スケーラビリティのため」「再処理可能性のため」「疎結合のため」で押し通す。
// コードは動く形にする（MSMQ キューと Windows サービスが無いと起動しないが、
// 環境があれば動く）。

using System;
using System.Data.SqlClient;
using System.IO;
using System.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using Newtonsoft.Json;

namespace Example.App.Import
{
    // --- キュー定義 ---
    internal static class QueueNames
    {
        public const string Ingest = @".\private$\product-import.ingest.v1";
        public const string DeadLetter = @".\private$\product-import.dlq.v1";
    }

    // --- Outbox への書き込み ---
    internal sealed class OutboxWriter
    {
        private readonly string connectionString;

        public OutboxWriter(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public void Enqueue(string aggregateId, string payload)
        {
            using (var conn = new SqlConnection(connectionString))
            using (var cmd = conn.CreateCommand())
            {
                conn.Open();
                cmd.CommandText =
                    "INSERT INTO outbox_events(id, aggregate_id, queue, payload, status) " +
                    "VALUES (@id, @aggregateId, @queue, @payload, 'PENDING')";
                cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
                cmd.Parameters.AddWithValue("@aggregateId", aggregateId);
                cmd.Parameters.AddWithValue("@queue", QueueNames.Ingest);
                cmd.Parameters.AddWithValue("@payload", payload);
                cmd.ExecuteNonQuery();
            }
        }
    }

    // --- ファイル読込 → Outbox 投入 ---
    public sealed class ProductFileIngestor
    {
        private readonly OutboxWriter outbox;
        private readonly string inputDir;

        public ProductFileIngestor(OutboxWriter outbox, string inputDir)
        {
            this.outbox = outbox;
            this.inputDir = inputDir;
        }

        public void Run()
        {
            string file = Path.Combine(
                inputDir, "products-" + DateTime.Today.ToString("yyyy-MM-dd") + ".csv");
            bool first = true;
            foreach (var line in File.ReadLines(file, Encoding.UTF8))
            {
                if (first) { first = false; continue; }
                string[] cols = line.Split(',');
                var ev = new ProductEvent(cols[0], cols[1], int.Parse(cols[2]));
                outbox.Enqueue(ev.Code, JsonConvert.SerializeObject(ev));
            }
        }
    }

    public sealed class ProductEvent
    {
        public ProductEvent(string code, string name, int price)
        {
            Code = code; Name = name; Price = price;
        }
        public string Code { get; private set; }
        public string Name { get; private set; }
        public int Price { get; private set; }
    }

    // --- Outbox から MSMQ へ送信する Relay（1秒ポーリング） ---
    internal sealed class OutboxRelay
    {
        private readonly string connectionString;
        private readonly MessageQueue ingestQueue;
        private readonly Timer timer;

        public OutboxRelay(string connectionString)
        {
            this.connectionString = connectionString;
            this.ingestQueue = new MessageQueue(QueueNames.Ingest);
            this.timer = new Timer(Poll, null, 0, 1000);
        }

        private void Poll(object state)
        {
            using (var conn = new SqlConnection(connectionString))
            using (var cmd = conn.CreateCommand())
            {
                conn.Open();
                cmd.CommandText =
                    "SELECT TOP 200 id, aggregate_id, queue, payload FROM outbox_events " +
                    "WHERE status = 'PENDING' ORDER BY id";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var msg = new Message(reader.GetString(3));
                        msg.Label = reader.GetString(1);
                        ingestQueue.Send(msg, MessageQueueTransactionType.Single);

                        using (var upd = conn.CreateCommand())
                        {
                            upd.CommandText =
                                "UPDATE outbox_events SET status = 'SENT' WHERE id = @id";
                            upd.Parameters.AddWithValue("@id", reader.GetGuid(0));
                            upd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }
    }

    // --- MSMQ 受信 → TPL Dataflow パイプライン → DB へ UPSERT ---
    // （別プロセスの常駐 Windows サービスとして動かす想定）
    internal sealed class ProductImportConsumer
    {
        private readonly MessageQueue ingestQueue;
        private readonly MessageQueue deadLetterQueue;
        private readonly TransformBlock<string, ProductEvent> parseBlock;
        private readonly ActionBlock<ProductEvent> upsertBlock;

        public ProductImportConsumer(string connectionString)
        {
            this.ingestQueue = new MessageQueue(QueueNames.Ingest);
            this.deadLetterQueue = new MessageQueue(QueueNames.DeadLetter);

            this.parseBlock = new TransformBlock<string, ProductEvent>(
                json => JsonConvert.DeserializeObject<ProductEvent>(json));

            this.upsertBlock = new ActionBlock<ProductEvent>(
                ev => Upsert(connectionString, ev),
                new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 6 });

            this.parseBlock.LinkTo(
                this.upsertBlock,
                new DataflowLinkOptions { PropagateCompletion = true });
        }

        public void Start()
        {
            ingestQueue.ReceiveCompleted += OnMessage;
            ingestQueue.BeginReceive();
        }

        private void OnMessage(object sender, ReceiveCompletedEventArgs e)
        {
            var queue = (MessageQueue)sender;
            var msg = queue.EndReceive(e.AsyncResult);
            try
            {
                string body;
                using (var sr = new StreamReader(msg.BodyStream))
                {
                    body = sr.ReadToEnd();
                }
                parseBlock.Post(body);
            }
            catch (Exception)
            {
                deadLetterQueue.Send(msg, MessageQueueTransactionType.Single);
            }
            queue.BeginReceive();
        }

        private static void Upsert(string connectionString, ProductEvent ev)
        {
            using (var conn = new SqlConnection(connectionString))
            using (var cmd = conn.CreateCommand())
            {
                conn.Open();
                cmd.CommandText = @"
MERGE products AS t
USING (SELECT @code AS code, @name AS name, @price AS price) AS s
   ON t.code = s.code
WHEN MATCHED THEN UPDATE SET name = s.name, price = s.price
WHEN NOT MATCHED THEN INSERT (code, name, price) VALUES (s.code, s.name, s.price);";
                cmd.Parameters.AddWithValue("@code", ev.Code);
                cmd.Parameters.AddWithValue("@name", ev.Name);
                cmd.Parameters.AddWithValue("@price", ev.Price);
                cmd.ExecuteNonQuery();
            }
        }
    }
}

// 配置（提案）:
// - 取込処理専用の Windows サービスを新設し、WPF アプリとは別プロセスで常駐させる
// - MSMQ のトランザクションキュー 2 本（ingest / dead-letter）をサーバに新設
// - Outbox テーブル用に DB スキーマ追加
// - 監視: PerformanceCounter（キュー滞留数・処理速度）+ 監視用の別 WPF 画面
// - Dead-letter を読み出して再投入する管理ツール（別画面）
