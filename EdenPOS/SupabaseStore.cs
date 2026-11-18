using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;

namespace EdenPOS
{
    internal sealed class SupabaseStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            IgnoreReadOnlyProperties = true
        };

        private readonly NpgsqlDataSource dataSource;

        internal string DatabaseIdentity { get; }
        internal static string Serialize<T>(T value) { return JsonSerializer.Serialize(value, JsonOptions); }
        internal static T Deserialize<T>(string value) { return JsonSerializer.Deserialize<T>(value, JsonOptions); }

        internal SupabaseStore(string value)
        {
            var settings = new NpgsqlConnectionStringBuilder(value)
            {
                SslMode = SslMode.VerifyFull,
                PersistSecurityInfo = false,
                Timeout = 8,
                CommandTimeout = 15,
                ApplicationName = "EdenPOS"
            };
            if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.Username))
                throw new InvalidOperationException("Configure el servidor y usuario de PostgreSQL para EdenPOS.");
            // Supavisor routes pooled connections by the project suffix in the login.
            var route = settings.Username.Split('.').Last();
            DatabaseIdentity = SupabaseSync.Hash(string.Join("|", settings.Host, settings.Port, settings.Database, route));
            dataSource = CreateDataSource(settings);
        }

        internal static SupabaseStore FromConfiguration()
        {
            var value = Environment.GetEnvironmentVariable("EDENPOS_POSTGRES_CONNECTION");
            if (string.IsNullOrWhiteSpace(value)) value = SupabaseSetup.ReadConnection();
            return string.IsNullOrWhiteSpace(value) ? null : new SupabaseStore(value);
        }

        internal static NpgsqlDataSource CreateDataSource(NpgsqlConnectionStringBuilder settings)
        {
            var rootPath = settings.RootCertificate;
            settings.SslMode = string.IsNullOrWhiteSpace(rootPath) ? SslMode.VerifyFull : SslMode.Require;
            var driverSettings = new NpgsqlConnectionStringBuilder(settings.ConnectionString);
            if (!string.IsNullOrWhiteSpace(rootPath)) driverSettings.Remove("Root Certificate");
            var builder = new NpgsqlDataSourceBuilder(driverSettings.ConnectionString);
            if (!string.IsNullOrWhiteSpace(rootPath))
            {
                // .NET Framework cannot assign a custom trust store to SslStream.
                // Verify the hostname and build the chain to the supplied root explicitly.
                byte[] rootBytes;
                using (var certificate = new X509Certificate2(rootPath)) rootBytes = certificate.RawData;
                builder.UseUserCertificateValidationCallback((sender, certificate, providedChain, errors) =>
                {
                    if (certificate == null || providedChain == null ||
                        (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != SslPolicyErrors.None)
                        return false;

                    using (var root = new X509Certificate2(rootBytes))
                    using (var leaf = new X509Certificate2(certificate))
                    using (var chain = new X509Chain())
                    {
                        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                        chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
                        chain.ChainPolicy.ExtraStore.Add(root);
                        foreach (X509ChainElement element in providedChain.ChainElements)
                            chain.ChainPolicy.ExtraStore.Add(element.Certificate);

                        if (!chain.Build(leaf)) return false;
                        var anchor = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
                        return anchor.RawData.SequenceEqual(rootBytes);
                    }
                });
            }
            return builder.Build();
        }

        internal async Task ListenForChanges(Action refresh, CancellationToken token)
        {
            using (var connection = await dataSource.OpenConnectionAsync(token))
            using (var listen = new NpgsqlCommand("listen edenpos_cambios", connection))
            {
                connection.Notification += (sender, notification) => refresh();
                await listen.ExecuteNonQueryAsync(token);
                refresh();
                while (!token.IsCancellationRequested)
                    connection.Wait(1000);
            }
        }

        internal Session Open(bool write) { return new Session(dataSource, write); }

        internal sealed class Session : IDisposable
        {
            private readonly NpgsqlConnection connection;
            private readonly NpgsqlTransaction transaction;

            internal Session(NpgsqlDataSource dataSource, bool write)
            {
                connection = dataSource.CreateConnection();
                try
                {
                    connection.Open();
                    transaction = connection.BeginTransaction(write ? IsolationLevel.ReadCommitted : IsolationLevel.RepeatableRead);
                    if (write)
                    {
                        // Serialize read/modify/write operations across EdenPOS terminals.
                        using (var command = Command("select pg_catalog.pg_advisory_xact_lock(1346720561, 1)"))
                            command.ExecuteNonQuery();
                    }
                }
                catch
                {
                    connection.Dispose();
                    throw;
                }
            }

            internal bool HasSettings()
            {
                using (var command = Command("select exists(select 1 from edenpos.settings)"))
                    return (bool)command.ExecuteScalar();
            }

            internal bool HasBusinessData()
            {
                var queries = Tables.Where(x => x.Key != "settings").Select(x => "exists(select 1 from edenpos." + x.Value.Name + ")");
                using (var command = Command("select " + string.Join(" or ", queries) + " or exists(select 1 from edenpos.app_users)"))
                    return (bool)command.ExecuteScalar();
            }

            internal Dictionary<string, string> ReadSnapshot(IEnumerable<string> families)
            {
                var names = families.ToArray();
                var queries = names.Select(name => "(" + Tables[name].ReadSql + ")");
                var result = new Dictionary<string, string>(names.Length);
                using (var command = Command("select " + string.Join(", ", queries)))
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("No se pudo leer el estado de EdenPOS.");
                    for (var i = 0; i < names.Length; i++)
                    {
                        if (reader.IsDBNull(i)) throw new InvalidOperationException("La base de EdenPOS todavía no está inicializada.");
                        result.Add(names[i], reader.GetString(i));
                    }
                }
                return result;
            }

            internal void WriteDocuments(IEnumerable<KeyValuePair<string, string>> documents)
            {
                using (var batch = new NpgsqlBatch(connection, transaction))
                {
                    foreach (var document in documents)
                    {
                        var json = document.Key == "settings" ? "[" + document.Value + "]" : document.Value;
                        foreach (var sql in Tables[document.Key].WriteSql)
                        {
                            var command = new NpgsqlBatchCommand(sql);
                            command.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, json);
                            batch.BatchCommands.Add(command);
                        }
                    }
                    batch.ExecuteNonQuery();
                }
            }

            internal void Commit() { transaction.Commit(); }

            private NpgsqlCommand Command(string sql)
            {
                return new NpgsqlCommand(sql, connection, transaction);
            }

            public void Dispose()
            {
                try { transaction.Dispose(); }
                finally { connection.Dispose(); }
            }
        }

        private sealed class Table
        {
            internal readonly string Name;
            internal readonly string ReadSql;
            internal readonly List<string> WriteSql = new List<string>();

            internal Table(string name, string columns, string key, string order,
                string extraRead = "", string source = "select value as item, ordinality as position from jsonb_array_elements(@payload) with ordinality")
            {
                Name = name;
                var keys = key.Split(' ');
                var fields = columns.Split(' ');
                var assignments = string.Join(", ", fields.Except(keys).Select(f => f + " = excluded." + f));
                var selector = "to_jsonb(t)" + extraRead;
                ReadSql = name == "settings"
                    ? "select (" + selector + ")::text from edenpos.settings t"
                    : "select coalesce(jsonb_agg(" + selector + " order by " + order + "), '[]'::jsonb)::text from edenpos." + name + " t";
                var rows = "with incoming as (" + source + "), records as (select r.* from incoming i cross join lateral " +
                    "jsonb_populate_record(null::edenpos." + name + ", i.item) r) ";
                var columnList = string.Join(", ", fields);
                WriteSql.Add(rows + "insert into edenpos." + name + " as target (" + columnList + ") " +
                    "select " + columnList + " from records where true on conflict (" + string.Join(", ", keys) + ") " +
                    (assignments.Length == 0 ? "do nothing" : "do update set " + assignments +
                        " where (" + string.Join(", ", fields.Select(f => "target." + f)) + ") is distinct from (" +
                        string.Join(", ", fields.Select(f => "excluded." + f)) + ")"));
                WriteSql.Add(rows + "delete from edenpos." + name + " t where not exists (select 1 from records r where " +
                    string.Join(" and ", keys.Select(f => "t." + f + " = r." + f)) + ")");
            }

            internal Table WithChildren(params Table[] children)
            {
                // Parent insert first, stale child rows removed before stale parents.
                WriteSql.InsertRange(1, children.SelectMany(x => x.WriteSql));
                return this;
            }
        }

        private static string Children(string array, string additions)
        {
            return "select child.value || " + additions + " as item from jsonb_array_elements(@payload) p " +
                "cross join lateral jsonb_array_elements(case when jsonb_typeof(p.value->'" + array +
                "') = 'array' then p.value->'" + array + "' else '[]'::jsonb end) with ordinality child";
        }

        private static string Collection(string alias, string source, string condition, string order, string value = "to_jsonb(c)")
        {
            return " || jsonb_build_object('" + alias + "', (select coalesce(jsonb_agg(" + value + " order by " + order +
                "), '[]'::jsonb) from edenpos." + source + " c where " + condition + "))";
        }

        private static readonly string LineColumns = "line_number product_id option_id name comment unit_price quantity";

        private static readonly Dictionary<string, Table> Tables = new Dictionary<string, Table>
        {
            ["settings"] = new Table("settings",
                "singleton store_name employee_name currency_symbol next_receipt_number next_shift_number dni_lookup_enabled inventory_schema_version catalog_schema_version security_schema_version",
                "singleton", "t.singleton",
                Collection("users", "app_users", "true", "c.created_at, c.id"),
                "select value || jsonb_build_object('singleton', true) as item from jsonb_array_elements(@payload)")
                .WithChildren(new Table("app_users", "id name role pin_salt pin_hash active created_at updated_at",
                    "id", "t.created_at, t.id", source: Children("users", "'{}'::jsonb"))),
            ["catalog"] = new Table("products", "id name category price image_file color_hex active stock initial_stock track_inventory display_order",
                "id", "t.display_order, t.id",
                Collection("options", "product_options", "c.product_id = t.id", "c.display_order, c.id"),
                "select value || jsonb_build_object('display_order', ordinality) as item from jsonb_array_elements(@payload) with ordinality")
                .WithChildren(new Table("product_options", "product_id id name price stock initial_stock track_inventory display_order",
                    "product_id id", "t.display_order, t.id", source: Children("options",
                        "jsonb_build_object('product_id', p.value->'id', 'display_order', child.ordinality)"))),
            ["receipts"] = new Table("receipts",
                "id number shift_id created_at employee customer_name customer_dni payment_method amount_received cash_amount yape_amount total status refunded_at refund_reason",
                "id", "t.number",
                Collection("lines", "receipt_lines", "c.receipt_id = t.id", "c.line_number"),
                "select value || jsonb_build_object('customer_dni', nullif(value->>'customer_dni', '')) as item from jsonb_array_elements(@payload)")
                .WithChildren(new Table("receipt_lines", "receipt_id " + LineColumns,
                    "receipt_id line_number", "t.line_number", source: Children("lines",
                        "jsonb_build_object('receipt_id', p.value->'id', 'line_number', child.ordinality)"))),
            ["saved_tickets"] = new Table("saved_tickets", "id name customer_name customer_dni adult_count child_count created_at updated_at",
                "id", "t.created_at, t.id",
                Collection("lines", "saved_ticket_lines", "c.ticket_id = t.id", "c.line_number") +
                Collection("locker_numbers", "saved_ticket_lockers", "c.ticket_id = t.id", "c.locker_number", "c.locker_number"),
                "select value || jsonb_build_object('customer_dni', nullif(value->>'customer_dni', '')) as item from jsonb_array_elements(@payload)")
                .WithChildren(
                    new Table("saved_ticket_lines", "ticket_id " + LineColumns,
                        "ticket_id line_number", "t.line_number", source: Children("lines",
                            "jsonb_build_object('ticket_id', p.value->'id', 'line_number', child.ordinality)")),
                    new Table("saved_ticket_lockers", "locker_number ticket_id", "locker_number", "t.locker_number",
                        source: @"select jsonb_build_object('ticket_id', p.value->'id', 'locker_number', l.value) as item
from jsonb_array_elements(@payload) p
cross join lateral jsonb_array_elements(case
    when jsonb_typeof(p.value->'locker_numbers') = 'array' and p.value->'locker_numbers' <> '[]'::jsonb
        then p.value->'locker_numbers'
    when coalesce((p.value->>'locker_number')::int, 0) > 0 then jsonb_build_array(p.value->'locker_number')
    else '[]'::jsonb end) l")),
            ["customers"] = new Table("customers", "dni full_name source created_at updated_at", "dni", "t.created_at, t.dni"),
            ["shifts"] = new Table("shifts", "id number employee opened_at closed_at opening_amount closing_amount expected_amount", "id", "t.number"),
            ["cash_movements"] = new Table("cash_movements", "id shift_id created_at employee type amount comment", "id", "t.created_at, t.id"),
            ["inventory_movements"] = new Table("inventory_movements", "id product_id product_name created_at type quantity previous_stock new_stock note reference_id", "id", "t.created_at, t.id")
        };
    }
}
