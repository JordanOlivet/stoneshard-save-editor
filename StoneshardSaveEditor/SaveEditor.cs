using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StoneshardSaveEditor
{
    public class SaveEditor
    {
        private readonly string _saveFilePath;
        private readonly JObject _rootJsonObject;
        public string BackupFilePath { get; private set; }
        public CharacterData Character { get; }
        public InventoryData Inventory { get; }

        public SaveEditor(string saveFilePath)
        {
            _saveFilePath = saveFilePath;

            _rootJsonObject = Utils.ReadJson(_saveFilePath);
            Character = new CharacterData();
            Character.Abilities = new BindingList<string>();

            var charDataMap = _rootJsonObject["characterDataMap"]!;
            Character.Name = charDataMap.Value<string>("nameKey");
            Character.Strength = charDataMap.Value<int>("STR");
            Character.Agility = charDataMap.Value<int>("AGL");
            Character.Perception = charDataMap.Value<int>("PRC");
            Character.Vitality = charDataMap.Value<int>("Vitality");
            Character.Willpower = charDataMap.Value<int>("WIL");
            Character.AbilityPoints = charDataMap.Value<int>("SP"); //strange
            Character.StatsPoints = charDataMap.Value<int>("AP"); //strange
            Character.Willpower = charDataMap.Value<int>("WIL");
            Character.Level = charDataMap.Value<int>("LVL");
            Character.XP = charDataMap.Value<int>("XP");

            var timeDataMap = _rootJsonObject["timeDataMap"]!;
            Character.GameTime = timeDataMap.Value<int>("months") + "M " +
                                 timeDataMap.Value<int>("days") + "d " +
                                 timeDataMap.Value<int>("hours") + "h " +
                                 timeDataMap.Value<int>("minutes") + "m " +
                                 timeDataMap.Value<int>("seconds") + "s";


            var skillsArray = (JArray)_rootJsonObject["skillsDataMap"]!["skillsAllDataList"]!;
            for (int i = 0; i < skillsArray.Count; i += 5)
            {
                if (skillsArray[i + 1].ToObject<int>() == 1)
                {
                    Character.Abilities.Add(skillsArray[i].ToObject<String>());
                }
            }

            Inventory = new InventoryData();

            var inventoryList = _rootJsonObject["inventoryDataList"]!;

            foreach (var v in inventoryList)
            {
                if (v is JArray arr && arr.FirstOrDefault() is JValue va && va.Value<string>() == "o_inv_moneybag")
                {
                    foreach (var b in v)
                    {
                        if (b is JObject bb && bb.ContainsKey("Stack"))
                        {
                            Inventory.Money = bb["Stack"].Value<int>();
                        }
                    }
                }
            }
        }

        public void Save()
        {
            var charDataMap = _rootJsonObject["characterDataMap"]!;

            charDataMap["STR"] = (float)Character.Strength;
            charDataMap["AGL"] = (float)Character.Agility;
            charDataMap["PRC"] = (float)Character.Perception;
            charDataMap["Vitality"] = (float)Character.Vitality;
            charDataMap["WIL"] = (float)Character.Willpower;
            charDataMap["SP"] = (float)Character.AbilityPoints; //strange
            charDataMap["AP"] = (float)Character.StatsPoints; //strange
            charDataMap["WIL"] = (float)Character.Willpower;
            charDataMap["LVL"] = (float)Character.Level;
            charDataMap["XP"] = (float)Character.XP;

            JArray skillsArray = (JArray)_rootJsonObject["skillsDataMap"]!["skillsAllDataList"]!;
            for (int i = 0; i < skillsArray.Count; i += 5)
            {
                var jToken = skillsArray[i + 1];
                if (jToken.ToObject<int>() == 1 && Character.Abilities.Contains(skillsArray[i].ToObject<String>()) == false)
                {
                    jToken.Replace(0f);
                }
            }

            var inventoryList = _rootJsonObject["inventoryDataList"]!;

            foreach (var item in inventoryList)
            {
                if (item is JArray arr && arr.FirstOrDefault() is JValue va && va.Value<string>() == "o_inv_moneybag")
                {
                    foreach (var value in item)
                    {
                        if (value is JObject val && val.ContainsKey("Stack"))
                        {
                            val["Stack"] = Inventory.Money;
                        }
                    }
                }
            }

            CreateBackup();
            WriteDataToFile();
        }

        private void WriteDataToFile()
        {
            var sb = new StringBuilder((int)new FileInfo(_saveFilePath).Length + 1000);
            sb.Append(_rootJsonObject.ToString(Formatting.None));

            sb.Append(CalcMd5(sb));
            var jsonAndMd5 = sb.ToString();
            //File.WriteAllText(_saveFilePath + "_mod.json", jsonAndMd5);

            using (FileStream fileStream = new FileStream(_saveFilePath, FileMode.Create, FileAccess.Write))
            {
                using (var outputStream = new DeflaterOutputStream(fileStream))
                {
                    var bytes = Encoding.UTF8.GetBytes(jsonAndMd5);
                    outputStream.Write(bytes, 0, bytes.Length);
                    outputStream.WriteByte(0);
                }
            }
        }

        private string CalcMd5(StringBuilder jsonString)
        {
            //see scr_slotSaveDataMapSave in decompiled game code:
            //salt: "stOne!characters_v1!" + character_N + "!" + save_folder + "!shArd"
            string[] pathParts = _saveFilePath.Split(Path.DirectorySeparatorChar);
            var salt = "stOne!characters_v1!" + pathParts[pathParts.Length - 3] +
                       "!" + pathParts[pathParts.Length - 2] + "!shArd";
            //var salt = "stOne!characters_v1!shArd";
            var md5Input = Encoding.UTF8.GetBytes(jsonString + salt);
            var result = new StringBuilder(32);
            using (var md5 = MD5.Create())
            {
                var md5Hash = md5.ComputeHash(md5Input);
                foreach (byte b in md5Hash)
                {
                    result.Append(b.ToString("x2"));
                }
            }

            return result.ToString();
        }

        private void CreateBackup()
        {
            var saveDir = Path.GetDirectoryName(_saveFilePath);
            BackupFilePath = saveDir + " " + DateTime.Now.ToString("s").Replace(':', '_') + ".backup.zip";
            ZipFile.CreateFromDirectory(
              saveDir, BackupFilePath, CompressionLevel.Optimal, true);
        }
    }
}
