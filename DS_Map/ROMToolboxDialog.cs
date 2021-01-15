﻿using NarcAPI;
using System;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Windows.Forms;

namespace DSPRE
{
    public partial class ROMToolboxDialog : Form
    {
        RomInfo rom;

        public bool standardizedItems = new bool();

        public ROMToolboxDialog(RomInfo romInfo)
        {
            InitializeComponent();
            this.rom = romInfo;
        }

        private void applyItemStandardizeButton_Click(object sender, EventArgs e)
        {
            string path = rom.GetScriptDirPath() + "\\" + rom.GetItemScriptFileNumber().ToString("D4");

            ScriptFile file = new ScriptFile(new FileStream(path, FileMode.Open), rom.GetGameVersion());
            for (int i = 0; i < file.scripts.Count - 1; i++)
            {
                file.scripts[i].commands[0].parameters[1] = BitConverter.GetBytes((ushort)i); // Fix item index
                file.scripts[i].commands[1].parameters[1] = BitConverter.GetBytes((ushort)1); // Fix item quantity
            }
            using (BinaryWriter writer = new BinaryWriter(new FileStream(path, FileMode.Create))) writer.Write(file.Save());

            standardizedItems = true;
        }

        private void applyARM9ExpansionButton_Click(object sender, EventArgs e) {
            arm9Expansion(rom.GetWorkingFolder() + @"arm9.bin", rom.GetGameVersion(), rom.GetGameLanguage());
        }

        private void arm9Expansion(string arm9path, string version, string lang) {
            long initOffset;
            String initString;

            long branchOffset;
            String branchCodeString;

            int fileID = -1;

            ResourceManager arm9DB = new ResourceManager("DSPRE.Resources.ROMToolboxDB.ARM9ExpansionDB", Assembly.GetExecutingAssembly());

            fileID = Int16.Parse(arm9DB.GetString("fileID" + version));
            switch (version) {
                case "Diamond":
                case "Pearl":
                    initString = arm9DB.GetString("initString" + "Diamond");
                    branchCodeString = arm9DB.GetString("branchCode" + "Diamond" + lang);
                    branchOffset = 0x02000C80;
                    switch (lang) {
                        case "USA":
                            initOffset = 0x021064EC;
                            break;
                        case "ESP":
                            initOffset = 0x0210668C;
                            break;
                        default:
                            unsupportedROMLanguage();
                            return;
                    }
                    break;
                case "Platinum":
                    initString = arm9DB.GetString("initString" + version + lang);
                    branchCodeString = arm9DB.GetString("branchCode" + version + lang);
                    branchOffset = 0x02000CB4;
                    switch (lang) {
                        case "USA":
                            initOffset = 0x02100E20;
                            break;
                        case "ESP":
                            initOffset = 0x0210101C;
                            break;
                        default:
                            unsupportedROMLanguage();
                            return;
                    }
                    break;
                case "HeartGold":
                case "SoulSilver":
                    initString = arm9DB.GetString("initString" + "HeartGold");
                    branchCodeString = arm9DB.GetString("branchCode" + "HeartGold" + lang);
                    branchOffset = 0x02000CD0;
                    switch (lang) {
                        case "USA":
                            initOffset = 0x02110334;
                            break;
                        case "ESP":
                            initOffset = 0x02110354;
                            break;
                        default:
                            unsupportedROMLanguage();
                            return;
                    }
                    break;
                default:
                    unsupportedROM();
                    return;
            }
            initOffset -= 0x02000000;
            branchOffset -= 0x02000000;

            DialogResult d;
            d = MessageBox.Show("Confirming this process will apply the following changes:\n\n" +
                "- Backup ARM9 file (arm9.bin.bak will be created)." + "\n\n" +
                "- Replace " + (branchCodeString.Length / 3 + 1) + " bytes of data at arm9 offset 0x" + branchOffset.ToString("X") + " with " + '\n' + branchCodeString + "\n\n" +
                "- Replace " + (initString.Length / 3 + 1) + " bytes of data at arm9 offset 0x" + initOffset.ToString("X") + " with " + '\n' + initString + "\n\n" +
                "- Modify file #" + fileID + " inside " + '\n' + rom.GetSyntheticOverlayPath() + '\n' + " to accommodate for 88KB of data (no backup)." + "\n\n" +
                "Do you wish to continue?",
                "Confirm to proceed", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (d == DialogResult.Yes) {
                if (arm9expand(arm9path, fileID, initOffset, initString, branchOffset, branchCodeString))
                    MessageBox.Show("Operation successful.", "Process completed.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else {
                    MessageBox.Show("Operation failed. It is strongly advised that you restore the arm9 backup (arm9.bin.bak).", "Something went wrong",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            } else {
                MessageBox.Show("No changes have been made.", "Operation canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void applyPokemonNamesToSentenceCase_Click(object sender, EventArgs e) {
            String version = rom.GetGameVersion();
            int[] fileArchives = null;
           
            switch (version) {
                case "Diamond":
                case "Pearl":
                    fileArchives = new int[2] { 443, 444 };
                    break;
                case "Platinum":
                    fileArchives = new int[7] { 493, 494,  793, 794, 795, 796, 797};
                    break;
                case "HeartGold":
                case "SoulSilver":
                    fileArchives = new int[7] { 237, 238,  817, 818, 819, 820, 821};
                    break;
                default:
                    unsupportedROM();
                    return;
            }
            DialogResult d;
            d = MessageBox.Show("Confirming this process will apply the following changes:\n\n" +
                "- Every Pokémon name will be converted to Sentence Case." + "\n\n" +
                "Do you wish to continue?",
                "Confirm to proceed", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (d == DialogResult.Yes) {
                namesToSentenceCase(fileArchives);
                MessageBox.Show("Operation successful.", "Pokémon names have been converted to Sentence Case.", MessageBoxButtons.OK, MessageBoxIcon.Information);
            } else {
                MessageBox.Show("No changes have been made.", "Operation canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void namesToSentenceCase(int[] fileArchives) {
            //TODO implement this
        }

        private bool arm9expand(String arm9path, int fileID, long initOffset, string initString, long branchOffset, string branchString) {
            if (File.Exists(arm9path + ".bak"))
                File.Delete(arm9path + ".bak");
            File.Copy(arm9path, arm9path + ".bak");

            try {
                long current = 0;
                MainProgram.WriteToArm9(current, MainProgram.ReadFromArm9(current, branchOffset - current)); //Copy all until branchOffset
                MainProgram.WriteToArm9(branchOffset, hexStringtoByteArray(branchString, branchString.Length)); //Write new branchOffset
                current = branchOffset + branchString.Length;

                MainProgram.WriteToArm9(current, MainProgram.ReadFromArm9(current, initOffset - current));  //Copy all from branchOffset to initOffset
                MainProgram.WriteToArm9(initOffset, hexStringtoByteArray(initString, initString.Length)); //Write new initOffset
                current = initOffset + initString.Length;

                MainProgram.WriteToArm9(current, MainProgram.ReadFromArm9(current, -1));
            } catch {
                return false;
            }

            String fullFilePath = rom.GetSyntheticOverlayPath() + '\\' + fileID.ToString("D4");
            File.Delete(fullFilePath);

            BinaryWriter f = new BinaryWriter(File.Create(fullFilePath));
            for (int i = 0; i < 0x16000; i++) 
                f.Write((byte)0x00);
            f.Close();
            

            return true;
        }

        private byte[] hexStringtoByteArray (String hexString, int size) {
            //FC B5 05 48 C0 46 41 21 
            //09 22 02 4D A8 47 00 20 
            //03 21 FC BD F1 64 00 02 
            //00 80 3C 02
            hexString = hexString.Trim();
            if ((hexString.Length+1) % 2 != 0)
                return null;

            if ((hexString.Length) > size*3)
                return null;

            byte[] b = new byte[size/3 + 1];
            for (int i = 0; i < hexString.Length; i += 2) {
                if (hexString[i] == ' ') {
                    hexString = hexString.Substring(1, hexString.Length - 1);
                }

                b[i/2] = Convert.ToByte(hexString.Substring(i, 2), 16);
            }

            return b;
        }

        private void unsupportedROM() {
            MessageBox.Show("This operation is currently impossible to carry out on any Pokémon " + rom.GetGameVersion() + "ROM.",
                "Unsupported ROM", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void unsupportedROMLanguage() {
            MessageBox.Show("This operation is currently impossible to carry out on the " + rom.GetGameLanguage() +
                " version of this ROM.", "Unsupported language", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
