using HyPlayer.UWP.Chopin.Abstractions.Interfaces;
using HyPlayer.UWP.Chopin.Abstractions.Models;
using System;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Media;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace HyPlayer.UWP.Chopin.Demo
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            Player.OnPositionChange += Player_OnPositionChange;
            _systemMediaTransportControls = SystemMediaTransportControls.GetForCurrentView();
            _systemMediaTransportControls.IsPlayEnabled = true;
            _systemMediaTransportControls.IsPauseEnabled = true;
            _systemMediaTransportControls.ButtonPressed += _systemMediaTransportControls_ButtonPressed;
        }

        private void _systemMediaTransportControls_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            if(args.Button == SystemMediaTransportControlsButton.Play)
            {
                Player.PlayAllAsync();
                _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Playing;
            }
            if(args.Button == SystemMediaTransportControlsButton.Pause)
            {
                Player.PauseAllAsync();
                _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Paused;
            }
        }

        private SystemMediaTransportControls _systemMediaTransportControls { get; set; }
        private void Player_OnPositionChange(TimeSpan position)
        {
            Positon = position;
            if (Sliding) return;
            _ = Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal,
                () =>
                {
                    Timeline.Maximum = (Player.PrimaryPlaybackSource as AudioGraphPlaybackSource).PlaybackSource.Duration?.TotalMilliseconds ?? 0;
                    Timeline.Value = position.TotalMilliseconds;
                });
        }

        AudioGraphPlayer Player = new AudioGraphPlayer();
        TimeSpan Positon = TimeSpan.Zero;
        bool Sliding = false;
        private void OutgoingVolume_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            Player.SetOutputVolumeAsync(e.NewValue / 100);
        }

        private void Timeline_ManipulationStarting(object sender, Windows.UI.Xaml.Input.ManipulationStartingRoutedEventArgs e)
        {
            var value = (Songs.SelectedItem as ComboBoxItem)?.Tag as IPlaybackSource;
            if (value == null) return;
            var target = TimeSpan.FromMilliseconds(Timeline.Value);
            Player.SeekPlaybackSourceAsync(target, value);
        }

        private void Timeline_ManipulationStarted(object sender, Windows.UI.Xaml.Input.ManipulationStartedRoutedEventArgs e)
        {
            Sliding = true;
        }

        private async void AddOnline_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var uri = new Uri(Url.Text);
            var musicResource = new AudioGraphPlaybackSource(new Uri(Url.Text)) { Name = "Unknown" };
            await Player.ConnectPlaybackSourceAsync(musicResource);
        }

        private async void SelectSong_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var file = await PickFileAsync();
            if (file == null)
            {
                return;
            }
            var musicResource = new AudioGraphPlaybackSource(file) { Name = file.Name };
            await Player.ConnectPlaybackSourceAsync(musicResource);
        }
        private async Task<StorageFile> PickFileAsync()
        {
            var filePicker = new FileOpenPicker();
            filePicker.ViewMode = PickerViewMode.Thumbnail;
            filePicker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
            filePicker.FileTypeFilter.Add(".flac");
            filePicker.FileTypeFilter.Add(".mp3");
            filePicker.FileTypeFilter.Add(".wav");

            StorageFile file = await filePicker.PickSingleFileAsync();
            if (file != null)
            {
                // Application now has read/write access to the picked file
                return file;
            }
            else
            {
                return null;
            }
        }
        private void Start_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Player.PlayPlaybackSourceAsync((Songs.SelectedItem as ComboBoxItem).Tag as IPlaybackSource);
        }

        private async void StartAudioGraph_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (!Player.PlayerCreated)
            {
                var settings = new AudioGraphAudioSetting();
                await Player.InitializePlayer(settings);
            }
            else
            {
                _ = Player.PlayAllAsync();
            }
        }

        private void Stop_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Player.PausePlaybackSourceAsync((Songs.SelectedItem as ComboBoxItem).Tag as IPlaybackSource);
        }

        private void StopAudioGraph_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Player.PauseAllAsync();
        }

        private async void ChangeDevice_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var devicePicker = new DevicePicker();
            devicePicker.Filter.SupportedDeviceClasses.Add(DeviceClass.AudioRender);
            var ge = ChangeDevice.TransformToVisual(null);
            var point = ge.TransformPoint(new Point());
            var rect = new Rect(point,
                new Point(point.X + ChangeDevice.ActualWidth,
                    point.Y + ChangeDevice.ActualHeight));
            var device = await devicePicker.PickSingleDeviceAsync(rect);
            if (device != null)
            {
                var outputDevice = new AudioGraphAudioSetting() { DefaultDeviceId = device.Id };
                await Player.ChangePlayerServiceImplementation(outputDevice);
            }
        }

        private async void Default_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var outputDevice = new AudioGraphAudioSetting();
            await Player.ChangePlayerServiceImplementation(outputDevice);
        }

        private void Dispose_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Player.Dispose();
        }

        private void DisposeSong_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Player.DisconnectPlaybackSourceAsync((Songs.SelectedItem as ComboBoxItem).Tag as IPlaybackSource);
        }

        private async void Refresh_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Songs.Items.Clear();
            var values = await Player.GetConnectedPlaybackSourceAsync();
            foreach (var tickets in values)
            {
                if (tickets is IPlaybackSource ticket)
                {
                    Songs.Items.Add(new ComboBoxItem() { Content = ticket.Name, Tag = ticket });
                }
            }

        }

        private void Timeline_ManipulationCompleted(object sender, Windows.UI.Xaml.Input.ManipulationCompletedRoutedEventArgs e)
        {
            var value = (Songs.SelectedItem as ComboBoxItem)?.Tag as IPlaybackSource;
            if (value == null) return;
            var target = TimeSpan.FromMilliseconds(Timeline.Value);
            Player.SeekPlaybackSourceAsync(target, value);
            Sliding = false;
        }

        private void Songs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Songs.SelectedItem == null || SetMasterTicket.IsChecked == false) return;
            Player.PrimaryPlaybackSource = ((Songs.SelectedItem as ComboBoxItem).Tag as IPlaybackSource);
        }

        private void SongVolume_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            var value = (Songs.SelectedItem as ComboBoxItem)?.Tag as IPlaybackSource;
            if (value == null) return;
            Player.SetPlaybackSourceOutputVolumeAsync(e.NewValue / 100, value);
        }
    }
}
