

namespace GamedataChecker;


class GamedataChecker
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Starting GamedataChecker");

        AccountSettingsStore.LoadFromFile("account.config");

        Downloader.LoadData();
        string gdfile = Downloader.Data.CS2FixesRepo + "/gamedata/cs2fixes.games.txt";

        // Check for CS2F signature changes
        Dictionary<string, Signature> oldSigs = Parser.Parse(gdfile);
        DateTime cs2ftime = Downloader.DownloadCS2Fixes();
        if (cs2ftime > Downloader.Data.latestUpdate)
        {
            Downloader.Data.latestUpdate = cs2ftime;
        }
        Dictionary<string, Signature> newSigs = Parser.Parse(gdfile);

        if (newSigs == null)
        {
            Console.WriteLine("Failed to get signatures from the CS2F repo. Exiting...");
            return 0;
        }

        bool sigChanges = SigChecker.HaveSigsChanged(oldSigs, newSigs);
        if (sigChanges)
        {
            Console.WriteLine("Signatures changed.");
        }



        // Check for CS2 update
        bool updated = await Downloader.Download(false);

        Downloader.WriteData();


        if (updated || sigChanges)
        {
            SigChecker.Check(newSigs);
            await Discord.Send(newSigs);
        }
        else
        {
            Console.WriteLine("No CS2 updates or signature changes.");
        }

        return 0;
    }
}