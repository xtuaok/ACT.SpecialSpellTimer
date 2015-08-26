namespace ACT.SpecialSpellTimer
{
    using System.Diagnostics;
    using System.Windows.Controls;
    using System.Windows.Media;

    using ACT.SpecialSpellTimer.Properties;
    using ACT.SpecialSpellTimer.Utility;
    using ACT.SpecialSpellTimer.Image;
    using System.Windows.Media.Imaging;

    /// <summary>
    /// SpellTimerControl
    /// </summary>
    public partial class SpellTimerControl : UserControl
    {
        /// <summary>
        /// リキャスト秒数の書式
        /// </summary>
        private static string recastTimeFormat = 
            Settings.Default.EnabledSpellTimerNoDecimal ? "N0" : "N1";

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public SpellTimerControl()
        {
#if DEBUG
            Debug.WriteLine("Spell");
#endif
            this.InitializeComponent();
        }

        /// <summary>
        /// スペルのTitle
        /// </summary>
        public string SpellTitle { get; set; }

        /// <summary>
        /// スペルのIcon
        /// </summary>
        public string SpellIcon { get; set; }

        /// <summary>
        /// スペルIconサイズ
        /// </summary>
        public int SpellIconSize { get; set; }

        /// <summary>
        /// 残りリキャストTime(秒数)
        /// </summary>
        public double RecastTime { get; set; }

        /// <summary>
        /// リキャストの進捗率
        /// </summary>
        public double Progress { get; set; }

        /// <summary>
        /// プログレスバーを逆にするか？
        /// </summary>
        public bool IsReverse { get; set; }

        /// <summary>
        /// スペル名を非表示とするか？
        /// </summary>
        public bool HideSpellName { get; set; }
        
        /// <summary>
        /// リキャストタイムを重ねて表示するか？
        /// </summary>
        public bool OverlapRecastTime { get; set; }

        /// <summary>
        /// バーの色
        /// </summary>
        public string BarColor { get; set; }

        /// <summary>
        /// バーOutlineの色
        /// </summary>
        public string BarOutlineColor { get; set; }

        /// <summary>
        /// バーの幅
        /// </summary>
        public int BarWidth { get; set; }
        /// <summary>
        /// バーの高さ
        /// </summary>
        public int BarHeight { get; set; }

        /// <summary>
        /// フォント
        /// </summary>
        public FontInfo FontInfo { get; set; }

        /// <summary>
        /// Fontの色
        /// </summary>
        public string FontColor { get; set; }

        /// <summary>
        /// FontOutlineの色
        /// </summary>
        public string FontOutlineColor { get; set; }

        /// <summary>フォントのBrush</summary>
        private SolidColorBrush FontBrush { get; set; }

        /// <summary>フォントのアウトラインBrush</summary>
        private SolidColorBrush FontOutlineBrush { get; set; }

        /// <summary>バーのBrush</summary>
        private SolidColorBrush BarBrush { get; set; }

        /// <summary>バーの背景のBrush</summary>
        private SolidColorBrush BarBackBrush { get; set; }

        /// <summary>バーのアウトラインのBrush</summary>
        private SolidColorBrush BarOutlineBrush { get; set; }

        /// <summary>
        /// 描画を更新する
        /// </summary>
        public void Refresh()
        {
#if false
            var sw = Stopwatch.StartNew();
#endif
            this.Width = this.BarWidth;

            // Brushを生成する
            var fontColor = string.IsNullOrWhiteSpace(this.FontColor) ?
                Settings.Default.FontColor.ToWPF() :
                this.FontColor.FromHTMLWPF();
            var fontOutlineColor = string.IsNullOrWhiteSpace(this.FontOutlineColor) ?
                Settings.Default.FontOutlineColor.ToWPF() :
                this.FontOutlineColor.FromHTMLWPF();
            var barColor = string.IsNullOrWhiteSpace(this.BarColor) ?
                Settings.Default.ProgressBarColor.ToWPF() :
                this.BarColor.FromHTMLWPF();
            var barBackColor = barColor.ChangeBrightness(0.4d);
            var barOutlineColor = string.IsNullOrWhiteSpace(this.BarOutlineColor) ?
                Settings.Default.ProgressBarOutlineColor.ToWPF() :
                this.BarOutlineColor.FromHTMLWPF();

            this.FontBrush = this.GetBrush(fontColor);
            this.FontOutlineBrush = this.GetBrush(fontOutlineColor);
            this.BarBrush = this.GetBrush(barColor);
            this.BarBackBrush = this.GetBrush(barBackColor);
            this.BarOutlineBrush = this.GetBrush(barOutlineColor);

            var tb = default(OutlineTextBlock);
            var font = this.FontInfo;

            // アイコンを描画する
            var image = this.SpellIconImage;
            if (image.Source == null && this.SpellIcon != "")
            {
                image.Source = new BitmapImage(new System.Uri(IconController.Default.getIconFile(this.SpellIcon).FullPath));
                image.Height = this.SpellIconSize;
                image.Width = this.SpellIconSize;
            }
            
            // Titleを描画する
            tb = this.SpellTitleTextBlock;
            var title = string.IsNullOrWhiteSpace(this.SpellTitle) ? "　" : this.SpellTitle;
            if (tb.Text != title)
            {
                tb.Text = title;
                tb.SetFontInfo(font);
                tb.Fill = this.FontBrush;
                tb.Stroke = this.FontOutlineBrush;
                tb.StrokeThickness = 0.5d * tb.FontSize / 13.0d;
            }
            if (this.HideSpellName)
            {
                tb.Visibility = System.Windows.Visibility.Collapsed;
            }

            // リキャスト時間を描画する
            tb = this.RecastTimeTextBlock;
            var recast = this.RecastTime > 0 ?
                this.RecastTime.ToString(recastTimeFormat) :
                this.IsReverse ? "Over" : "Ready";
            if (tb.Text != recast)
            {
                tb.Text = recast;
                tb.SetFontInfo(font);
                tb.Fill = this.FontBrush;
                tb.Stroke = this.FontOutlineBrush;
                tb.StrokeThickness = 0.5d * tb.FontSize / 13.0d;
            }
            if (this.OverlapRecastTime)
            {
                this.RecastTimePanel.SetValue(Grid.ColumnProperty, 0);
                this.RecastTimePanel.SetValue(HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
                this.RecastTimePanel.SetValue(VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
                this.RecastTimePanel.Width = this.SpellIconSize - 6;
                this.RecastTimePanel.Height = this.SpellIconSize - 6;
            }
            else
            {
                this.RecastTimePanel.Width = double.NaN;
                this.RecastTimePanel.Height = double.NaN;
            }

            // ProgressBarを描画する
            var foreRect = this.BarRectangle;
            foreRect.Stroke = this.BarBrush;
            foreRect.Fill = this.BarBrush;
            foreRect.Width = this.IsReverse ?
                (double)(this.BarWidth * (1.0d - this.Progress)) :
                (double)(this.BarWidth * this.Progress);
            foreRect.Height = this.BarHeight;
            foreRect.RadiusX = 2.0d;
            foreRect.RadiusY = 2.0d;
            Canvas.SetLeft(foreRect, 0);
            Canvas.SetTop(foreRect, 0);

            var backRect = this.BarBackRectangle;
            backRect.Stroke = this.BarBackBrush;
            backRect.Fill = this.BarBackBrush;
            backRect.Width = this.BarWidth;
            backRect.Height = foreRect.Height;
            backRect.RadiusX = 2.0d;
            backRect.RadiusY = 2.0d;
            Canvas.SetLeft(backRect, 0);
            Canvas.SetTop(backRect, 0);

            var outlineRect = this.BarOutlineRectangle;
            outlineRect.Stroke = this.BarOutlineBrush;
            outlineRect.StrokeThickness = 1.0d;
            outlineRect.Width = backRect.Width;
            outlineRect.Height = foreRect.Height;
            outlineRect.RadiusX = 2.0d;
            outlineRect.RadiusY = 2.0d;
            Canvas.SetLeft(outlineRect, 0);
            Canvas.SetTop(outlineRect, 0);

            // バーのエフェクトの色を設定する
            this.BarEffect.Color = this.BarBrush.Color.ChangeBrightness(1.05d);

            this.ProgressBarCanvas.Width = backRect.Width;
            this.ProgressBarCanvas.Height = backRect.Height;

#if false
            sw.Stop();
            Debug.WriteLine("Spell Refresh -> " + sw.ElapsedMilliseconds.ToString("N0") + "ms");
#endif
        }
    }
}
