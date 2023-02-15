using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ECommons.ImGuiMethods;
using FFTriadBuddy;
using ImGuiNET;
using PunishLib.ImGuiMethods;
using Saucy.CuffACur;
using Saucy.TripleTriad;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using TriadBuddyPlugin;


namespace Saucy
{
    using System.Globalization;
    using NPOI.SS.UserModel;
    using NPOI.XSSF.Model;
    using NPOI.XSSF.UserModel;

    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    public class PluginUI : IDisposable
    {
        private Configuration configuration;

        // this extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;
        public bool Visible
        {
            get { return visible; }
            set { visible = value; }
        }

        private bool settingsVisible = false;
        private GameNpcInfo currentNPC;

        public bool SettingsVisible
        {
            get { return settingsVisible; }
            set { settingsVisible = value; }
        }

        public GameNpcInfo CurrentNPC
        {
            get => currentNPC;
            set
            {
                if (currentNPC != value)
                {
                    TriadAutomater.TempCardsWonList.Clear();
                    currentNPC = value;
                }
            }
        }

        public PluginUI(Configuration configuration)
        {
            this.configuration = Service.Configuration;
        }

        public void Dispose()
        {
        }

        public bool Enabled { get; set; } = false;

        public void Draw()
        {
            DrawMainWindow();
        }

        public void DrawMainWindow()
        {
            if (!Visible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(520, 420), ImGuiCond.FirstUseEver);
            //ImGui.SetNextWindowSizeConstraints(new Vector2(520, 420), new Vector2(float.MaxValue, float.MaxValue));
            if (ImGui.Begin("Saucy Config", ref visible))
            {
                if (ImGui.BeginTabBar("Games"))
                {
                    if (ImGui.BeginTabItem("Cuff-a-Cur"))
                    {
                        DrawCufTab();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Triple Triad"))
                    {
                        DrawTriadTab();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Stats"))
                    {
                        DrawStatsTab();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("About"))
                    {
                        AboutTab.Draw(Saucy.P);
                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }
            }
            ImGui.End();
        }

        private void DrawStatsTab()
        {
            if (ImGui.BeginTabBar("Stats"))
            {
                if (ImGui.BeginTabItem("Lifetime"))
                {
                    this.DrawStatsTab(Service.Configuration.Stats, out bool reset);

                    if (reset)
                    {
                        Service.Configuration.Stats = new();
                        Service.Configuration.Save();
                    }

                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Session"))
                {
                    this.DrawStatsTab(Service.Configuration.SessionStats, out bool reset);
                    if (reset)
                        Service.Configuration.SessionStats = new();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }

        private void DrawStatsTab(Stats stat, out bool reset)
        {
            ImGui.Columns(3, "stats", false);
            ImGui.NextColumn();
            ImGuiEx.CenterColumnText(ImGuiColors.ParsedGold, "SAUCY STATS", true);
            ImGui.Columns(1);
            ImGui.BeginChild("TT Stats", new Vector2(0, ImGui.GetContentRegionAvail().Y - 30f), true);
            ImGui.Columns(3, null, false);
            ImGui.NextColumn();
            ImGuiEx.CenterColumnText(ImGuiColors.DalamudRed, "Triple Triad", true);
            ImGuiHelpers.ScaledDummy(10f);
            ImGui.NextColumn();
            ImGui.NextColumn();
            ImGui.NextColumn();
            ImGuiEx.CenterColumnText("Games Played", true);
            ImGui.NextColumn();
            ImGui.NextColumn();
            ImGui.NextColumn();
            ImGuiEx.CenterColumnText($"{stat.GamesPlayedWithSaucy.ToString("N0")}");
            ImGui.NextColumn();
            ImGui.NextColumn();
            ImGui.Spacing();
            ImGuiEx.CenterColumnText("Wins", true);
            ImGui.NextColumn();
            ImGuiEx.CenterColumnText("Losses", true);
            ImGui.NextColumn();
            ImGuiEx.CenterColumnText("Draws", true);
            ImGui.NextColumn();
            ImGuiEx.CenterColumnText($"{stat.GamesWonWithSaucy.ToString("N0")}");
            ImGui.NextColumn();
            ImGuiEx.CenterColumnText($"{stat.GamesLostWithSaucy.ToString("N0")}");
            ImGui.NextColumn();
            ImGuiEx.CenterColumnText($"{stat.GamesDrawnWithSaucy.ToString("N0")}");
            ImGui.NextColumn();
            ImGuiEx.CenterColumnText("Win Rate", true);
            ImGui.NextColumn();
            ImGuiEx.CenterColumnText("Cards Won", true);
            ImGui.NextColumn();
            if (stat.NPCsPlayed.Count > 0)
            {
                ImGuiEx.CenterColumnText("Most Played NPC", true);
                ImGui.NextColumn();
            }
            else
            {
                ImGui.NextColumn();
            }

            if (stat.GamesPlayedWithSaucy > 0)
            {
                ImGuiEx.CenterColumnText($"{Math.Round(((double)stat.GamesWonWithSaucy / (double)stat.GamesPlayedWithSaucy) * 100, 2)}%");
            }
            else
            {
                ImGuiEx.CenterColumnText("");
            }
            ImGui.NextColumn();
            ImGuiEx.CenterColumnText($"{stat.CardsDroppedWithSaucy.ToString("N0")}");
            ImGui.NextColumn();

            if (stat.NPCsPlayed.Count > 0)
            {
                ImGuiEx.CenterColumnText($"{stat.NPCsPlayed.OrderByDescending(x => x.Value).First().Key}");
                ImGuiEx.CenterColumnText($"{stat.NPCsPlayed.OrderByDescending(x => x.Value).First().Value.ToString("N0")} times");
                ImGui.NextColumn();
                ImGui.NextColumn();
                ImGui.NextColumn();
            }

            ImGui.NextColumn();
            ImGuiEx.CenterColumnText("MGP Won", true);
            ImGui.NextColumn();
            ImGuiEx.CenterColumnText("Total Card Drop Value", true);
            ImGui.NextColumn();
            if (stat.CardsWon.Count > 0)
            {
                ImGuiEx.CenterColumnText("Most Won Card", true);
            }
            ImGui.NextColumn();
            ImGuiEx.CenterColumnText($"{stat.MGPWon.ToString("N0")} MGP");
            ImGui.NextColumn();
            ImGuiEx.CenterColumnText($"{GetDroppedCardValues(stat).ToString("N0")} MGP");
            ImGui.NextColumn();
            if (stat.CardsWon.Count > 0)
            {
                ImGuiEx.CenterColumnText($"{TriadCardDB.Get().FindById((int)stat.CardsWon.OrderByDescending(x => x.Value).First().Key).Name.GetLocalized()}");
                ImGui.NextColumn();
                ImGui.NextColumn();
                ImGui.NextColumn();
                ImGuiEx.CenterColumnText($"{stat.CardsWon.OrderByDescending(x => x.Value).First().Value.ToString("N0")} times");
            }
            ImGui.Columns(1);

            if (ImGui.CollapsingHeader("Recorded Matches"))
            {

                if (ImGui.BeginTable("RecordedMatches", 8, ImGuiTableFlags.ScrollY | ImGuiTableFlags.Borders | ImGuiTableFlags.Reorderable | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Hideable))
                {
                    ImGui.TableSetupColumn("Date",      ImGuiTableColumnFlags.WidthFixed, 120);
                    ImGui.TableSetupColumn("NPC",       ImGuiTableColumnFlags.WidthStretch, 1.4f);
                    ImGui.TableSetupColumn("Result",    ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("MGP",       ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("CardWon",   ImGuiTableColumnFlags.WidthStretch, 1.3f);
                    ImGui.TableSetupColumn("Time",      ImGuiTableColumnFlags.WidthFixed, 35f);
                    ImGui.TableSetupColumn("Deck",      ImGuiTableColumnFlags.WidthFixed, 25f);
                    ImGui.TableSetupColumn("DeckCards", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupScrollFreeze(0, 1);
                    ImGui.TableHeadersRow();

                    for (int i = stat.recordedMatches.Count - 1; i >= 0; i--)
                    {
                        TriadMatchRecord matchRecord = stat.recordedMatches[i];
                        ImGui.TableNextColumn();
                        ImGui.Text(matchRecord.date.ToString(CultureInfo.CurrentCulture));
                        ImGui.TableNextColumn();
                        ImGui.Text(matchRecord.npc);
                        ImGui.TableNextColumn();
                        ImGui.Text(matchRecord.result.ToString());
                        ImGui.TableNextColumn();
                        ImGui.Text(matchRecord.mgp.ToString());
                        ImGui.TableNextColumn();
                        ImGui.Text(matchRecord.GetWonCard()?.Name.GetLocalized() ?? string.Empty);
                        ImGui.TableNextColumn();
                        ImGui.Text(matchRecord.time.ToString("F2") + "s");
                        ImGui.TableNextColumn();
                        ImGui.Text((matchRecord.deck + 1).ToString());
                        ImGui.TableNextColumn();
                        ImGui.Text(string.Join(" | ", matchRecord.GetResolvedDeckCards().Select(tc => tc.Name.GetLocalized())));
                    }

                    ImGui.EndTable();
                }

                if (ImGui.Button("Export to .csv"))
                {
                    IWorkbook  wb        = new XSSFWorkbook();
                    
                    ISheet    sheet     = wb.CreateSheet("Recorded Matches");
                    IRow      headerRow = sheet.CreateRow(0);
                    
                    headerRow.CreateCell(0).SetCellValue("Date");
                    headerRow.CreateCell(1).SetCellValue("NPC");
                    headerRow.CreateCell(2).SetCellValue("Result");
                    headerRow.CreateCell(3).SetCellValue("MGP");
                    headerRow.CreateCell(4).SetCellValue("CardWon");
                    headerRow.CreateCell(5).SetCellValue("Time");
                    headerRow.CreateCell(6).SetCellValue("Deck");
                    headerRow.CreateCell(7).SetCellValue("DeckCards");

                    for (int i = 0; i < stat.recordedMatches.Count; i++)
                    {
                        IRow             row         = sheet.CreateRow(i + 1);
                        TriadMatchRecord matchRecord = stat.recordedMatches[i];
                        row.CreateCell(0).SetCellValue(matchRecord.date.ToString(CultureInfo.CurrentCulture));
                        row.CreateCell(1).SetCellValue(matchRecord.npc);
                        row.CreateCell(2).SetCellValue(matchRecord.result.ToString());
                        row.CreateCell(3, CellType.Numeric).SetCellValue(matchRecord.mgp);
                        row.CreateCell(4).SetCellValue(matchRecord.GetWonCard()?.Name.GetLocalized() ?? string.Empty);
                        row.CreateCell(5, CellType.Numeric).SetCellValue(matchRecord.time);
                        row.CreateCell(6, CellType.Numeric).SetCellValue((matchRecord.deck + 1));
                        row.CreateCell(7).SetCellValue(string.Join(" | ", matchRecord.GetResolvedDeckCards().Select(tc => tc.Name.GetLocalized())));
                    }

                    sheet.ForceFormulaRecalculation = true;
                    FileStream fs = File.Create(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Saucy.xlsx"));
                    wb.Write(fs, false);
                    fs.Close();
                }
            }

            ImGui.EndChild();
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
            reset = ImGui.Button("RESET STATS (Hold Ctrl)", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y)) && ImGui.GetIO().KeyCtrl;
        }

        private int GetDroppedCardValues(Stats stat)
        {
            int output = 0;
            foreach (var card in stat.CardsWon)
                output += GameCardDB.Get().FindById((int)card.Key).SaleValue * stat.CardsWon[card.Key];

            return output;
        }

        public void DrawTriadTab()
        {
            bool enabled = TriadAutomater.ModuleEnabled;

            ImGui.TextWrapped(@"How to use: Challenge an NPC you wish to play cards with. Once you have initiated the challenge, click ""Enable Triad Module"".");
            ImGui.Separator();

            if (ImGui.Checkbox("Enable Triad Module", ref enabled))
            {
                TriadAutomater.ModuleEnabled = enabled;
            }

            bool autoOpen = configuration.OpenAutomatically;

            if (ImGui.Checkbox("Open Saucy When Challenging an NPC", ref autoOpen))
            {
                configuration.OpenAutomatically= autoOpen;
                configuration.Save();
            }

            int selectedDeck = configuration.SelectedDeckIndex;

            if (Saucy.TTSolver.profileGS.GetPlayerDecks().Count() > 0)
            {
                if (!Service.Configuration.UseRecommendedDeck)
                {
                    ImGui.PushItemWidth(200);
                    string preview = selectedDeck >= 0 ? Saucy.TTSolver.profileGS.GetPlayerDecks()[selectedDeck].name : string.Empty;
                    if (ImGui.BeginCombo("Select Deck", preview))
                    {
                        if (ImGui.Selectable(""))
                        {
                            configuration.SelectedDeckIndex = -1;
                        }

                        foreach (var deck in Saucy.TTSolver.profileGS.GetPlayerDecks())
                        {
                            var index = deck.id;
                            //var index = Saucy.TTSolver.preGameDecks.Where(x => x.Value == deck).First().Key;
                            if (ImGui.Selectable(deck.name, index == selectedDeck))
                            {
                                configuration.SelectedDeckIndex = index;
                                configuration.Save();
                            }
                        }

                        ImGui.EndCombo();
                    }

                    ImGui.SameLine();
                }
                bool useAutoDeck = Service.Configuration.UseRecommendedDeck;
                if (ImGui.Checkbox("Automatically choose your deck with the best win chance", ref useAutoDeck))
                {
                    Service.Configuration.UseRecommendedDeck = useAutoDeck;
                    Service.Configuration.Save();
                }
            }
            else
            {
                ImGui.TextWrapped("Please initiate a challenge with an NPC to populate your deck list.");
            }

            if (ImGui.Checkbox("Play X Amount of Times", ref TriadAutomater.PlayXTimes) && (TriadAutomater.NumberOfTimes <= 0 || TriadAutomater.PlayUntilCardDrops || TriadAutomater.PlayUntilAllCardsDropOnce))
            {
                TriadAutomater.NumberOfTimes = 1;
                TriadAutomater.PlayUntilCardDrops = false;
                TriadAutomater.PlayUntilAllCardsDropOnce = false;
            }

            if (ImGui.Checkbox("Play Until Any Cards Drop", ref TriadAutomater.PlayUntilCardDrops) && (TriadAutomater.NumberOfTimes <= 0 || TriadAutomater.PlayXTimes || TriadAutomater.PlayUntilAllCardsDropOnce))
            {
                TriadAutomater.NumberOfTimes = 1;
                TriadAutomater.PlayXTimes = false;
                TriadAutomater.PlayUntilAllCardsDropOnce = false;
            }


            if (GameNpcDB.Get().mapNpcs.TryGetValue(Saucy.TTSolver.preGameNpc?.Id ?? -1, out var npcInfo))
            {
                CurrentNPC = npcInfo;
            }
            else
            {
                CurrentNPC = null;
            }

            if (ImGui.Checkbox($"Play Until All Cards Drop from NPC at Least X Times {(CurrentNPC is null ? "" : $"({TriadNpcDB.Get().FindByID(CurrentNPC.npcId).Name.GetLocalized()})")}", ref TriadAutomater.PlayUntilAllCardsDropOnce))
            {
                TriadAutomater.TempCardsWonList.Clear();
                TriadAutomater.PlayUntilCardDrops = false;
                TriadAutomater.PlayXTimes = false;
                TriadAutomater.NumberOfTimes = 1;
            }

            bool onlyUnobtained = Service.Configuration.OnlyUnobtainedCards;

            if (TriadAutomater.PlayUntilAllCardsDropOnce)
            {
                ImGui.SameLine();
                if (ImGui.Checkbox("Only Unobtained Cards", ref onlyUnobtained))
                {
                    TriadAutomater.TempCardsWonList.Clear();
                    Service.Configuration.OnlyUnobtainedCards = onlyUnobtained;
                    Service.Configuration.Save();
                }
            }

            if (TriadAutomater.PlayUntilAllCardsDropOnce && CurrentNPC != null)
            {
                ImGui.Indent();
                GameCardDB.Get().Refresh();
                foreach (var card in CurrentNPC.rewardCards)
                {
                    if ((Service.Configuration.OnlyUnobtainedCards && !GameCardDB.Get().FindById(card).IsOwned) || !Service.Configuration.OnlyUnobtainedCards)
                    {
                        TriadAutomater.TempCardsWonList.TryAdd((uint)card, 0);
                        ImGui.Text($"- {TriadCardDB.Get().FindById((int)GameCardDB.Get().FindById(card).CardId).Name.GetLocalized()} {TriadAutomater.TempCardsWonList[(uint)card]}/{TriadAutomater.NumberOfTimes}");
                    }

                }

                if (Service.Configuration.OnlyUnobtainedCards && TriadAutomater.TempCardsWonList.Count == 0)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                    ImGui.TextWrapped($@"You already have all cards from this NPC. This feature will not work until you untick ""Only Unobtained Cards"" or choose a different NPC.");
                    ImGui.PopStyleColor();
                }
                ImGui.Unindent();
            }


            if (TriadAutomater.PlayXTimes || TriadAutomater.PlayUntilCardDrops || TriadAutomater.PlayUntilAllCardsDropOnce)
            {
                ImGui.PushItemWidth(150f);
                ImGui.Text("How many times:");
                ImGui.SameLine();

                if (ImGui.InputInt("###NumberOfTimes", ref TriadAutomater.NumberOfTimes))
                {
                    if (TriadAutomater.NumberOfTimes <= 0)
                        TriadAutomater.NumberOfTimes = 1;
                }

                ImGui.Checkbox("Log out after finishing", ref TriadAutomater.LogOutAfterCompletion);

                bool playSound = Service.Configuration.PlaySound;

                ImGui.Columns(2, null, false);
                if (ImGui.Checkbox("Play sound upon completion", ref playSound))
                {
                    Service.Configuration.PlaySound = playSound;
                    Service.Configuration.Save();
                }

                if (playSound)
                {
                    ImGui.NextColumn();
                    ImGui.Text("Select Sound");
                    if (ImGui.BeginCombo("###SelectSound", Service.Configuration.SelectedSound))
                    {
                        string path = Path.Combine(Service.Interface.AssemblyLocation.Directory.FullName, "Sounds");
                        foreach (var file in new DirectoryInfo(path).GetFiles())
                        {
                            if (ImGui.Selectable($"{Path.GetFileNameWithoutExtension(file.FullName)}", Service.Configuration.SelectedSound == Path.GetFileNameWithoutExtension(file.FullName)))
                            {
                                Service.Configuration.SelectedSound = Path.GetFileNameWithoutExtension(file.FullName);
                                Service.Configuration.Save();
                            }
                        }

                        ImGui.EndCombo();
                    }

                    if (ImGui.Button("Open Sound Folder"))
                    {
                        Process.Start("explorer.exe", @$"{Path.Combine(Service.Interface.AssemblyLocation.Directory.FullName, "Sounds")}");
                    }
                    ImGuiComponents.HelpMarker("Drop any MP3 files into the sound folder to add your own custom sounds.");
                }
                ImGui.Columns(1);
            }
        }


        public void DrawCufTab()
        {
            bool enabled = CufModule.ModuleEnabled;

            ImGui.TextWrapped(@"How to use: Click ""Enable Cuff Module"" then walk up to a Cuff-a-cur machine.");
            ImGui.Separator();

            if (ImGui.Checkbox("Enable Cuff Module", ref enabled))
            {
                CufModule.ModuleEnabled = enabled;
            }
        }
    }
}
