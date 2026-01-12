using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace LZ4Sharp.Benchmarks;

internal static class JsonDatasetCorpus
{
    internal enum Group
    {
        Lt10Kb,
        Lt100Kb,
        Lt1Mb,
        Gt1Mb,
    }

    internal readonly record struct Payload(byte[] Data)
    {
        public int Size => Data.Length;
    }

    private static readonly Lazy<IReadOnlyDictionary<Group, Payload[]>> s_payloads = new(BuildCorpus);

    public static IReadOnlyList<Payload> Get(Group group) => s_payloads.Value[group];

    // At least 1000 distinct payloads total.
    private const int TargetLt10Kb = 500;
    private const int TargetLt100Kb = 400;
    private const int TargetLt1Mb = 200;
    private const int TargetGt1Mb = 50;

    private enum Schema
    {
        TwitterV2,
        GitHubEvent,
        KubernetesPod,
        GeoJson,
    }

    private static IReadOnlyDictionary<Group, Payload[]> BuildCorpus()
    {
        var lt10 = new List<Payload>(TargetLt10Kb);
        var lt100 = new List<Payload>(TargetLt100Kb);
        var lt1m = new List<Payload>(TargetLt1Mb);
        var gt1m = new List<Payload>(TargetGt1Mb);

        int seed = 0;
        while (lt10.Count < TargetLt10Kb || lt100.Count < TargetLt100Kb || lt1m.Count < TargetLt1Mb || gt1m.Count < TargetGt1Mb)
        {
            var rng = new Random(0x51C3_51A + seed);

            // Bias target sizes towards buckets we still need.
            int targetBytes = PickTargetBytes(rng, lt10.Count < TargetLt10Kb, lt100.Count < TargetLt100Kb, lt1m.Count < TargetLt1Mb, gt1m.Count < TargetGt1Mb);

            var schema = (Schema)rng.Next(0, 4);
            var bytes = Generate(schema, seed, targetBytes);
            var payload = new Payload(bytes);

            var group = Classify(payload.Size);
            switch (group)
            {
                case Group.Lt10Kb:
                    if (lt10.Count < TargetLt10Kb) lt10.Add(payload);
                    break;
                case Group.Lt100Kb:
                    if (lt100.Count < TargetLt100Kb) lt100.Add(payload);
                    break;
                case Group.Lt1Mb:
                    if (lt1m.Count < TargetLt1Mb) lt1m.Add(payload);
                    break;
                case Group.Gt1Mb:
                    if (gt1m.Count < TargetGt1Mb) gt1m.Add(payload);
                    break;
            }

            seed++;
        }

        return new Dictionary<Group, Payload[]>
        {
            [Group.Lt10Kb] = lt10.ToArray(),
            [Group.Lt100Kb] = lt100.ToArray(),
            [Group.Lt1Mb] = lt1m.ToArray(),
            [Group.Gt1Mb] = gt1m.ToArray(),
        };
    }

    private static int PickTargetBytes(Random rng, bool needLt10, bool needLt100, bool needLt1m, bool needGt1m)
    {
        // We pick targets slightly below bucket boundaries to avoid spilling.
        var candidates = new List<Func<int>>(4);
        if (needLt10) candidates.Add(() => rng.Next(512, 10 * 1024 - 256));
        if (needLt100) candidates.Add(() => rng.Next(10 * 1024, 100 * 1024 - 1024));
        if (needLt1m) candidates.Add(() => rng.Next(100 * 1024, 1024 * 1024 - 8 * 1024));
        if (needGt1m) candidates.Add(() => rng.Next(1024 * 1024 + 8 * 1024, 2 * 1024 * 1024));

        return candidates[rng.Next(candidates.Count)].Invoke();
    }

    private static Group Classify(int size)
    {
        if (size < 10 * 1024) return Group.Lt10Kb;
        if (size < 100 * 1024) return Group.Lt100Kb;
        if (size < 1024 * 1024) return Group.Lt1Mb;
        return Group.Gt1Mb;
    }

    private static byte[] Generate(Schema schema, int seed, int targetBytes)
    {
        // 64KB initial capacity avoids lots of growth for medium payloads.
        var buffer = new ArrayBufferWriter<byte>(Math.Min(targetBytes, 64 * 1024));
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false });

        switch (schema)
        {
            case Schema.TwitterV2:
                WriteTwitterV2(writer, seed, targetBytes);
                break;
            case Schema.GitHubEvent:
                WriteGitHubEvent(writer, seed, targetBytes);
                break;
            case Schema.KubernetesPod:
                WriteKubernetesPod(writer, seed, targetBytes);
                break;
            case Schema.GeoJson:
                WriteGeoJson(writer, seed, targetBytes);
                break;
        }

        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteTwitterV2(Utf8JsonWriter w, int seed, int targetBytes)
    {
        var rng = new Random(1000 + seed);
        int approxTweetSize = 700;
        int count = Math.Clamp(targetBytes / approxTweetSize, 1, 2000);

        w.WriteStartObject();
        w.WriteString("type", "twitter_v2_timeline");
        w.WriteNumber("seed", seed);
        w.WritePropertyName("data");
        w.WriteStartArray();
        for (int i = 0; i < count; i++)
        {
            w.WriteStartObject();
            w.WriteString("id", (seed * 1_000_000L + i).ToString());
            w.WriteString("created_at", DateTimeOffset.FromUnixTimeSeconds(1_600_000_000 + rng.Next(0, 31_536_000)).ToString("o"));
            w.WriteString("lang", new[] { "en", "es", "de", "fr", "pt" }[rng.Next(5)]);
            w.WriteString("text", MakeText(rng, 120, 280));
            w.WritePropertyName("entities");
            w.WriteStartObject();
            w.WritePropertyName("hashtags");
            w.WriteStartArray();
            int ht = rng.Next(0, 6);
            for (int h = 0; h < ht; h++)
            {
                w.WriteStartObject();
                w.WriteString("tag", $"tag{rng.Next(1, 2000)}");
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
            w.WriteEndObject();
        }
        w.WriteEndArray();

        WritePadding(w, targetBytes, overheadBytes: 64);
        w.WriteEndObject();
    }

    private static void WriteGitHubEvent(Utf8JsonWriter w, int seed, int targetBytes)
    {
        var rng = new Random(2000 + seed);
        int approxEventSize = 1200;
        int commits = Math.Clamp(targetBytes / approxEventSize, 1, 4000);

        w.WriteStartObject();
        w.WriteString("type", "github_push_event");
        w.WriteString("repo", $"org{seed % 100}/repo{seed % 1000}");
        w.WriteString("ref", $"refs/heads/feature/{seed % 97}");
        w.WritePropertyName("commits");
        w.WriteStartArray();
        for (int i = 0; i < commits; i++)
        {
            w.WriteStartObject();
            w.WriteString("sha", Guid.NewGuid().ToString("N"));
            w.WriteString("author", $"user{rng.Next(1, 50000)}");
            w.WriteString("message", MakeText(rng, 40, 160));
            w.WritePropertyName("files");
            w.WriteStartArray();
            int files = rng.Next(0, 8);
            for (int f = 0; f < files; f++)
                w.WriteStringValue($"src/{rng.Next(1, 50)}/file{rng.Next(1, 200)}.cs");
            w.WriteEndArray();
            w.WriteEndObject();
        }
        w.WriteEndArray();

        WritePadding(w, targetBytes, overheadBytes: 96);
        w.WriteEndObject();
    }

    private static void WriteKubernetesPod(Utf8JsonWriter w, int seed, int targetBytes)
    {
        var rng = new Random(3000 + seed);
        int approxContainerSize = 900;
        int containers = Math.Clamp(targetBytes / approxContainerSize, 1, 2500);

        w.WriteStartObject();
        w.WriteString("apiVersion", "v1");
        w.WriteString("kind", "Pod");
        w.WritePropertyName("metadata");
        w.WriteStartObject();
        w.WriteString("name", $"pod-{seed}-{rng.Next(1, 100000)}");
        w.WriteString("namespace", new[] { "default", "prod", "staging", "kube-system" }[rng.Next(4)]);
        w.WritePropertyName("labels");
        w.WriteStartObject();
        w.WriteString("app", $"app{seed % 500}");
        w.WriteString("tier", new[] { "frontend", "backend", "batch" }[rng.Next(3)]);
        w.WriteEndObject();
        w.WriteEndObject();

        w.WritePropertyName("spec");
        w.WriteStartObject();
        w.WritePropertyName("containers");
        w.WriteStartArray();
        for (int i = 0; i < containers; i++)
        {
            w.WriteStartObject();
            w.WriteString("name", $"c{i}");
            w.WriteString("image", $"registry.local/image{rng.Next(1, 500)}:{rng.Next(1, 50)}");
            w.WritePropertyName("env");
            w.WriteStartArray();
            int env = rng.Next(0, 10);
            for (int e = 0; e < env; e++)
            {
                w.WriteStartObject();
                w.WriteString("name", $"ENV_{e}");
                w.WriteString("value", $"{rng.Next():X8}");
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();

        WritePadding(w, targetBytes, overheadBytes: 128);
        w.WriteEndObject();
    }

    private static void WriteGeoJson(Utf8JsonWriter w, int seed, int targetBytes)
    {
        var rng = new Random(4000 + seed);
        int approxFeatureSize = 600;
        int features = Math.Clamp(targetBytes / approxFeatureSize, 1, 8000);

        w.WriteStartObject();
        w.WriteString("type", "FeatureCollection");
        w.WritePropertyName("features");
        w.WriteStartArray();
        for (int i = 0; i < features; i++)
        {
            w.WriteStartObject();
            w.WriteString("type", "Feature");
            w.WritePropertyName("properties");
            w.WriteStartObject();
            w.WriteNumber("id", seed * 10_000 + i);
            w.WriteString("name", $"place-{rng.Next(1, 1_000_000)}");
            w.WriteString("category", new[] { "poi", "road", "water", "landuse" }[rng.Next(4)]);
            w.WriteEndObject();

            w.WritePropertyName("geometry");
            w.WriteStartObject();
            w.WriteString("type", "Point");
            w.WritePropertyName("coordinates");
            w.WriteStartArray();
            w.WriteNumberValue(rng.NextDouble() * 360 - 180);
            w.WriteNumberValue(rng.NextDouble() * 180 - 90);
            w.WriteEndArray();
            w.WriteEndObject();
            w.WriteEndObject();
        }
        w.WriteEndArray();

        WritePadding(w, targetBytes, overheadBytes: 64);
        w.WriteEndObject();
    }

    private static void WritePadding(Utf8JsonWriter w, int targetBytes, int overheadBytes)
    {
        // NOTE: This is approximate (writer doesn't expose current size), but it's good enough to hit buckets.
        // The classifier buckets by actual serialized length.
        int pad = Math.Max(0, targetBytes - overheadBytes);
        w.WriteString("pad", new string('x', pad));
    }

    private static string MakeText(Random rng, int minLen, int maxLen)
    {
        int len = rng.Next(minLen, maxLen + 1);
        const string alphabet = "abcdefghijklmnopqrstuvwxyz     0123456789";
        var sb = new StringBuilder(len);
        for (int i = 0; i < len; i++)
            sb.Append(alphabet[rng.Next(alphabet.Length)]);
        return sb.ToString();
    }
}
