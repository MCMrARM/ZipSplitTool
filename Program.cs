using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Security.Cryptography;
using ZipSplitTool;


const string RepoDirName = "ZipSplitData";
const string RepoChunkDirName = "Chunks";
const string ToolFileExt = ".ZipSplit";
const string ToolFileTmpExt = ".ZipSplit.Tmp";
const int MinFileSize = 16 * 1024 * 1024;


static int Run(string[] args)
{
    var newRepo = new Command("newrepo", "Create a new repository")
    {
        new Argument<string>("path", "Folder where the repository will be created."),
    };
    var addFiles = new Command("add", "Adds files to a repository")
    {
        new Argument<string[]>("path", "File paths. Must be part of a repository."),
        new Option("--recursive", "Recursively scans directories"),
        new Option("--estimate-only", "Only estimate the saving"),
    };
    var verifyFiles = new Command("verify", "Verifies that the generated files are valid.")
    {
        new Argument<string[]>("path", "File paths. Must be part of a repository."),
        new Option("--recursive", "Recursively scans directories"),
    };

    newRepo.Handler = CommandHandler.Create(HandleNewRepo);
    addFiles.Handler = CommandHandler.Create(HandleAddFiles);
    verifyFiles.Handler = CommandHandler.Create(HandleVerifyFiles);

    var cmd = new RootCommand
    {
        newRepo,
        addFiles,
        verifyFiles
    };
    return cmd.Invoke(args);
}


static void HandleNewRepo(string path)
{
    var repoDir = Path.Combine(path, RepoDirName);
    var chunkDir = Path.Combine(path, RepoDirName, RepoChunkDirName);

    if (!Directory.Exists(repoDir))
        Directory.CreateDirectory(repoDir);
    if (!Directory.Exists(chunkDir))
        Directory.CreateDirectory(chunkDir);
}

static void HandleAddFiles(string[] path, bool recursive, bool estimateOnly)
{
    var allFiles = new List<string>();
    foreach (string file in path)
    {
        if (Directory.Exists(file) && recursive)
        {
            var dirFiles = Directory.GetFiles(file, "*.zip", SearchOption.AllDirectories);
            foreach (var dirFile in dirFiles)
            {
                if (new FileInfo(dirFile).Length >= MinFileSize && !File.Exists(dirFile + ToolFileExt))
                    allFiles.Add(dirFile);
            }
        }
        else
        {
            if (File.Exists(file + ToolFileExt))
            {
                Console.WriteLine("Skipping " + file + " since the " + ToolFileExt +
                                  " file for it already exists. Please delete it if you want to overwrite it.");
                continue;
            }

            if (new FileInfo(file).Length >= MinFileSize)
                allFiles.Add(file);
        }
    }

    var sharedChunkStorageEstimator = new SharedDataChunkStorageEstimator();
    var receiptFileEstimator = new BinaryReceiptFileEstimator();
    var estimatorHelper = new ZipSplitHelper(sharedChunkStorageEstimator);

    long totalSourceSize = 0;
    int i = 0;
    foreach (string file in allFiles)
    {
        var fileSize = new FileInfo(file).Length;
        Console.WriteLine("[" + (++i) + "/" + allFiles.Count + "] " + file + " " + FormatFileSizeGb(fileSize));
        
        var repoPath = FindRepository(file);
        if (repoPath == null)
        {
            Console.WriteLine("Skipping " + file + ": Failed to find repository");
            continue;
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            if (estimateOnly)
            {
                estimatorHelper.ProcessZip(file, receiptFileEstimator);
            }
            else
            {
                using var sharedChunkStorage = new DiskDataChunkStorage(Path.Combine(repoPath, RepoChunkDirName), false);
                var helper = new ZipSplitHelper(sharedChunkStorage);
                using (var receiptFileStream = File.OpenWrite(file + ToolFileTmpExt))
                {
                    using (var receiptFileBinWriter = new BinaryWriter(receiptFileStream))
                    {
                        helper.ProcessZip(file, new BinaryReceiptFileWriter(receiptFileBinWriter));
                        receiptFileEstimator.Size += receiptFileStream.Position;
                    }
                }

                File.Move(file + ToolFileTmpExt, file + ToolFileExt);
                sharedChunkStorage.Flush();
                sharedChunkStorageEstimator.TotalSize += sharedChunkStorage.AddedTotalSize;
                sharedChunkStorageEstimator.Count += sharedChunkStorage.AddedCount;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Error: " + e);
            if (File.Exists(file + ToolFileTmpExt))
                File.Delete(file + ToolFileTmpExt);
            watch.Stop();
            continue;
        }

        watch.Stop();

        totalSourceSize += fileSize;
        
        Console.WriteLine("SharedSize=" + sharedChunkStorageEstimator.TotalSize + "/" + sharedChunkStorageEstimator.Count + 
                          " ReceiptSize=" + receiptFileEstimator.Size + 
                          " Speed=" + ((fileSize / 1000) / (watch.ElapsedMilliseconds + 1)) + "MB/s" +
                          " Time=" + watch.ElapsedMilliseconds);
        Console.WriteLine("Shared=" + FormatFileSizeGb(sharedChunkStorageEstimator.TotalSize) +
                          " Receipt=" + FormatFileSizeGb(receiptFileEstimator.Size) + 
                          " Source=" + FormatFileSizeGb(totalSourceSize));
    }
}

static void HandleVerifyFiles(string[] path, bool recursive)
{
    var allFiles = new List<string>();
    foreach (string file in path)
    {
        if (Directory.Exists(file) && recursive)
        {
            var dirFiles = Directory.GetFiles(file, "*" + ToolFileExt, SearchOption.AllDirectories);
            allFiles.AddRange(dirFiles);
        }
        else
        {
            allFiles.Add(file);
        }
    }

    int ok = 0, errors = 0;
    
    int i = 0;
    foreach (string file in allFiles)
    {
        Console.WriteLine("[" + (++i) + "/" + allFiles.Count + "] " + file);
        
        var repoPath = FindRepository(file);
        if (repoPath == null)
        {
            Console.WriteLine("Skipping " + file + ": Failed to find repository");
            continue;
        }

        try
        {
            using var sharedChunkStorage = new DiskDataChunkStorage(Path.Combine(repoPath, RepoChunkDirName), true);
            var helper = new ZipSplitHelper(sharedChunkStorage);

            using var receiptFileStream = File.OpenRead(file);
            using var receiptFileBinReader = new BinaryReader(receiptFileStream);
            
            using var sha = SHA256.Create();
            using var stream = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write);
            var hash = helper.RestoreOriginalFile(new BinaryReceiptFileReader(receiptFileBinReader), stream, false);
            stream.FlushFinalBlock();
            if (sha.Hash!.SequenceEqual(hash))
            {
                ok++;
            }
            else
            {
                Console.WriteLine("Error: Verification failed.");
                Console.WriteLine("Original hash   = " + Convert.ToHexString(hash));
                Console.WriteLine("Calculated hash = " + Convert.ToHexString(sha.Hash!));
                errors++;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Error: " + e);
            errors++;
        }
    }
    
    Console.WriteLine();
    Console.WriteLine("Scanned " + (ok+errors) + " files.");
    Console.WriteLine("OK: " + ok + " files.");
    Console.WriteLine(errors > 0 ? "Errors: " + errors + " files." : "No errors.");
}

static string FormatFileSizeGb(long fileSize)
{
    return (fileSize / 1000000000.0).ToString("F1") + "GB";
}

static string? FindRepository(string path)
{
    var rpath = Path.GetDirectoryName(path);
    while (rpath != null && !Directory.Exists(Path.Combine(rpath, RepoDirName)))
        rpath = Path.GetDirectoryName(rpath);
    return rpath != null ? Path.Combine(rpath, RepoDirName) : null;
}


return Run(args);
