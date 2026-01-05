using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace LZ4Sharp.Benchmarks
{
    /// <summary>
    /// Benchmarks comparing LZ4Sharp against K4os.Compression.LZ4 using standard compression corpus data
    /// Includes Calgary Corpus, Canterbury Corpus, and JSON bench data patterns
    /// </summary>
    [MemoryDiagnoser]
    [Config(typeof(Config))]
    public class CorpusBenchmarks
    {
        private class Config : ManualConfig
        {
            public Config()
            {
                AddJob(Job.Default.WithId(".NET 10"));
            }
        }

        private byte[] _data = null!;
        private byte[] _compressedLZ4Sharp = null!;
        private byte[] _compressedK4os = null!;
        private int _compressedSizeLZ4Sharp;
        private int _compressedSizeK4os;

        [ParamsSource(nameof(CorpusFilesSource))]
        public CorpusFile File { get; set; } = null!;

        public IEnumerable<CorpusFile> CorpusFilesSource()
        {
            // Calgary Corpus representative files
            yield return new CorpusFile("bib", CorpusType.Calgary, GenerateBibliographyData);
            yield return new CorpusFile("book1", CorpusType.Calgary, GenerateBookData);
            yield return new CorpusFile("paper1", CorpusType.Calgary, GeneratePaperData);
            yield return new CorpusFile("progc", CorpusType.Calgary, GenerateCProgramData);
            yield return new CorpusFile("progl", CorpusType.Calgary, GenerateLispProgramData);

            // Canterbury Corpus representative files
            yield return new CorpusFile("alice29.txt", CorpusType.Canterbury, GenerateAliceData);
            yield return new CorpusFile("asyoulik.txt", CorpusType.Canterbury, GenerateShakespeareData);
            yield return new CorpusFile("lcet10.txt", CorpusType.Canterbury, GenerateLiteratureData);
            yield return new CorpusFile("plrabn12.txt", CorpusType.Canterbury, GeneratePoetryData);

            // JSON bench data
            yield return new CorpusFile("json-simple", CorpusType.JSONBench, GenerateSimpleJsonData);
            yield return new CorpusFile("json-complex", CorpusType.JSONBench, GenerateComplexJsonData);
            yield return new CorpusFile("json-array", CorpusType.JSONBench, GenerateJsonArrayData);
        }

        [GlobalSetup]
        public void Setup()
        {
            _data = File.Generator();

            // Setup LZ4Sharp
            int maxCompressedSize = LZ4Codec.CompressBound(_data.Length);
            _compressedLZ4Sharp = new byte[maxCompressedSize];
            _compressedSizeLZ4Sharp = LZ4Codec.CompressDefault(_data, _compressedLZ4Sharp, _data.Length, maxCompressedSize);

            // Setup K4os
            _compressedK4os = new byte[K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(_data.Length)];
            _compressedSizeK4os = K4os.Compression.LZ4.LZ4Codec.Encode(
                _data, 0, _data.Length,
                _compressedK4os, 0, _compressedK4os.Length,
                K4os.Compression.LZ4.LZ4Level.L00_FAST);
        }

        #region Compression Benchmarks

        [Benchmark(Description = "LZ4Sharp - Compress")]
        public int CompressLZ4Sharp()
        {
            var dest = new byte[_compressedLZ4Sharp.Length];
            return LZ4Codec.CompressDefault(_data, dest, _data.Length, dest.Length);
        }

        [Benchmark(Description = "K4os.LZ4 - Compress", Baseline = true)]
        public int CompressK4os()
        {
            var dest = new byte[_compressedK4os.Length];
            return K4os.Compression.LZ4.LZ4Codec.Encode(
                _data, 0, _data.Length,
                dest, 0, dest.Length,
                K4os.Compression.LZ4.LZ4Level.L00_FAST);
        }

        #endregion

        #region Decompression Benchmarks

        [Benchmark(Description = "LZ4Sharp - Decompress")]
        public int DecompressLZ4Sharp()
        {
            var dest = new byte[_data.Length];
            return LZ4Codec.DecompressSafe(_compressedLZ4Sharp, dest, _compressedSizeLZ4Sharp, _data.Length);
        }

        [Benchmark(Description = "K4os.LZ4 - Decompress")]
        public int DecompressK4os()
        {
            var dest = new byte[_data.Length];
            return K4os.Compression.LZ4.LZ4Codec.Decode(
                _compressedK4os, 0, _compressedSizeK4os,
                dest, 0, _data.Length);
        }

        #endregion

        #region Calgary Corpus Data Generators

        /// <summary>
        /// Generate bibliography data (similar to bib from Calgary Corpus)
        /// Format: BibTeX-like entries with repeated structure
        /// </summary>
        private static byte[] GenerateBibliographyData()
        {
            var sb = new StringBuilder();
            var random = new Random(42);
            
            for (int i = 0; i < 500; i++)
            {
                sb.AppendLine($"@article{{ref{i:D4},");
                sb.AppendLine($"  author = \"Author{random.Next(100)} and Author{random.Next(100)}\",");
                sb.AppendLine($"  title = \"On the Theory of Compression and Data Structures Volume {random.Next(50)}\",");
                sb.AppendLine($"  journal = \"Journal of Computer Science\",");
                sb.AppendLine($"  year = {1990 + random.Next(30)},");
                sb.AppendLine($"  volume = {random.Next(100)},");
                sb.AppendLine($"  pages = \"{random.Next(1, 500)}--{random.Next(501, 1000)}\"");
                sb.AppendLine("}");
                sb.AppendLine();
            }
            
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        /// <summary>
        /// Generate book-like text data (similar to book1/book2 from Calgary Corpus)
        /// </summary>
        private static byte[] GenerateBookData()
        {
            var sb = new StringBuilder();
            var chapters = new[] { "Introduction", "Background", "Methodology", "Results", "Discussion", "Conclusion" };
            var words = new[] { "the", "compression", "algorithm", "data", "structure", "performance", "analysis", 
                "implementation", "optimization", "benchmark", "memory", "efficiency", "speed", "ratio" };
            var random = new Random(42);
            
            for (int chapter = 1; chapter <= 20; chapter++)
            {
                sb.AppendLine($"\n\nChapter {chapter}: {chapters[random.Next(chapters.Length)]}\n");
                
                for (int para = 0; para < 50; para++)
                {
                    for (int sent = 0; sent < 5; sent++)
                    {
                        for (int word = 0; word < 10 + random.Next(10); word++)
                        {
                            sb.Append(words[random.Next(words.Length)]);
                            sb.Append(' ');
                        }
                        sb.Append(". ");
                    }
                    sb.AppendLine();
                }
            }
            
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        /// <summary>
        /// Generate academic paper data (similar to paper1-6 from Calgary Corpus)
        /// </summary>
        private static byte[] GeneratePaperData()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("ABSTRACT");
            sb.AppendLine("========\n");
            sb.AppendLine("This paper presents a comprehensive analysis of lossless data compression algorithms.");
            sb.AppendLine("We evaluate various compression techniques including LZ77, LZ78, and their derivatives.");
            sb.AppendLine("Our experimental results show significant improvements in compression ratios.\n");
            
            sb.AppendLine("1. INTRODUCTION");
            sb.AppendLine("===============\n");
            
            var sections = new[] { "Related Work", "Methodology", "Implementation Details", 
                "Experimental Setup", "Results and Analysis", "Discussion", "Conclusion" };
            
            var random = new Random(42);
            for (int i = 0; i < sections.Length; i++)
            {
                sb.AppendLine($"\n{i + 2}. {sections[i].ToUpper()}");
                sb.AppendLine(new string('=', sections[i].Length + 3));
                sb.AppendLine();
                
                for (int para = 0; para < 20; para++)
                {
                    var sentence = $"The compression algorithm demonstrates significant performance improvements over baseline methods. ";
                    sentence += $"Figure {random.Next(1, 10)} shows the relationship between compression ratio and execution time. ";
                    sentence += $"As shown in Table {random.Next(1, 5)}, our approach achieves a {random.Next(10, 40)}% improvement. ";
                    sb.AppendLine(sentence);
                }
            }
            
            sb.AppendLine("\nREFERENCES");
            sb.AppendLine("==========\n");
            for (int i = 1; i <= 30; i++)
            {
                sb.AppendLine($"[{i}] Author et al., \"Title of Paper {i}\", Conference {i % 5}, pp. {i * 10}-{i * 10 + 15}, {1990 + i}.");
            }
            
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        /// <summary>
        /// Generate C program source code (similar to progc from Calgary Corpus)
        /// </summary>
        private static byte[] GenerateCProgramData()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("#include <stdio.h>");
            sb.AppendLine("#include <stdlib.h>");
            sb.AppendLine("#include <string.h>");
            sb.AppendLine();
            
            for (int i = 0; i < 100; i++)
            {
                sb.AppendLine($"int function_{i}(int argc, char *argv[]) {{");
                sb.AppendLine("    int result = 0;");
                sb.AppendLine("    char buffer[1024];");
                sb.AppendLine($"    for (int i = 0; i < {i + 10}; i++) {{");
                sb.AppendLine("        result += i * 2;");
                sb.AppendLine("        if (result > 1000) {");
                sb.AppendLine("            printf(\"Result exceeded threshold\\n\");");
                sb.AppendLine("            break;");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine("    return result;");
                sb.AppendLine("}");
                sb.AppendLine();
            }
            
            sb.AppendLine("int main(int argc, char *argv[]) {");
            sb.AppendLine("    int total = 0;");
            for (int i = 0; i < 100; i++)
            {
                sb.AppendLine($"    total += function_{i}(argc, argv);");
            }
            sb.AppendLine("    printf(\"Total: %d\\n\", total);");
            sb.AppendLine("    return 0;");
            sb.AppendLine("}");
            
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        /// <summary>
        /// Generate Lisp program source code (similar to progl from Calgary Corpus)
        /// </summary>
        private static byte[] GenerateLispProgramData()
        {
            var sb = new StringBuilder();
            
            for (int i = 0; i < 200; i++)
            {
                sb.AppendLine($"(defun function-{i} (x y z)");
                sb.AppendLine($"  (let ((result 0))");
                sb.AppendLine($"    (dotimes (i {i + 10})");
                sb.AppendLine("      (setq result (+ result (* i 2))))");
                sb.AppendLine("    (if (> result 1000)");
                sb.AppendLine("        (format t \"Result exceeded threshold~%\")");
                sb.AppendLine("        (format t \"Result: ~a~%\" result))");
                sb.AppendLine("    result))");
                sb.AppendLine();
            }
            
            sb.AppendLine("(defun main ()");
            sb.AppendLine("  (let ((total 0))");
            for (int i = 0; i < 200; i++)
            {
                sb.AppendLine($"    (setq total (+ total (function-{i} 1 2 3)))");
            }
            sb.AppendLine("    (format t \"Total: ~a~%\" total)))");
            
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        #endregion

        #region Canterbury Corpus Data Generators

        /// <summary>
        /// Generate Alice in Wonderland-like text (similar to alice29.txt from Canterbury Corpus)
        /// </summary>
        private static byte[] GenerateAliceData()
        {
            var sb = new StringBuilder();
            var random = new Random(42);
            
            sb.AppendLine("ALICE'S ADVENTURES IN WONDERLAND");
            sb.AppendLine();
            sb.AppendLine("CHAPTER I");
            sb.AppendLine("Down the Rabbit-Hole");
            sb.AppendLine();
            
            var sentences = new[]
            {
                "Alice was beginning to get very tired of sitting by her sister on the bank.",
                "The rabbit-hole went straight on like a tunnel for some way.",
                "Either the well was very deep, or she fell very slowly.",
                "Down, down, down. Would the fall never come to an end?",
                "There were doors all round the hall, but they were all locked.",
                "Alice tried to fancy what the flame of a candle looks like after it is blown out.",
                "Suddenly she came upon a little three-legged table, all made of solid glass.",
                "The pool had by this time become quite crowded with the birds and animals.",
            };
            
            for (int chapter = 1; chapter <= 12; chapter++)
            {
                sb.AppendLine($"\n\nCHAPTER {chapter}\n");
                
                for (int para = 0; para < 30; para++)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        sb.Append(sentences[random.Next(sentences.Length)]);
                        sb.Append(" ");
                    }
                    sb.AppendLine();
                    sb.AppendLine();
                }
            }
            
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        /// <summary>
        /// Generate Shakespeare-like text (similar to asyoulik.txt from Canterbury Corpus)
        /// </summary>
        private static byte[] GenerateShakespeareData()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("AS YOU LIKE IT");
            sb.AppendLine();
            sb.AppendLine("by William Shakespeare");
            sb.AppendLine();
            
            var characters = new[] { "ROSALIND", "ORLANDO", "CELIA", "TOUCHSTONE", "JACQUES", "DUKE SENIOR" };
            var lines = new[]
            {
                "All the world's a stage, and all the men and women merely players.",
                "They have their exits and their entrances, and one man in his time plays many parts.",
                "Sweet are the uses of adversity which, like the toad, ugly and venomous.",
                "The fool doth think he is wise, but the wise man knows himself to be a fool.",
                "Men have died from time to time, and worms have eaten them, but not for love.",
            };
            
            var random = new Random(42);
            
            for (int act = 1; act <= 5; act++)
            {
                sb.AppendLine($"\n\nACT {act}\n");
                
                for (int scene = 1; scene <= 7; scene++)
                {
                    sb.AppendLine($"\n\nSCENE {scene}\n");
                    
                    for (int exchange = 0; exchange < 20; exchange++)
                    {
                        var character = characters[random.Next(characters.Length)];
                        sb.AppendLine($"{character}:");
                        sb.AppendLine($"  {lines[random.Next(lines.Length)]}");
                        sb.AppendLine();
                    }
                }
            }
            
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        /// <summary>
        /// Generate literature text (similar to lcet10.txt from Canterbury Corpus)
        /// </summary>
        private static byte[] GenerateLiteratureData()
        {
            var sb = new StringBuilder();
            var random = new Random(42);
            
            var paragraphs = new[]
            {
                "The Project Gutenberg eBook of a collection of literature.",
                "This eBook is for the use of anyone anywhere at no cost.",
                "You may copy it, give it away or re-use it under the terms of the Project Gutenberg License.",
            };
            
            for (int i = 0; i < 1000; i++)
            {
                sb.AppendLine(paragraphs[random.Next(paragraphs.Length)]);
                sb.AppendLine("The quick brown fox jumps over the lazy dog. " +
                    "The five boxing wizards jump quickly. " +
                    "How vexingly quick daft zebras jump!");
                sb.AppendLine();
            }
            
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        /// <summary>
        /// Generate poetry text (similar to plrabn12.txt from Canterbury Corpus - Paradise Lost)
        /// </summary>
        private static byte[] GeneratePoetryData()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("Paradise Lost");
            sb.AppendLine("by John Milton");
            sb.AppendLine();
            
            var lines = new[]
            {
                "Of Man's first disobedience, and the fruit",
                "Of that forbidden tree whose mortal taste",
                "Brought death into the World, and all our woe,",
                "With loss of Eden, till one greater Man",
                "Restore us, and regain the blissful seat,",
                "Sing, Heavenly Muse, that, on the secret top",
                "Of Oreb, or of Sinai, didst inspire",
            };
            
            for (int book = 1; book <= 12; book++)
            {
                sb.AppendLine($"\n\nBOOK {book}\n");
                
                for (int i = 0; i < 500; i++)
                {
                    sb.AppendLine(lines[i % lines.Length]);
                }
            }
            
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        #endregion

        #region JSON Bench Data Generators

        /// <summary>
        /// Generate simple JSON data for benchmarking
        /// </summary>
        private static byte[] GenerateSimpleJsonData()
        {
            var sb = new StringBuilder();
            sb.Append('[');
            
            for (int i = 0; i < 1000; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(JsonSerializer.Serialize(new
                {
                    id = i,
                    name = $"User{i}",
                    email = $"user{i}@example.com",
                    active = i % 2 == 0
                }));
            }
            
            sb.Append(']');
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        /// <summary>
        /// Generate complex nested JSON data for benchmarking
        /// </summary>
        private static byte[] GenerateComplexJsonData()
        {
            var sb = new StringBuilder();
            sb.Append('[');
            
            for (int i = 0; i < 500; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(JsonSerializer.Serialize(new
                {
                    id = i,
                    user = new
                    {
                        name = $"User{i}",
                        email = $"user{i}@example.com",
                        profile = new
                        {
                            age = 20 + (i % 50),
                            country = new[] { "USA", "UK", "Canada", "Australia" }[i % 4],
                            preferences = new
                            {
                                notifications = true,
                                theme = i % 2 == 0 ? "dark" : "light",
                                language = "en"
                            }
                        }
                    },
                    posts = Enumerable.Range(0, 5).Select(j => new
                    {
                        id = i * 100 + j,
                        title = $"Post {j} by User {i}",
                        content = "Lorem ipsum dolor sit amet, consectetur adipiscing elit.",
                        tags = new[] { "technology", "programming", "compression" },
                        timestamp = DateTime.UtcNow.AddDays(-i).ToString("o")
                    }).ToArray()
                }));
            }
            
            sb.Append(']');
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        /// <summary>
        /// Generate large JSON array data for benchmarking
        /// </summary>
        private static byte[] GenerateJsonArrayData()
        {
            var data = new
            {
                metadata = new
                {
                    version = "1.0",
                    generated = DateTime.UtcNow.ToString("o"),
                    recordCount = 2000
                },
                records = Enumerable.Range(0, 2000).Select(i => new
                {
                    id = i,
                    timestamp = DateTime.UtcNow.AddSeconds(-i).ToString("o"),
                    value = 100.0 + (i % 100),
                    status = new[] { "active", "inactive", "pending" }[i % 3],
                    metadata = new Dictionary<string, object>
                    {
                        ["key1"] = $"value{i}",
                        ["key2"] = i % 10,
                        ["key3"] = i % 2 == 0
                    }
                }).ToArray()
            };
            
            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data));
        }

        #endregion
    }

    public class CorpusFile
    {
        public string Name { get; }
        public CorpusType Type { get; }
        public Func<byte[]> Generator { get; }

        public CorpusFile(string name, CorpusType type, Func<byte[]> generator)
        {
            Name = name;
            Type = type;
            Generator = generator;
        }

        public override string ToString() => $"{Type}/{Name}";
    }

    public enum CorpusType
    {
        Calgary,
        Canterbury,
        JSONBench
    }
}
