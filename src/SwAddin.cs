using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;

namespace SolidWorksSlicerBridge
{
    [ComVisible(true)]
    [Guid("D51D3347-A8E7-4892-A8BD-391203C2E8A4")]
    [ProgId("SolidWorksSlicerBridge.Addin")]
    [ClassInterface(ClassInterfaceType.None)]
    public class SwAddin : ISwAddin
    {
        private const int CommandGroupId = 73191;
        private const string CommandTabName = "3D Print";
        private const string RegistrySettings = @"Software\SolidWorksSlicerBridge";
        private const string AddinGuid = "{D51D3347-A8E7-4892-A8BD-391203C2E8A4}";

        private ISldWorks swApp;
        private ICommandManager commandManager;
        private ICommandGroup commandGroup;
        private int addinCookie;

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            try
            {
                swApp = (ISldWorks)ThisSW;
                addinCookie = Cookie;
                swApp.SetAddinCallbackInfo2(0, this, addinCookie);
                commandManager = swApp.GetCommandManager(addinCookie);
                AddCommandManager();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("–ù–µ —É–¥–∞–ª–æ—Å—å –∑–∞–≥—Ä—É–∑–∏—Ç—å SolidWorks Slicer Bridge.\r\n\r\n" + ex.Message,
                    "SolidWorks Slicer Bridge", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        public bool DisconnectFromSW()
        {
            try
            {
                RemoveCommandManager();
            }
            catch
            {
                // SOLIDWORKS can already be partly shutting down here.
            }

            commandGroup = null;
            commandManager = null;
            swApp = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            return true;
        }

        private void AddCommandManager()
        {
            int createErrors = 0;
            object previousIds = null;
            bool hasPrevious = commandManager.GetGroupDataFromRegistry(CommandGroupId, out previousIds);

            // Command IDs are stable in this version. Preserve prior toolbar placement when possible.
            bool ignorePrevious = !hasPrevious;
            commandGroup = commandManager.CreateCommandGroup2(
                CommandGroupId,
                "3D Print Slicers",
                "–≠–∫—Å–ø–æ—Ä—Ç–∏—Ä–æ–≤–∞—Ç—å –∞–∫—Ç–∏–≤–Ω—É—é –º–æ–¥–µ–ª—å –≤ 3MF –∏ –æ—Ç–∫—Ä—ã—Ç—å –≤ —Å–ª–∞–π—Å–µ—Ä–µ",
                "",
                -1,
                ignorePrevious,
                ref createErrors);

            if (commandGroup == null)
                throw new InvalidOperationException("SOLIDWORKS –Ω–µ —Å–æ–∑–¥–∞–ª CommandGroup. –ö–æ–¥: " + createErrors);

            string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string iconDir = Path.Combine(baseDir, "Icons");
            int[] sizes = new int[] { 20, 32, 40, 64, 96, 128 };
            string[] strips = new string[sizes.Length];
            string[] mains = new string[sizes.Length];
            int i;

            for (i = 0; i < sizes.Length; i++)
            {
                strips[i] = Path.Combine(iconDir, "toolbar_" + sizes[i] + ".png");
                mains[i] = Path.Combine(iconDir, "main_" + sizes[i] + ".png");
            }

            commandGroup.IconList = strips;
            commandGroup.MainIconList = mains;

            int menuAndToolbar = (int)swCommandItemType_e.swMenuItem | (int)swCommandItemType_e.swToolbarItem;

            int orcaIndex = commandGroup.AddCommandItem2(
                "OrcaSlicer", -1,
                "–≠–∫—Å–ø–æ—Ä—Ç 3MF –∏ –æ—Ç–∫—Ä—ã—Ç—å –≤ OrcaSlicer",
                "Open in OrcaSlicer", 0,
                "OpenInOrca", "CanExport", 1001, menuAndToolbar);

            int bambuIndex = commandGroup.AddCommandItem2(
                "Bambu Studio", -1,
                "–≠–∫—Å–ø–æ—Ä—Ç 3MF –∏ –æ—Ç–∫—Ä—ã—Ç—å –≤ Bambu Studio",
                "Open in Bambu Studio", 1,
                "OpenInBambu", "CanExport", 1002, menuAndToolbar);

            int prusaIndex = commandGroup.AddCommandItem2(
                "PrusaSlicer", -1,
                "–≠–∫—Å–ø–æ—Ä—Ç 3MF –∏ –æ—Ç–∫—Ä—ã—Ç—å –≤ PrusaSlicer",
                "Open in PrusaSlicer", 2,
                "OpenInPrusa", "CanExport", 1003, menuAndToolbar);

            int settingsIndex = commandGroup.AddCommandItem2(
                "Slicer Settings", -1,
                "–ù–∞—Å—Ç—Ä–æ–∏—Ç—å –ø—É—Ç–∏ –∫ —Å–ª–∞–π—Å–µ—Ä–∞–º",
                "Slicer Settings", 3,
                "ShowSettings", "AlwaysEnabled", 1004, menuAndToolbar);

            commandGroup.HasToolbar = true;
            commandGroup.HasMenu = true;
            commandGroup.Activate();

            AddCommandTab((int)swDocumentTypes_e.swDocPART,
                new int[] {
                    commandGroup.get_CommandID(orcaIndex),
                    commandGroup.get_CommandID(bambuIndex),
                    commandGroup.get_CommandID(prusaIndex),
                    commandGroup.get_CommandID(settingsIndex)
                });

            AddCommandTab((int)swDocumentTypes_e.swDocASSEMBLY,
                new int[] {
                    commandGroup.get_CommandID(orcaIndex),
                    commandGroup.get_CommandID(bambuIndex),
                    commandGroup.get_CommandID(prusaIndex),
                    commandGroup.get_CommandID(settingsIndex)
                });
        }

        private void AddCommandTab(int docType, int[] commandIds)
        {
            ICommandTab tab = commandManager.GetCommandTab(docType, CommandTabName);
            if (tab != null)
                return;

            tab = commandManager.AddCommandTab(docType, CommandTabName);
            if (tab == null)
                return;

            CommandTabBox box = tab.AddCommandTabBox();
            int[] styles = new int[commandIds.Length];
            int i;
            for (i = 0; i < styles.Length; i++)
                styles[i] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextHorizontal;

            box.AddCommands(commandIds, styles);
        }

        private void RemoveCommandManager()
        {
            if (commandManager == null)
                return;

            // Runtime-only removal preserves toolbar placement in the SOLIDWORKS registry.
            try { commandManager.RemoveCommandGroup2(CommandGroupId, true); }
            catch { }
        }

        public int CanExport()
        {
            try
            {
                IModelDoc2 model = swApp == null ? null : (IModelDoc2)swApp.ActiveDoc;
                if (model == null)
                    return 0;

                int type = model.GetType();
                if (type == (int)swDocumentTypes_e.swDocPART ||
                    type == (int)swDocumentTypes_e.swDocASSEMBLY)
                    return 1;
            }
            catch { }
            return 0;
        }

        public int AlwaysEnabled()
        {
            return 1;
        }

        public void OpenInOrca()
        {
            ExportAndOpen(SlicerKind.Orca);
        }

        public void OpenInBambu()
        {
            ExportAndOpen(SlicerKind.Bambu);
        }

        public void OpenInPrusa()
        {
            ExportAndOpen(SlicerKind.Prusa);
        }

        public void ShowSettings()
        {
            using (SettingsForm form = new SettingsForm())
                form.ShowDialog();
        }

        private void ExportAndOpen(SlicerKind kind)
        {
            try
            {
                IModelDoc2 model = swApp == null ? null : (IModelDoc2)swApp.ActiveDoc;
                if (model == null)
                {
                    MessageBox.Show("–û—Ç–∫—Ä–æ–π –¥–µ—Ç–∞–ª—å –∏–ª–∏ —Å–±–æ—Ä–∫—É.", "3D Print",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int docType = model.GetType();
                if (docType != (int)swDocumentTypes_e.swDocPART &&
                    docType != (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    MessageBox.Show($+t.¥`t/Ù/¥`4`à4/Ù/¥-4-4-t`4-¥.4,¥,4-t`¥`tc»4-4.Ùc»4-4-t`¥,4.Ù-t.H4.4`t,t/¥`4/¥.ãàãå—ö[ùãàY\‹ÿYŸPõﬁù]€úÀì“ÀY\‹ÿYŸPõﬁX€€ãí[ôõ‹õX][€äN¬àô]\õé¬àBÇà›ö[ô»^HH€XŸ\î]Àîô\€€ôJ⁄[ôùYJN¬àYà
›ö[ôÀí\”ù[‹ë[\J^JJBàô]\õé¬Çà›ö[ô»›]]H‹ôX]S›]]]
[Ÿ[
N¬à^‹ù”Yä[Ÿ[›]]
N¬à][ò⁄€XŸ\ä^K›]]
N¬à€X[ù\€^‹ù 
N¬àBàÿ]⁄
^Ÿ\[€à^
Bà¬àY\‹ÿYŸPõﬁî⁄› 		Ìçç≠›≠˝Ì-˝=˝=≠•«%∆Â«%∆‚"≤WÇ‰÷W76vR¿¢#4B&ñÁB"¬÷W76vT&˜Ñ'WGFˆÁ2‰Ù≤¬÷W76vT&˜Ññ6ˆ‚‰W'&˜"ì∞¢–¢–†¢&ófFRfˆñBWá˜'C4÷bÑî÷ˆFVƒFˆ3"÷ˆFV¬¬7G&ñÊr˜WGWEFÇê¢∞¢&ˆˆ¬ˆ∆E6Ü˜tñÊfÚ“f«6S∞¢&ˆˆ¬ˆ∆E&WfñWr“f«6S∞¢&ˆˆ¬ÜfU6Ü˜tñÊfÚ“f«6S∞¢&ˆˆ¬ÜfU&WfñWr“f«6S∞†¢G'ê¢∞¢ˆ∆E6Ü˜tñÊfÚ“7t‰vWEW6W%&VfW&VÊ6UFˆvv∆RÇÜñÁBó7uW6W%&VfW&VÊ6UFˆvv∆UˆRÁ7s4‘e6Ü˜tñÊfÙˆÂ6fRì∞¢ÜfU6Ü˜tñÊfÚ“G'VS∞¢–¢6F6Ç≤–†¢G'ê¢∞¢ˆ∆E&WfñWr“7t‰vWEW6W%&VfW&VÊ6UFˆvv∆RÇÜñÁBó7uW6W%&VfW&VÊ6UFˆvv∆UˆRÁ7u5D≈&WfñWrì∞¢ÜfU&WfñWr“G'VS∞¢–¢6F6Ç≤–†¢G'ê¢∞¢ÚÚ6fT32Wá˜'G2ˆÊ«í6V∆V7FVBf6W2ˆ&ˆFñW2ñb6V∆V7Fñˆ‚WÜó7G2‡¢ÚÚ6∆V"óB6ÚFˆˆ∆&"6∆ñ6≤«vó2Wá˜'G2FÜRvÜˆ∆R7FófR÷ˆFV¬‡¢÷ˆFV¬‰6∆V%6V∆V7Fñˆ„"áG'VRì∞†¢G'í≤7tÂ6WEW6W%&VfW&VÊ6UFˆvv∆RÇÜñÁBó7uW6W%&VfW&VÊ6UFˆvv∆UˆRÁ7s4‘e6Ü˜tñÊfÙˆÂ6fR¬f«6Rì≤–¢6F6Ç≤–¢G'í≤7tÂ6WEW6W%&VfW&VÊ6UFˆvv∆RÇÜñÁBó7uW6W%&VfW&VÊ6UFˆvv∆UˆRÁ7u5D≈&WfñWr¬f«6Rì≤–¢6F6Ç≤–†¢ñÁBW'&˜'2“∞¢ñÁBv&ÊñÊw2“∞¢î÷ˆFVƒFˆ4WáFVÁ6ñˆ‚WáB“÷ˆFV¬‰WáFVÁ6ñˆ„∞¢&ˆˆ¬ˆ≤“WáBÂ6fT32Ä¢˜WGWEFÇ¿¢ÜñÁBó7u6fT5fW'6ñˆÂˆRÁ7u6fT47W'&VÁEfW'6ñˆ‚¿¢ÜñÁBó7u6fT4˜FñˆÁ5ˆRÁ7u6fT4˜FñˆÁ5ı6ñ∆VÁB¿¢ÁV∆¬¿¢ÁV∆¬¿¢˜WBW'&˜'2¿¢˜WBv&ÊñÊw2ì∞†¢ñbÇˆ≤«¬W'&˜'2“«¬fñ∆R‰WÜó7G2Ü˜WGWEFÇíê¢Fá&˜rÊWrñÁf∆ñD˜W&Fñˆ‰WÜ6WFñˆ‚Ä¢-
	Ì	Ω	ç	M	Ì
Ω
›RÕÌ2Ì]›ç-¬≤›-Ìm]›}çR‚W'&˜#“"≤W'&˜'2≤"¬v&ÊñÊs“"≤v&ÊñÊw2ì∞¢–¢fñÊ∆«ê¢∞¢ñbÜÜfU6Ü˜tñÊfÚê¢∞¢G'í≤7tÂ6WEW6W%&VfW&VÊ6UFˆvv∆RÇÜñÁBó7uW6W%&VfW&VÊ6UFˆvv∆UˆRÁ7s4‘e6Ü˜tñÊfÙˆÂ6fR¬ˆ∆E6Ü˜tñÊfÚì≤–¢6F6Ç≤–¢–¢ñbÜÜfU&WfñWrê¢∞¢G'í≤7tÂ6WEW6W%&VfW&VÊ6UFˆvv∆RÇÜñÁBó7uW6W%&VfW&VÊ6UFˆvv∆UˆRÁ7u5D≈&WfñWr¬ˆ∆E&WfñWrì≤–¢6F6Ç≤–¢–¢–¢–†¢&ófFR7FFñ27G&ñÊr7&VFT˜WGWEFÇÑî÷ˆFVƒFˆ3"÷ˆFV¬ê¢∞¢7G&ñÊrfˆ∆FW"“FÇ‰6ˆ÷&ñÊRÖFÇ‰vWEFV◊FÇÇí¬%6ˆ∆ñEv˜&∑56∆ñ6W$'&ñFvR"ì∞¢Fó&V7F˜'í‰7&VFTFó&V7F˜'íÜfˆ∆FW"ì∞†¢7G&ñÊrFóF∆R“÷ˆFV¬‰vWEFóF∆RÇì∞¢ñÁBF˜B“FóF∆R‰∆7DñÊFWÑˆbÇr‚rì∞¢ñbÜF˜B‚ê¢FóF∆R“FóF∆RÂ7V'7G&ñÊrÉ¬F˜Bì∞†¢FóF∆R“÷∂U6fTfñ∆TÊ÷RáFóF∆Rì∞¢7G&ñÊr7F◊“FFUFñ÷R‰Ê˜rÂFı7G&ñÊrÇ'óóóî‘÷FEÙÑÜ÷◊75ˆffb"ì∞¢&WGW&‚FÇ‰6ˆ÷&ñÊRÜfˆ∆FW"¬FóF∆R≤%Ú"≤7F◊≤"„6÷b"ì∞¢–†¢&ófFR7FFñ27G&ñÊr÷∂U6fTfñ∆TÊ÷Rá7G&ñÊrf«VRê¢∞¢ñbÖ7G&ñÊr‰ó4ÁV∆ƒ˜%vÜóFU76Ráf«VRíê¢&WGW&‚%6ˆ∆ñEv˜&∑4÷ˆFV¬#∞†¢6Ü%µ“ñÁf∆ñB“FÇ‰vWDñÁf∆ñDfñ∆TÊ÷T6Ü'2Çì∞¢7G&ñÊt'Vñ∆FW"6"“ÊWr7G&ñÊt'Vñ∆FW"áf«VR‰∆VÊwFÇì∞¢ñÁBì∞¢f˜"Üí“≤í¬f«VR‰∆VÊwFÉ≤í≤≤ê¢∞¢6Ü"2“f«VU∂ï”∞¢ñbÑ'&í‰ñÊFWÑˆbÜñÁf∆ñB¬2í„“ê¢6"‰VÊBÇuÚrì∞¢V«6P¢6"‰VÊBÜ2ì∞¢–¢&WGW&‚6"ÂFı7G&ñÊrÇì∞¢–†¢&ófFR7FFñ2fˆñB∆VÊ6Ö6∆ñ6W"á7G&ñÊrWÜR¬7G&ñÊr÷ˆFV≈FÇê¢∞¢&ˆ6W757F'DñÊfÚ6í“ÊWr&ˆ6W757F'DñÊfÚÇì∞¢6í‰fñ∆TÊ÷R“WÜS∞¢6í‰&wV÷VÁG2“%¬""≤÷ˆFV≈FÇ≤%¬"#∞¢6íÂv˜&∂ñÊtFó&V7F˜'í“FÇ‰vWDFó&V7F˜'îÊ÷RÜWÜRì∞¢6íÂW6U6ÜV∆ƒWÜV7WFR“G'VS∞¢&ˆ6W72Â7F'Bá6íì∞¢–†¢&ófFR7FFñ2fˆñB6∆VÁWˆ∆DWá˜'G2Çê¢∞¢G'ê¢∞¢7G&ñÊrfˆ∆FW"“FÇ‰6ˆ÷&ñÊRÖFÇ‰vWEFV◊FÇÇí¬%6ˆ∆ñEv˜&∑56∆ñ6W$'&ñFvR"ì∞¢ñbÇFó&V7F˜'í‰WÜó7G2Üfˆ∆FW"íê¢&WGW&„∞†¢7G&ñÊuµ“fñ∆W2“Fó&V7F˜'í‰vWDfñ∆W2Üfˆ∆FW"¬"¢„6÷b"ì∞¢FFUFñ÷R7WFˆfb“FFUFñ÷R‰Ê˜r‰FDFó2Ç”rì∞¢ñÁBì∞¢f˜"Üí“≤í¬fñ∆W2‰∆VÊwFÉ≤í≤≤ê¢∞¢G'ê¢∞¢ñbÑfñ∆R‰vWD∆7Ew&óFUFñ÷RÜfñ∆W5∂ï“í¬7WFˆfbê¢fñ∆R‰FV∆WFRÜfñ∆W5∂ï“ì∞¢–¢6F6Ç≤–¢–¢–¢6F6Ç≤–¢–†¢¥6ˆ’&Vvó7FW$gVÊ7FñˆÂ–¢V&∆ñ27FFñ2fˆñB&Vvó7FW"ÖGóRBê¢∞¢W6ñÊrÖ&Vvó7G'î∂Wí∆““&Vvó7G'î∂Wí‰˜V‰&6T∂WíÖ&Vvó7G'îÜófR‰∆ˆ6ƒ÷6ÜñÊR¬&Vvó7G'ïfñWrÂ&Vvó7G'ìcBíê¢W6ñÊrÖ&Vvó7G'î∂Wí∂Wí“∆“‰7&VFU7V$∂WíÑ%4ÙeEt$U≈6ˆ∆ñEv˜&∑5ƒFFñÁ5¬"≤FFñ‰wVñBíê¢∞¢∂WíÂ6WEf«VRÜÁV∆¬¬¬&Vvó7G'ïf«VT∂ñÊB‰Ev˜&Bì∞¢∂WíÂ6WEf«VRÇ%FóF∆R"¬%6ˆ∆ñEv˜&∑26∆ñ6W"'&ñFvR"¬&Vvó7G'ïf«VT∂ñÊBÂ7G&ñÊrì∞¢∂WíÂ6WEf«VRÇ$FW67&óFñˆ‚"¬$ˆÊR÷6∆ñ6≤4‘bWá˜'BFÚ˜&66∆ñ6W"¬&÷'R7GVFñÚÊB'W66∆ñ6W""¬&Vvó7G'ïf«VT∂ñÊBÂ7G&ñÊrì∞¢–†¢W6ñÊrÖ&Vvó7G'î∂Wí7R“&Vvó7G'î∂Wí‰˜V‰&6T∂WíÖ&Vvó7G'îÜófR‰7W'&VÁEW6W"¬&Vvó7G'ïfñWrÂ&Vvó7G'ìcBíê¢W6ñÊrÖ&Vvó7G'î∂Wí∂Wí“7R‰7&VFU7V$∂WíÑ%6ˆgGv&U≈6ˆ∆ñEv˜&∑5ƒFDñÁ57F'GW¬"≤FFñ‰wVñBíê¢∞¢∂WíÂ6WEf«VRÜÁV∆¬¬¬&Vvó7G'ïf«VT∂ñÊB‰Ev˜&Bì∞¢–¢–†¢¥6ˆ’VÁ&Vvó7FW$gVÊ7FñˆÂ–¢V&∆ñ27FFñ2fˆñBVÁ&Vvó7FW"ÖGóRBê¢∞¢G'ê¢∞¢W6ñÊrÖ&Vvó7G'î∂Wí∆““&Vvó7G'î∂Wí‰˜V‰&6T∂WíÖ&Vvó7G'îÜófR‰∆ˆ6ƒ÷6ÜñÊR¬&Vvó7G'ïfñWrÂ&Vvó7G'ìcBíê¢∆“‰FV∆WFU7V$∂WïG&VRÑ%4ÙeEt$U≈6ˆ∆ñEv˜&∑5ƒFFñÁ5¬"≤FFñ‰wVñB¬f«6Rì∞¢–¢6F6Ç≤–†¢G'ê¢∞¢W6ñÊrÖ&Vvó7G'î∂Wí7R“&Vvó7G'î∂Wí‰˜V‰&6T∂WíÖ&Vvó7G'îÜófR‰7W'&VÁEW6W"¬&Vvó7G'ïfñWrÂ&Vvó7G'ìcBíê¢7R‰FV∆WFU7V$∂WïG&VRÑ%6ˆgGv&U≈6ˆ∆ñEv˜&∑5ƒFDñÁ57F'GW¬"≤FFñ‰wVñB¬f«6Rì∞¢–¢6F6Ç≤–¢–¢–†¢ñÁFW&Ê¬VÁV“6∆ñ6W$∂ñÊ@¢∞¢˜&6¿¢&÷'R¿¢'W6¢–†¢ñÁFW&Ê¬7FFñ26∆726∆ñ6W%Fá0¢∞¢&ófFR6ˆÁ7B7G&ñÊr&Vvó7G'ï6WGFñÊw2“%6ˆgGv&U≈6ˆ∆ñEv˜&∑56∆ñ6W$'&ñFvR#∞†¢V&∆ñ27FFñ27G&ñÊr&W6ˆ«fRÖ6∆ñ6W$∂ñÊB∂ñÊB¬&ˆˆ¬∆∆˜u&ˆ◊Bê¢∞¢7G&ñÊr6fVB“&VE6fVBÜ∂ñÊBì∞¢ñbÑfñ∆R‰WÜó7G2á6fVBíê¢&WGW&‚6fVC∞†¢7G&ñÊrWFÚ“WFÙFWFV7BÜ∂ñÊBì∞¢ñbÇ7G&ñÊr‰ó4ÁV∆ƒ˜$V◊GíÜWFÚíê¢∞¢6fRÜ∂ñÊB¬WFÚì∞¢&WGW&‚WFÛ∞¢–†¢ñbÇ∆∆˜u&ˆ◊Bê¢&WGW&‚"#∞†¢W6ñÊrÑ˜V‰fñ∆TFñ∆ˆrFñ∆ˆr“ÊWr˜V‰fñ∆TFñ∆ˆrÇíê¢∞¢Fñ∆ˆrÂFóF∆R“-
=≠mÇ"≤Fó7∆îÊ÷RÜ∂ñÊBí≤"ÊWÜR#∞¢Fñ∆ˆr‰fñ«FW"“$WÜV7WF&∆RÇ¢ÊWÜRó¬¢ÊWÜWƒ∆¬fñ∆W2Ç¢‚¢ó¬¢‚¢#∞¢Fñ∆ˆr‰6ÜV6¥fñ∆TWÜó7G2“G'VS∞¢ñbÜFñ∆ˆrÂ6Ü˜tFñ∆ˆrÇí”“Fñ∆ˆu&W7V«B‰Ù≤ê¢∞¢6fRÜ∂ñÊB¬Fñ∆ˆr‰fñ∆TÊ÷Rì∞¢&WGW&‚Fñ∆ˆr‰fñ∆TÊ÷S∞¢–¢–¢&WGW&‚"#∞¢–†¢V&∆ñ27FFñ27G&ñÊr&VE6fVBÖ6∆ñ6W$∂ñÊB∂ñÊBê¢∞¢G'ê¢∞¢W6ñÊrÖ&Vvó7G'î∂Wí∂Wí“&Vvó7G'í‰7W'&VÁEW6W"‰˜VÂ7V$∂WíÖ&Vvó7G'ï6WGFñÊw2¬f«6Ríê¢∞¢ñbÜ∂Wí”“ÁV∆¬ê¢&WGW&‚"#∞¢ˆ&¶V7Bf«VR“∂Wí‰vWEf«VRÖf«VTÊ÷RÜ∂ñÊBí¬""ì∞¢&WGW&‚f«VR”“ÁV∆¬Ú""¢f«VRÂFı7G&ñÊrÇì∞¢–¢–¢6F6Ç≤&WGW&‚"#≤–¢–†¢V&∆ñ27FFñ2fˆñB6fRÖ6∆ñ6W$∂ñÊB∂ñÊB¬7G&ñÊrFÇê¢∞¢W6ñÊrÖ&Vvó7G'î∂Wí∂Wí“&Vvó7G'í‰7W'&VÁEW6W"‰7&VFU7V$∂WíÖ&Vvó7G'ï6WGFñÊw2íê¢∂WíÂ6WEf«VRÖf«VTÊ÷RÜ∂ñÊBí¬FÇ”“ÁV∆¬Ú""¢FÇ¬&Vvó7G'ïf«VT∂ñÊBÂ7G&ñÊrì∞¢–†¢&ófFR7FFñ27G&ñÊrWFÙFWFV7BÖ6∆ñ6W$∂ñÊB∂ñÊBê¢∞¢7G&ñÊrWÜTÊ÷S∞¢7G&ñÊuµ“6ÊFñFFW3∞¢7G&ñÊr&ˆw&‘fñ∆W2“VÁfó&ˆÊ÷VÁB‰vWDfˆ∆FW%FÇÑVÁfó&ˆÊ÷VÁBÂ7V6ñƒfˆ∆FW"Â&ˆw&‘fñ∆W2ì∞¢7G&ñÊr∆ˆ6¬“VÁfó&ˆÊ÷VÁB‰vWDfˆ∆FW%FÇÑVÁfó&ˆÊ÷VÁBÂ7V6ñƒfˆ∆FW"‰∆ˆ6ƒ∆ñ6Fñˆ‰FFì∞†¢ñbÜ∂ñÊB”“6∆ñ6W$∂ñÊB‰˜&6ê¢∞¢WÜTÊ÷R“&˜&6◊6∆ñ6W"ÊWÜR#∞¢6ÊFñFFW2“ÊWr7G&ñÊuµ“∞¢FÇ‰6ˆ÷&ñÊRá&ˆw&‘fñ∆W2¬$˜&66∆ñ6W%∆˜&6◊6∆ñ6W"ÊWÜR"í¿¢FÇ‰6ˆ÷&ñÊRá&ˆw&‘fñ∆W2¬$˜&66∆ñ6W%∆˜&6◊6∆ñ6W"ÊWÜR"í¿¢FÇ‰6ˆ÷&ñÊRÜ∆ˆ6¬¬%&ˆw&◊5ƒ˜&66∆ñ6W%∆˜&6◊6∆ñ6W"ÊWÜR"í¿¢FÇ‰6ˆ÷&ñÊRÜ∆ˆ6¬¬%&ˆw&◊5ƒ˜&66∆ñ6W%∆˜&6◊6∆ñ6W"ÊWÜR"ê¢”∞¢–¢V«6RñbÜ∂ñÊB”“6∆ñ6W$∂ñÊB‰&÷'Rê¢∞¢WÜTÊ÷R“&&÷'R◊7GVFñÚÊWÜR#∞¢6ÊFñFFW2“ÊWr7G&ñÊuµ“∞¢FÇ‰6ˆ÷&ñÊRá&ˆw&‘fñ∆W2¬$&÷'R7GVFñı∆&÷'R◊7GVFñÚÊWÜR"í¿¢FÇ‰6ˆ÷&ñÊRÜ∆ˆ6¬¬%&ˆw&◊5ƒ&÷'R7GVFñı∆&÷'R◊7GVFñÚÊWÜR"í¿¢FÇ‰6ˆ÷&ñÊRÜ∆ˆ6¬¬$&÷'U7GVFñı∆&÷'R◊7GVFñÚÊWÜR"ê¢”∞¢–¢V«6P¢∞¢WÜTÊ÷R“''W6◊6∆ñ6W"ÊWÜR#∞¢6ÊFñFFW2“ÊWr7G&ñÊuµ“∞¢FÇ‰6ˆ÷&ñÊRá&ˆw&‘fñ∆W2¬%'W64E≈'W66∆ñ6W%«'W6◊6∆ñ6W"ÊWÜR"í¿¢FÇ‰6ˆ÷&ñÊRá&ˆw&‘fñ∆W2¬%'W66∆ñ6W%«'W6◊6∆ñ6W"ÊWÜR"í¿¢FÇ‰6ˆ÷&ñÊRÜ∆ˆ6¬¬%&ˆw&◊5≈'W66∆ñ6W%«'W6◊6∆ñ6W"ÊWÜR"ê¢”∞¢–†¢7G&ñÊrg&ˆ’&Vvó7G'í“fñÊDFÇÜWÜTÊ÷Rì∞¢ñbÑfñ∆R‰WÜó7G2Üg&ˆ’&Vvó7G'ííê¢&WGW&‚g&ˆ’&Vvó7G'ì∞†¢ñÁBì∞¢f˜"Üí“≤í¬6ÊFñFFW2‰∆VÊwFÉ≤í≤≤ê¢∞¢ñbÑfñ∆R‰WÜó7G2Ü6ÊFñFFW5∂ï“íê¢&WGW&‚6ÊFñFFW5∂ï”∞¢–¢&WGW&‚"#∞¢–†¢&ófFR7FFñ27G&ñÊrfñÊDFÇá7G&ñÊrWÜTÊ÷Rê¢∞¢7G&ñÊr7V"“%4ÙeEt$Uƒ÷ñ7&˜6ˆgE≈vñÊF˜w5ƒ7W'&VÁEfW'6ñˆÂƒFá5¬"≤WÜTÊ÷S∞¢&Vvó7G'îÜófUµ“ÜófW2“ÊWr&Vvó7G'îÜófUµ“≤&Vvó7G'îÜófR‰7W'&VÁEW6W"¬&Vvó7G'îÜófR‰∆ˆ6ƒ÷6ÜñÊR”∞¢&Vvó7G'ïfñWuµ“fñWw2“ÊWr&Vvó7G'ïfñWuµ“≤&Vvó7G'ïfñWrÂ&Vvó7G'ìcB¬&Vvó7G'ïfñWrÂ&Vvó7G'ì3"”∞¢ñÁBÉ∞¢ñÁBc∞†¢f˜"ÜÇ“≤Ç¬ÜófW2‰∆VÊwFÉ≤Ç≤≤ê¢∞¢f˜"áb“≤b¬fñWw2‰∆VÊwFÉ≤b≤≤ê¢∞¢G'ê¢∞¢W6ñÊrÖ&Vvó7G'î∂Wí&ˆ˜B“&Vvó7G'î∂Wí‰˜V‰&6T∂WíÜÜófW5∂Ö“¬fñWw5∑e“íê¢W6ñÊrÖ&Vvó7G'î∂Wí∂Wí“&ˆ˜B‰˜VÂ7V$∂Wíá7V"¬f«6Ríê¢∞¢ñbÜ∂Wí“ÁV∆¬ê¢∞¢ˆ&¶V7Bf«VR“∂Wí‰vWEf«VRÜÁV∆¬ì∞¢ñbáf«VR“ÁV∆¬bbfñ∆R‰WÜó7G2áf«VRÂFı7G&ñÊrÇííê¢&WGW&‚f«VRÂFı7G&ñÊrÇì∞¢–¢–¢–¢6F6Ç≤–¢–¢–¢&WGW&‚"#∞¢–†¢V&∆ñ27FFñ27G&ñÊrFó7∆îÊ÷RÖ6∆ñ6W$∂ñÊB∂ñÊBê¢∞¢ñbÜ∂ñÊB”“6∆ñ6W$∂ñÊB‰˜&6í&WGW&‚$˜&66∆ñ6W"#∞¢ñbÜ∂ñÊB”“6∆ñ6W$∂ñÊB‰&÷'Rí&WGW&‚$&÷'R7GVFñÚ#∞¢&WGW&‚%'W66∆ñ6W"#∞¢–†¢&ófFR7FFñ27G&ñÊrf«VTÊ÷RÖ6∆ñ6W$∂ñÊB∂ñÊBê¢∞¢ñbÜ∂ñÊB”“6∆ñ6W$∂ñÊB‰˜&6í&WGW&‚$˜&6FÇ#∞¢ñbÜ∂ñÊB”“6∆ñ6W$∂ñÊB‰&÷'Rí&WGW&‚$&÷'UFÇ#∞¢&WGW&‚%'W6FÇ#∞¢–¢–ß–