using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MiningOperation
{
    [Serializable] public sealed class WorldSaveEnvelope
    {
        public string signature="MINING_OPERATION_WORLD_V1";
        public string checksum;public string payload;
    }
    public sealed class WorldSaveService
    {
        private readonly string path;
        public string PathOnDisk {get{return path;}}
        public WorldSaveService(string fullPath){path=fullPath;}
        private static string Hash(string value)
        {
            using(var sha=SHA256.Create())
            {var hash=sha.ComputeHash(Encoding.UTF8.GetBytes(value));return BitConverter.ToString(hash).Replace("-","").ToLowerInvariant();}
        }
        public void Save(WorldRuntime game)
        {
            game.Validate();
            var payload=JsonUtility.ToJson(game.State);
            var doc=JsonUtility.ToJson(new WorldSaveEnvelope {payload=payload,checksum=Hash(payload)},true);
            var directory=System.IO.Path.GetDirectoryName(path);
            if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);
            var tmp = path + ".tmp";
            var backup = path + ".bak";
            using(var stream=new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None))
            using(var writer=new StreamWriter(stream,new UTF8Encoding(false)))
            {writer.Write(doc);writer.Flush();stream.Flush(true);}
            ReadChecked(tmp); // Verify before replacing the previous save.
            if(File.Exists(path))
            {
                try{File.Replace(tmp,path,backup);}
                catch(PlatformNotSupportedException)
                {
                    if(File.Exists(backup))File.Delete(backup);
                    File.Move(path,backup);
                    try{File.Move(tmp,path);}catch{if(!File.Exists(path))File.Move(backup,path);throw;}
                }
            }
            else File.Move(tmp,path);
        }
        private static WorldState ReadChecked(string path)
        {
            var json=File.ReadAllText(path,Encoding.UTF8);
            var envelope=JsonUtility.FromJson<WorldSaveEnvelope>(json);
            if(envelope==null||envelope.signature!="MINING_OPERATION_WORLD_V1" || string.IsNullOrEmpty(envelope.payload)
                || !string.Equals(envelope.checksum,Hash(envelope.payload),StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Invalid world save or checksum");
            var result=JsonUtility.FromJson<WorldState>(envelope.payload);
            if(result==null||result.schemaVersion!=1)throw new InvalidDataException("Unknown save schema");
            return result;
        }
        public WorldRuntime Load()
        {
            WorldState state;
            try{state=ReadChecked(path);}
            catch(Exception ex) when(ex is IOException || ex is InvalidDataException || ex is ArgumentException)
            {
                if(!File.Exists(path+".bak"))throw;
                state=ReadChecked(path+".bak");
            }
            var game=new WorldRuntime(state);game.Validate();return game;
        }
        public static WorldSaveService Default()
        {return new WorldSaveService(System.IO.Path.Combine(Application.persistentDataPath,"MiningOperation","Saves","prototype.json"));}
    }
}
