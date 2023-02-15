using ClickLib;
using ClickLib.Clicks;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Plugin;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using MgAl2O4.Utils;
using NAudio.Wave;
using PunishLib;
using PunishLib.Sponsor;
using Saucy.CuffACur;
using Saucy.TripleTriad;
using System;
using System.IO;
using System.Linq;
using TriadBuddyPlugin;
using static ECommons.GenericHelpers;

namespace Saucy
{
    public sealed class Saucy : IDalamudPlugin
    {
        public string Name => "Saucy";
        internal static Saucy P;

        private const string commandName = "/saucy";
        public static PluginUI PluginUi { get; set; }

        public static Solver TTSolver = new();

        public static UIReaderTriadGame uiReaderGame;
        public static UIReaderTriadPrep uiReaderPrep;
        public static UIReaderTriadCardList uiReaderCardList;
        public static UIReaderTriadDeckEdit uiReaderDeckEdit;
        public static UIReaderScheduler uiReaderScheduler;
        public static StatTracker statTracker;
        public static GameDataLoader dataLoader;

        public static bool GameFinished => TTSolver.cachedScreenState == null;

        public Saucy([RequiredVersion("1.0")] DalamudPluginInterface pluginInterface, [RequiredVersion("1.0")] CommandManager commandManager, GameGui gameGui, DataManager dataManager)
        {
            pluginInterface.Create<Service>();
            Service.Plugin = this;

            Service.Configuration = Service.Interface.GetPluginConfig() as Configuration ?? new Configuration();
            Service.Configuration.Initialize(Service.Interface);

            ECommonsMain.Init(Service.Interface, this);
            PunishLibMain.Init(Service.Interface, this);
            SponsorManager.SetSponsorInfo("https://ko-fi.com/taurenkey");
            P = this;

            PluginUi = new PluginUI(Service.Configuration);

            Service.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the Saucy menu."
            });

            Service.Interface.UiBuilder.Draw += DrawUI;
            Service.Interface.UiBuilder.OpenConfigUi += DrawConfigUI;

            dataLoader = new GameDataLoader();
            dataLoader.StartAsyncWork(dataManager);

            TTSolver.profileGS = new UnsafeReaderProfileGS(gameGui);

            uiReaderGame = new UIReaderTriadGame(gameGui);
            uiReaderGame.OnUIStateChanged += TTSolver.UpdateGame;

            uiReaderPrep = new UIReaderTriadPrep(gameGui);
            uiReaderPrep.shouldScanDeckData = (TTSolver.profileGS == null) || TTSolver.profileGS.HasErrors;
            uiReaderPrep.OnUIStateChanged += TTSolver.UpdateDecks;

            uiReaderCardList = new UIReaderTriadCardList(gameGui);

            var uiReaderMatchResults = new UIReaderTriadResults(gameGui);
            uiReaderMatchResults.OnUpdated += CheckResults;

            uiReaderScheduler = new UIReaderScheduler(gameGui);
            uiReaderScheduler.AddObservedAddon(uiReaderGame);
            uiReaderScheduler.AddObservedAddon(uiReaderPrep.uiReaderMatchRequest);
            uiReaderScheduler.AddObservedAddon(uiReaderPrep.uiReaderDeckSelect);
            uiReaderScheduler.AddObservedAddon(uiReaderMatchResults);

            var memReaderTriadFunc = new UnsafeReaderTriadCards(Service.SigScanner);
            GameCardDB.Get().memReader = memReaderTriadFunc;
            GameNpcDB.Get().memReader = memReaderTriadFunc;

            Svc.Framework.Update += RunBot;
            Click.Initialize();
        }

        private void CheckResults(UIStateTriadResults obj)
        {
            if (TriadAutomater.ModuleEnabled)
            {
                Service.Configuration.UpdateStats(stats =>
                                                  {
                                                      stats.GamesPlayedWithSaucy++;
                                                      stats.MGPWon += this.GetBonusMGP(obj.numMGP);

                                                      if (stats.NPCsPlayed.TryGetValue(TTSolver.lastGameNpc.Name.GetLocalized(), out int plays))
                                                          stats.NPCsPlayed[TTSolver.lastGameNpc.Name.GetLocalized()] += 1;
                                                      else
                                                          stats.NPCsPlayed.TryAdd(TTSolver.lastGameNpc.Name.GetLocalized(), 1);

                                                      if (obj.isLose)
                                                          stats.GamesLostWithSaucy++;
                                                      if (obj.isDraw)
                                                          stats.GamesDrawnWithSaucy++;
                                                  });

                if (TriadAutomater.PlayXTimes)
                    TriadAutomater.NumberOfTimes--;

                int cardId = -1;

                if (obj.isWin)
                {
                    Service.Configuration.UpdateStats(stats => stats.GamesWonWithSaucy++);
                    if (obj.cardItemId > 0)
                    {
                        if (TriadAutomater.PlayUntilCardDrops)
                            TriadAutomater.NumberOfTimes--;

                        Service.Configuration.UpdateStats(stats => stats.CardsDroppedWithSaucy++);

                        var cardDB = GameCardDB.Get();

                        foreach ((int _, GameCardInfo cardInfo) in cardDB.mapCards)
                        {
                            if (cardInfo.ItemId == obj.cardItemId)
                            {
                                cardId = cardInfo.CardId;
                                Service.Configuration.UpdateStats(stats =>
                                                                  {
                                                                      if (stats.CardsWon.ContainsKey((uint)cardInfo.CardId))
                                                                          stats.CardsWon[(uint)cardInfo.CardId] += 1;
                                                                      else
                                                                          stats.CardsWon[(uint)cardInfo.CardId] = 1;
                                                                  });

                                if (TriadAutomater.TempCardsWonList.ContainsKey((uint)cardInfo.CardId))
                                    TriadAutomater.TempCardsWonList[(uint)cardInfo.CardId] += 1;
                            }
                        }
                    }
                }

                TriadMatchRecord matchRecord = new()
                                               {
                                                   date = TriadAutomater.lastMatchStart,
                                                   result = obj.isWin  ? GameEndState.Won :
                                                            obj.isDraw ? GameEndState.Draw :
                                                                         GameEndState.Lost,
                                                   card = cardId,
                                                   mgp = this.GetBonusMGP(obj.numMGP),
                                                   npc = TTSolver.lastGameNpc.Name.GetLocalized(),
                                                   deck = TriadAutomater.lastPlayedDeck,
                                                   deckCards = TTSolver.preGameDecks[TriadAutomater.lastPlayedDeck].solverDeck.knownCards.Select(tc => tc.Id).ToArray(),
                                                   time = DateTime.Now.Subtract(TriadAutomater.lastMatchStart).TotalSeconds
                                               };

                Service.Configuration.UpdateStats(stats =>
                                                  {
                                                      stats.recordedMatches.Add(matchRecord);
                                                  });

                this.Rematch();
            }

            Service.Configuration.Save();
        }

        private int GetBonusMGP(int numMGP)
        {
            double multiplier = 1;
            //Jackpot
            if (Service.ClientState.LocalPlayer.StatusList.Any(x => x.StatusId == 902))
            {
                multiplier += (double)Service.ClientState.LocalPlayer.StatusList.First(x => x.StatusId == 902).Param / 100;
            }
            if (Service.ClientState.LocalPlayer.StatusList.Any(x => x.StatusId == 1079))
            {
                multiplier += 0.15;
            }

            var bonusMGP = Math.Round(numMGP * multiplier, 0);
            return (int)bonusMGP;
        }

        private unsafe void Rematch()
        {
            try
            {
                if (TryGetAddonByName<AtkUnitBase>("TripleTriadResult", out var addon) && TriadAutomater.ModuleEnabled)
                {
                    if (((TriadAutomater.PlayXTimes || TriadAutomater.PlayUntilCardDrops) && TriadAutomater.NumberOfTimes == 0) || (TriadAutomater.TempCardsWonList.Count > 0 && TriadAutomater.TempCardsWonList.All(x => x.Value >= TriadAutomater.NumberOfTimes)))
                    {
                        addon->Hide(true);
                        return;
                    }

                    var values = stackalloc AtkValue[2];
                    values[0] = new()
                    {
                        Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int,
                        Int = 0,
                    };
                    values[1] = new()
                    {
                        Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.UInt,
                        UInt = 1,
                    };
                    addon->FireCallback(2, values);
                }
            }
            catch { }
        }

        private async void RunBot(Framework framework)
        {
            try
            {
                if (dataLoader.IsDataReady)
                {
                    float deltaSeconds = (float)framework.UpdateDelta.TotalSeconds;
                    uiReaderScheduler.Update(deltaSeconds);
                }
            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex, "state update failed");
            }

            if (Service.Configuration.OpenAutomatically && uiReaderPrep.HasMatchRequestUI && !TriadAutomater.ModuleEnabled)
            {
                Saucy.PluginUi.Visible = true;
            }

            if (CufModule.ModuleEnabled)
            {
                CufModule.RunModule();
                return;
            }
            else
            {
                if (CufModule.DisableInput)
                {
                    CufModule.Inputs.ForceRelease(Dalamud.Game.ClientState.Keys.VirtualKey.NUMPAD0);
                    CufModule.DisableInput = false;
                }
            }

            if (TriadAutomater.ModuleEnabled)
            {
                if (((TriadAutomater.PlayXTimes || TriadAutomater.PlayUntilCardDrops) && TriadAutomater.NumberOfTimes == 0) || (TriadAutomater.TempCardsWonList.Count > 0 && TriadAutomater.TempCardsWonList.All(x => x.Value >= TriadAutomater.NumberOfTimes)))
                {
                    if (Svc.Condition[ConditionFlag.OccupiedInQuestEvent] ||
                        Svc.Condition[ConditionFlag.Occupied33] ||
                        Svc.Condition[ConditionFlag.OccupiedInEvent] ||
                        Svc.Condition[ConditionFlag.Occupied30] ||
                        Svc.Condition[ConditionFlag.Occupied38] ||
                        Svc.Condition[ConditionFlag.Occupied39] ||
                        Svc.Condition[ConditionFlag.OccupiedSummoningBell] ||
                        Svc.Condition[ConditionFlag.WatchingCutscene] ||
                        Svc.Condition[ConditionFlag.Mounting71] ||
                        Svc.Condition[ConditionFlag.CarryingObject])
                    {
                        SkipDialogue();
                    }
                    else
                    {
                        if (Service.Configuration.PlaySound)
                        {
                            PlaySound();
                        }

                        TriadAutomater.PlayXTimes = false;
                        TriadAutomater.PlayUntilCardDrops = false;
                        TriadAutomater.PlayUntilAllCardsDropOnce = false;
                        TriadAutomater.ModuleEnabled = false;
                        TriadAutomater.TempCardsWonList.Clear();


                        if (TriadAutomater.LogOutAfterCompletion)
                        {
                            await Svc.Framework.RunOnTick(() => TriadAutomater.Logout(), TimeSpan.FromMilliseconds(2000));
                            await Svc.Framework.RunOnTick(() => TriadAutomater.SelectYesLogout(), TimeSpan.FromMilliseconds(3500));
                        }
                    }

                    return;
                }

                TriadAutomater.RunModule();
                return;
            }
            else
            {

            }

        }

        private unsafe void SkipDialogue()
        {
            try
            {
                var talk = Svc.GameGui.GetAddonByName("Talk", 1);
                if (talk == IntPtr.Zero) return;
                var talkAddon = (AtkUnitBase*)talk;
                if (!IsAddonReady(talkAddon)) return;
                ClickTalk.Using(talk).Click();
            }
            catch { }
        }

        private readonly object _lockObj = new();

        private void PlaySound()
        {
            lock (_lockObj)
            {
                string sound = Service.Configuration.SelectedSound;
                string path = Path.Combine(Service.Interface.AssemblyLocation.Directory.FullName, "Sounds", $"{sound}.mp3");
                if (!File.Exists(path)) return;
                var reader = new Mp3FileReader(path);
                var waveOut = new WaveOutEvent();

                waveOut.Init(reader);
                waveOut.Play();
            }
        }

        public void Dispose()
        {
            PluginUi.Dispose();
            Service.CommandManager.RemoveHandler(commandName);
            Svc.Framework.Update -= RunBot;

        }

        private void OnCommand(string command, string arguments)
        {
            if (arguments.Length == 0)
                PluginUi.Visible = !PluginUi.Visible;

            var args = arguments.Split();
            if (args.Length > 0)
            {
                if (args[0].ToLower() == "tt")
                {
                    if (args[1].ToLower() == "go")
                    {
                        TriadAutomater.ModuleEnabled = true;
                        Svc.Chat.Print("[Saucy] Triad Module Enabled!");
                        return;
                    }

                    if (args[1].ToLower() == "play")
                    {
                        TriadAutomater.PlayXTimes = true;
                        Svc.Chat.Print("[Saucy] Play X Amount of Times Enabled!");

                        if (int.TryParse(args[2], out int val))
                        {
                            TriadAutomater.NumberOfTimes = val;
                            return;
                        }
                        else
                        {
                            return;
                        }
                    }

                    if (args[1].ToLower() == "cards")
                    {
                        if (args[2].ToLower() == "any")
                        {
                            TriadAutomater.PlayUntilCardDrops = true;
                            Svc.Chat.Print("[Saucy] Play Until Any Cards Drop Enabled!");
                        }
                        if (args[2].ToLower() == "all")
                        {
                            TriadAutomater.PlayUntilAllCardsDropOnce = true;
                            Svc.Chat.Print("[Saucy] Play Until All Cards Drop from NPC at Least X Times Enabled!");
                        }

                        if (int.TryParse(args[3], out int val))
                        {
                            TriadAutomater.NumberOfTimes = val;
                            return;
                        }
                        else
                        {
                            return;
                        }
                    }
                }
            }
        }

        private void DrawUI()
        {
            PluginUi.Draw();
        }

        private void DrawConfigUI()
        {
            PluginUi.Visible = true;
        }
    }


}
