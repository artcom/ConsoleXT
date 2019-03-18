using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace ArtCom.Logging
{
    public static class LogUtility
    {
        public static void WriteAllSpecs(Log log)
        {
            WriteSystemSpecs(log);
            WriteEnvironmentVariables(log);
            WriteUnitySpecs(log);
            WriteUnitySystemInfo(log);
            WriteLoadedAssemblies(log);
            WriteMemoryUsage(log);
        }

        public static void WriteSystemSpecs(Log log)
        {
            log.Write(
                "System Specs:" + Environment.NewLine + 
                "  Command Line: {0}" + Environment.NewLine + 
                "  Working Dir: {1}" + Environment.NewLine + 
                "  User Name: {2}" + Environment.NewLine + 
                "  User Domain: {3}" + Environment.NewLine + 
                "  Machine Name: {4}" + Environment.NewLine + 
                "  Operating System: {5}" + Environment.NewLine + 
                "  CLR Version: {6}" + Environment.NewLine + 
                "  64-Bit Process: {7}" + Environment.NewLine + 
                "  Processor Count: {8}" + Environment.NewLine,
                Environment.CommandLine,
                Environment.CurrentDirectory,
                Environment.UserName,
                Environment.UserDomainName,
                Environment.MachineName,
                Environment.OSVersion,
                Environment.Version,
                IntPtr.Size == 8 ? "true" : (IntPtr.Size == 4 ? "false" : "unknown"),
                Environment.ProcessorCount);
        }
        public static void WriteEnvironmentVariables(Log log)
        {
            try
            {
                var machineVars = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);
                var userVars = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User);
                var processVars = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process);

                log.Write(
                    "Machine Variables:" + Environment.NewLine + "{0}",
                    machineVars.Keys
                    .OfType<string>()
                    .Select(key => new KeyValuePair<string, object>(key, machineVars[key]))
                    .ToString(
                        pair => string.Format("  {0}: {1}", pair.Key, pair.Value), 
                        Environment.NewLine));
                log.Write(
                    "User Variables:" + Environment.NewLine + "{0}",
                    userVars.Keys
                    .OfType<string>()
                    .Select(key => new KeyValuePair<string, object>(key, userVars[key]))
                    .ToString(
                        pair => string.Format("  {0}: {1}", pair.Key, pair.Value), 
                        Environment.NewLine));
                log.Write(
                    "Process Variables:" + Environment.NewLine + "{0}",
                    processVars.Keys
                    .OfType<string>()
                    .Select(key => new KeyValuePair<string, object>(key, processVars[key]))
                    .ToString(
                        pair => string.Format("  {0}: {1}", pair.Key, pair.Value), 
                        Environment.NewLine));
            }
            catch (Exception e)
            {
                log.WriteWarning("Error logging environment variables: {0}", LogFormat.Exception(e));
            }
        }
        public static void WriteLoadedAssemblies(Log log)
        {
            try
            {
                log.Write(
                    "Currently Loaded Assemblies:" + Environment.NewLine + "{0}",
                    AppDomain.CurrentDomain.GetAssemblies()
                    .ToString(
                        assembly => string.Format("  {0}", LogFormat.Assembly(assembly)), 
                        Environment.NewLine));
            }
            catch (Exception e)
            {
                log.WriteWarning("Error logging loaded assemblies: {0}", LogFormat.Exception(e));
            }
        }
        public static void WriteUnitySpecs(Log log)
        {
            try
            {
                log.Write(
                    "Unity Application paths:" + Environment.NewLine +
                    "  Persistent data:  {0}" + Environment.NewLine +
                    "  Application Data: {1}" + Environment.NewLine +
                    "  Temp data:        {2}" + Environment.NewLine +
                    "  Streaming Assets: {3}",
                    UnityEngine.Application.persistentDataPath,
                    UnityEngine.Application.dataPath,
                    UnityEngine.Application.temporaryCachePath,
                    UnityEngine.Application.streamingAssetsPath);
                
                log.Write(
                    "Unity Screen info:" + Environment.NewLine +
                    "  Output size:        {0}x{1}" + Environment.NewLine +
                    "  Fullscreen:         {2}" + Environment.NewLine +
                    "  Screen Resolution:  {3}" + Environment.NewLine +
                    "  Screen Orientation: {4}" + Environment.NewLine +
                    "  Screen DPI:         {5}",
                    UnityEngine.Screen.width,
                    UnityEngine.Screen.height,
                    UnityEngine.Screen.fullScreen,
                    UnityEngine.Screen.currentResolution,
                    UnityEngine.Screen.orientation,
                    UnityEngine.Screen.dpi);
                
                log.Write("Unity Display info:");
                log.PushIndent();
                for (int i = 0; i < UnityEngine.Display.displays.Length; i++) {
                    UnityEngine.Display display = UnityEngine.Display.displays[i];
                    log.Write(
                        "Display #{0} {1}" + Environment.NewLine +
                        "  Native size:    {2}x{3}" + Environment.NewLine +
                        "  Rendering size: {4}x{5}",
                        i,
                        display == UnityEngine.Display.main ? "(main)" : "",
                        display.systemWidth,
                        display.systemHeight,
                        display.renderingWidth,
                        display.renderingHeight);
                }
                log.PopIndent();

                UnityEngine.AudioConfiguration audioConfig = UnityEngine.AudioSettings.GetConfiguration();
                log.Write(
                    "Unity Audio Device info:" + Environment.NewLine +
                    "  Driver Caps:        {0}" + Environment.NewLine +
                    "  Speaker Mode:       {1}" + Environment.NewLine +
                    "  Output Sample Rate: {2}" + Environment.NewLine +
                    "  DSP Buffer Size:    {3}" + Environment.NewLine +
                    "  # Real Voices:      {4}" + Environment.NewLine +
                    "  # Virtual  Voices:  {5}",
                    UnityEngine.AudioSettings.driverCapabilities,
                    UnityEngine.AudioSettings.speakerMode,
                    UnityEngine.AudioSettings.outputSampleRate,
                    audioConfig.dspBufferSize,
                    audioConfig.numRealVoices,
                    audioConfig.numVirtualVoices);
            }
            catch (Exception e)
            {
                log.WriteWarning("Error logging Unity specs: {0}", LogFormat.Exception(e));
            }
        }
        public static void WriteUnitySystemInfo(Log log)
        {
            try
            {
                log.Write(
                    "Unity Device SystemInfo:" + Environment.NewLine +
                    "  Type:  {0}" + Environment.NewLine +
                    "  Model: {1}" + Environment.NewLine +
                    "  Name:  {2}" + Environment.NewLine +
                    "  UUID:  {3}",
                    UnityEngine.SystemInfo.deviceType,
                    UnityEngine.SystemInfo.deviceModel,
                    UnityEngine.SystemInfo.deviceName,
                    UnityEngine.SystemInfo.deviceUniqueIdentifier);
                
                log.Write(
                    "Unity Device Feature SystemInfo:" + Environment.NewLine +
                    "  Audio:            {0}" + Environment.NewLine +
                    "  Gyroscope:        {1}" + Environment.NewLine +
                    "  Location Service: {2}" + Environment.NewLine +
                    "  Vibration:        {3}" + Environment.NewLine +
                    "  Accelerometer:    {4}",
                    UnityEngine.SystemInfo.supportsAudio,
                    UnityEngine.SystemInfo.supportsGyroscope,
                    UnityEngine.SystemInfo.supportsLocationService,
                    UnityEngine.SystemInfo.supportsVibration,
                    UnityEngine.SystemInfo.supportsAccelerometer);

                log.Write(
                    "Unity Machine / OS SystemInfo:" + Environment.NewLine +
                    "  OS:               {0}" + Environment.NewLine +
                    "  OS Family:        {1}" + Environment.NewLine +
                    "  Processor Type:   {2}" + Environment.NewLine +
                    "  Processor Count:  {3}" + Environment.NewLine +
                    "  Processor Freq.:  {4}" + Environment.NewLine +
                    "  Sys. Memory size: {5}",
                    UnityEngine.SystemInfo.operatingSystem,
                    UnityEngine.SystemInfo.operatingSystemFamily,
                    UnityEngine.SystemInfo.processorType,
                    UnityEngine.SystemInfo.processorCount,
                    UnityEngine.SystemInfo.processorFrequency,
                    UnityEngine.SystemInfo.systemMemorySize);
                
                log.Write(
                    "Unity Graphics SystemInfo:" + Environment.NewLine +
                    "  Device Type:    {0}" + Environment.NewLine +
                    "  Device Name:    {1}" + Environment.NewLine +
                    "  Device ID:      {2}" + Environment.NewLine +
                    "  Device Version: {3}" + Environment.NewLine +
                    "  Vendor:         {4}" + Environment.NewLine +
                    "  Vendor ID:      {5}",
                    UnityEngine.SystemInfo.graphicsDeviceType,
                    UnityEngine.SystemInfo.graphicsDeviceName,
                    UnityEngine.SystemInfo.graphicsDeviceID,
                    UnityEngine.SystemInfo.graphicsDeviceVersion,
                    UnityEngine.SystemInfo.graphicsDeviceVendor,
                    UnityEngine.SystemInfo.graphicsDeviceVendorID);

                Array textureFormats = Enum.GetValues(typeof(UnityEngine.TextureFormat));
                HashSet<UnityEngine.TextureFormat> supportedTextureFormats = new HashSet<UnityEngine.TextureFormat>();
                foreach (UnityEngine.TextureFormat format in textureFormats)
                {
                    try
                    {
                        if (UnityEngine.SystemInfo.SupportsTextureFormat(format))
                            supportedTextureFormats.Add(format);
                    }
                    catch (Exception) {}
                }

                Array renderTextureFormats = Enum.GetValues(typeof(UnityEngine.RenderTextureFormat));
                HashSet<UnityEngine.RenderTextureFormat> supportedRenderTextureFormats = new HashSet<UnityEngine.RenderTextureFormat>();
                foreach (UnityEngine.RenderTextureFormat format in renderTextureFormats)
                {
                    try
                    {
                        if (UnityEngine.SystemInfo.SupportsRenderTextureFormat(format))
                            supportedRenderTextureFormats.Add(format);
                    }
                    catch (Exception) {}
                }

                log.Write(
                    "Unity Graphics Feature SystemInfo:" + Environment.NewLine +
                    "  GPU Memory Size:     {0}" + Environment.NewLine +
                    "  Shader Level:        {1}" + Environment.NewLine +
                    "  Compute Shaders:     {2}" + Environment.NewLine +
                    "  Multi-Threaded:      {3}" + Environment.NewLine +
                    "  Reversed Z-Buffer:   {4}" + Environment.NewLine +
                    "  Max. Texture Size:   {5}" + Environment.NewLine +
                    "  NPOT Textures:       {6}" + Environment.NewLine +
                    "  CopyTexture:         {7}" + Environment.NewLine +
                    "  RenderToCubemap:     {8}" + Environment.NewLine +
                    "  RenderTarget Count:  {9}" + Environment.NewLine +
                    "  2D Array Textures:   {10}" + Environment.NewLine +
                    "  Cube Array Textures: {11}" + Environment.NewLine +
                    "  3D Textures:         {12}" + Environment.NewLine +
                    "  Sparse Textures:     {13}" + Environment.NewLine +
                    "  Instancing:          {14}" + Environment.NewLine +
                    "  Image Effects:       {15}" + Environment.NewLine +
                    "  Motion Vectors:      {16}" + Environment.NewLine +
                    "  Raw Shadow Depth:    {17}" + Environment.NewLine +
                    "  Shadows:             {18}" + Environment.NewLine +
                    "  RenderTex Formats:   {19}" + Environment.NewLine +
                    "  Texture Formats:     {20}",
                    UnityEngine.SystemInfo.graphicsMemorySize,
                    UnityEngine.SystemInfo.graphicsShaderLevel,
                    UnityEngine.SystemInfo.supportsComputeShaders,
                    UnityEngine.SystemInfo.graphicsMultiThreaded,
                    UnityEngine.SystemInfo.usesReversedZBuffer,
                    UnityEngine.SystemInfo.maxTextureSize,
                    UnityEngine.SystemInfo.npotSupport,
                    UnityEngine.SystemInfo.copyTextureSupport,
                    UnityEngine.SystemInfo.supportsRenderToCubemap,
                    UnityEngine.SystemInfo.supportedRenderTargetCount,
                    UnityEngine.SystemInfo.supports2DArrayTextures,
                    UnityEngine.SystemInfo.supportsCubemapArrayTextures,
                    UnityEngine.SystemInfo.supports3DTextures,
                    UnityEngine.SystemInfo.supportsSparseTextures,
                    UnityEngine.SystemInfo.supportsInstancing,
                    UnityEngine.SystemInfo.supportsImageEffects,
                    UnityEngine.SystemInfo.supportsMotionVectors,
                    UnityEngine.SystemInfo.supportsRawShadowDepthSampling,
                    UnityEngine.SystemInfo.supportsShadows,
                    supportedRenderTextureFormats.ToString(", "),
                    supportedTextureFormats.ToString(", "));

            }
            catch (Exception e)
            {
                log.WriteWarning("Error logging Unity system info: {0}", LogFormat.Exception(e));
            }
        }
        public static void WriteMemoryUsage(Log log) {
            if (UnityEngine.Profiling.Profiler.supported)
            {
                log.Write("Memory Usage (Unity Profiler):" + Environment.NewLine +
                    "  Mono Heap Size:         {0} MiB" + Environment.NewLine +
                    "  Mono Used Size:         {1} MiB" + Environment.NewLine +
                    "  Total Allocated Memory: {2} MiB" + Environment.NewLine +
                    "  Total Reserved Memory:  {3} MiB" + Environment.NewLine +
                    "  Unused Reserved Memory: {4} MiB" + Environment.NewLine +
                    "  Used Heap Size:         {5} MiB",
                    #if UNITY_5_6_OR_NEWER
                    UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong() / 1024L / 1024L,
                    UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1024L / 1024L,
                    UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1024L / 1024L,
                    UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1024L / 1024L,
                    UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemoryLong() / 1024L / 1024L,
                    UnityEngine.Profiling.Profiler.usedHeapSizeLong / 1024L / 1024L);
                    #else
                    UnityEngine.Profiling.Profiler.GetMonoHeapSize() / 1024L / 1024L,
                    UnityEngine.Profiling.Profiler.GetMonoUsedSize() / 1024L / 1024L,
                    UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory() / 1024L / 1024L,
                    UnityEngine.Profiling.Profiler.GetTotalReservedMemory() / 1024L / 1024L,
                    UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemory() / 1024L / 1024L,
                    UnityEngine.Profiling.Profiler.usedHeapSize / 1024L / 1024L);
                    #endif
            }

            try
            {
                log.Write("Memory Usage (GC):" + Environment.NewLine +
                    "  Total Memory: {0} MiB" + Environment.NewLine +
                    "  Collections:  {1} | {2} | {3}",
                    GC.GetTotalMemory(false) / 1024L / 1024L,
                    GC.CollectionCount(0),
                    GC.CollectionCount(1),
                    GC.CollectionCount(2));
            }
            catch (Exception e)
            {
                log.WriteWarning("Error logging GC memory stats: {0}", LogFormat.Exception(e));
            }
        }
    }

}