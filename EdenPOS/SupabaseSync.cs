using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace EdenPOS
{
    internal sealed class SupabaseSync : IDisposable
    {
        private static readonly string[] Families =
        {
            "settings", "catalog", "customers", "shifts", "receipts",
            "saved_tickets", "cash_movements", "inventory_movements"
        };

        private const int RetryIntervalMilliseconds = 15000;
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("EdenPOS-Supabase-cache-v1");
        private readonly object gate = new object();
        private readonly SupabaseStore store;
        private readonly string path;
        private readonly FileStream instanceLock;
        private readonly Timer timer;
        private State current;
        private int syncing;
        private volatile bool disposed;
        private bool started;
        private string status = "Comprobando conexión con Supabase";

        public sealed class State
        {
            public string Database { get; set; }
            public Dictionary<string, string> Data { get; set; }
            public Dictionary<string, string> Published { get; set; }
            public string RemoteFingerprint { get; set; }
            public Attempt Pending { get; set; }
        }

        public sealed class Attempt
        {
            public string RemoteFingerprint { get; set; }
            public Dictionary<string, string> Published { get; set; }
        }

        internal SupabaseSync(SupabaseStore store, string directory, Func<Dictionary<string, string>> initialData)
        {
            this.store = store;
            path = Path.Combine(directory, "supabase-cache.protected");
            instanceLock = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try
            {
                if (File.Exists(path))
                {
                    var bytes = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
                    current = JsonSerializer.Deserialize<State>(bytes);
                    if (current == null || current.Database != store.DatabaseIdentity ||
                        current.Data == null || current.Published == null || current.RemoteFingerprint == null ||
                        Families.Any(family => !current.Data.ContainsKey(family) || !current.Published.ContainsKey(family)))
                        throw new InvalidDataException("La copia de Supabase pertenece a otra base o está incompleta.");
                }
                else
                {
                    using (var session = store.Open(true))
                    {
                        if (!session.HasSettings())
                        {
                            if (session.HasBusinessData())
                                throw new InvalidOperationException("La base contiene datos sin la configuración de EdenPOS.");
                            var data = initialData();
                            foreach (var family in Families) session.WriteJson(family, data[family]);
                        }
                        var remote = ReadRemote(session);
                        session.Commit();
                        SaveState(new State
                        {
                            Database = store.DatabaseIdentity, Data = remote, Published = Fingerprints(remote),
                            RemoteFingerprint = RemoteFingerprint(remote)
                        });
                        status = "Supabase al día";
                    }
                }
                timer = new Timer(_ => Synchronize(), null, Timeout.Infinite, Timeout.Infinite);
            }
            catch
            {
                instanceLock.Dispose();
                throw;
            }
        }

        internal void Start()
        {
            lock (gate)
            {
                started = true;
                timer.Change(0, RetryIntervalMilliseconds);
            }
        }

        internal string Status
        {
            get { lock (gate) return status; }
        }

        internal Operation Begin() { return new Operation(this); }

        internal sealed class Operation : IDisposable
        {
            private readonly SupabaseSync sync;
            private bool closed;
            private readonly Dictionary<string, string> data;

            internal Operation(SupabaseSync sync)
            {
                this.sync = sync;
                Monitor.Enter(sync.gate);
                try { data = new Dictionary<string, string>(sync.current.Data); }
                catch
                {
                    Monitor.Exit(sync.gate);
                    throw;
                }
            }

            internal T Read<T>(string family) { return SupabaseStore.Deserialize<T>(data[family]); }
            internal void Write<T>(string family, T value) { data[family] = SupabaseStore.Serialize(value); }

            internal void Commit()
            {
                var previous = sync.current;
                if (Families.Any(family => previous.Data[family] != data[family]))
                {
                    sync.SaveState(new State
                    {
                        Database = previous.Database, Data = data, Published = previous.Published,
                        RemoteFingerprint = previous.RemoteFingerprint, Pending = previous.Pending
                    });
                    sync.status = "Cambios guardados en este equipo, pendientes de sincronizar";
                    if (sync.started) sync.timer.Change(0, RetryIntervalMilliseconds);
                }
            }

            public void Dispose()
            {
                if (closed) return;
                closed = true;
                Monitor.Exit(sync.gate);
            }
        }

        private void Synchronize()
        {
            if (disposed || Interlocked.Exchange(ref syncing, 1) != 0) return;
            try
            {
                State snapshot;
                lock (gate) snapshot = current;
                var published = snapshot.Published;
                var baseline = snapshot.RemoteFingerprint;
                using (var session = store.Open(true))
                {
                    var remote = ReadRemote(session);
                    var remoteHash = RemoteFingerprint(remote);
                    // A previous upload may have committed before its local acknowledgment was written.
                    if (snapshot.Pending != null && snapshot.Pending.RemoteFingerprint == remoteHash)
                    {
                        baseline = remoteHash;
                        published = snapshot.Pending.Published;
                    }

                    var hashes = Fingerprints(snapshot.Data);
                    var changed = Families.Where(family => hashes[family] != published[family]).ToArray();
                    if (changed.Length == 0)
                    {
                        session.Commit();
                        lock (gate)
                        {
                            if (!ReferenceEquals(current, snapshot)) return;
                            if (snapshot.Pending != null || baseline != remoteHash)
                                SaveState(new State
                                {
                                    Database = snapshot.Database, Data = remote, Published = Fingerprints(remote),
                                    RemoteFingerprint = remoteHash
                                });
                            status = "Supabase al día";
                        }
                        return;
                    }

                    if (baseline != remoteHash)
                    {
                        lock (gate) status = "Sincronización en conflicto: hay cambios en otra caja";
                        return;
                    }

                    foreach (var family in changed) session.WriteJson(family, snapshot.Data[family]);
                    var resultingHash = RemoteFingerprint(ReadRemote(session));
                    lock (gate)
                    {
                        var latest = current;
                        SaveState(new State
                        {
                            Database = latest.Database, Data = latest.Data, Published = published,
                            RemoteFingerprint = baseline,
                            Pending = new Attempt { RemoteFingerprint = resultingHash, Published = hashes }
                        });
                    }

                    session.Commit();
                    lock (gate)
                    {
                        var latest = current;
                        SaveState(new State
                        {
                            Database = latest.Database, Data = latest.Data, Published = hashes,
                            RemoteFingerprint = resultingHash
                        });
                        status = Families.All(family => latest.Data[family] == snapshot.Data[family])
                            ? "Supabase al día" : "Cambios guardados en este equipo, pendientes de sincronizar";
                    }
                }
            }
            catch (Exception ex)
            {
                lock (gate)
                {
                    const string unavailable = "Supabase no disponible. EdenPOS volverá a intentarlo.";
                    if (status != unavailable) AppData.LogError(ex, "Sincronizar Supabase");
                    status = unavailable;
                }
            }
            finally { Interlocked.Exchange(ref syncing, 0); }
        }

        private static Dictionary<string, string> ReadRemote(SupabaseStore.Session session)
        {
            return session.ReadSnapshot(Families);
        }

        private static Dictionary<string, string> Fingerprints(Dictionary<string, string> documents)
        {
            return Families.ToDictionary(family => family, family => Hash(documents[family]));
        }

        private static string RemoteFingerprint(Dictionary<string, string> documents)
        {
            return Hash(string.Join("\n", Families.Select(family => Hash(documents[family]))));
        }

        internal static string Hash(string value)
        {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }

        private void SaveState(State next)
        {
            var content = JsonSerializer.SerializeToUtf8Bytes(next);
            var encrypted = ProtectedData.Protect(content, Entropy, DataProtectionScope.CurrentUser);
            var temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(encrypted, 0, encrypted.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
            current = next;
        }

        public void Dispose()
        {
            disposed = true;
            using (var stopped = new ManualResetEvent(false))
            {
                if (timer.Dispose(stopped)) stopped.WaitOne();
            }
            instanceLock.Dispose();
        }
    }
}
