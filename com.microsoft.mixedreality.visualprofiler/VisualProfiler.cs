// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;

#if UNITY_STANDALONE_WIN || UNITY_WSA
using UnityEngine.Windows.Speech;
#endif

#if WINDOWS_UWP
using Windows.System;
#endif

namespace Microsoft.MixedReality.Profiling
{
    /// <summary>
    /// 
    /// ABOUT: The VisualProfiler provides a drop in, single file, solution for viewing 
    /// your Windows Mixed Reality Unity application's frame rate and memory usage. Missed 
    /// frames are displayed over time to visually find problem areas. Memory is reported 
    /// as current, peak and max usage in a bar graph. 
    /// 
    /// USAGE: To use this profiler simply add this script as a component of any GameObject in 
    /// your Unity scene. The profiler is initially active and visible (toggle-able via the 
    /// IsVisible property), but can be toggled via the enabled/disable voice commands keywords.
    ///
    /// NOTE: For improved rendering performance you can optionally include the 
    /// "Hidden/Instanced-Colored" shader in your project along with the VisualProfiler.
    /// 
    /// IMPORTANT: Please make sure to add the microphone capability to your app if you plan 
    /// on using the enable/disable keywords, in Unity under Edit -> Project Settings -> 
    /// Player -> Settings for Windows Store -> Publishing Settings -> Capabilities or in your 
    /// Visual Studio Package.appxmanifest capabilities.
    /// 
    /// </summary>
    public class VisualProfiler : MonoBehaviour
    {
        [Header("Profiler Settings")]
        [SerializeField, Tooltip("Is the profiler currently visible?")]
        private bool isVisible = true;

        public bool IsVisible
        {
            get { return isVisible; }
            set
            {
                if (isVisible != value)
                {
                    isVisible = value;

                    if (isVisible)
                    {
                        Refresh();
                    }
                }
            }
        }

        [SerializeField, Tooltip("The amount of time, in seconds, to collect frames for frame rate calculation.")]
        private float frameSampleRate = 0.1f;

        public float FrameSampleRate
        {
            get { return frameSampleRate; }
            set { frameSampleRate = value; }
        }

        [Header("Window Settings")]
        [SerializeField, Tooltip("What part of the view port to anchor the window to.")]
        private TextAnchor windowAnchor = TextAnchor.LowerCenter;

        public TextAnchor WindowAnchor
        {
            get { return windowAnchor; }
            set { windowAnchor = value; }
        }

        [SerializeField, Tooltip("The offset from the view port center applied based on the window anchor selection.")]
        private Vector2 windowOffset = new Vector2(0.1f, 0.1f);

        public Vector2 WindowOffset
        {
            get { return windowOffset; }
            set { windowOffset = value; }
        }

        [SerializeField, Range(0.5f, 5.0f), Tooltip("Use to scale the window size up or down, can simulate a zooming effect.")]
        private float windowScale = 1.0f;

        public float WindowScale
        {
            get { return windowScale; }
            set { windowScale = Mathf.Clamp(value, 0.5f, 5.0f); }
        }

        [SerializeField, Range(0.0f, 100.0f), Tooltip("How quickly to interpolate the window towards its target position and rotation.")]
        private float windowFollowSpeed = 5.0f;

        public float WindowFollowSpeed
        {
            get { return windowFollowSpeed; }
            set { windowFollowSpeed = Mathf.Abs(value); }
        }

        [SerializeField, Tooltip("Voice commands to toggle the profiler on and off.")]
        private string[] toggleKeyworlds = new string[] { "Profiler", "Toggle Profiler", "Show Profiler", "Hide Profiler" };

        [Header("UI Settings")]
        [SerializeField, Tooltip("The material to use when rendering the profiler. The material should use the \"Hidden / Visual Profiler\" shader and have a font texture.")]
        private Material material;

        [SerializeField, Range(0, 3), Tooltip("How many decimal places to display on numeric strings.")]
        private int displayedDecimalDigits = 1;

        [SerializeField, Tooltip("The color of the window backplate.")]
        private Color baseColor = new Color(20 / 256.0f, 20 / 256.0f, 20 / 256.0f, 1.0f);

        [SerializeField, Tooltip("The color to display on frames which meet or exceed the target frame rate.")]
        private Color targetFrameRateColor = new Color(127 / 256.0f, 186 / 256.0f, 0 / 256.0f, 1.0f);

        [SerializeField, Tooltip("The color to display on frames which fall below the target frame rate.")]
        private Color missedFrameRateColor = new Color(242 / 256.0f, 80 / 256.0f, 34 / 256.0f, 1.0f);

        [SerializeField, Tooltip("The color to display for current memory usage values.")]
        private Color memoryUsedColor = new Color(0 / 256.0f, 164 / 256.0f, 239 / 256.0f, 1.0f);

        [SerializeField, Tooltip("The color to display for peak (aka max) memory usage values.")]
        private Color memoryPeakColor = new Color(255 / 256.0f, 185 / 256.0f, 0 / 256.0f, 1.0f);

        [SerializeField, Tooltip("The color to display for the platforms memory usage limit.")]
        private Color memoryLimitColor = new Color(50 / 256.0f, 50 / 256.0f, 50 / 256.0f, 1.0f);

        [Header("Font Settings")]
        [SerializeField, Tooltip("The width and height of a mono spaced character in the font texture (in pixels).")]
        private Vector2Int fontCharacterSize = new Vector2Int(16, 30);

        [SerializeField, Tooltip("The x and y scale to render a character at.")]
        private Vector2 fontScale = new Vector2(0.00023f, 0.00028f);

        [SerializeField, Min(1), Tooltip("How many characters are in a row of the font texture.")]
        private int fontColumns = 32;

        private class TextData
        {
            public Vector3 Position;
            public bool RightAligned;
            public int Offset;

            public TextData(Vector3 position, bool rightAligned, int offset)
            {
                Position = position;
                RightAligned = rightAligned;
                Offset = offset;
            }
        }

        // Constants.
        private const int maxStringLength = 32;
        private const int maxTargetFrameRate = 120;
        private const int maxFrameTimings = 128;
        private const int frameRange = 30;

        private const int backplateInstanceOffset = 0;

        private const int framesInstanceOffset = backplateInstanceOffset + 1;

        private const int limitInstanceOffset = framesInstanceOffset + frameRange;
        private const int peakInstanceOffset = limitInstanceOffset + 1;
        private const int usedInstanceOffset = peakInstanceOffset + 1;

        private const int cpuframeRateTextOffset = usedInstanceOffset + 1;
        private const int gpuframeRateTextOffset = cpuframeRateTextOffset + maxStringLength;

        private const int drawCallPassTextOffset = gpuframeRateTextOffset + maxStringLength;
        private const int verticiesTextOffset = drawCallPassTextOffset + maxStringLength;

        private const int usedMemoryTextOffset = verticiesTextOffset + maxStringLength;
        private const int peakMemoryTextOffset = usedMemoryTextOffset + maxStringLength;
        private const int limitMemoryTextOffset = peakMemoryTextOffset + maxStringLength;

        private const int instanceCount = limitMemoryTextOffset + maxStringLength;

        private const string drawPassCallPrefix = "Draw/Pass: ";
        private const string verticiesPrefix = "Verts: ";
        private const string usedMemoryPrefix = "Used: ";
        private const string peakMemoryPrefix = "Peak: ";
        private const string limitMemoryPrefix = "Limit: ";

        // Pre computed state.
        private char[][] frameRateStrings = new char[maxTargetFrameRate + 1][];
        private char[][] gpuFrameRateStrings = new char[maxTargetFrameRate + 1][];
        private Vector4[] characterUVs = new Vector4[128];
        private Vector3 characterScale;

        // State.
        private Vector3 windowPosition = Vector3.zero;
        private Quaternion windowRotation = Quaternion.identity;

        private TextData cpuFrameRateText;
        private TextData gpuFrameRateText;

        private TextData drawCallPassText;
        private TextData verticiesText;

        private TextData usedMemoryText;
        private TextData peakMemoryText;
        private TextData limitMemoryText;

        private Quaternion windowHorizontalRotation;
        private Quaternion windowHorizontalRotationInverse;
        private Quaternion windowVerticalRotation;
        private Quaternion windowVerticalRotationInverse;

        private int colorID;
        private int uvOffsetScaleXID;
        private int windowLocalToWorldID;

        private int frameCount;
        private System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        private FrameTiming[] frameTimings = new FrameTiming[maxFrameTimings];

        private char[] stringBuffer = new char[maxStringLength];

        private ProfilerRecorder drawCallsRecorder;
        private ProfilerRecorder setPassCallsRecorder;
        private ProfilerRecorder verticesRecorder;

        private long drawCalls;
        private long setPassCalls;
        private long vertexCount;
        private ulong memoryUsage;
        private ulong peakMemoryUsage;
        private ulong limitMemoryUsage;

        // Rendering resources.
        private Mesh quadMesh;

        private MaterialPropertyBlock instancePropertyBlock;
        private Matrix4x4[] instanceMatrices = new Matrix4x4[instanceCount];
        private Vector4[] instanceColors = new Vector4[instanceCount];
        private Vector4[] instanceUVOffsetScaleX = new Vector4[instanceCount];
        private bool instanceColorsDirty;
        private bool instanceUVOffsetScaleXDirty;

        private void Start()
        {
            Initialize();
            BuildWindow();

#if UNITY_STANDALONE_WIN || UNITY_WSA
            BuildKeywordRecognizer();
#endif
        }

        private void OnValidate()
        {
            Refresh();
            BuildWindow();
        }

        private void OnDestroy()
        {
#if UNITY_STANDALONE_WIN || UNITY_WSA
            if (keywordRecognizer.IsRunning)
            {
                keywordRecognizer.Stop();
            }
#endif
            verticesRecorder.Dispose();
            setPassCallsRecorder.Dispose();
            drawCallsRecorder.Dispose();
        }

        private void LateUpdate()
        {
            if (IsVisible)
            {
                // Update window transformation.
                Transform cameraTransform = Camera.main ? Camera.main.transform : null;

                if (cameraTransform != null)
                {
                    float t = Time.deltaTime * windowFollowSpeed;
                    windowPosition = Vector3.Lerp(windowPosition, CalculateWindowPosition(cameraTransform), t);
                    // Lerp rather than slerp for speed over quality.
                    windowRotation = Quaternion.Lerp(windowRotation, CalculateWindowRotation(cameraTransform), t);
                }

                // Capture frame timings every frame and read from it depending on the frameSampleRate.
                FrameTimingManager.CaptureFrameTimings();

                ++frameCount;
                float elapsedSeconds = stopwatch.ElapsedMilliseconds * 0.001f;

                if (elapsedSeconds >= frameSampleRate)
                {
                    int cpuFrameRate = (int)(1.0f / (elapsedSeconds / frameCount));
                    int gpuFrameRate = 0;

                    // Many platforms do not yet support the FrameTimingManager. When timing data is returned from the FrameTimingManager we will use
                    // its timing data, else we will depend on the stopwatch. Wider support is coming in Unity 2022.1+
                    // https://blog.unity.com/technology/detecting-performance-bottlenecks-with-unity-frame-timing-manager
                    uint frameTimingsCount = FrameTimingManager.GetLatestTimings((uint)Mathf.Min(frameCount, maxFrameTimings), frameTimings);

                    if (frameTimingsCount != 0)
                    {
                        float cpuFrameTime, gpuFrameTime;
                        AverageFrameTiming(frameTimings, frameTimingsCount, out cpuFrameTime, out gpuFrameTime);
                        cpuFrameRate = (int)(1.0f / (cpuFrameTime / frameCount));
                        gpuFrameRate = (int)(1.0f / (gpuFrameTime / frameCount));
                    }

                    Color cpuFrameColor = (cpuFrameRate < ((int)(AppFrameRate) - 1)) ? missedFrameRateColor : targetFrameRateColor;

                    // Update frame rate text.
                    {
                        char[] text = frameRateStrings[Mathf.Clamp(cpuFrameRate, 0, maxTargetFrameRate)];
                        SetText(cpuFrameRateText, text, text.Length, cpuFrameColor);
                    }

                    if (gpuFrameRate != 0)
                    {
                        char[] text = gpuFrameRateStrings[Mathf.Clamp(gpuFrameRate, 0, maxTargetFrameRate)];
                        Color color = (gpuFrameRate < ((int)(AppFrameRate) - 1)) ? missedFrameRateColor : targetFrameRateColor;
                        SetText(gpuFrameRateText, text, text.Length, color);
                    }

                    // Update frame colors.
                    // TODO: Ideally we would query a device specific API (like the HolographicFramePresentationReport) to detect missed frames.
                    for (int i = frameRange - 1; i > 0; --i)
                    {
                        instanceColors[framesInstanceOffset + i] = instanceColors[framesInstanceOffset + i - 1];
                    }

                    instanceColors[framesInstanceOffset + 0] = cpuFrameColor;
                    instancePropertyBlock.SetVectorArray(colorID, instanceColors);
                    instanceColorsDirty = true;

                    // Reset timers.
                    frameCount = 0;
                    stopwatch.Reset();
                    stopwatch.Start();
                }

                // Update scene statistics.
                long lastDrawCalls = drawCallsRecorder.LastValue;
                long lastSetPassCalls = setPassCallsRecorder.LastValue;

                if (lastDrawCalls != drawCalls || lastSetPassCalls != setPassCalls)
                {
                    DrawPassCallsToString(stringBuffer, drawCallPassText, drawPassCallPrefix, lastDrawCalls, lastSetPassCalls);

                    drawCalls = lastDrawCalls;
                    setPassCalls = lastSetPassCalls;
                }

                long lastVertexCount = verticesRecorder.LastValue;

                if (lastVertexCount != vertexCount)
                {
                    if (WillDisplayedVertexCountDiffer(lastVertexCount, vertexCount, displayedDecimalDigits))
                    {
                        VertexCountToString(stringBuffer, displayedDecimalDigits, verticiesText, verticiesPrefix, lastVertexCount);
                    }

                    vertexCount = lastVertexCount;
                }
            }

            // Update memory statistics.
            ulong limit = AppMemoryUsageLimit;

            if (limit != limitMemoryUsage)
            {
                if (IsVisible && WillDisplayedMemoryUsageDiffer(limitMemoryUsage, limit, displayedDecimalDigits))
                {
                    MemoryUsageToString(stringBuffer, displayedDecimalDigits, limitMemoryText, limitMemoryPrefix, limit, Color.white);
                }

                limitMemoryUsage = limit;
            }

            ulong usage = AppMemoryUsage;

            if (usage != memoryUsage)
            {
                Vector4 offsetScale = instanceUVOffsetScaleX[usedInstanceOffset];
                offsetScale.z = -1.0f + (float)usage / limitMemoryUsage;
                instanceUVOffsetScaleX[usedInstanceOffset] = offsetScale;

                if (IsVisible && WillDisplayedMemoryUsageDiffer(memoryUsage, usage, displayedDecimalDigits))
                {
                    MemoryUsageToString(stringBuffer, displayedDecimalDigits, usedMemoryText, usedMemoryPrefix, usage, memoryUsedColor);
                }

                memoryUsage = usage;
            }

            if (memoryUsage > peakMemoryUsage)
            {
                Vector4 offsetScale = instanceUVOffsetScaleX[peakInstanceOffset];
                offsetScale.z = -1.0f + (float)memoryUsage / limitMemoryUsage;
                instanceUVOffsetScaleX[peakInstanceOffset] = offsetScale;

                if (IsVisible && WillDisplayedMemoryUsageDiffer(peakMemoryUsage, memoryUsage, displayedDecimalDigits))
                {
                    MemoryUsageToString(stringBuffer, displayedDecimalDigits, peakMemoryText, peakMemoryPrefix, memoryUsage, memoryPeakColor);
                }

                peakMemoryUsage = memoryUsage;
            }

            // Render.
            if (IsVisible)
            {
                Matrix4x4 windowLocalToWorldMatrix = Matrix4x4.TRS(windowPosition, windowRotation, Vector3.one * windowScale);
                instancePropertyBlock.SetMatrix(windowLocalToWorldID, windowLocalToWorldMatrix);

                if (instanceColorsDirty)
                {
                    instancePropertyBlock.SetVectorArray(colorID, instanceColors);
                    instanceColorsDirty = false;
                }

                if (instanceUVOffsetScaleXDirty)
                {
                    instancePropertyBlock.SetVectorArray(uvOffsetScaleXID, instanceUVOffsetScaleX);
                    instanceUVOffsetScaleXDirty = false;
                }

                if (material != null)
                {
                    Graphics.DrawMeshInstanced(quadMesh, 0, material, instanceMatrices, instanceMatrices.Length, instancePropertyBlock, UnityEngine.Rendering.ShadowCastingMode.Off, false);
                }
            }
        }

        private void Initialize()
        {
            // Create a quad mesh with artificially large bounds to disable culling for instanced rendering.
            // TODO: Use shared mesh with normal bounds once Unity allows for more control over instance culling.
            {
                MeshFilter quadMeshFilter = GameObject.CreatePrimitive(PrimitiveType.Quad).GetComponent<MeshFilter>();
                quadMesh = quadMeshFilter.mesh;
                quadMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100.0f);

                Destroy(quadMeshFilter.gameObject);
            }

            colorID = Shader.PropertyToID("_Color");
            uvOffsetScaleXID = Shader.PropertyToID("_UVOffsetScaleX");
            windowLocalToWorldID = Shader.PropertyToID("_WindowLocalToWorldMatrix");
            instancePropertyBlock = new MaterialPropertyBlock();

            Vector2 defaultWindowRotation = new Vector2(10.0f, 20.0f);

            windowHorizontalRotation = Quaternion.AngleAxis(defaultWindowRotation.y, Vector3.right);
            windowHorizontalRotationInverse = Quaternion.Inverse(windowHorizontalRotation);
            windowVerticalRotation = Quaternion.AngleAxis(defaultWindowRotation.x, Vector3.up);
            windowVerticalRotationInverse = Quaternion.Inverse(windowVerticalRotation);

            drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            setPassCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            verticesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");

            stopwatch.Reset();
            stopwatch.Start();
        }

        private void BuildWindow()
        {
            BuildFrameRateStrings();
            BuildCharacterUVs();

            // White space is the bottom right of the font texture.
            Vector4 whiteSpaceUV = new Vector4(0.99f, 1.0f - 0.99f, 0.0f, 0.0f);

            Vector3 defaultWindowSize = new Vector3(0.2f, 0.04f, 1.0f);
            float edgeX = defaultWindowSize.x * 0.5f;

            // Add a window back plate.
            {
                instanceMatrices[backplateInstanceOffset] = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, defaultWindowSize);
                instanceColors[backplateInstanceOffset] = baseColor;
                instanceUVOffsetScaleX[backplateInstanceOffset] = whiteSpaceUV;
            }

            // Add frame rate text.
            {
                float height = 0.02f;
                cpuFrameRateText = new TextData(new Vector3(-edgeX, height, 0.0f), false, cpuframeRateTextOffset);
                gpuFrameRateText = new TextData(new Vector3(edgeX, height, 0.0f), true, gpuframeRateTextOffset);
            }

            // Add frame rate indicators.
            {
                float height = 0.008f;
                float size = (1.0f / frameRange) * defaultWindowSize.x;
                Vector3 scale = new Vector3(size, size, 1.0f);
                Vector3 position = new Vector3(-defaultWindowSize.x * 0.5f, height, 0.0f);
                position.x += scale.x * 0.5f;

                for (int i = 0; i < frameRange; ++i)
                {
                    instanceMatrices[framesInstanceOffset + i] = Matrix4x4.TRS(position, Quaternion.identity, new Vector3(scale.x * 0.8f, scale.y * 0.8f, scale.z));
                    position.x += scale.x;
                    instanceColors[framesInstanceOffset + i] = targetFrameRateColor;
                    instanceUVOffsetScaleX[framesInstanceOffset + i] = whiteSpaceUV;
                }
            }

            // Add scene statistics text.
            {
                float height = 0.0045f;
                drawCallPassText = new TextData(new Vector3(-edgeX, height, 0.0f), false, drawCallPassTextOffset);
                verticiesText = new TextData(new Vector3(edgeX, height, 0.0f), true, verticiesTextOffset);
            }

            // Add memory usage bars.
            {
                float height = -0.0075f;
                Vector3 position = new Vector3(0.0f, height, 0.0f);
                Vector3 scale = defaultWindowSize;
                scale.Scale(new Vector3(0.99f, 0.15f, 1.0f));

                {
                    instanceMatrices[limitInstanceOffset] = Matrix4x4.TRS(position, Quaternion.identity, scale);
                    instanceColors[limitInstanceOffset] = memoryLimitColor;
                    instanceUVOffsetScaleX[limitInstanceOffset] = whiteSpaceUV;
                }
                {
                    instanceMatrices[peakInstanceOffset] = Matrix4x4.TRS(position, Quaternion.identity, scale);
                    instanceColors[peakInstanceOffset] = memoryPeakColor;
                    instanceUVOffsetScaleX[peakInstanceOffset] = whiteSpaceUV;
                }
                {
                    instanceMatrices[usedInstanceOffset] = Matrix4x4.TRS(position, Quaternion.identity, scale);
                    instanceColors[usedInstanceOffset] = memoryUsedColor;
                    instanceUVOffsetScaleX[usedInstanceOffset] = whiteSpaceUV;
                }
            }

            // Add memory usage text.
            {
                float height = -0.011f;
                usedMemoryText = new TextData(new Vector3(-edgeX, height, 0.0f), false, usedMemoryTextOffset);
                peakMemoryText = new TextData(new Vector3(-0.03f, height, 0.0f), false, peakMemoryTextOffset);
                limitMemoryText = new TextData(new Vector3(edgeX, height, 0.0f), true, limitMemoryTextOffset);
            }

            // Initialize property block state.
            instanceColorsDirty = true;
            instanceUVOffsetScaleXDirty = true;

            if (instancePropertyBlock != null && material != null && material.mainTexture != null)
            {
                instancePropertyBlock.SetVector("_BaseColor", baseColor);
                instancePropertyBlock.SetVector("_FontScale", new Vector2((float)fontCharacterSize.x / material.mainTexture.width,
                                                                          (float)fontCharacterSize.y / material.mainTexture.height));
            }
        }

        private void BuildFrameRateStrings()
        {
            string displayedDecimalFormat = string.Format("{{0:F{0}}}", displayedDecimalDigits);

            StringBuilder stringBuilder = new StringBuilder(32);
            StringBuilder milisecondStringBuilder = new StringBuilder(16);

            for (int i = 0; i < frameRateStrings.Length; ++i)
            {
                float miliseconds = (i == 0) ? 0.0f : (1.0f / i) * 1000.0f;
                milisecondStringBuilder.AppendFormat(displayedDecimalFormat, miliseconds);
                string frame = "-", ms = "-.-";

                if (i != 0)
                {
                    frame = i.ToString();
                    ms = milisecondStringBuilder.ToString();
                }

                stringBuilder.AppendFormat("{0} fps ({1} ms)", frame, ms);
                frameRateStrings[i] = ToCharArray(stringBuilder);
                stringBuilder.Length = 0;
                stringBuilder.AppendFormat("GPU: {0} fps ({1} ms)", frame, ms);
                gpuFrameRateStrings[i] = ToCharArray(stringBuilder);
                milisecondStringBuilder.Length = 0;
                stringBuilder.Length = 0;
            }
        }

        private void BuildCharacterUVs()
        {
            characterScale = new Vector3(fontCharacterSize.x * fontScale.x, fontCharacterSize.y * fontScale.y, 1.0f);

            if (material != null && material.mainTexture != null)
            {
                for (char c = ' '; c < characterUVs.Length; ++c)
                {
                    int index = c - ' ';
                    float height = (float)fontCharacterSize.y / material.mainTexture.height;
                    float x = ((float)(index % fontColumns) * fontCharacterSize.x) / material.mainTexture.width;
                    float y = ((float)(index / fontColumns) * fontCharacterSize.y) / material.mainTexture.height;
                    characterUVs[c] = new Vector4(x, 1.0f - height - y, 0.0f, 0.0f);
                }
            }
        }

        private void Refresh()
        {
            drawCalls = 0;
            setPassCalls = 0;
            vertexCount = 0;
            memoryUsage = 0;
            peakMemoryUsage = 0;
            limitMemoryUsage = 0;
        }

        private Vector3 CalculateWindowPosition(Transform cameraTransform)
        {
            float windowDistance = Mathf.Max(16.0f / Camera.main.fieldOfView, Camera.main.nearClipPlane + 0.25f);
            Vector3 position = cameraTransform.position + (cameraTransform.forward * windowDistance);
            Vector3 horizontalOffset = cameraTransform.right * windowOffset.x;
            Vector3 verticalOffset = cameraTransform.up * windowOffset.y;

            switch (windowAnchor)
            {
                case TextAnchor.UpperLeft: position += verticalOffset - horizontalOffset; break;
                case TextAnchor.UpperCenter: position += verticalOffset; break;
                case TextAnchor.UpperRight: position += verticalOffset + horizontalOffset; break;
                case TextAnchor.MiddleLeft: position -= horizontalOffset; break;
                case TextAnchor.MiddleRight: position += horizontalOffset; break;
                case TextAnchor.LowerLeft: position -= verticalOffset + horizontalOffset; break;
                case TextAnchor.LowerCenter: position -= verticalOffset; break;
                case TextAnchor.LowerRight: position -= verticalOffset - horizontalOffset; break;
            }

            return position;
        }

        private Quaternion CalculateWindowRotation(Transform cameraTransform)
        {
            Quaternion rotation = cameraTransform.rotation;

            switch (windowAnchor)
            {
                case TextAnchor.UpperLeft: rotation *= windowHorizontalRotationInverse * windowVerticalRotationInverse; break;
                case TextAnchor.UpperCenter: rotation *= windowHorizontalRotationInverse; break;
                case TextAnchor.UpperRight: rotation *= windowHorizontalRotationInverse * windowVerticalRotation; break;
                case TextAnchor.MiddleLeft: rotation *= windowVerticalRotationInverse; break;
                case TextAnchor.MiddleRight: rotation *= windowVerticalRotation; break;
                case TextAnchor.LowerLeft: rotation *= windowHorizontalRotation * windowVerticalRotationInverse; break;
                case TextAnchor.LowerCenter: rotation *= windowHorizontalRotation; break;
                case TextAnchor.LowerRight: rotation *= windowHorizontalRotation * windowVerticalRotation; break;
            }

            return rotation;
        }

        void SetText(TextData data, char[] text, int count, Color color)
        {
            Vector3 position = data.Position;
            position -= Vector3.up * characterScale.y * 0.5f;
            position += (data.RightAligned) ? Vector3.right * -characterScale.x * 0.5f : Vector3.right * characterScale.x * 0.5f;

            for (int i = 0; i < maxStringLength; ++i)
            {
                if (i < count)
                {
                    instanceMatrices[data.Offset + i] = Matrix4x4.TRS(position, Quaternion.identity, characterScale);
                    instanceColors[data.Offset + i] = color;
                    int charIndex = (data.RightAligned) ? count - i - 1 : i;
                    instanceUVOffsetScaleX[data.Offset + i] = characterUVs[text[charIndex]];

                    position += (data.RightAligned) ? Vector3.right * -characterScale.x : Vector3.right * characterScale.x;
                }
                else
                {
                    instanceMatrices[data.Offset + i] = Matrix4x4.zero;
                }
            }

            instanceColorsDirty = true;
            instanceUVOffsetScaleXDirty = true;
        }

#if UNITY_STANDALONE_WIN || UNITY_WSA
        private KeywordRecognizer keywordRecognizer;

        private void BuildKeywordRecognizer()
        {
            keywordRecognizer = new KeywordRecognizer(toggleKeyworlds);
            keywordRecognizer.OnPhraseRecognized += OnPhraseRecognized;

            keywordRecognizer.Start();
        }

        private void OnPhraseRecognized(PhraseRecognizedEventArgs args)
        {
            IsVisible = !IsVisible;
            Refresh();
        }
#endif

        private void MemoryUsageToString(char[] buffer, int displayedDecimalDigits, TextData data, string prefix, ulong memoryUsage, Color color)
        {
            float memoryUsageMB = ConvertBytesToMegabytes(memoryUsage);
            int memoryUsageIntegerDigits = (int)memoryUsageMB;
            int memoryUsageFractionalDigits = (int)((memoryUsageMB - memoryUsageIntegerDigits) * Mathf.Pow(10.0f, displayedDecimalDigits));
            int bufferIndex = 0;

            for (int i = 0; i < prefix.Length; ++i)
            {
                buffer[bufferIndex++] = prefix[i];
            }

            bufferIndex = ItoA(memoryUsageIntegerDigits, buffer, bufferIndex);

            if (displayedDecimalDigits != 0)
            {
                buffer[bufferIndex++] = '.';
            }

            if (memoryUsageFractionalDigits != 0)
            {
                bufferIndex = ItoA(memoryUsageFractionalDigits, buffer, bufferIndex);
            }
            else
            {
                for (int i = 0; i < displayedDecimalDigits; ++i)
                {
                    buffer[bufferIndex++] = '0';
                }
            }

            buffer[bufferIndex++] = 'M';
            buffer[bufferIndex++] = 'B';

            SetText(data, buffer, bufferIndex, color);
        }

        private void DrawPassCallsToString(char[] buffer, TextData data, string prefix, long drawCalls, long setPassCalls)
        {
            int bufferIndex = 0;

            for (int i = 0; i < prefix.Length; ++i)
            {
                buffer[bufferIndex++] = prefix[i];
            }

            bufferIndex = ItoA((int)drawCalls, buffer, bufferIndex);
            buffer[bufferIndex++] = '/';
            bufferIndex = ItoA((int)setPassCalls, buffer, bufferIndex);

            SetText(data, buffer, bufferIndex, Color.white);
        }

        private void VertexCountToString(char[] buffer, int displayedDecimalDigits, TextData data, string prefix, long vertexCount)
        {
            int bufferIndex = 0;

            for (int i = 0; i < prefix.Length; ++i)
            {
                buffer[bufferIndex++] = prefix[i];
            }

            float vertexCountK = vertexCount / 1000.0f;
            int vertexIntegerDigits = (int)vertexCountK;
            int vertexFractionalDigits = (int)((vertexCountK - vertexIntegerDigits) * Mathf.Pow(10.0f, displayedDecimalDigits));

            bufferIndex = ItoA(vertexIntegerDigits, buffer, bufferIndex);

            if (displayedDecimalDigits != 0)
            {
                buffer[bufferIndex++] = '.';
            }

            if (vertexFractionalDigits != 0)
            {
                bufferIndex = ItoA(vertexFractionalDigits, buffer, bufferIndex);
            }
            else
            {
                for (int i = 0; i < displayedDecimalDigits; ++i)
                {
                    buffer[bufferIndex++] = '0';
                }
            }

            buffer[bufferIndex++] = 'k';

            SetText(data, buffer, bufferIndex, Color.white);
        }

        private static char[] ToCharArray(StringBuilder stringBuilder)
        {
            char[] output = new char[stringBuilder.Length];

            for (int i = 0; i < output.Length; ++i)
            {
                output[i] = stringBuilder[i];
            }

            return output;
        }

        private static int ItoA(int value, char[] stringBuffer, int bufferIndex)
        {
            // Using a custom number to string method to avoid the overhead, and allocations, of built in string.Format/StringBuilder methods.
            // We can also make some assumptions since the domain of the input number is known.

            if (value == 0)
            {
                stringBuffer[bufferIndex++] = '0';
            }
            else
            {
                int startIndex = bufferIndex;

                for (; value != 0; value /= 10)
                {
                    stringBuffer[bufferIndex++] = (char)((char)(value % 10) + '0');
                }

                char temp;
                for (int endIndex = bufferIndex - 1; startIndex < endIndex; ++startIndex, --endIndex)
                {
                    temp = stringBuffer[startIndex];
                    stringBuffer[startIndex] = stringBuffer[endIndex];
                    stringBuffer[endIndex] = temp;
                }
            }

            return bufferIndex;
        }

        private static float AppFrameRate
        {
            get
            {
                // If the current XR SDK does not report refresh rate information, assume 60Hz.
                float refreshRate = UnityEngine.XR.XRDevice.refreshRate;
                return ((int)refreshRate == 0) ? 60.0f : refreshRate;
            }
        }

        private static void AverageFrameTiming(FrameTiming[] frameTimings, uint frameTimingsCount, out float cpuFrameTime, out float gpuFrameTime)
        {
            double cpuTime = 0.0f;
            double gpuTime = 0.0f;

            for (int i = 0; i < frameTimingsCount; ++i)
            {
                cpuTime += frameTimings[i].cpuFrameTime;
                gpuTime += frameTimings[i].gpuFrameTime;
            }

            cpuTime /= frameTimingsCount;
            gpuTime /= frameTimingsCount;

            cpuFrameTime = (float)(cpuTime * 0.001);
            gpuFrameTime = (float)(gpuTime * 0.001);
        }

        private static ulong AppMemoryUsage
        {
            get
            {
#if WINDOWS_UWP
                return MemoryManager.AppMemoryUsage;
#else
                return (ulong)Profiler.GetTotalAllocatedMemoryLong();
#endif
            }
        }

        private static ulong AppMemoryUsageLimit
        {
            get
            {
#if WINDOWS_UWP
                return MemoryManager.AppMemoryUsageLimit;
#else
                return ConvertMegabytesToBytes(SystemInfo.systemMemorySize);
#endif
            }
        }

        private static bool WillDisplayedVertexCountDiffer(long oldCount, long newCount, int displayedDecimalDigits)
        {
            float oldCountK = oldCount / 1000.0f;
            float newCountK = newCount / 1000.0f;
            float decimalPower = Mathf.Pow(10.0f, displayedDecimalDigits);

            return (int)(oldCountK * decimalPower) != (int)(newCountK * decimalPower);
        }

        private static bool WillDisplayedMemoryUsageDiffer(ulong oldUsage, ulong newUsage, int displayedDecimalDigits)
        {
            float oldUsageMBs = ConvertBytesToMegabytes(oldUsage);
            float newUsageMBs = ConvertBytesToMegabytes(newUsage);
            float decimalPower = Mathf.Pow(10.0f, displayedDecimalDigits);

            return (int)(oldUsageMBs * decimalPower) != (int)(newUsageMBs * decimalPower);
        }

        private static ulong ConvertMegabytesToBytes(int megabytes)
        {
            return ((ulong)megabytes * 1024UL) * 1024UL;
        }

        private static float ConvertBytesToMegabytes(ulong bytes)
        {
            return (bytes / 1024.0f) / 1024.0f;
        }
    }
}
