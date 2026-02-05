using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ValveKeyValue;
using static GamedataChecker.CS2FGamedata;

namespace GamedataChecker;

class CS2FGamedata
{
    public class Root
    {
        public CSGO csgo { get; set; }
    }

    public class CSGO
    {
        public Dictionary<string, Sig> Signatures { get; set; }
    }

    public class Sig
    {
        public string library { get; set; }
        public string windows { get; set; }
        public string linux { get; set; }
    }
}

public enum SigState
{
    None,       /* Exists but not checked */
    Found,      /* Valid byte signature found exactly once */
    Multiple,   /* Valid byte signature found multiple times in the binary */
    NotFound,   /* Valid byte signature but not found in the binary */
    Fatal,      /* Fatal error occurred while parsing string into bytes */
    NotDefined  /* Not defined in the gamedata file */
}
public class Signature
{
    public string library { get; set; }
    public byte[] windows { get; set; }
    public byte[] linux { get; set; }
    public SigState WindowsState { get; set; }
    public SigState LinuxState { get; set; }
    public int WindowsCount = 0;
    public int LinuxCount = 0;

    public bool Equals(Signature other)
    {
        if(library != other.library) return false;
        if(windows.Length != other.windows.Length) return false;
        for(int i = 0; i< windows.Length;i++)
        {
            if(windows[i] != other.windows[i]) return false;
        }

        if(linux.Length != other.linux.Length) return false;
        for (int i = 0; i < linux.Length; i++)
        {
            if (linux[i] != other.linux[i]) return false;
        }
        return true;
    }
}
public static class Parser
{
    public static Dictionary<string, Signature> Parse(string gamedatafile)
    {
        Console.WriteLine("Parsing signatures from: {0}", gamedatafile);
        if(!File.Exists(gamedatafile))
        {
            Console.WriteLine("Gamedata file doesn't exist!\n");
            return null;
        }
        var stream = File.OpenRead(gamedatafile);

        var kvs = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        CS2FGamedata.Root kv = kvs.Deserialize<CS2FGamedata.Root>(stream);
        stream.Close();

        CSGO csgo = kv.csgo;

        Dictionary<string, Signature> sigs = new Dictionary<string, Signature>();
        foreach (var k in csgo.Signatures.Keys)
        {
            Signature s = new Signature
            {
                library = csgo.Signatures[k].library,
            };
            
            s.windows = GetBytesFromHexString(csgo.Signatures[k].windows, out var wState);
            s.WindowsState = wState;

            s.linux = GetBytesFromHexString(csgo.Signatures[k].linux, out var lState);
            s.LinuxState = lState;

            sigs[k] = s;
        }

        Console.WriteLine();
        return sigs;
    }

    public static byte[] GetBytesFromHexString(string hex, out SigState state)
    {
        state = SigState.None;
        if (hex == null)
        { 
            state = SigState.NotDefined;
            return Array.Empty<byte>();
        }

        byte[] bytes = new byte[hex.Length/4];
        //Console.WriteLine("parsed bytes: {0}", bytes.Length);

        if (hex.Length % 4 != 0)
        {
            Console.WriteLine("Signature bytes not formatted correctly");
            state = SigState.Fatal;
            return bytes;
        }

        string[] sBytes = hex.Split("\\x", StringSplitOptions.RemoveEmptyEntries);
        if (sBytes.Length != bytes.Length)
        {
            Console.WriteLine("Signature bytes not formatted correctly");
            state = SigState.Fatal;
            return bytes;
        }

        for (int i = 0; i < sBytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(sBytes[i], 16);
        }

        return bytes;
    }
}
