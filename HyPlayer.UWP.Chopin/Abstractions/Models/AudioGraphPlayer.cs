using HyPlayer.UWP.Chopin.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Audio;
using Timer = System.Timers.Timer;

namespace HyPlayer.UWP.Chopin.Abstractions.Models
{
    public class AudioGraphPlayer : IPlayer, IDisposable
    {
        private Dictionary<IPlaybackSource, MediaSourceAudioInputNode> _audioInputNodes = new Dictionary<IPlaybackSource, MediaSourceAudioInputNode>();
        private Dictionary<IPlaybackSource, bool> _playbackStatus = new Dictionary<IPlaybackSource, bool>();
        private AudioGraph _defaultPlayer;
        private AudioDeviceOutputNode _outputNode;
        private bool disposedValue;
        private Timer PositionTimer = new Timer() { AutoReset = true, Enabled = true, Interval = 100 };

        public bool PlayerCreated => _defaultPlayer != null;
        public delegate void PositionChangeHandler(TimeSpan position);
        public event PositionChangeHandler OnPositionChange;
        public delegate void TrackReachesEndHandler(IPlaybackSource source);
        public event TrackReachesEndHandler OnTrackReachesEnd;

        public double Volume { get => _outputNode?.OutgoingGain ?? 0; }
        public IPlaybackSource PrimaryPlaybackSource { get; set; }
        public async Task ChangePlayerServiceImplementation(IAudioSettings settings)
        {
            if (settings is AudioGraphAudioSetting audioGraphSetting)
            {
                var oldPlayer = _defaultPlayer;
                var oldOutputNode = _outputNode;
                var setting = await audioGraphSetting.GetAudioGraphSettingsAsync();
                var newPlayerResult = await AudioGraph.CreateAsync(setting);
                AudioGraph newPlayer;
                if (newPlayerResult.Status == AudioGraphCreationStatus.Success)
                {
                    newPlayer = newPlayerResult.Graph;
                }
                else
                {
                    throw newPlayerResult.ExtendedError;
                }
                oldPlayer.Stop();
                var oldNodes = _audioInputNodes;
                var deviceNodeCreateResult = await newPlayer.CreateDeviceOutputNodeAsync();
                if (deviceNodeCreateResult.Status != AudioDeviceNodeCreationStatus.Success) throw deviceNodeCreateResult.ExtendedError;
                _outputNode = deviceNodeCreateResult.DeviceOutputNode;
                var newNodes = new Dictionary<IPlaybackSource, MediaSourceAudioInputNode>();
                foreach (var node in oldNodes)
                {
                    if (node.Key is AudioGraphPlaybackSource audioGraphPlaybackSource)
                    {
                        if (node.Key.PlaybackSource is null) await node.Key.CreatePlaybackSource();
                        node.Key.PlaybackSource.Reset();
                        var createResult = await newPlayer.CreateMediaSourceAudioInputNodeAsync(node.Key.PlaybackSource);
                        if (createResult.Status != MediaSourceAudioInputNodeCreationStatus.Success) throw createResult.ExtendedError;
                        var outputNode = createResult.Node;
                        newNodes[node.Key] = outputNode;
                        outputNode.Seek(node.Value.Position);
                        outputNode.PlaybackSpeedFactor = node.Value.PlaybackSpeedFactor;
                        outputNode.OutgoingGain = node.Value.OutgoingGain;
                        outputNode.AddOutgoingConnection(_outputNode);
                        foreach (var effect in node.Value.EffectDefinitions)
                        {
                            outputNode.EnableEffectsByDefinition(effect);
                        }
                        if (_playbackStatus[node.Key]) outputNode.Start();
                    }
                }
                _outputNode.OutgoingGain = oldOutputNode.OutgoingGain;
                _defaultPlayer = newPlayer;
                newPlayer.Start();
                _audioInputNodes = newNodes;
                oldOutputNode?.Dispose();
                oldPlayer.Dispose();
            }
            else
            {
                throw new ArgumentException("Setting is not AudioGraphSetting");
            }
        }

        public async Task InitializePlayer(IAudioSettings settings)
        {
            PositionTimer.Elapsed += PositionTimer_Elapsed;
            if (settings is AudioGraphAudioSetting audioGraphSetting)
            {
                var setting = await audioGraphSetting.GetAudioGraphSettingsAsync();
                var newPlayerResult = await AudioGraph.CreateAsync(setting);
                AudioGraph newPlayer;
                if (newPlayerResult.Status == AudioGraphCreationStatus.Success)
                {
                    newPlayer = newPlayerResult.Graph;
                }
                else
                {
                    throw newPlayerResult.ExtendedError;
                }
                _defaultPlayer = newPlayer;
                var createResult = await newPlayer.CreateDeviceOutputNodeAsync();
                if (createResult.Status != AudioDeviceNodeCreationStatus.Success) throw createResult.ExtendedError;
                _outputNode = createResult.DeviceOutputNode;
                _outputNode.OutgoingGain = audioGraphSetting.OutputVolume;
                _defaultPlayer.Start();
            }
            else
            {
                throw new ArgumentException("Setting is not AudioGraphSetting");
            }
        }

        private void PositionTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (PrimaryPlaybackSource is null) return;
            var position = _audioInputNodes[PrimaryPlaybackSource].Position;
            OnPositionChange?.Invoke(position);
        }

        public Task PauseAllAsync()
        {
            if (_defaultPlayer == null) return Task.CompletedTask;
            _defaultPlayer.Stop();
            return Task.CompletedTask;
        }

        public Task PausePlaybackSourceAsync(IPlaybackSource playbackSource)
        {
            _audioInputNodes[playbackSource].Stop();
            _playbackStatus[playbackSource] = false;
            return Task.CompletedTask;
        }

        public Task PlayAllAsync()
        {
            if (_defaultPlayer == null) return Task.CompletedTask;
            _defaultPlayer.Start();
            return Task.CompletedTask;
        }

        public Task PlayPlaybackSourceAsync(IPlaybackSource playbackSource)
        {
            _audioInputNodes[playbackSource].Start();
            _playbackStatus[playbackSource] = true;
            return Task.CompletedTask;
        }

        public Task SeekPlaybackSourceAsync(TimeSpan target, IPlaybackSource playbackSource)
        {
            _audioInputNodes[playbackSource].Seek(target);
            return Task.CompletedTask;
        }

        public Task SetOutputVolumeAsync(double volume)
        {
            if (_outputNode != null)
            {
                _outputNode.OutgoingGain = volume;
            }
            return Task.CompletedTask;
        }

        public Task SetPlaybackSourceOutputVolumeAsync(double volume, IPlaybackSource playbackSource)
        {
            var item = _audioInputNodes[playbackSource];
            item.OutgoingGain = volume;
            return Task.CompletedTask;
        }

        public Task SetPlaybackSourceSpeedAsync(double speed, IPlaybackSource playbackSource)
        {
            var item = _audioInputNodes[playbackSource];
            item.PlaybackSpeedFactor = speed;
            return Task.CompletedTask;
        }
        public async Task ConnectPlaybackSourceAsync(IPlaybackSource playbackSource)
        {
            if (_defaultPlayer == null) return;
            if (playbackSource.PlaybackSource == null) await playbackSource.CreatePlaybackSource();
            var nodeResult = await _defaultPlayer.CreateMediaSourceAudioInputNodeAsync(playbackSource.PlaybackSource);
            if (nodeResult.Status != MediaSourceAudioInputNodeCreationStatus.Success) throw nodeResult.ExtendedError;
            _audioInputNodes[playbackSource] = nodeResult.Node;
            nodeResult.Node.Stop();
            nodeResult.Node.AddOutgoingConnection(_outputNode);
            _playbackStatus[playbackSource] = false;
            if (_audioInputNodes.Count == 1) PrimaryPlaybackSource = playbackSource;
            _audioInputNodes[playbackSource].MediaSourceCompleted += OnMediaSourceCompleted;
        }

        private void OnMediaSourceCompleted(MediaSourceAudioInputNode sender, object args)
        {
            var playbackSource = _audioInputNodes.Where(t => t.Value == sender).FirstOrDefault().Key;
            OnTrackReachesEnd?.Invoke(playbackSource);
        }

        public Task DisconnectPlaybackSourceAsync(IPlaybackSource playbackSource)
        {
            _audioInputNodes[playbackSource].MediaSourceCompleted -= OnMediaSourceCompleted;
            if (PrimaryPlaybackSource == playbackSource) PrimaryPlaybackSource = null;
            _audioInputNodes[playbackSource].RemoveOutgoingConnection(_outputNode);
            _audioInputNodes[playbackSource].Dispose();
            _audioInputNodes.Remove(playbackSource);
            _playbackStatus.Remove(playbackSource);
            return Task.CompletedTask;
        }
        public Task<List<IPlaybackSource>> GetConnectedPlaybackSourceAsync()
        {
            return Task.FromResult(_audioInputNodes.Keys.ToList());
        }
        public Task<MediaSourceAudioInputNode> GetAudioInputNode(IPlaybackSource playbackSource)
        {
            return Task.FromResult(_audioInputNodes[playbackSource]);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {

                }

                foreach (var item in _audioInputNodes.Values)
                {
                    item.RemoveOutgoingConnection(_outputNode);
                    item.Dispose();
                }
                _outputNode.Dispose();
                _defaultPlayer.Dispose();
                disposedValue = true;
            }
        }

        ~AudioGraphPlayer()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        public void ThrowExceptionIfDisposed()
        {
            if (disposedValue) throw new ObjectDisposedException(nameof(AudioGraphPlaybackSource));
        }
    }
}
