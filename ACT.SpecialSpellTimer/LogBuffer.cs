namespace ACT.SpecialSpellTimer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using ACT.SpecialSpellTimer.Properties;
    using Advanced_Combat_Tracker;

    /// <summary>
    /// ログのバッファ
    /// </summary>
    public class LogBuffer : IDisposable
    {
        /// <summary>
        /// ペットID更新ロックオブジェクト
        /// </summary>
        private static object lockPetidObject = new object();

        /// <summary>
        /// パーティメンバの代名詞が有効か？
        /// </summary>
        private static bool enabledPartyMemberPlaceHolder = Settings.Default.EnabledPartyMemberPlaceholder;

        /// <summary>
        /// パーティメンバ
        /// </summary>
        private static List<string> ptmember;

        /// <summary>
        /// パーティメンバ（Combant情報）またプレイヤを含む
        /// </summary>
        private static List<Combatant> ptmemberCombatant;

        /// <summary>
        /// ペットのID
        /// </summary>
        private static string petid;

        /// <summary>
        /// ペットのIDを取得したゾーン
        /// </summary>
        private static string petidZone;

        /// <summary>
        /// 内部バッファ
        /// </summary>
        private List<string> buffer = new List<string>();

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public LogBuffer()
        {
            ActGlobals.oFormActMain.OnLogLineRead += this.oFormActMain_OnLogLineRead;
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            ActGlobals.oFormActMain.OnLogLineRead -= this.oFormActMain_OnLogLineRead;
            this.Clear();

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// ログ行を返す
        /// </summary>
        /// <returns>
        /// ログ行の配列</returns>
        public string[] GetLogLines()
        {
            lock (this.buffer)
            {
                var logLines = this.buffer.ToArray();
                this.buffer.Clear();
                return logLines;
            }
        }

        /// <summary>
        /// バッファをクリアする
        /// </summary>
        public void Clear()
        {
            lock (this.buffer)
            {
                this.buffer.Clear();

                if (ptmember != null)
                {
                    ptmember.Clear();
                }

#if DEBUG
                Debug.WriteLine("Logをクリアしました");
#endif
            }
        }

        /// <summary>
        /// ログを一行読取った
        /// </summary>
        /// <param name="isImport">Importか？</param>
        /// <param name="logInfo">ログ情報</param>
        private void oFormActMain_OnLogLineRead(bool isImport, LogLineEventArgs logInfo)
        {
            if (isImport)
            {
                return;
            }

#if false
            Debug.WriteLine(logInfo.logLine);
#endif

            var logLine = logInfo.logLine.Trim();

            // ジョブに変化あり？
            if (logLine.Contains("にチェンジした。") || logLine.Contains("You change to "))
            {
                FF14PluginHelper.RefreshPlayer();
            }

            // パーティに変化あり？
            if (ptmember == null ||
                logLine.Contains("パーティを解散しました。") ||
                logLine.Contains("がパーティに参加しました。") ||
                logLine.Contains("がパーティから離脱しました。") ||
                logLine.Contains("をパーティから離脱させました。") ||
                logLine.Contains("の攻略を開始した。") ||
                logLine.Contains("の攻略を終了した。") ||
                (logLine.Contains("You join ") && logLine.Contains("'s party.")) ||
                logLine.Contains("You left the party.") ||
                logLine.Contains("You dissolve the party.") ||
                logLine.Contains("The party has been disbanded.") ||
                logLine.Contains("joins the party.") ||
                logLine.Contains("has left the party.") ||
                logLine.Contains("was removed from the party."))
            {
                Task.Run(() =>
                {
                    Thread.Sleep(5 * 1000);
                    RefreshPTList();
                });
            }

            // ペットIDのCacheを更新する
            var player = FF14PluginHelper.GetPlayer();
            if (player != null)
            {
                var jobName = Job.GetJobName(player.Job);
#if DEBUG
                Debug.WriteLine("JOB NAME!! " + jobName);
#endif
                if (jobName == "巴術士" || jobName == "ARC" ||
                    jobName == "学者" || jobName == "SCH" ||
                    jobName == "召喚士" || jobName == "SMN")
                {
                    if (logLine.Contains(player.Name + "の「サモン") || logLine.Contains("You cast Summon"))
                    {
                        Task.Run(() =>
                        {
                            Thread.Sleep(5 * 1000);
                            RefreshPetID();
                        });
                    }

                    if (petidZone != ActGlobals.oFormActMain.CurrentZone)
                    {
                        Task.Run(() =>
                        {
                            lock (lockPetidObject)
                            {
                                var count = 0;
                                while (petidZone != ActGlobals.oFormActMain.CurrentZone)
                                {
                                    Thread.Sleep(15 * 1000);
                                    RefreshPetID();
                                    count++;

                                    if (count >= 6)
                                    {
                                        petidZone = ActGlobals.oFormActMain.CurrentZone;
                                        break;
                                    }
                                }
                            }
                        });
                    }
                }
            }

            lock (this.buffer)
            {
                this.buffer.Add(logLine);
            }
        }

        /// <summary>
        /// マッチングキーワードを生成する
        /// </summary>
        /// <param name="keyword">元のキーワード</param>
        /// <returns>生成したキーワード</returns>
        public static string MakeKeyword(
            string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return keyword.Trim();
            }

            var sb = new StringBuilder(keyword.Trim());

            var player = FF14PluginHelper.GetPlayer();
            if (player != null)
            {
                sb.Replace("<me>", player.Name.Trim());
            }

            if (enabledPartyMemberPlaceHolder)
            {
                if (ptmember != null)
                {
                    for (int i = 0; i < ptmember.Count; i++)
                    {
                        sb.Replace(
                            "<" + (i + 2).ToString() + ">",
                            ptmember[i].Trim());
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(petid))
            {
                sb.Replace("<petid>", petid);
            }

            // ジョブ名プレースホルダを置換する
            // ex. <PLD>, <PLD1> ...
            if (ptmemberCombatant != null &&
                ptmemberCombatant.Count > 0)
            {
                foreach (var job in Job.GetJobList())
                {
                    // このジョブに該当するパーティメンバを抽出する
                    var combatants = (
                        from x in ptmemberCombatant
                        where
                        x.Job == job.JobId
                        orderby
                        x.Name
                        select
                        x).ToArray();

                    if (!combatants.Any())
                    {
                        continue;
                    }

                    // <JOBn>形式を置換する
                    // ex. <PLD1> → Taro Paladin
                    // ex. <PLD2> → Jiro Paladin
                    for (int i = 0; i < combatants.Length; i++)
                    {
                        var placeholder = string.Format(
                            "<{0}{1}>",
                            job.JobName,
                            i + 1);

                        sb.Replace(placeholder.ToUpper(), combatants[i].Name);
                        sb.Replace(placeholder.ToLower(), combatants[i].Name);
                    }

                    // <JOB>形式を置換する
                    // ただし、この場合は正規表現のグループ形式とする
                    // また、グループ名にはジョブの略称を設定する
                    // ex. <PLD> → (?<pld>Taro Paladin|Jiro Paladin)
                    var names = string.Join("|", combatants.Select(x => x.Name).ToArray());
                    var oldValue = string.Format("<{0}>", job.JobName);
                    var newValue = string.Format(
                        "(?<{0}>{1})",
                        job.JobName.ToLower(),
                        names);

                    sb.Replace(oldValue.ToUpper(), newValue);
                    sb.Replace(oldValue.ToLower(), newValue);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 正規表現向けマッチングキーワードを生成する
        /// </summary>
        /// <param name="keyword">元のキーワード</param>
        /// <returns>生成したキーワード</returns>
        public static string MakeKeywordToRegex(
            string keyword)
        {
            keyword = MakeKeyword(keyword);

            if (string.IsNullOrWhiteSpace(keyword))
            {
                return keyword.Trim();
            }

            return ".*" + keyword + ".*";
        }

        /// <summary>
        /// PTメンバーリストを返す
        /// </summary>
        /// <returns>PTメンバーリスト</returns>
        public static string[] GetPTMember()
        {
            if (ptmember != null)
            {
                return ptmember.ToArray();
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// パーティリストを更新する
        /// </summary>
        public static void RefreshPTList()
        {
            if (ptmember == null)
            {
                ptmember = new List<string>();
            }
            else
            {
                ptmember.Clear();
            }

            if (enabledPartyMemberPlaceHolder)
            {
#if DEBUG
                Debug.WriteLine("PT: Refresh");
#endif
                // プレイヤー情報を取得する
                var player = FF14PluginHelper.GetPlayer();
                if (player == null)
                {
                    return;
                }

                // PTメンバの名前を記録しておく
                ptmemberCombatant = FF14PluginHelper.GetCombatantListParty();

                // FF14内部のPTメンバ自動ソート順で並び替える
                var sorted =
                    from x in ptmemberCombatant
                    join y in Job.GetJobList() on
                        x.Job equals y.JobId
                    where
                    x.ID != player.ID
                    orderby
                    y.Role,
                    x.Job,
                    x.ID
                    select
                    x.Name.Trim();

                foreach (var name in sorted)
                {
                    ptmember.Add(name);
#if DEBUG
                    Debug.WriteLine("<-  " + name);
#endif
                }
            }
        }

        /// <summary>
        /// ペットIDを更新する
        /// </summary>
        public static void RefreshPetID()
        {
            // Combatantリストを取得する
            var combatant = FF14PluginHelper.GetCombatantList();

            if (combatant != null &&
                combatant.Count > 0)
            {
                var pet = (
                    from x in combatant
                    where
                    x.OwnerID == combatant[0].ID &&
                    (
                        x.Name.Contains("フェアリー・") ||
                        x.Name.Contains("・エギ") ||
                        x.Name.Contains("カーバンクル・")
                    )
                    select
                    x).FirstOrDefault();

                if (pet != null)
                {
                    petid = Convert.ToString((long)((ulong)pet.ID), 16).ToUpper();
                    petidZone = ActGlobals.oFormActMain.CurrentZone;
                }
            }
        }
    }
}
