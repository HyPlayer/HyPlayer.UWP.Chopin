using HyPlayer.UWP.Chopin.Abstractions.Models;
using System;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Storage;

namespace HyPlayer.UWP.Chopin.Abstractions.Interfaces
{
    public interface IPlaybackSource
    {
        string Name { get; set; }
        MediaSource PlaybackSource { get; }
        StorageFile LocalStorageFile { get; }
        PlaybackSourceType PlaybackSourceType { get; }
        Uri Path { get; set; }
        Task CreatePlaybackSource();
    }
}
