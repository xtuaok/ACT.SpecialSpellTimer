namespace ACT.SpecialSpellTimer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using ACT.SpecialSpellTimer.Properties;
    using ACT.SpecialSpellTimer.Utility;
    using Advanced_Combat_Tracker;

    /// <summary>
    /// SpellTimerの中核
    /// </summary>
    public class SpellTimerCore
    {
        /// <summary>
        /// シングルトンinstance
        /// </summary>
        private static SpellTimerCore instance;

        /// <summary>
        /// シングルトンinstance
        /// </summary>
        public static SpellTimerCore Default
        {
            get
            {
                if (instance == null)
                {
                    instance = new SpellTimerCore();
#if DEBUG
                    Debug.WriteLine("SpellTimerCore");
#endif
                }

                return instance;
            }
        }

        /// <summary>
        /// Refreshタイマ
        /// </summary>
        private System.Windows.Forms.Timer RefreshTimer;

        /// <summary>
        /// RefreshWindowタイマ
        /// </summary>
        private System.Windows.Forms.Timer RefreshWindowTimer;

        /// <summary>
        /// 画面更新のインターバル
        /// </summary>
        private double RefreshInterval;

        /// <summary>
        /// ログバッファ
        /// </summary>
        private LogBuffer LogBuffer;

        /// <summary>
        /// 最後にFF14プロセスをチェックした時間
        /// </summary>
        private DateTime LastFFXIVProcessDateTime;

        /// <summary>
        /// SpellTimerのPanelリスト
        /// </summary>
        private List<SpellTimerListWindow> SpellTimerPanels
        {
            get;
            set;
        }

        /// <summary>
        /// 開始する
        /// </summary>
        public void Begin()
        {
            // 戦闘分析を初期化する
            CombatAnalyzer.Default.Initialize();

            // Panelリストを生成する
            this.SpellTimerPanels = new List<SpellTimerListWindow>();

            // ログバッファを生成する
            this.LogBuffer = new LogBuffer();

            // RefreshWindowタイマを開始する
            this.RefreshWindowTimer = new System.Windows.Forms.Timer()
            {
                Interval = 10
            };

            this.RefreshWindowTimer.Tick += this.RefreshWindowTimerOnTick;
            this.RefreshWindowTimer.Start();

            // Refreshタイマを開始する
            this.RefreshInterval = Settings.Default.RefreshInterval;
            this.RefreshTimer = new System.Windows.Forms.Timer()
            {
                Interval = (int)this.RefreshInterval
            };

            this.RefreshTimer.Tick += this.RefreshTimerOnTick;
            this.RefreshTimer.Start();
        }

        /// <summary>
        /// 終了する
        /// </summary>
        public void End()
        {
            // 戦闘分析を開放する
            CombatAnalyzer.Default.Denitialize();

            // ログバッファを開放する
            if (this.LogBuffer != null)
            {
                this.LogBuffer.Dispose();
                this.LogBuffer = null;
            }

            // 監視を開放する
            if (this.RefreshTimer != null)
            {
                this.RefreshTimer.Stop();
                this.RefreshTimer.Dispose();
                this.RefreshTimer = null;
            }

            if (this.RefreshWindowTimer != null)
            {
                this.RefreshWindowTimer.Stop();
                this.RefreshWindowTimer.Dispose();
                this.RefreshWindowTimer = null;
            }

            // 全てのPanelを閉じる
            this.ClosePanels();
            OnePointTelopController.CloseTelops();

            // 設定を保存する
            Settings.Default.Save();
            SpellTimerTable.Save();
            OnePointTelopTable.Default.Save();

            // instanceを初期化する
            instance = null;
        }

        /// <summary>
        /// RefreshTimerOnTick
        /// </summary>
        /// <param name="sender">イベント発生元</param>
        /// <param name="e">イベント引数</param>
        private void RefreshTimerOnTick(
            object sender,
            EventArgs e)
        {
#if DEBUG
            var sw = Stopwatch.StartNew();
#endif
            try
            {
                if (this.RefreshTimer != null &&
                    this.RefreshTimer.Enabled)
                {
                    this.WatchLog();
                }
            }
            catch (Exception ex)
            {
                ActGlobals.oFormActMain.WriteExceptionLog(
                    ex,
                    Translate.Get("SpellTimerRefreshError"));
            }
            finally
            {
#if DEBUG
                sw.Stop();
                Debug.WriteLine("●Refresh " + sw.Elapsed.TotalMilliseconds.ToString("N4") + "ms");
#endif
                this.RefreshTimer.Interval = (int)this.RefreshInterval;
            }
        }

        /// <summary>
        /// RefreshWindowTimerOnTick
        /// </summary>
        /// <param name="sender">イベント発生元</param>
        /// <param name="e">イベント引数</param>
        private void RefreshWindowTimerOnTick(
            object sender,
            EventArgs e)
        {
            try
            {
                if (this.RefreshWindowTimer != null &&
                    this.RefreshWindowTimer.Enabled)
                {
                    // 有効なスペルとテロップのリストを取得する
                    var spellArray = SpellTimerTable.EnabledTable;
                    var telopArray = OnePointTelopTable.Default.EnabledTable;

                    // 不要なWindowを閉じる
                    OnePointTelopController.GarbageWindows(telopArray);
                    this.GarbageSpellPanelWindows(spellArray);

                    if (ActGlobals.oFormActMain == null ||
                        !ActGlobals.oFormActMain.Visible)
                    {
                        this.HidePanels();
                        return;
                    }

                    if ((DateTime.Now - this.LastFFXIVProcessDateTime).TotalSeconds >= 5.0d)
                    {
                        // FF14が起動していない？
                        if (FF14PluginHelper.GetFFXIVProcess == null)
                        {
                            if (!Settings.Default.OverlayForceVisible)
                            {
                                this.ClosePanels();
                                OnePointTelopController.CloseTelops();
                                return;
                            }
                        }

                        this.LastFFXIVProcessDateTime = DateTime.Now;
                    }

                    // オーバーレイが非表示？
                    if (!Settings.Default.OverlayVisible)
                    {
                        this.HidePanels();
                        OnePointTelopController.HideTelops();
                        return;
                    }

                    // テロップWindowを表示する
                    OnePointTelopController.RefreshTelopWindows(telopArray);

                    // スペルWindowを表示する
                    this.RefreshSpellPanelWindows(spellArray);
                }
            }
            catch (Exception ex)
            {
                ActGlobals.oFormActMain.WriteExceptionLog(
                    ex,
                    Translate.Get("SpellTimerRefreshError"));
            }
        }

        /// <summary>
        /// ログを監視する
        /// </summary>
        private void WatchLog()
        {
#if DEBUG
            var sw1 = Stopwatch.StartNew();
#endif
            // 有効なスペルとテロップのリストを取得する
            var spellArray = SpellTimerTable.EnabledTable;
            var telopArray = OnePointTelopTable.Default.EnabledTable;

#if DEBUG
            sw1.Stop();
            Debug.WriteLine("Refresh get spells & telops ->" + sw1.Elapsed.TotalMilliseconds.ToString("N4") + "ms");
#endif

            // ACTが起動していない？
#if DEBUG
            var sw7 = Stopwatch.StartNew();
#endif
            if (ActGlobals.oFormActMain == null ||
                !ActGlobals.oFormActMain.Visible)
            {
                this.RefreshInterval = 1000;
                return;
            }

            if ((DateTime.Now - this.LastFFXIVProcessDateTime).TotalSeconds >= 5.0d)
            {
                // FF14が起動していない？
                if (FF14PluginHelper.GetFFXIVProcess == null)
                {
                    this.RefreshInterval = 1000;

                    if (!Settings.Default.OverlayForceVisible)
                    {
                        return;
                    }
                }

                this.LastFFXIVProcessDateTime = DateTime.Now;
            }

            // タイマの間隔を標準に戻す
            this.RefreshInterval = Settings.Default.RefreshInterval;
#if DEBUG
            sw7.Stop();
            Debug.WriteLine("Refresh Exists FF14 ->" + sw7.Elapsed.TotalMilliseconds.ToString("N4") + "ms");
#endif

            // ログを取り出す
#if DEBUG
            var sw2 = Stopwatch.StartNew();
#endif
            var logLines = this.LogBuffer.GetLogLines();
#if DEBUG
            sw2.Stop();
            Debug.WriteLine("Refresh GetLog ->" + sw2.Elapsed.TotalMilliseconds.ToString("N4") + "ms");
#endif

            if (logLines.Length < 1)
            {
                return;
            }

            var task1 = Task.Run(() =>
            {
                // テロップとマッチングする
#if DEBUG
                var sw3 = Stopwatch.StartNew();
#endif
                OnePointTelopController.Match(
                    telopArray,
                    logLines);
#if DEBUG
                sw3.Stop();
                Debug.WriteLine("Refresh MatchTelop ->" + sw3.Elapsed.TotalMilliseconds.ToString("N4") + "ms");
#endif
            });

            var task2 = Task.Run(() =>
            {
                // スペルリストとマッチングする
#if DEBUG
                var sw4 = Stopwatch.StartNew();
#endif
                this.MatchSpells(
                    spellArray,
                    logLines);
#if DEBUG
                sw4.Stop();
                Debug.WriteLine("Refresh MatchSpell ->" + sw4.Elapsed.TotalMilliseconds.ToString("N4") + "ms");
#endif
            });

#if DEBUG
            var swC = Stopwatch.StartNew();
#endif
            // コマンドとマッチングする
            TextCommandController.MatchCommand(
                logLines);

#if DEBUG
            swC.Stop();
            Debug.WriteLine("Match Command ->" + swC.Elapsed.TotalMilliseconds.ToString("N4") + "ms");
#endif

            task1.Wait();
            task2.Wait();
        }

        /// <summary>
        /// 不要なスペルタイマWindowを閉じる
        /// </summary>
        /// <param name="spells">Spell</param>
        private void GarbageSpellPanelWindows(
            SpellTimer[] spells)
        {
            if (this.SpellTimerPanels != null)
            {
                var removeList = new List<SpellTimerListWindow>();
                foreach (var panel in this.SpellTimerPanels)
                {
                    // パネルの位置を保存する
                    var setting = (
                        from x in PanelSettings.Default.SettingsTable
                        where
                        x.PanelName == panel.PanelName
                        select
                        x).FirstOrDefault();

                    if (setting == null)
                    {
                        setting = PanelSettings.Default.SettingsTable.NewPanelSettingsRow();
                        PanelSettings.Default.SettingsTable.AddPanelSettingsRow(setting);
                    }

                    setting.PanelName = panel.PanelName;
                    setting.Left = panel.Left;
                    setting.Top = panel.Top;

                    // 毎分0秒の時保存する
                    if (DateTime.Now.Second == 0)
                    {
                        PanelSettings.Default.Save();
                    }

                    // スペルリストに存在しないパネルを閉じる
                    if (!spells.Any(x => x.Panel == panel.PanelName))
                    {
                        ActInvoker.Invoke(() => panel.Close());
                        removeList.Add(panel);
                    }
                }

                foreach (var item in removeList)
                {
                    this.SpellTimerPanels.Remove(item);
                }
            }
        }

        /// <summary>
        /// Spellをマッチングする
        /// </summary>
        /// <param name="spells">Spell</param>
        /// <param name="logLines">ログ</param>
        private void MatchSpells(
            SpellTimer[] spells,
            string[] logLines)
        {
            Parallel.ForEach(spells, (spell) =>
            {
                var regex = spell.Regex;
                var notifyNeeded = false;

                // マッチする？
                foreach (var logLine in logLines)
                {
                    // 正規表現が無効？
                    if (!spell.RegexEnabled ||
                        regex == null)
                    {
                        var keyword = spell.KeywordReplaced;
                        if (string.IsNullOrWhiteSpace(keyword))
                        {
                            continue;
                        }

                        // キーワードが含まれるか？
                        if (logLine.ToUpper().Contains(
                            keyword.ToUpper()))
                        {
                            // ヒットしたログを格納する
                            spell.MatchedLog = logLine;

                            spell.SpellTitleReplaced = spell.SpellTitle;
                            spell.MatchDateTime = DateTime.Now;
                            spell.OverDone = false;
                            spell.BeforeDone = false;
                            spell.TimeupDone = false;
                            spell.CompleteScheduledTime = spell.MatchDateTime.AddSeconds(spell.RecastTime);

                            // マッチ時点のサウンドを再生する
                            this.Play(spell.MatchSound);
                            this.Play(spell.MatchTextToSpeak);

                            notifyNeeded = true;
                        }
                    }
                    else
                    {
                        // 正規表現でマッチングする
                        var match = regex.Match(logLine);
                        if (match.Success)
                        {
                            // ヒットしたログを格納する
                            spell.MatchedLog = logLine;

                            // 置換したスペル名を格納する
                            spell.SpellTitleReplaced = match.Result(spell.SpellTitle);

                            spell.MatchDateTime = DateTime.Now;
                            spell.OverDone = false;
                            spell.BeforeDone = false;
                            spell.TimeupDone = false;
                            spell.CompleteScheduledTime = spell.MatchDateTime.AddSeconds(spell.RecastTime);

                            // マッチ時点のサウンドを再生する
                            this.Play(spell.MatchSound);

                            if (!string.IsNullOrWhiteSpace(spell.MatchTextToSpeak))
                            {
                                var tts = match.Result(spell.MatchTextToSpeak);
                                this.Play(tts);
                            }

                            notifyNeeded = true;
                        }
                    }

                    // 延長をマッチングする
                    if (spell.MatchDateTime > DateTime.MinValue)
                    {
                        var keywords = new string[] { spell.KeywordForExtendReplaced1, spell.KeywordForExtendReplaced2 };
                        var regexes = new Regex[] { spell.RegexForExtend1, spell.RegexForExtend2 };
                        var timeToExtends = new long[] { spell.RecastTimeExtending1, spell.RecastTimeExtending2 };

                        for (int i = 0; i < 2; i++)
                        {
                            var keywordToExtend = keywords[i];
                            var regexToExtend = regexes[i];
                            var timeToExtend = timeToExtends[i];

                            // マッチングする
                            var match = false;

                            if (!spell.RegexEnabled ||
                                regexToExtend == null)
                            {
                                if (!string.IsNullOrWhiteSpace(keywordToExtend))
                                {
                                    match = logLine.ToUpper().Contains(keywordToExtend.ToUpper());
                                }
                            }
                            else
                            {
                                match = regexToExtend.Match(logLine).Success;
                            }

                            if (!match)
                            {
                                continue;
                            }

                            var now = DateTime.Now;

                            // リキャストタイムを延長する
                            var newSchedule = spell.CompleteScheduledTime.AddSeconds(timeToExtend);
                            spell.BeforeDone = false;

                            if (spell.ExtendBeyondOriginalRecastTime)
                            {
                                if (spell.UpperLimitOfExtension > 0)
                                {
                                    var newDuration = (newSchedule - now).TotalSeconds;
                                    if (newDuration > (double)spell.UpperLimitOfExtension)
                                    {
                                        newSchedule = newSchedule.AddSeconds(
                                            (newDuration - (double)spell.UpperLimitOfExtension) * -1);
                                    }
                                }
                            }
                            else
                            {
                                var newDuration = (newSchedule - now).TotalSeconds;
                                if (newDuration > (double)spell.RecastTime)
                                {
                                    newSchedule = newSchedule.AddSeconds(
                                        (newDuration - (double)spell.RecastTime) * -1);
                                }
                            }

                            spell.MatchDateTime = now;
                            spell.CompleteScheduledTime = newSchedule;

                            notifyNeeded = true;
                        }
                    }
                    // end if 延長マッチング
                }
                // end foreach マッチング

                // ACT標準のSpellTimerに変更を通知する
                if (notifyNeeded)
                {
                    updateNormalSpellTimer(spell, false);
                    notifyNormalSpellTimer(spell);
                }

                // Repeat対象のSpellを更新する
                if (spell.RepeatEnabled &&
                    spell.MatchDateTime > DateTime.MinValue)
                {
                    if (DateTime.Now >= spell.MatchDateTime.AddSeconds(spell.RecastTime))
                    {
                        spell.MatchDateTime = DateTime.Now;
                        spell.OverDone = false;
                        spell.TimeupDone = false;
                    }
                }

                // ｎ秒後のSoundを再生する
                if (spell.OverTime > 0 &&
                    !spell.OverDone &&
                    spell.MatchDateTime > DateTime.MinValue)
                {
                    var over = spell.MatchDateTime.AddSeconds(spell.OverTime);

                    if (DateTime.Now >= over)
                    {
                        this.Play(spell.OverSound);
                        if (!string.IsNullOrWhiteSpace(spell.OverTextToSpeak))
                        {
                            var tts = spell.RegexEnabled && regex != null ?
                                regex.Replace(spell.MatchedLog, spell.OverTextToSpeak) :
                                spell.OverTextToSpeak;
                            this.Play(tts);
                        }

                        spell.OverDone = true;
                    }
                }

                // リキャストｎ秒前のSoundを再生する
                if (spell.BeforeTime > 0 &&
                    !spell.BeforeDone &&
                    spell.MatchDateTime > DateTime.MinValue)
                {
                    if (spell.CompleteScheduledTime > DateTime.MinValue)
                    {
                        var before = spell.CompleteScheduledTime.AddSeconds(spell.BeforeTime * -1);

                        if (DateTime.Now >= before)
                        {
                            this.Play(spell.BeforeSound);
                            if (!string.IsNullOrWhiteSpace(spell.BeforeTextToSpeak))
                            {
                                var tts = spell.RegexEnabled && regex != null ?
                                    regex.Replace(spell.MatchedLog, spell.BeforeTextToSpeak) :
                                    spell.BeforeTextToSpeak;
                                this.Play(tts);
                            }

                            spell.BeforeDone = true;
                        }
                    }
                }

                // リキャスト完了のSoundを再生する
                if (spell.RecastTime > 0 &&
                    !spell.TimeupDone &&
                    spell.MatchDateTime > DateTime.MinValue)
                {
                    if (spell.CompleteScheduledTime > DateTime.MinValue &&
                        DateTime.Now >= spell.CompleteScheduledTime)
                    {
                        this.Play(spell.TimeupSound);
                        if (!string.IsNullOrWhiteSpace(spell.TimeupTextToSpeak))
                        {
                            var tts = spell.RegexEnabled && regex != null ?
                                regex.Replace(spell.MatchedLog, spell.TimeupTextToSpeak) :
                                spell.TimeupTextToSpeak;
                            this.Play(tts);
                        }

                        spell.TimeupDone = true;
                    }
                }
            }); // end loop spells
        }

        /// <summary>
        /// スペルパネルWindowを更新する
        /// </summary>
        /// <param name="spells">
        /// 対象のスペル</param>
        private void RefreshSpellPanelWindows(
            SpellTimer[] spells)
        {
            var panelNames = spells.Select(x => x.Panel.Trim()).Distinct();
            foreach (var name in panelNames)
            {
                var w = this.SpellTimerPanels.Where(x => x.PanelName == name).FirstOrDefault();
                if (w == null)
                {
                    w = new SpellTimerListWindow()
                    {
                        Title = "SpecialSpellTimer - " + name,
                        PanelName = name,
                    };

                    this.SpellTimerPanels.Add(w);

                    // クリックスルー？
                    if (Settings.Default.ClickThroughEnabled)
                    {
                        w.ToTransparentWindow();
                    }

                    w.Show();
                }

                w.SpellTimers = (
                    from x in spells
                    where
                    x.Panel.Trim() == name
                    select
                    x).ToArray();

                // ドラッグ中じゃない？
                if (!w.IsDragging)
                {
                    w.RefreshSpellTimer();
                }
            }
        }

        /// <summary>
        /// Panelの位置を設定する
        /// </summary>
        /// <param name="panelName">パネルの名前</param>
        /// <param name="left">Left</param>
        /// <param name="top">Top</param>
        public void SetPanelLocation(
            string panelName,
            double left,
            double top)
        {
            if (this.SpellTimerPanels != null)
            {
                var panel = this.SpellTimerPanels
                    .Where(x => x.PanelName == panelName)
                    .FirstOrDefault();

                if (panel != null)
                {
                    panel.Left = left;
                    panel.Top = top;
                }

                var panelSettings = PanelSettings.Default.SettingsTable
                    .Where(x => x.PanelName == panelName)
                    .FirstOrDefault();

                if (panelSettings != null)
                {
                    panelSettings.Left = left;
                    panelSettings.Top = top;
                }
            }
        }

        /// <summary>
        /// Panelの位置を取得する
        /// </summary>
        /// <param name="panelName">パネルの名前</param>
        /// <param name="left">Left</param>
        /// <param name="top">Top</param>
        public void GetPanelLocation(
            string panelName,
            out double left,
            out double top)
        {
            left = 10.0d;
            top = 10.0d;

            if (this.SpellTimerPanels != null)
            {
                var panel = this.SpellTimerPanels
                    .Where(x => x.PanelName == panelName)
                    .FirstOrDefault();

                if (panel != null)
                {
                    left = panel.Left;
                    top = panel.Top;
                }
                else
                {
                    var panelSettings = PanelSettings.Default.SettingsTable
                        .Where(x => x.PanelName == panelName)
                        .FirstOrDefault();

                    if (panelSettings != null)
                    {
                        left = panelSettings.Left;
                        top = panelSettings.Top;
                    }
                }
            }
        }

        /// <summary>
        /// SpellTimer間のマージンを設定する
        /// </summary>
        /// <param name="panelName">パネルの名前</param>
        /// <param name="marign">マージン</param>
        public void SetSpellMargin(
            string panelName,
            int margin)
        {
            if (this.SpellTimerPanels != null)
            {
                var panel = this.FindPanelByName(panelName);
                if (panel != null)
                {
                    panel.SpellMargin = margin;
                }

                var setting = this.FindPanelSettingByName(panelName);
                if (setting != null)
                {
                    setting.Margin = margin;
                }
            }
        }

        /// <summary>
        /// SpellTimer間のマージンを取得する
        /// </summary>
        /// <param name="panelName">パネルの名前</param>
        /// <param name="margin">マージン</param>
        public void GetSpellMargin(
            string panelName,
            out int margin)
        {
            margin = 0;
            
            if (this.SpellTimerPanels != null)
            {
                var panel = this.FindPanelByName(panelName);
                if (panel != null)
                {
                    margin = panel.SpellMargin;
                }
                else
                {
                    var setting = this.FindPanelSettingByName(panelName);
                    if (setting != null)
                    {
                        margin = setting.Margin;
                    }
                }
            }
        }

        /// <summary>
        /// Panelのレイアウトを設定する
        /// </summary>
        /// <param name="panelName">パネルの名前</param>
        /// <param name="horizontal">水平レイアウトか？</param>
        /// <param name="fixedPositionSpell">スペル位置を固定するか？</param>
        public void SetPanelLayout(
            string panelName,
            bool horizontal,
            bool fixedPositionSpell)
        {
            if (this.SpellTimerPanels != null)
            {
                var panel = this.FindPanelByName(panelName);
                if (panel != null)
                {
                    panel.IsHorizontal = horizontal;
                    panel.SpellPositionFixed = fixedPositionSpell;
                }

                var setting = this.FindPanelSettingByName(panelName);
                if (setting != null)
                {
                    setting.Horizontal = horizontal;
                    setting.FixedPositionSpell = fixedPositionSpell;
                }
            }
        }

        /// <summary>
        /// Panelのレイアウトを取得する
        /// </summary>
        /// <param name="panelName">パネルの名前</param>
        /// <param name="horizontal">水平レイアウトか？</param>
        /// <param name="fixedPositionSpell">スペル位置を固定するか？</param>
        public void GetPanelLayout(
            string panelName,
            out bool horizontal,
            out bool fixedPositionSpell)
        {
            horizontal = false;
            fixedPositionSpell = false;

            if (this.SpellTimerPanels != null)
            {
                var panel = this.FindPanelByName(panelName);
                if (panel != null)
                {
                    horizontal = panel.IsHorizontal;
                    fixedPositionSpell = panel.SpellPositionFixed;
                }
                else
                {
                    var setting = this.FindPanelSettingByName(panelName);
                    if (setting != null)
                    {
                        horizontal = setting.Horizontal;
                        fixedPositionSpell = setting.FixedPositionSpell;
                    }
                }
            }
        }

        /// <summary>
        /// SpellTimerListWindowを取得する
        /// </summary>
        /// <param name="panelName">パネルの名前</param>
        private SpellTimerListWindow FindPanelByName(string panelName)
        {
            if (this.SpellTimerPanels != null)
            {
                return this.SpellTimerPanels
                    .Where(x => x.PanelName == panelName)
                    .FirstOrDefault();
            }

            return null;
        }

        /// <summary>
        /// PanelSettingsRowを取得する
        /// </summary>
        /// <param name="panelName">パネルの名前</param>
        private SpellTimerDataSet.PanelSettingsRow FindPanelSettingByName(string panelName)
        {
            if (this.SpellTimerPanels != null)
            {
                return PanelSettings.Default.SettingsTable
                    .Where(x => x.PanelName == panelName)
                    .FirstOrDefault();
            }
            
            return null;
        }

        /// <summary>
        /// 有効なSpellTimerをACT標準のSpellTimerに設定を反映させる
        /// </summary>
        public void applyToNormalSpellTimer()
        {
            // 標準スペルタイマーへの通知が無効であれば何もしない
            if (!Settings.Default.EnabledNotifyNormalSpellTimer)
            {
                return;
            }

            // 設定を一旦すべて削除する
            clearNormalSpellTimer();

            var spells = SpellTimerTable.Table.Where(x => x.Enabled);
            foreach (var spell in spells)
            {
                updateNormalSpellTimer(spell, true);
            }

            var telops = OnePointTelopTable.Default.Table.Where(x => x.Enabled);
            foreach (var telop  in telops)
            {
                updateNormalSpellTimerForTelop(telop, false);
            }

            // ACTのスペルタイマーに変更を反映する
            ActGlobals.oFormSpellTimers.RebuildSpellTreeView();
        }

        /// <summary>
        /// ACT標準のスペルタイマーの設定を追加・更新する
        /// </summary>
        /// <param name="spellTimer">元になるスペルタイマー</param>
        /// <param name="useRecastTime">リキャスト時間にRecastの値を使うか。falseの場合はCompleteScheduledTimeから計算される</param>
        public void updateNormalSpellTimer(SpellTimer spellTimer, bool useRecastTime)
        {
            if (!Settings.Default.EnabledNotifyNormalSpellTimer)
            {
                return;
            }

            var prefix = Settings.Default.NotifyNormalSpellTimerPrefix;
            var spellName = prefix + "spell_" + spellTimer.SpellTitle;
            var categoryName = prefix + spellTimer.Panel;
            var recastTime = useRecastTime ? spellTimer.RecastTime : (spellTimer.CompleteScheduledTime - DateTime.Now).TotalSeconds;

            var timerData = new TimerData(spellName, categoryName);
            timerData.TimerValue = (int)recastTime;
            timerData.RemoveValue = (int)-Settings.Default.TimeOfHideSpell;
            timerData.WarningValue = 0;
            timerData.OnlyMasterTicks = true;
            timerData.Tooltip = spellTimer.SpellTitleReplaced;

            timerData.Panel1Display = false;
            timerData.Panel2Display = false;

            timerData.WarningSoundData = "none"; // disable warning sound

            // initialize other parameters
            timerData.RestrictToMe = false;
            timerData.AbsoluteTiming = false;
            timerData.RestrictToCategory = false;

            ActGlobals.oFormSpellTimers.AddEditTimerDef(timerData);
        }

        /// <summary>
        /// ACT標準のスペルタイマーの設定を追加・更新する（テロップ用）
        /// </summary>
        /// <param name="spellTimer">元になるテロップ</param>
        /// <param name="forceHide">強制非表示か？</param>
        public void updateNormalSpellTimerForTelop(OnePointTelop telop, bool forceHide)
        {
            if (!Settings.Default.EnabledNotifyNormalSpellTimer)
            {
                return;
            }

            var prefix = Settings.Default.NotifyNormalSpellTimerPrefix;
            var spellName = prefix + "telop_" + telop.Title;
            var categoryName = prefix + "telops";

            var timerData = new TimerData(spellName, categoryName);
            timerData.TimerValue = forceHide ? 1 : (int)(telop.DisplayTime + telop.Delay);
            timerData.RemoveValue = forceHide ? -timerData.TimerValue : 0;
            timerData.WarningValue = (int)telop.DisplayTime;
            timerData.OnlyMasterTicks = telop.AddMessageEnabled ? false : true;
            timerData.Tooltip = telop.MessageReplaced;

            timerData.Panel1Display = false;
            timerData.Panel2Display = false;

            timerData.WarningSoundData = "none"; // disable warning sound

            // initialize other parameters
            timerData.RestrictToMe = false;
            timerData.AbsoluteTiming = false;
            timerData.RestrictToCategory = false;

            ActGlobals.oFormSpellTimers.AddEditTimerDef(timerData);
        }

        /// <summary>
        /// ACT標準のスペルタイマーに通知する
        /// </summary>
        /// <param name="spellTimer">通知先に対応するスペルタイマー</param>
        public void notifyNormalSpellTimer(SpellTimer spellTimer)
        {
            if (!Settings.Default.EnabledNotifyNormalSpellTimer)
            {
                return;
            }

            var prefix = Settings.Default.NotifyNormalSpellTimerPrefix;
            var spellName = prefix + "spell_" + spellTimer.SpellTitle;
            ActGlobals.oFormSpellTimers.NotifySpell("attacker", spellName, false, "victim", false);
        }

        /// <summary>
        /// ACT標準のスペルタイマーに通知する（テロップ用）
        /// </summary>
        /// <param name="telopTitle">通知先に対応するテロップ名</param>
        public void notifyNormalSpellTimerForTelop(string telopTitle)
        {
            if (!Settings.Default.EnabledNotifyNormalSpellTimer)
            {
                return;
            }

            var prefix = Settings.Default.NotifyNormalSpellTimerPrefix;
            var spellName = prefix + "telop_" + telopTitle;
            ActGlobals.oFormSpellTimers.NotifySpell("attacker", spellName, false, "victim", false);
        }

        /// <summary>
        /// ACT標準のスペルタイマーから設定を削除する
        /// </summary>
        /// <param name="immediate">変更を即時に反映させるか？</param>
        public void clearNormalSpellTimer(bool immediate=false)
        {
            var prefix = Settings.Default.NotifyNormalSpellTimerPrefix;
            var timerDefs = ActGlobals.oFormSpellTimers.TimerDefs
                .Where(p => p.Key.StartsWith(prefix))
                .Select(x => x.Value)
                .ToList();
            foreach (var timerDef in timerDefs)
            {
                ActGlobals.oFormSpellTimers.RemoveTimerDef(timerDef);
            }

            // ACTのスペルタイマーに変更を反映する
            if (immediate)
            {
                ActGlobals.oFormSpellTimers.RebuildSpellTreeView();
            }
        }

        /// <summary>
        /// Panelをアクティブ化する
        /// </summary>
        public void ActivatePanels()
        {
            if (this.SpellTimerPanels != null)
            {
                ActInvoker.Invoke(() =>
                {
                    foreach (var panel in this.SpellTimerPanels)
                    {
                        panel.Activate();
                    }
                });
            }
        }

        /// <summary>
        /// Panelを隠す
        /// </summary>
        public void HidePanels()
        {
            if (this.SpellTimerPanels != null)
            {
                ActInvoker.Invoke(() =>
                {
                    foreach (var panel in this.SpellTimerPanels)
                    {
                        panel.HideOverlay();
                    }
                });
            }
        }

        /// <summary>
        /// Panelを閉じる
        /// </summary>
        public void ClosePanels()
        {
            if (this.SpellTimerPanels != null)
            {
                // Panelの位置を保存する
                foreach (var panel in this.SpellTimerPanels)
                {
                    var setting = (
                        from x in PanelSettings.Default.SettingsTable
                        where
                        x.PanelName == panel.PanelName
                        select
                        x).FirstOrDefault();

                    if (setting == null)
                    {
                        setting = PanelSettings.Default.SettingsTable.NewPanelSettingsRow();
                        PanelSettings.Default.SettingsTable.AddPanelSettingsRow(setting);
                    }

                    setting.PanelName = panel.PanelName;
                    setting.Left = panel.Left;
                    setting.Top = panel.Top;
                }

                if (this.SpellTimerPanels.Count > 0)
                {
                    PanelSettings.Default.Save();
                }

                ActInvoker.Invoke(() =>
                {
                    foreach (var panel in this.SpellTimerPanels)
                    {
                        panel.Close();
                    }

                    this.SpellTimerPanels.Clear();
                });
            }
        }

        /// <summary>
        /// Panelの位置を設定する
        /// </summary>
        public void LayoutPanels()
        {
            if (this.SpellTimerPanels != null)
            {
                ActInvoker.Invoke(() =>
                {
                    foreach (var panel in this.SpellTimerPanels)
                    {
                        var setting = PanelSettings.Default.SettingsTable
                            .Where(x => x.PanelName == panel.PanelName)
                            .FirstOrDefault();

                        if (setting != null)
                        {
                            panel.Left = setting.Left;
                            panel.Top = setting.Top;
                        }
                        else
                        {
                            panel.Left = 10.0d;
                            panel.Top = 10.0d;
                        }
                    }
                });
            }
        }

        /// <summary>
        /// 再生する
        /// </summary>
        /// <param name="source">再生するSource</param>
        private void Play(
            string source)
        {
            ACT.SpecialSpellTimer.Sound.SoundController.Default.Play(source);
        }
    }
}
