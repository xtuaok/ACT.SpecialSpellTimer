namespace ACT.SpecialSpellTimer.Utility
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Threading;

    /// <summary>
    /// キャプションボタンパネル
    /// </summary>
    public partial class CaptionButtonPanel : UserControl
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public CaptionButtonPanel()
        {
            this.InitializeComponent();

            if (!this.Minimize)
            {
                this.MinimizeButton.Visibility = Visibility.Collapsed;
            }

            if (!this.Maximize)
            {
                this.MaximizeButton.Visibility = Visibility.Collapsed;
                this.NormalizeButton.Visibility = Visibility.Collapsed;
            }

            this.Loaded += this.CaptionButtonPanel_Loaded;
        }

        public bool Minimize
        {
            get;
            set;
        }

        public bool Maximize
        {
            get;
            set;
        }

        /// <summary>
        /// Loaded
        /// </summary>
        /// <param name="sender">イベント発生元</param>
        /// <param name="e">イベント引数</param>
        private void CaptionButtonPanel_Loaded(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);

            if (owner == null)
            {
                return;
            }

            var stateChanged = new Action(() =>
            {
                switch (owner.WindowState)
                {
                    case WindowState.Maximized:
                        this.MaximizeButton.Visibility = Visibility.Collapsed;
                        if (this.Maximize)
                        {
                            this.NormalizeButton.Visibility = Visibility.Visible;
                        }

                        break;

                    case WindowState.Minimized:
                        break;

                    case WindowState.Normal:
                        if (this.Maximize)
                        {
                            this.MaximizeButton.Visibility = Visibility.Visible;
                        }

                        this.NormalizeButton.Visibility = Visibility.Collapsed;

                        break;
                }
            });

            stateChanged();
            owner.StateChanged += (s, e1) =>
            {
                stateChanged();
            };

            owner.Activated += (s, e1) =>
            {
                if (owner.WindowStyle != WindowStyle.None)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        owner.WindowStyle = WindowStyle.None;
                    }),
                    DispatcherPriority.ApplicationIdle);
                }
            };

            this.MinimizeButton.Click += (s, e1) =>
            {
                switch (owner.WindowState)
                {
                    case WindowState.Maximized:
                    case WindowState.Normal:
                        owner.WindowState = WindowState.Minimized;
                        break;
                }
            };

            this.MaximizeButton.Click += (s, e1) =>
            {
                switch (owner.WindowState)
                {
                    case WindowState.Normal:
                    case WindowState.Minimized:
                        owner.WindowState = WindowState.Maximized;
                        break;
                }
            };

            this.NormalizeButton.Click += (s, e1) =>
            {
                switch (owner.WindowState)
                {
                    case WindowState.Maximized:
                        owner.WindowState = WindowState.Normal;
                        break;
                }
            };

            this.CloseButton.Click += (s, e1) =>
            {
                owner.Close();
            };
        }
    }
}
