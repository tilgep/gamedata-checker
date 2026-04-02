using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GamedataChecker;

public static class SigChecker
{
    const string GAME_FOLDER = "data/depots/game/";
    public const byte WILDCARD = 0x2A;
    const string WINDOWS_ENGINE = GAME_FOLDER + "bin/win64/engine2.dll";
    const string WINDOWS_SERVER = GAME_FOLDER + "csgo/bin/win64/server.dll";

    const string LINUX_ENGINE = GAME_FOLDER + "bin/linuxsteamrt64/libengine2.so";
    const string LINUX_SERVER = GAME_FOLDER + "csgo/bin/linuxsteamrt64/libserver.so";

    public static void Check(Dictionary<string, Signature> sigs)
    {
        Console.WriteLine("Checking windows sigs");
        byte[] engine = File.ReadAllBytes(WINDOWS_ENGINE);
        byte[] server = File.ReadAllBytes(WINDOWS_SERVER);

        int progress = 1;
        foreach (var kv in sigs)
        {
            Signature sig = kv.Value;
            Console.Write("[W] Checking {0} ({1}/{2})....", kv.Key, progress, sigs.Count);
            progress++;

            if (sig.WindowsState != SigState.None)
            {
                Console.WriteLine("{0}", sig.WindowsState);
                continue;
            }

            int matchCount = 0;
            SigState state = SigState.None;
            if (sig.library.ToLower() == "engine")
            {
                state = FindSignature(engine, sig.windows, out var matchLocs);
                matchCount = matchLocs.Count;
            }
            else if (sig.library.ToLower() == "server")
            {
                state = FindSignature(server, sig.windows, out var matchLocs);
                matchCount = matchLocs.Count;
            }

            sig.WindowsCount = matchCount;
            sig.WindowsState = state;

            Console.WriteLine("{0}", sig.WindowsState);
        }

        Console.WriteLine();
        Console.WriteLine("Checking linux sigs");
        engine = File.ReadAllBytes(LINUX_ENGINE);
        server = File.ReadAllBytes(LINUX_SERVER);

        progress = 1;
        foreach (var kv in sigs)
        {
            Signature sig = kv.Value;
            Console.Write("[L] Checking {0} ({1}/{2})....", kv.Key, progress, sigs.Count);
            progress++;

            if (sig.LinuxState != SigState.None)
            {
                Console.WriteLine("{0}", sig.LinuxState);
                continue;
            }

            int matchCount = 0;
            SigState state = SigState.None;
            if (sig.library.ToLower() == "engine")
            {
                state = FindSignature(engine, sig.linux, out var matchLocs);
                matchCount = matchLocs.Count;
            }
            else if (sig.library.ToLower() == "server")
            {
                state = FindSignature(server, sig.linux, out var matchLocs);
                matchCount = matchLocs.Count;
            }

            sig.LinuxCount = matchCount;
            sig.LinuxState = state;

            Console.WriteLine("{0}", sig.LinuxState);
        }
    }

    private static SigState FindSignature(byte[] data, byte[] pattern, out List<long> matchLocs)
    {
        int matches = 0;
        matchLocs = new List<long>();

        for (long i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i+j] == pattern[j] || pattern[j] == WILDCARD)
                {
                    continue;
                }

                match = false;
                break;
            }

            if (match)
            {
                matchLocs.Add(i);
                matches++;
            }
        }

        if (matches == 0)
            return SigState.NotFound;

        if (matches == 1)
            return SigState.Found;

        return SigState.Multiple;
        
    }

    public static bool HaveSigsChanged(Dictionary<string, Signature> oldSigs, Dictionary<string, Signature> newSigs)
    {
        if (oldSigs == null)
        {
            return true;
        }
        else
        {
            if (oldSigs.Count != newSigs.Count) return true;
            else
            {
                foreach (string key in oldSigs.Keys)
                {
                    if (!newSigs.ContainsKey(key))
                        return true;

                    if (!oldSigs[key].Equals(newSigs[key]))
                        return true;
                }
            }
        }

        return false;
    }
}
