using CSharpDiscordWebhook;
using CSharpDiscordWebhook.Objects;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace GamedataChecker;

public class DiscordData
{
    public DiscordWebhook Webhook;
    public DiscordData()
    {
        Webhook = new DiscordWebhook(new Uri(Downloader.Data.WebhookUrl));
    }
}

public static class Discord
{
    private static DiscordData Data = new();
    public static async Task Send(Dictionary<string, Signature> sigs)
    {
        Console.WriteLine("Sending discord message");
        if (Downloader.Data.WebhookUrl == null || Downloader.Data.WebhookUrl == "")
        {
            Console.WriteLine("No WebhookUrl setting specified!!!!!");
            return;
        }

        DateTime utc = Downloader.Data.latestUpdate.ToUniversalTime();
        DateTimeOffset dto = new DateTimeOffset(utc);

        string timestamp = "<t:" + dto.ToUnixTimeSeconds().ToString() + ":S>";
        string title = "Update " + timestamp;

        // Build footer links
        string cs2fcommit = string.Format("[CS2F](<https://github.com/Source2ZE/CS2Fixes/tree/{0}>)", Downloader.Data.LatestCommit);

        string windowschange = string.Format("[Windows](<https://steamdb.info/depot/{0}/history/?changeid=M:{1}>)", 
            Downloader.WINDOWS_BIN_DEPOT.ToString(), 
            Downloader.Data.latestWindowsManifest.ToString());

        string linuxchange = string.Format("[Linux](<https://steamdb.info/depot/{0}/history/?changeid=M:{1}>)",
            Downloader.LINUX_BIN_DEPOT.ToString(),
            Downloader.Data.latestLinuxManifest.ToString());

        string footer = string.Format("{0} | {1} | {2}",
            cs2fcommit, windowschange, linuxchange);


        string desc = "";
        List<EmbedFieldBuilder> fields = new();
        bool makenewfield = true;
        foreach (var kv in sigs)
        {
            string siginfo = "`" + kv.Key + "`\n";
            siginfo += string.Format(" `Windows: {0}`{1}  `Linux: {2}`{3}\n\n",
                kv.Value.WindowsCount, GetEmoji(kv.Value.WindowsState),
                kv.Value.LinuxCount, GetEmoji(kv.Value.LinuxState));

            if(makenewfield)
            {
                EmbedFieldBuilder f = new EmbedFieldBuilder();
                f.Value = siginfo;
                fields.Add(f);
            }
            else
            {
                fields[fields.Count - 1].Value += siginfo;
            }

            if (fields[fields.Count - 1].Value.Length > 900)
                makenewfield = true;
            else
                makenewfield = false;
        }

        EmbedFieldBuilder footerfield = new EmbedFieldBuilder();
        footerfield.Value = "-# " + footer;
        fields.Add(footerfield);

        //Console.WriteLine("Message using {0} fields", fields.Count);

        await Data.Webhook.SendMessageAsync(new MessageBuilder
        {
            // ⚪⚫🔴🟠🟢⚠️
            Content = "",
            Username = "CS2Fixes Gamedata Check",
            Embeds = [
                new EmbedBuilder
                {
                    Title = title,
                    Color = Color.LightBlue,
                    Fields = fields,
                    Description = desc,
                }
            ],
        });
    }

    private static string GetEmoji(SigState state)
    {
        if (state == SigState.Found)
            return "🟢";
        else if (state == SigState.Multiple)
            return "🟠";
        else if (state == SigState.NotFound)
            return "🔴";
        else if (state == SigState.Fatal)
            return "⚠️";
        else if (state == SigState.NotDefined)
            return "⚫";

        return "⚪";
    }
}
