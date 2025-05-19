using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HyPlayer.UWP.Chopin.Abstractions.Interfaces
{
    public interface IPlayer
    {
        double Volume { get; }
        Task InitializePlayer(IAudioSettings settings);
        Task ConnectPlaybackSourceAsync(IPlaybackSource playbackSource);
        Task DisconnectPlaybackSourceAsync(IPlaybackSource playbackSource);
        Task<List<IPlaybackSource>> GetConnectedPlaybackSourceAsync();
        Task PlayAllAsync();
        Task PauseAllAsync();
        Task SeekPlaybackSourceAsync(TimeSpan target, IPlaybackSource playbackSource);
        Task PausePlaybackSourceAsync(IPlaybackSource playbackSource);
        Task PlayPlaybackSourceAsync(IPlaybackSource playbackSource);
        Task SetPlaybackSourceSpeedAsync(double speed, IPlaybackSource playbackSource);
        Task SetOutputVolumeAsync(double volume);
        Task SetPlaybackSourceOutputVolumeAsync(double volume, IPlaybackSource playbackSource);
        Task ChangePlayerServiceImplementation(IAudioSettings settings);
    }
}
