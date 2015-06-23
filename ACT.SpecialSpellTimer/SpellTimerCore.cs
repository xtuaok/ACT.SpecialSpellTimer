namespace ACT.SpecialSpellTimer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
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
                    this.RefreshWindow();
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
        /// Windowを更新する
        /// </summary>
        private void RefreshWindow()
        {
#if DEBUG
            var sw1 = Stopwatch.StartNew();
#endif
            // 有効なスペルとテロップのリストを取得する
            var spellArray = SpellTimerTable.EnabledTable;
            var telopArray = OnePointTelopTable.Default.EnabledTable;

            // 不要なWindowを閉じる
            OnePointTelopController.GarbageWindows(telopArray);
            this.GarbageSpellPanelWindows(spellArray);
#if DEBUG
            sw1.Stop();
            Debug.WriteLine("Refresh ClosePanels ->" + sw1.Elapsed.TotalMilliseconds.ToString("N4") + "ms");
#endif

            // ACTが起動していない？
#if DEBUG
            var sw7 = Stopwatch.StartNew();
#endif
            if (ActGlobals.oFormActMain == null ||
                !ActGlobals.oFormActMain.Visible)
            {
                this.HidePanels();
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
                        this.ClosePanels();
                        OnePointTelopController.CloseTelops();
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

#if DEBUG
            var swC = Stopwatch.StartNew();
#endif
            // コマンドとマッチングする
            TextCommandController.MatchCommand(
                logLines);

            // オーバーレイが非表示？
            if (!Settings.Default.OverlayVisible)
            {
                this.HidePanels();
                OnePointTelopController.HideTelops();
                return;
            }
#if DEBUG
            swC.Stop();
            Debug.WriteLine("Match Command ->" + swC.Elapsed.TotalMilliseconds.ToString("N4") + "ms");
#endif

            // テロップWindowを表示する
#if DEBUG
            var sw5 = Stopwatch.StartNew();
#endif
            OnePointTelopController.RefreshTelopWindows(telopArray);
#if DEBUG
            sw5.Stop();
            Debug.WriteLine("Refresh RefreshTelopWindows ->" + sw5.Elapsed.TotalMilliseconds.ToString("N4") + "ms");
#endif

            // スペルWindowを表示する
#if DEBUG
            var sw6 = Stopwatch.StartNew();
#endif
            this.RefreshSpellPanelWindows(spellArray);
#if DEBUG
            sw6.Stop();
            Debug.WriteLine("Refresh RefreshSpellPanelWindows ->" + sw6.Elapsed.TotalMilliseconds.ToString("N4") + "ms");
#endif
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
                var regexForExpand = spell.RegexForExpand;

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
                            spell.TimeupDone = false;
                            spell.RecastTimeActive = spell.RecastTime;

                            // マッチ時点のサウンドを再生する
                            this.Play(spell.MatchSound);
                            this.Play(spell.MatchTextToSpeak);
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
                            spell.TimeupDone = false;
                            spell.RecastTimeActive = spell.RecastTime;

                            // マッチ時点のサウンドを再生する
                            this.Play(spell.MatchSound);

                            if (!string.IsNullOrWhiteSpace(spell.MatchTextToSpeak))
                            {
                                var tts = match.Result(spell.MatchTextToSpeak);
                                this.Play(tts);
                            }
                        }
                    }

                    // リキャストタイムの延長をマッチングする
                    if (spell.MatchDateTime > DateTime.MinValue)
                    {
                        if (!spell.RegexEnabled ||
                            regexForExpand == null)
                        {
                            var keyword = spell.KeywordForExpand;
                            if (string.IsNullOrWhiteSpace(keyword))
                            {
                                continue;
                            }

                            if (logLine.ToUpper().Contains(
                                keyword.ToUpper()))
                            {
                                spell.RecastTimeActive += spell.RecastTimeExpanding;
                            }
                        }
                        else
                        {
                            var match = regexForExpand.Match(logLine);
                            if (match.Success)
                            {
                                spell.RecastTimeActive += spell.RecastTimeExpanding;
                            }
                        }
                    }
                }

                // Repeat対象のSpellを更新する
                if (spell.RepeatEnabled &&
                    spell.MatchDateTime > DateTime.MinValue)
                {
                    if (DateTime.Now >= spell.MatchDateTime.AddSeconds(spell.RecastTimeActive))
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

                // リキャスト完了のSoundを再生する
                if (spell.RecastTimeActive > 0 &&
                    !spell.TimeupDone &&
                    spell.MatchDateTime > DateTime.MinValue)
                {
                    var recast = spell.MatchDateTime.AddSeconds(spell.RecastTimeActive);
                    if (DateTime.Now >= recast)
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
