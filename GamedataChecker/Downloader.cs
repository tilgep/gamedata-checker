using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static GamedataChecker.CS2FGamedata;
using static SteamKit2.GC.Dota.Internal.CMsgDOTALeague;
using static SteamKit2.GC.TF2.Internal.CMsgGCMsgMasterSetDirectory;
using static SteamKit2.Internal.CSiteManagerClient_TrackedPayments_Notification;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace GamedataChecker;

class DownloaderData
{
    public ulong latestWindowsManifest = ContentDownloader.INVALID_MANIFEST_ID;
    public ulong latestLinuxManifest = ContentDownloader.INVALID_MANIFEST_ID;

    public bool hasLatestWindowsDownloaded = false;
    public bool hasLatestLinuxDownloaded = false;

    public Commit LatestCommit = null;

    public DateTime latestUpdate = DateTime.UnixEpoch;

    public string WebhookUrl = "";
    public string GithubUser = "";
    public string GithubPass = "";
    public string CS2FixesRepo = "";
}

static class Downloader
{
    const uint CS2_APPID = 730;
    const string DEFAULT_BRANCH = "public";
    public const uint WINDOWS_BIN_DEPOT = 2347771;
    public const uint LINUX_BIN_DEPOT = 2347773;

    const string DATA_FILENAME = "data/GamedataCheckerData.json";
    public static DownloaderData Data = new();
    
    const string RegexPrefix = "regex:";
    const string WINDOWS_FILELIST = "data/windows.txt";
    const string LINUX_FILELIST = "data/linux.txt";

    public static bool LoadData()
    {
        Console.WriteLine("Loading data from file...");
        if (!Directory.Exists("data"))
        {
            Directory.CreateDirectory("data");
        }

        ContentDownloader.Config.InstallDirectory = "data/depots";
        ContentDownloader.Config.MaxDownloads = 8;
        ContentDownloader.Config.VerifyAll = true;

        if (!File.Exists(DATA_FILENAME))
        {
            Console.WriteLine($"{DATA_FILENAME} does not exist!");
            Console.WriteLine(" Please go fill in the following fields for this to work as intended");
            Console.WriteLine(" {0}\n {1}\n {2}", nameof(DownloaderData.WebhookUrl), nameof(DownloaderData.GithubUser), nameof(DownloaderData.GithubPass));
            FileStream f = File.Create(DATA_FILENAME);
            f.Close();
            return false;
        }

        try
        {
            string jsonString = File.ReadAllText(DATA_FILENAME);
            using JsonDocument doc = JsonDocument.Parse(jsonString);

            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("Config", out var configelem))
            {
                if (!ReadConfigSection(configelem))
                {
                    Console.WriteLine("There was an error reading Config settings");
                    Console.WriteLine("Please check the following options are set:");
                    Console.WriteLine(" {0}\n {1}\n {2}\n {3}", 
                        nameof(DownloaderData.WebhookUrl), 
                        nameof(DownloaderData.GithubUser), 
                        nameof(DownloaderData.GithubPass),
                        nameof(DownloaderData.CS2FixesRepo));
                    return false;
                }
            }
            else
            {
                Console.WriteLine("No config detected");
            }

            if (root.TryGetProperty("Data", out var dataelem))
            {
                ReadDataSection(dataelem);
            }
            else
            {
                Console.WriteLine("No data detected");
            }

            Debug.WriteLine("Reading DONE");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

        return true;
    }

    public static bool ReadConfigSection(JsonElement configElem)
    {
        bool ok = true;
        Debug.WriteLine("Reading webhook");
        if (configElem.TryGetProperty(nameof(DownloaderData.WebhookUrl), out JsonElement webhookelem))
        {
            string? webhook = webhookelem.GetString();
            if (webhook != null)
            {
                Data.WebhookUrl = webhook;
            }
            else ok = false;
        }
        else ok = false;

        Debug.WriteLine("Reading github username");
        if (configElem.TryGetProperty(nameof(DownloaderData.GithubUser), out JsonElement GithubUserelem))
        {
            string? username = GithubUserelem.GetString();
            if (username != null)
            {
                Data.GithubUser = username;
            }
            else ok = false;
        }
        else ok = false;

        Debug.WriteLine("Reading github password");
        if (configElem.TryGetProperty(nameof(DownloaderData.GithubPass), out JsonElement GithubPasselem))
        {
            string? passw = GithubPasselem.GetString();
            if (passw != null)
            {
                Data.GithubPass = passw;
            }
            else ok = false;
        }
        else ok = false;

        Debug.WriteLine("Reading CS2Fixes Repo path");
        if (configElem.TryGetProperty(nameof(DownloaderData.CS2FixesRepo), out JsonElement cs2frepoelem))
        {
            string? cs2frepo = cs2frepoelem.GetString();
            if (cs2frepo != null)
            {
                Data.CS2FixesRepo = cs2frepo;
            }
            else ok = false;
        }
        else ok = false;

        return ok;
    }

    public static void ReadDataSection(JsonElement dataElem)
    {
        Debug.WriteLine("Reading Windows ManifestId");
        if (dataElem.TryGetProperty(nameof(DownloaderData.latestWindowsManifest), out JsonElement windowsElem))
        {
            string? windowsManifest = windowsElem.GetString();
            if (windowsManifest != null)
            {
                if (UInt64.TryParse(windowsManifest, out ulong temp))
                {
                    Data.latestWindowsManifest = temp;
                }
            }
        }

        Debug.WriteLine("Reading Linux ManifestId");
        if (dataElem.TryGetProperty(nameof(DownloaderData.latestLinuxManifest), out JsonElement linuxElem))
        {
            string? linuxManifest = linuxElem.GetString();
            if (linuxManifest != null)
            {
                if (UInt64.TryParse(linuxManifest, out ulong temp))
                {
                    Data.latestLinuxManifest = temp;
                }
            }
        }

        Debug.WriteLine("Reading last update time");
        if (dataElem.TryGetProperty(nameof(DownloaderData.latestUpdate), out JsonElement latestUpdateelem))
        {
            string? lastUpdate = latestUpdateelem.GetString();
            if (lastUpdate != null)
            {
                Data.latestUpdate = DateTime.Parse(lastUpdate);
                Console.WriteLine("Parse datetime as:{0}", Data.latestUpdate);
            }
        }
    }

    public static void WriteData()
    {
        try
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    writer.WriteStartObject("Config");
                    writer.WriteString(nameof(DownloaderData.WebhookUrl), Data.WebhookUrl);
                    writer.WriteString(nameof(DownloaderData.GithubUser), Data.GithubUser);
                    writer.WriteString(nameof(DownloaderData.GithubPass), Data.GithubPass);
                    writer.WriteString(nameof(DownloaderData.CS2FixesRepo), Data.CS2FixesRepo);
                    writer.WriteEndObject();
                    writer.WriteStartObject("Data");
                    writer.WriteString(nameof(DownloaderData.latestWindowsManifest), Data.latestWindowsManifest.ToString());
                    writer.WriteString(nameof(DownloaderData.latestLinuxManifest), Data.latestLinuxManifest.ToString());
                    writer.WriteString(nameof(DownloaderData.latestUpdate), Data.latestUpdate.ToString());
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }

                File.WriteAllBytes(DATA_FILENAME, stream.ToArray());
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }
    public static async Task<bool> Download(bool manifestOnly)
    {
        Console.WriteLine("Checking for CS2 update...");
        ContentDownloader.Config.DownloadManifestOnly = manifestOnly;
        var depotManifestIds = new List<(uint, ulong)>();
        depotManifestIds.Add((WINDOWS_BIN_DEPOT, ContentDownloader.INVALID_MANIFEST_ID));
        depotManifestIds.Add((LINUX_BIN_DEPOT, ContentDownloader.INVALID_MANIFEST_ID));
        
        bool updated = false;

        if (InitializeSteam())
        {
            try
            {
                var manifests = await ContentDownloader.GetDepotLatestManifestInfo(CS2_APPID, depotManifestIds, DEFAULT_BRANCH);
                var windowsInfo = new List<DepotDownloadInfo>();
                var linuxInfo = new List<DepotDownloadInfo>();
                foreach (var mani in manifests) 
                {
                    if(mani.DepotId == WINDOWS_BIN_DEPOT)
                    {
                        windowsInfo.Add(mani);
                        if (mani.ManifestId != Data.latestWindowsManifest)
                        {
                            Data.latestWindowsManifest = mani.ManifestId;
                            Data.hasLatestWindowsDownloaded = false;
                        }
                        else
                            Data.hasLatestWindowsDownloaded = true;
                    }
                    else if(mani.DepotId ==  LINUX_BIN_DEPOT)
                    {
                        linuxInfo.Add(mani);
                        if (mani.ManifestId != Data.latestLinuxManifest)
                        {
                            Data.latestLinuxManifest = mani.ManifestId;
                            Data.hasLatestLinuxDownloaded = false;
                        }
                        else
                            Data.hasLatestLinuxDownloaded = true;
                    }
                }

                if (!Data.hasLatestWindowsDownloaded)
                {
                    Console.WriteLine("Donwloading new Windows Depot");
                    SetFileList(WINDOWS_FILELIST);
                    await ContentDownloader.DownloadSteam3Async(windowsInfo).ConfigureAwait(false);
                    Data.hasLatestWindowsDownloaded = true;
                    updated = true;
                }

                if (!Data.hasLatestLinuxDownloaded)
                {
                    Console.WriteLine("Donwloading new Linux Depot");
                    SetFileList(LINUX_FILELIST);
                    await ContentDownloader.DownloadSteam3Async(linuxInfo).ConfigureAwait(false);
                    Data.hasLatestLinuxDownloaded = true;
                    updated = true;
                }
            }
            catch (Exception ex) when (
                ex is ContentDownloaderException
                || ex is OperationCanceledException)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine("Download failed to due to an unhandled exception: {0}", e.Message);
            }
            finally
            {
                ContentDownloader.ShutdownSteam3();
            }
        }
        else
        {
            Console.WriteLine("Error: InitializeSteam failed\n");
            return false;
        }

        Console.WriteLine();
        return updated;
    }

    static bool InitializeSteam()
    {
        return ContentDownloader.InitializeSteam3(null, null);
    }

    public static DateTime DownloadCS2Fixes()
    {
        Console.WriteLine("Checking CS2Fixes Git");
        if (Data.GithubUser == "" || Data.GithubPass == "")
        {
            Console.WriteLine("Missing GithubUser or GithubPass setting!!!!\n");
            return DateTime.UnixEpoch;
        }

        if (Data.CS2FixesRepo == "")
        {
            Console.WriteLine("Missing CS2FixesRepo path setting!!!!\n");
            return DateTime.UnixEpoch;
        }

        string logMessage = "";
        using (var repo = new Repository(Data.CS2FixesRepo))
        {
            // FETCH
            var remote = repo.Network.Remotes["origin"];
            var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
            Commands.Fetch(repo, remote.Name, refSpecs, null, logMessage);
            // END FETCH

            // PULL
            // Credential information to fetch
            LibGit2Sharp.PullOptions options = new LibGit2Sharp.PullOptions();
            options.FetchOptions = new FetchOptions();
            options.FetchOptions.CredentialsProvider = new CredentialsHandler(
                (url, usernameFromUrl, types) =>
                    new UsernamePasswordCredentials()
                    {
                        Username = Data.GithubUser,
                        Password = Data.GithubPass
                    });

            // User information to create a merge commit
            var signature = new LibGit2Sharp.Signature(
                new Identity("MERGE_USER_NAME", "MERGE_USER_EMAIL"), DateTimeOffset.Now);

            Commands.Pull(repo, signature, options);
            // END PULL



            Commit latestCommit = repo.Head.Tip;
            Data.LatestCommit = latestCommit;

            if (latestCommit == null)
            {
                Console.WriteLine("No commits found in the repository.\n");
                return DateTime.UnixEpoch;
            }

            // Display commit details
            Console.WriteLine("Latest Commit Information:");
            Console.WriteLine($"Hash: {latestCommit.Sha}");
            Console.WriteLine($"Author: {latestCommit.Author.Name} <{latestCommit.Author.Email}>");
            Console.WriteLine($"Date: {latestCommit.Author.When}");
            Console.WriteLine($"Message: {latestCommit.MessageShort}\n");

            return latestCommit.Author.When.Date;
        }
    }

    static async void SetFileList(string filelist)
    {
        if (!File.Exists(filelist))
            return;

        ContentDownloader.Config.UsingFileList = true;
        ContentDownloader.Config.FilesToDownload = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ContentDownloader.Config.FilesToDownloadRegex = [];

        var files = await File.ReadAllLinesAsync(filelist);

        foreach (var fileEntry in files)
        {
            if (string.IsNullOrWhiteSpace(fileEntry))
            {
                continue;
            }

            if (fileEntry.StartsWith(RegexPrefix))
            {
                var rgx = new Regex(fileEntry[RegexPrefix.Length..], RegexOptions.Compiled | RegexOptions.IgnoreCase);
                ContentDownloader.Config.FilesToDownloadRegex.Add(rgx);
            }
            else
            {
                ContentDownloader.Config.FilesToDownload.Add(fileEntry.Replace('\\', '/'));
            }
        }
    }
}
