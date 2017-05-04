using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.IO;
using System.Media;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace TomatoTimer {

    public partial class MainWindow : Window
    {
        /// <summary>
        /// Create application main window
        /// </summary>
        public MainWindow() {
            InitializeComponent();
            StartClocks();
            Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;

            MemoryStream ms = new MemoryStream();
            Resource.tb.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            BitmapImage bi = new BitmapImage();
            bi.BeginInit();
            ms.Seek(0, SeekOrigin.Begin);
            bi.StreamSource = ms;
            bi.EndInit();
            helpImage.Source = bi;

            trayNotifyIcon = new System.Windows.Forms.NotifyIcon();
            trayNotifyIcon.Icon = System.Drawing.Icon.FromHandle(Resource.t.GetHicon());
            trayNotifyIcon.Text = "TomatoTimer: Idle";
            trayNotifyIcon.BalloonTipTitle = "TomatoTimer";
            trayNotifyIcon.Click += delegate(object sender, EventArgs args) {
                if(WindowState == WindowState.Normal) {
                    Hide();
                    WindowState = WindowState.Minimized;
                } else {
                    Show();
                    WindowState = WindowState.Normal;
                }
            };
            trayNotifyIcon.Visible = true;
        }

        /// <summary>
        /// Define actions on application closing
        /// </summary>
        /// <param name="e">CancelEventArgs object reference</param>
        protected override void OnClosing(CancelEventArgs e) {
            trayNotifyIcon.Dispose();
            trayNotifyIcon = null;
            clocks.Abort();
            base.OnClosing(e);
        }

        /// <summary>
        /// Define actions when 'Go' button is clicked
        /// </summary>
        /// <param name="sender">action sender object reference</param>
        /// <param name="e">RoutedEventArgs object reference</param>
        private void GoButtonClick(object sender, RoutedEventArgs e) {
            if (null == te) {
                te = new TomatoTimer.TomatoEngine(bunchSize, tomatoDuration, breakDuration);
                te.SetControlledIntervalElapsedCallback(() => {
                    UpdateTimer();
                });
                te.SetEngineStateChangedCallback(() => {
                    UpdateTimer();
                    SetCurrentStateLabel();
                    ComeToFront();
                    trayNotifyIcon.ShowBalloonTip(1000);
                });

                te.Start();
                UpdateTimer();
                SetCurrentStateLabel();

                ((Button)sender).IsEnabled = false;
                pauseBtn.IsEnabled = true;
                bunchSizeCombobox.IsEnabled = false;
                tomatoDurationCombobox.IsEnabled = false;
                breakDurationCombobox.IsEnabled = false;
            } else {
                throw new Exception("Logic problem with start button");
            }
        }

        /// <summary>
        /// Update timer-related user interface controls
        /// </summary>
        private void UpdateTimer() {
            downcounterLabel.Dispatcher.BeginInvoke(new Action(() => {
                downcounterLabel.Content = String.Format("0:{0:D2}", te.MinutesToGo);
            }));
            downtimerProgressBar.Dispatcher.BeginInvoke(new Action(() => {
                downtimerProgressBar.Minimum = 0;
                downtimerProgressBar.Maximum = te.CurrentStateDuration;
                downtimerProgressBar.Value = te.MinutesToGo;
            }));
            bunchCounterLabel.Dispatcher.BeginInvoke(new Action(() => {
                bunchCounterLabel.Content = String.Format("-{0}-", te.BunchSize);
            }));
            CreateTooltip();
        }

        /// <summary>
        /// Make application main window on top on the screen
        /// </summary>
        private void ComeToFront() {
            this.Dispatcher.BeginInvoke(new Action(() => {
                if(WindowState == WindowState.Minimized) {
                    Show();
                    WindowState = WindowState.Normal;
                }
                Focus();
            }));
        }

        /// <summary>
        /// Play tomato started sound if configured
        /// </summary>
        private void PlayTomatoStartedSoundIfConfigured() {
            if(isTomatoStartedSoundNeeded) {
                soundPlayer.Stream = Resource.go_working;
                soundPlayer.Play();
            }
        }

        /// <summary>
        /// Play break time started sound if configured
        /// </summary>
        private void PlayBreakStartedSoundIfConfigured() {
            if(isBreakStartedSoundNeeded) {
                soundPlayer.Stream = Resource._break;
                soundPlayer.Play();
            }
        }

        /// <summary>
        /// Play tomatoes bunch exhaused sound if configured
        /// </summary>
        private void PlayBunchEndedSoundIfConfigured() {
            if(isBunchEndedSoundNeeded) {
                soundPlayer.Stream = Resource.finish;
                soundPlayer.Play();
            }
        }

        /// <summary>
        /// Adaptive create current tool tip text for tray icon
        /// </summary>
        private void CreateTooltip() {
            switch(te.CurrentState) {
            case TomatoEngine.State.IDLE:
                trayNotifyIcon.Text = "TomatoTmer: idle";
                trayNotifyIcon.BalloonTipText = "Idle";
                break;
            case TomatoEngine.State.WORKING:
                trayNotifyIcon.Text = String.Format("TomatoTimer: working - {0} min left", te.MinutesToGo);
                trayNotifyIcon.BalloonTipText = "Working";
                break;
            case TomatoEngine.State.BREAK:
                trayNotifyIcon.Text = String.Format("TomatoTimer: break - {0} min left", te.MinutesToGo);
                trayNotifyIcon.BalloonTipText = "Break";
                break;
            case TomatoEngine.State.PAUSED:
            case TomatoEngine.State.PAUSED_IN_BREAK:
                trayNotifyIcon.Text = "TomatoTimer: paused";
                trayNotifyIcon.BalloonTipText = "Paused";
                break;
            case TomatoEngine.State.FINISHED:
                trayNotifyIcon.Text = "TomatoTimer: finished";
                trayNotifyIcon.BalloonTipText = "Finished";
                break;
            }
        }

        /// <summary>
        /// Adaptive create current state title label
        /// </summary>
        private void SetCurrentStateLabel() {
            string message = "У нас сейчас: ждём команды";
            switch(te.CurrentState) {
            case TomatoEngine.State.IDLE:
                break;
            case TomatoEngine.State.WORKING:
                message = "У нас сейчас: помидорка";
                PlayTomatoStartedSoundIfConfigured();
                break;
            case TomatoEngine.State.BREAK:
                message = "У нас сейчас: перерыв";
                PlayBreakStartedSoundIfConfigured();
                break;
            case TomatoEngine.State.PAUSED_IN_BREAK:
            case TomatoEngine.State.PAUSED:
                message = "У нас сейчас: пауза";
                break;
            case TomatoEngine.State.FINISHED:
                message = "У нас сейчас: отсчёт закончен";
                PlayBunchEndedSoundIfConfigured();
                bunchSizeCombobox.Dispatcher.BeginInvoke(new Action(() => {
                    bunchSizeCombobox.IsEnabled = false;
                }));
                tomatoDurationCombobox.Dispatcher.BeginInvoke(new Action(() => {
                    tomatoDurationCombobox.IsEnabled = false;
                }));
                breakDurationCombobox.Dispatcher.BeginInvoke(new Action(() => {
                    breakDurationCombobox.IsEnabled = false;
                }));
                break;
            }
            currentStateLabel.Dispatcher.BeginInvoke(new Action(() => {
                currentStateLabel.Content = message;
                CreateTooltip();
                trayNotifyIcon.ShowBalloonTip(0);
            }));
        }

        /// <summary>
        /// Define actions when 'Pause' button is clicked
        /// </summary>
        /// <param name="sender">action sender object reference</param>
        /// <param name="e">RoutedEventArgs object reference</param>
        private void PauseButtonClick(object sender, RoutedEventArgs e) {
            if(null != te) {
                switch (((Button)sender).Content) {
                case "Пауза":
                    ((Button)sender).Content = "Дальше";
                    te.Pause();
                    break;
                case "Дальше":
                    ((Button)sender).Content = "Пауза";
                    te.Proceed();
                    break;
                }
                SetCurrentStateLabel();
            }
        }

        /// <summary>
        /// Define actions when 'Finish' application button is clicked
        /// </summary>
        /// <param name="sender">action sender object reference</param>
        /// <param name="e">RoutedEventArgs object reference</param>
        private void FinishApplicationButtonClick(object sender, RoutedEventArgs e) {
            if(null != te) {
                te.Cancel();
            }
            clocks.Abort();
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Define actions when bunch size combobox selection is made
        /// </summary>
        /// <param name="sender">action sender object reference</param>
        /// <param name="e">RoutedEventArgs object reference</param>
        private void BunchSizeSelectorChanged(object sender, RoutedEventArgs e) {
            bunchSize = UInt16.Parse((string)((ComboBoxItem)sender).Content);
        }

        /// <summary>
        /// Define actions when tomato duration combobox selection is made
        /// </summary>
        /// <param name="sender">action sender object reference</param>
        /// <param name="e">RoutedEventArgs object reference</param>
        private void TomatoDurationSelectorChanged(object sender, RoutedEventArgs e) {
            tomatoDuration = UInt16.Parse((string)((ComboBoxItem)sender).Content);
        }

        /// <summary>
        /// Define actions when break time duration combobox selection is made
        /// </summary>
        /// <param name="sender">action sender object reference</param>
        /// <param name="e">RoutedEventArgs object reference</param>
        private void BreakDurationSelectorChanged(object sender, RoutedEventArgs e) {
            breakDuration = UInt16.Parse((string)((ComboBoxItem)sender).Content);
        }

        /// <summary>
        /// Define action when tomato sound needed checkbox is clicked
        /// </summary>
        /// <param name="sender">action sender object reference</param>
        /// <param name="e">RoutedEventArgs object reference</param>
        private void TomatoStartedSoundNeeded(object sender, RoutedEventArgs e) {
            bool? flag = ((CheckBox)sender).IsChecked;
            if(flag.HasValue) {
                isTomatoStartedSoundNeeded = (bool)flag;
            } else {
                isTomatoStartedSoundNeeded = false;
            }
        }

        /// <summary>
        /// Define action when break sound needed checkbox is clicked
        /// </summary>
        /// <param name="sender">action sender object reference</param>
        /// <param name="e">RoutedEventArgs object reference</param>
        private void BreakStartedSoundNeeded(object sender, RoutedEventArgs e) {
            bool? flag = ((CheckBox)sender).IsChecked;
            if(flag.HasValue) {
                isBreakStartedSoundNeeded = (bool)flag;
            } else {
                isBreakStartedSoundNeeded = false;
            }
        }

        /// <summary>
        /// Define action when bunch ended sound needed checkbox is clicked
        /// </summary>
        /// <param name="sender">action sender object reference</param>
        /// <param name="e">RoutedEventArgs object reference</param>
        private void BunchEndedSoundNeeded(object sender, RoutedEventArgs e) {
            bool? flag = ((CheckBox)sender).IsChecked;
            if(flag.HasValue) {
                isBunchEndedSoundNeeded= (bool)flag;
            } else {
                isBunchEndedSoundNeeded = false;
            }
        }

        /// <summary>
        /// Start autonomous clocks to be shown on application mail panel
        /// </summary>
        private void StartClocks() {
            clocks = new Thread(() => {
                while(true) {
                    TimeSpan now = DateTime.Now.TimeOfDay;
                    clocksLabel.Dispatcher.BeginInvoke(new Action(() => {
                        clocksLabel.Content = String.Format("{0:D2}:{1:D2}", now.Hours, now.Minutes);
                    }));
                    Thread.Sleep(1000 * (60 - now.Seconds));
                }
            });
            clocks.Start();
        }

        private uint bunchSize = 2;
        private uint tomatoDuration = 30;
        private uint breakDuration = 5;

        private bool isTomatoStartedSoundNeeded = false;
        private bool isBreakStartedSoundNeeded = false;
        private bool isBunchEndedSoundNeeded = false;

        private TomatoTimer.TomatoEngine te = null;

        private SoundPlayer soundPlayer = new SoundPlayer();
        private System.Windows.Forms.NotifyIcon trayNotifyIcon = null;
        private Thread clocks = null;
    }
}
