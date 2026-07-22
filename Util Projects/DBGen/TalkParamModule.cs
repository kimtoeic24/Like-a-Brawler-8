using System;
using System.Collections.Generic;
using System.Linq;
using LibARMP;
using LibARMP.IO;
using System.IO;
using HActLib;
using System.ComponentModel.DataAnnotations;

namespace DBGen
{
    internal static class TalkParamModule
    {
        //adjust references
        public static bool ComplexMode = true;

        public static bool AdjustPibs = true;

        private static Dictionary<string, ushort> m_cueIDMap = new Dictionary<string, ushort>();
        private static Dictionary<string, int> m_hactTypeMap = new Dictionary<string, int>();


        private static ARMP particlePUID;

        public static void Procedure()
        {

                //false in Y6, only adjusts
            bool canAdd = true;
            bool isRepackGame = false;

            ARMP talkParamBin = Program.GetInputTable("talk_param");

            if (talkParamBin == null)
                canAdd = false;

            Console.WriteLine("------|TALK PARAM GEN|-----");

            string genFilePath = "hact_yazawa/hact_gen.txt";
    
            if (File.Exists(genFilePath))
                isRepackGame = true;

            string hactSrcDir = "";

            if (isRepackGame)
                hactSrcDir = "hact_yazawa";
            else
            {
                if (Program.NoCodename)
                    hactSrcDir = "hact";
                else
                    hactSrcDir = "hact." + Program.project;
            }


            if (!Directory.Exists(hactSrcDir))
                return;

            List<string> genFileDat = null;


            if (canAdd)
            {
                if (!File.Exists(genFilePath))
                    File.Create(genFilePath).Close();

                
                foreach(string str in File.ReadAllLines(genFilePath))
                {
                    string[] split = str.Split('|');
                    string hactName = split[0];
                    int hactType = int.Parse(split[1]);

                    m_hactTypeMap[hactName] = hactType;
                }
            }

            foreach (string hactDir in Directory.GetDirectories(hactSrcDir))
            {
                string dirName = new DirectoryInfo(hactDir).Name;

                if (canAdd)
                {
                    try
                    {
                        talkParamBin.GetMainTable().GetEntry(dirName);
                        continue;
                    }
                    catch
                    {
                    }

                    if (m_hactTypeMap.ContainsKey(dirName))
                        continue;
                    else
                    {
                        if (!m_hactTypeMap.ContainsKey(dirName))
                            m_hactTypeMap[dirName] = 9;

                        Console.WriteLine("Added " + hactDir);
                    }


                    File.WriteAllLines(genFilePath, m_hactTypeMap.Select(x => x.Key + "|" + x.Value.ToString()));
                }
            }

            if (!canAdd)
            {
                genFileDat = Directory.GetDirectories(hactSrcDir).ToList();

                if (Directory.Exists("auth"))
                    genFileDat.AddRange(Directory.GetDirectories("auth"));
            }

            string[] auths = new string[0];

            if (Directory.Exists("auth"))
                auths = Directory.GetDirectories("auth");


            foreach(var kv in m_hactTypeMap)
            {
                string str = kv.Key;
                string hactDir = null;

                hactDir = Path.Combine(hactSrcDir, str);

                if (canAdd)
                {
                    try
                    {
                        talkParamBin.GetMainTable().GetEntry(str);
                        Console.WriteLine(str + " already exists in talk param bin, skipping...");
                        continue;
                    }
                    catch
                    {
                    }
                }

                string cmnPath = Path.Combine(hactDir, "cmn", "cmn.bin");

                if (canAdd)
                {
                    ArmpEntry entry = null;
                    if (!talkParamBin.GetMainTable().TryGetEntry(str, out entry))
                    {
                        ArmpEntry talkEntry = talkParamBin.GetMainTable().AddEntry(str);
                        try
                        {
                            string path = "hact";

                            if (!Program.NoCodename)
                                path += "_" + Program.project;

                            path += "/";

                            talkEntry.SetValueFromColumn("path", path + str);
                            talkEntry.SetValueFromColumn("type", (byte)kv.Value);
                        }
                        catch
                        {

                        }

                        Console.WriteLine($"Added {str}, ID: {talkEntry.ID}");
                    }
                }

                bool dirty = false;

                if (ComplexMode)
                    AdjustHAct(cmnPath, str);

                if (dirty)
                {

                }

            }
            foreach(string str in auths)
            {
                string cmnPath = Path.Combine(str, "cmn", "cmn.bin");

                if(ComplexMode)
                    AdjustHAct(cmnPath, str);
            }

            if (canAdd)
            {
                ArmpFileWriter.WriteARMPToFile(talkParamBin, Path.Combine(Program.dbPath, "talk_param.bin"));
            }

            Console.WriteLine("------|TALK PARAM GEN COMPLETE|-----");
        }

        private static void AdjustHAct(string cmnPath, string str)
        {
            bool dirty = false;

            if (!File.Exists(cmnPath))
                return;

            if (!ComplexMode)
                return;

            CMN hact = CMN.Read(cmnPath, Program.Game);
            
            if (AdjustSound(hact, str))
                dirty = true;

            if (AdjustPibs)
            {
                if (AdjustPib(hact, str))
                    dirty = true;
            }

            if(dirty)
            {
                CMN.Write(hact, cmnPath);
            }
        }

        private static bool AdjustSound(CMN hact, string name)
        {
            if (SoundCuesheetModule.Result == null)
                return false;

            bool dirty = false;

            string str = name;

            string findName = str.Substring(0, (str.Length >= 11 ? 10 : str.Length));
            NodeElement[] soundNodes = hact.AllElements.Where(x => x is DEElementSE).ToArray();

            ushort newCuesheetID = 0;

            if (soundNodes.Length > 0)
            {
                foreach (DEElementSE soundNode in soundNodes)
                {
                    string nameToFind2 = soundNode.Name;
                    string nameToFind = "hact_" + soundNode.Name.Replace("hact_", "").ToLowerInvariant();

                    if (m_cueIDMap.ContainsKey(nameToFind))
                    {
                        soundNode.CueSheet = m_cueIDMap[nameToFind];
                        continue;
                    }

                    if (m_cueIDMap.ContainsKey(nameToFind2))
                    {
                        soundNode.CueSheet = m_cueIDMap[nameToFind2];
                        continue;
                    }

                    ArmpEntry cuesheetEntry = SoundCuesheetModule.Result.GetMainTable().GetAllEntries().FirstOrDefault(x => x.GetValueFromColumn("name").ToString().Contains(nameToFind));

                    if(cuesheetEntry == null)
                        cuesheetEntry = SoundCuesheetModule.Result.GetMainTable().GetAllEntries().FirstOrDefault(x => x.GetValueFromColumn("name").ToString().Contains(nameToFind2));

                    DEElementSE se = soundNode as DEElementSE;

                    if (cuesheetEntry != null)
                    {
                        newCuesheetID = ((ushort)cuesheetEntry.GetValueFromColumn("*cuesheet_id"));
                        if (se.CueSheet != newCuesheetID)
                        {
                            se.CueSheet = newCuesheetID;
                            dirty = true;

                            Console.WriteLine("Adjusted hact cuesheet ID for " + str);
                        }

                        m_cueIDMap[findName] = newCuesheetID;
                    }
                }

                /*
                if (cuesheetEntry != null)
                {
                    newCuesheetID = ((ushort)cuesheetEntry.GetValueFromColumn("*cuesheet_id"));

                    foreach (NodeElement soundNode in soundNodes)
                    {
                        DEElementSE se = soundNode as DEElementSE;

                        if (se != null)
                        {
                            if (se.CueSheet != newCuesheetID)
                            {
                                se.CueSheet = newCuesheetID;
                                dirty = true;
                            }
                        }
                    }
                    Console.WriteLine("Adjusted hact cuesheet IDs for " + str);
                }
                */
            }

            return dirty;
        }

        private static bool AdjustPib(CMN hact, string name)
        {
            if(particlePUID == null)
                particlePUID = Program.GetOutputPUIDTable("particle");

            if (particlePUID == null)
                return false;

            DEElementParticle[] particleNodes = hact.AllElements.Where(x => x is DEElementParticle).Cast<DEElementParticle>().ToArray();
            GameVersion hactVer = hact.GameVersion;

            bool dirty = false;


            foreach (DEElementParticle particle in particleNodes)
            {
                string ptcName = "";

                if (hactVer < GameVersion.DE1)
                    ptcName = particle.Name.Substring(0, 7);
                else
                    ptcName = particle.ParticleName;

                uint newID = 0;


                if (ParticleModule.pibMap.ContainsKey(ptcName))
                {
                    newID = ParticleModule.pibMap[ptcName];
                }
                else
                {
                    ArmpEntry foundEntry = null;
                    bool found = particlePUID.GetMainTable().TryGetEntry(ptcName, out foundEntry);

                    if (found)
                        newID = foundEntry.ID;
                }

                if (newID <= 0)
                    continue;

                if(particle.ParticleID != newID)
                {
                    dirty = true;
                    particle.ParticleID = newID;
                }    
            }


            return dirty;
        }
    }
}
