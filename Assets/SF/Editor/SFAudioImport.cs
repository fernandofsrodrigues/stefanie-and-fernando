using UnityEditor;
using UnityEngine;

namespace StefanieAndFernando.Editor
{
    public sealed class SFAudioImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if(!assetPath.StartsWith("Assets/SF/Resources/Audio/"))return;
            var importer=(AudioImporter)assetImporter;
            bool music=assetPath.EndsWith("pressure.wav")||System.IO.Path.GetFileName(assetPath).StartsWith("music_");
            importer.forceToMono=!music;
            var settings=importer.defaultSampleSettings;
            settings.preloadAudioData=!music;
            settings.loadType=music?AudioClipLoadType.CompressedInMemory:AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat=AudioCompressionFormat.Vorbis;
            settings.quality=music?.85f:1f;
            settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings=settings;
        }
    }
}
