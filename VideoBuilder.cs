using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;

namespace HourglassApp
{
    public static class VideoBuilder
    {
        private const int MF_VERSION = 0x00020070;
        private const int MFSTARTUP_NOSOCKET = 0x1;

        private static readonly Guid MF_MT_MAJOR_TYPE = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        private static readonly Guid MF_MT_SUBTYPE = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
        private static readonly Guid MF_MT_AVG_BITRATE = new Guid("20332624-fb0d-4d9e-bd0d-cbf6786c102e");
        private static readonly Guid MF_MT_INTERLACE_MODE = new Guid("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
        private static readonly Guid MF_MT_FRAME_SIZE = new Guid("1652c33d-d6b2-4012-b834-72030849a37d");
        private static readonly Guid MF_MT_FRAME_RATE = new Guid("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
        private static readonly Guid MF_MT_PIXEL_ASPECT_RATIO = new Guid("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6");

        private static readonly Guid MFMediaType_Video = new Guid("73646976-0000-0010-8000-00aa00389b71");
        private static readonly Guid MFVideoFormat_H264 = new Guid("34363248-0000-0010-8000-00aa00389b71");
        private static readonly Guid MFVideoFormat_RGB32 = new Guid("00000016-0000-0010-8000-00aa00389b71");

        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFStartup(int version, int dwFlags);

        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFShutdown();

        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFCreateMediaType(out IMFMediaType ppMFType);

        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFCreateSample(out IMFSample ppIMFSample);

        [DllImport("mfplat.dll", ExactSpelling = true)]
        private static extern int MFCreateMemoryBuffer(int cbMaxLength, out IMFMediaBuffer ppBuffer);

        [DllImport("mfreadwrite.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int MFCreateSinkWriterFromURL(
            string pwszOutputURL,
            IntPtr pByteStream,
            IntPtr pAttributes,
            out IMFSinkWriter ppSinkWriter);

        [ComImport, SuppressUnmanagedCodeSecurity, Guid("045FA593-8799-42b8-BC8D-8968C6453507"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMFMediaBuffer
        {
            [PreserveSig] int Lock(out IntPtr ppbBuffer, out int pcbMaxLength, out int pcbCurrentLength);
            [PreserveSig] int Unlock();
            [PreserveSig] int GetCurrentLength(out int pcbCurrentLength);
            [PreserveSig] int SetCurrentLength(int cbCurrentLength);
            [PreserveSig] int GetMaxLength(out int pcbMaxLength);
        }

        [ComImport, SuppressUnmanagedCodeSecurity, Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMFMediaType
        {
            [PreserveSig] int GetItem([In] ref Guid guidKey, IntPtr pValue);
            [PreserveSig] int GetItemType([In] ref Guid guidKey, out int pType);
            [PreserveSig] int CompareItem([In] ref Guid guidKey, IntPtr Value, out bool pbResult);
            [PreserveSig] int Compare(IntPtr pTheirs, int MatchType, out bool pbResult);
            [PreserveSig] int GetUINT32([In] ref Guid guidKey, out int punValue);
            [PreserveSig] int GetUINT64([In] ref Guid guidKey, out long punValue);
            [PreserveSig] int GetDouble([In] ref Guid guidKey, out double pfValue);
            [PreserveSig] int GetGUID([In] ref Guid guidKey, out Guid pguidValue);
            [PreserveSig] int GetStringLength([In] ref Guid guidKey, out int pcchLength);
            [PreserveSig] int GetString([In] ref Guid guidKey, IntPtr pwszValue, int cchBufSize, out int pcchLength);
            [PreserveSig] int GetAllocatedString([In] ref Guid guidKey, out IntPtr ppwszValue, out int pcchLength);
            [PreserveSig] int GetBlobSize([In] ref Guid guidKey, out int pcbBlobSize);
            [PreserveSig] int GetBlob([In] ref Guid guidKey, IntPtr pBuf, int cbBufSize, out int pcbBlobSize);
            [PreserveSig] int GetAllocatedBlob([In] ref Guid guidKey, out IntPtr ip, out int pcbSize);
            [PreserveSig] int GetUnknown([In] ref Guid guidKey, [In] ref Guid riid, out IntPtr ppv);
            [PreserveSig] int SetItem([In] ref Guid guidKey, IntPtr Value);
            [PreserveSig] int DeleteItem([In] ref Guid guidKey);
            [PreserveSig] int DeleteAllItems();
            [PreserveSig] int SetUINT32([In] ref Guid guidKey, int unValue);
            [PreserveSig] int SetUINT64([In] ref Guid guidKey, long unValue);
            [PreserveSig] int SetDouble([In] ref Guid guidKey, double fValue);
            [PreserveSig] int SetGUID([In] ref Guid guidKey, [In] ref Guid guidValue);
            [PreserveSig] int SetString([In] ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
            [PreserveSig] int SetBlob([In] ref Guid guidKey, IntPtr pBuf, int cbBufSize);
            [PreserveSig] int SetUnknown([In] ref Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
            [PreserveSig] int LockStore();
            [PreserveSig] int UnlockStore();
            [PreserveSig] int GetCount(out int pcItems);
            [PreserveSig] int GetItemByIndex(int unIndex, out Guid pguidKey, IntPtr pValue);
            [PreserveSig] int CopyAllItems(IntPtr pDest);
        }

        [ComImport, SuppressUnmanagedCodeSecurity, Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMFSample
        {
            [PreserveSig] int GetItem([In] ref Guid guidKey, IntPtr pValue);
            [PreserveSig] int GetItemType([In] ref Guid guidKey, out int pType);
            [PreserveSig] int CompareItem([In] ref Guid guidKey, IntPtr Value, out bool pbResult);
            [PreserveSig] int Compare(IntPtr pTheirs, int MatchType, out bool pbResult);
            [PreserveSig] int GetUINT32([In] ref Guid guidKey, out int punValue);
            [PreserveSig] int GetUINT64([In] ref Guid guidKey, out long punValue);
            [PreserveSig] int GetDouble([In] ref Guid guidKey, out double pfValue);
            [PreserveSig] int GetGUID([In] ref Guid guidKey, out Guid pguidValue);
            [PreserveSig] int GetStringLength([In] ref Guid guidKey, out int pcchLength);
            [PreserveSig] int GetString([In] ref Guid guidKey, IntPtr pwszValue, int cchBufSize, out int pcchLength);
            [PreserveSig] int GetAllocatedString([In] ref Guid guidKey, out IntPtr ppwszValue, out int pcchLength);
            [PreserveSig] int GetBlobSize([In] ref Guid guidKey, out int pcbBlobSize);
            [PreserveSig] int GetBlob([In] ref Guid guidKey, IntPtr pBuf, int cbBufSize, out int pcbBlobSize);
            [PreserveSig] int GetAllocatedBlob([In] ref Guid guidKey, out IntPtr ip, out int pcbSize);
            [PreserveSig] int GetUnknown([In] ref Guid guidKey, [In] ref Guid riid, out IntPtr ppv);
            [PreserveSig] int SetItem([In] ref Guid guidKey, IntPtr Value);
            [PreserveSig] int DeleteItem([In] ref Guid guidKey);
            [PreserveSig] int DeleteAllItems();
            [PreserveSig] int SetUINT32([In] ref Guid guidKey, int unValue);
            [PreserveSig] int SetUINT64([In] ref Guid guidKey, long unValue);
            [PreserveSig] int SetDouble([In] ref Guid guidKey, double fValue);
            [PreserveSig] int SetGUID([In] ref Guid guidKey, [In] ref Guid guidValue);
            [PreserveSig] int SetString([In] ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
            [PreserveSig] int SetBlob([In] ref Guid guidKey, IntPtr pBuf, int cbBufSize);
            [PreserveSig] int SetUnknown([In] ref Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
            [PreserveSig] int LockStore();
            [PreserveSig] int UnlockStore();
            [PreserveSig] int GetCount(out int pcItems);
            [PreserveSig] int GetItemByIndex(int unIndex, out Guid pguidKey, IntPtr pValue);
            [PreserveSig] int CopyAllItems(IntPtr pDest);

            // IMFSample methods
            [PreserveSig] int GetSampleFlags(out int pdwSampleFlags);
            [PreserveSig] int SetSampleFlags(int dwSampleFlags);
            [PreserveSig] int GetSampleTime(out long phnsSampleTime);
            [PreserveSig] int SetSampleTime(long hnsSampleTime);
            [PreserveSig] int GetSampleDuration(out long phnsSampleDuration);
            [PreserveSig] int SetSampleDuration(long hnsSampleDuration);
            [PreserveSig] int GetBufferCount(out int pdwBufferCount);
            [PreserveSig] int GetBufferByIndex(int dwIndex, out IMFMediaBuffer ppBuffer);
            [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer ppBuffer);
            [PreserveSig] int AddBuffer(IMFMediaBuffer pBuffer);
        }

        [ComImport, SuppressUnmanagedCodeSecurity, Guid("3137f1cd-fe5e-4805-a5d8-fb477448cb3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMFSinkWriter
        {
            [PreserveSig] int AddStream(IMFMediaType pTargetMediaType, out int pdwStreamIndex);
            [PreserveSig] int SetInputMediaType(int dwStreamIndex, IMFMediaType pInputMediaType, IntPtr pEncodingParameters);
            [PreserveSig] int BeginWriting();
            [PreserveSig] int WriteSample(int dwStreamIndex, IMFSample pSample);
            [PreserveSig] int SendStreamTick(int dwStreamIndex, long llTimestamp);
            [PreserveSig] int PlaceMarker(int dwStreamIndex, IntPtr pvContext);
            [PreserveSig] int NotifyEndOfSegment(int dwStreamIndex);
            [PreserveSig] int Flush(int dwStreamIndex);
            [PreserveSig] int Finalize_();
        }

        private static long Pack2UINT32AsUINT64(int high, int low)
        {
            return ((long)high << 32) | (uint)low;
        }

        public static void GenerateTutorialVideo(string outputPath)
        {
            const int width = 1280;
            const int height = 720;
            const int fps = 30;
            const int totalSeconds = 24;
            const int totalFrames = fps * totalSeconds; // 720 frames
            const long frameDuration100Ns = 10000000L / fps;

            if (File.Exists(outputPath)) File.Delete(outputPath);

            int hr = MFStartup(MF_VERSION, MFSTARTUP_NOSOCKET);
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);

            IMFSinkWriter writer = null;
            try
            {
                hr = MFCreateSinkWriterFromURL(outputPath, IntPtr.Zero, IntPtr.Zero, out writer);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);

                // 1. Output Media Type (H.264 MP4)
                IMFMediaType mediaTypeOut;
                MFCreateMediaType(out mediaTypeOut);
                Guid videoMajor = MFMediaType_Video;
                Guid h264Sub = MFVideoFormat_H264;
                Guid mfmtMajor = MF_MT_MAJOR_TYPE;
                Guid mfmtSub = MF_MT_SUBTYPE;
                Guid mfmtBitrate = MF_MT_AVG_BITRATE;
                Guid mfmtInterlace = MF_MT_INTERLACE_MODE;
                Guid mfmtFrameSize = MF_MT_FRAME_SIZE;
                Guid mfmtFrameRate = MF_MT_FRAME_RATE;
                Guid mfmtAspect = MF_MT_PIXEL_ASPECT_RATIO;

                mediaTypeOut.SetGUID(ref mfmtMajor, ref videoMajor);
                mediaTypeOut.SetGUID(ref mfmtSub, ref h264Sub);
                mediaTypeOut.SetUINT32(ref mfmtBitrate, 4500000); // 4.5 Mbps crisp HD
                mediaTypeOut.SetUINT32(ref mfmtInterlace, 2); // Progressive
                mediaTypeOut.SetUINT64(ref mfmtFrameSize, Pack2UINT32AsUINT64(width, height));
                mediaTypeOut.SetUINT64(ref mfmtFrameRate, Pack2UINT32AsUINT64(fps, 1));
                mediaTypeOut.SetUINT64(ref mfmtAspect, Pack2UINT32AsUINT64(1, 1));

                int streamIndex;
                hr = writer.AddStream(mediaTypeOut, out streamIndex);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                Marshal.ReleaseComObject(mediaTypeOut);

                // 2. Input Media Type (RGB32)
                IMFMediaType mediaTypeIn;
                MFCreateMediaType(out mediaTypeIn);
                Guid rgb32Sub = MFVideoFormat_RGB32;
                mediaTypeIn.SetGUID(ref mfmtMajor, ref videoMajor);
                mediaTypeIn.SetGUID(ref mfmtSub, ref rgb32Sub);
                mediaTypeIn.SetUINT32(ref mfmtInterlace, 2);
                mediaTypeIn.SetUINT64(ref mfmtFrameSize, Pack2UINT32AsUINT64(width, height));
                mediaTypeIn.SetUINT64(ref mfmtFrameRate, Pack2UINT32AsUINT64(fps, 1));
                mediaTypeIn.SetUINT64(ref mfmtAspect, Pack2UINT32AsUINT64(1, 1));

                hr = writer.SetInputMediaType(streamIndex, mediaTypeIn, IntPtr.Zero);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                Marshal.ReleaseComObject(mediaTypeIn);

                hr = writer.BeginWriting();
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);

                using (HourglassForm hg = new HourglassForm())
                using (Bitmap frameBmp = new Bitmap(width, height, PixelFormat.Format32bppRgb))
                using (Graphics g = Graphics.FromImage(frameBmp))
                {
                    hg.HideTrayForRender();
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.CompositingQuality = CompositingQuality.HighQuality;

                    int rowBytes = width * 4;
                    int bufferSize = rowBytes * height;
                    byte[] tempRow = new byte[rowBytes];

                    for (int f = 0; f < totalFrames; f++)
                    {
                        double t = (double)f / fps;
                        DrawTutorialFrame(g, hg, width, height, t, f);

                        // Copy bitmap to Media Foundation buffer (bottom-up RGB32 for DIB/MF RGB32)
                        IMFMediaBuffer mediaBuffer;
                        MFCreateMemoryBuffer(bufferSize, out mediaBuffer);

                        IntPtr pDest;
                        int maxLen, curLen;
                        mediaBuffer.Lock(out pDest, out maxLen, out curLen);

                        BitmapData bd = frameBmp.LockBits(
                            new Rectangle(0, 0, width, height),
                            ImageLockMode.ReadOnly,
                            PixelFormat.Format32bppRgb);

                        try
                        {
                            // Media Foundation RGB32 expects bottom-up rows by default
                            for (int y = 0; y < height; y++)
                            {
                                IntPtr srcRow = IntPtr.Add(bd.Scan0, (height - 1 - y) * bd.Stride);
                                IntPtr dstRow = IntPtr.Add(pDest, y * rowBytes);
                                Marshal.Copy(srcRow, tempRow, 0, rowBytes);
                                Marshal.Copy(tempRow, 0, dstRow, rowBytes);
                            }
                        }
                        finally
                        {
                            frameBmp.UnlockBits(bd);
                        }

                        mediaBuffer.Unlock();
                        mediaBuffer.SetCurrentLength(bufferSize);

                        IMFSample sample;
                        MFCreateSample(out sample);
                        sample.AddBuffer(mediaBuffer);
                        sample.SetSampleTime(f * frameDuration100Ns);
                        sample.SetSampleDuration(frameDuration100Ns);

                        hr = writer.WriteSample(streamIndex, sample);
                        if (hr < 0) Marshal.ThrowExceptionForHR(hr);

                        Marshal.ReleaseComObject(sample);
                        Marshal.ReleaseComObject(mediaBuffer);
                    }
                }

                hr = writer.Finalize_();
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
            }
            finally
            {
                if (writer != null) Marshal.ReleaseComObject(writer);
                MFShutdown();
            }
        }

        // =====================================================================
        // Scene Choreography & Real-World Desktop Simulation
        // =====================================================================
        private static void DrawTutorialFrame(Graphics g, HourglassForm hg, int w, int h, double t, int frameIdx)
        {
            long simMs = frameIdx * 33L;

            if (t < 4.0)
            {
                // SCENE 1: Real-World Desktop & Dragging Transparent Hourglass
                DrawDesktopWorkspace(g, w, h, false, "업무 / 문서 작업 중에도 시야를 가리지 않는 투명 모래시계 위젯");

                double p = Clamp01((t - 0.5) / 2.2);
                double ease = SmoothStep(p);
                float hgX = (float)(640 + ease * 270);
                float hgY = (float)(160 + ease * 45);

                hg.RenderCustomFrame(
                    g, hgX, hgY, 1.15f,
                    false, 0, TimerState.Ready,
                    300, 300, false, 1.0f,
                    p < 0.9 ? "DRAG TO MOVE" : "READY // 05:00", simMs);

                float curX = hgX + 120f;
                float curY = hgY + 135f;
                DrawAnimatedCursor(g, curX, curY, p > 0.02 && p < 0.96, false);

                DrawCalloutBubble(g, hgX - 245f, hgY + 60f, 225f, 64f,
                    "드래그하여 원하는 위치에 배치",
                    "모래시계 외곽은 100% 투명 배경!");

                DrawBottomBanner(g, w, h, 1, 6,
                    "실사용 예시 1 — 투명 라인아트 모드 & 자유로운 드래그 배치",
                    "어느 창 위에 올려두어도 이질감 없이 어우러지며, '항상 위에 고정'으로 가려지지 않습니다.");
            }
            else if (t < 8.0)
            {
                // SCENE 2: Mouse Wheel Time & Scale Adjustment
                DrawDesktopWorkspace(g, w, h, false, "마우스 휠 스크롤만으로 1초 만에 타이머 시간 설정");

                double lt = t - 4.0;
                double setSec = 300; // 05:00
                string hud = "SET // 05:00";
                if (lt >= 0.7 && lt < 1.4) { setSec = 600; hud = "SET // 10:00"; }
                else if (lt >= 1.4 && lt < 2.1) { setSec = 900; hud = "SET // 15:00"; }
                else if (lt >= 2.1) { setSec = 1500; hud = "SET // 25:00 (POMODORO)"; }

                float hgX = 880f;
                float hgY = 175f;
                hg.RenderCustomFrame(
                    g, hgX, hgY, 1.22f,
                    false, 0, TimerState.Ready,
                    setSec, setSec, false, 1.0f,
                    hud, simMs);

                DrawWheelIndicator(g, hgX - 85f, hgY + 95f, lt);
                DrawAnimatedCursor(g, hgX + 135f, hgY + 145f, false, false);

                DrawCalloutBubble(g, hgX - 330f, hgY + 30f, 235f, 88f,
                    "마우스 휠 스크롤 조작",
                    "• 기본 휠 : ±1분 (±10초)\n• Shift + 휠 : ±5분 빠른 조절\n• Ctrl + 휠 : 크기 확대/축소");

                DrawBottomBanner(g, w, h, 2, 6,
                    "튜토리얼 1 — 마우스 휠로 직관적인 시간 & 크기 조절",
                    "모래시계 위에서 마우스 휠을 위/아래로 굴리면 도트 숫자가 즉시 변경됩니다.");
            }
            else if (t < 12.5)
            {
                // SCENE 3: Single Click Start/Pause & Double Click 180-Degree Flip
                DrawDesktopWorkspace(g, w, h, false, "클릭 1번으로 시작, 더블클릭으로 모래시계 180° 뒤집기");

                double lt = t - 8.0;
                float hgX = 880f;
                float hgY = 175f;

                bool isFlip = false;
                float flipProg = 1.0f;
                double rem = 1500 - lt * 120.0;
                string hud = null;

                if (lt < 0.5)
                {
                    hud = "CLICK -> RUN!";
                }
                else if (lt >= 2.3 && lt < 3.0)
                {
                    isFlip = true;
                    flipProg = (float)Clamp01((lt - 2.3) / 0.65);
                    rem = 1500;
                    hud = "FLIP // 25:00";
                }
                else if (lt >= 3.0)
                {
                    rem = 1500 - (lt - 3.0) * 90.0;
                }

                hg.RenderCustomFrame(
                    g, hgX, hgY, 1.22f,
                    false, 0, TimerState.Running,
                    1500, rem, isFlip, flipProg,
                    hud, simMs);

                bool clickPulse = (lt >= 0.1 && lt <= 0.45) || (lt >= 2.15 && lt <= 2.45);
                DrawAnimatedCursor(g, hgX + 130f, hgY + 140f, clickPulse, lt >= 2.15 && lt <= 2.5);

                DrawCalloutBubble(g, hgX - 310f, hgY + 50f, 275f, 82f,
                    lt < 2.1 ? "▶ 클릭 1번 : 타이머 시작 / 일시정지" : "🔄 더블클릭 : 180° 뒤집기 재시작",
                    lt < 2.1 ? "도트 모래 알갱이가 실시간으로 떨어집니다." : "모래시계가 회전하며 처음부터 다시 시작합니다.");

                DrawBottomBanner(g, w, h, 3, 6,
                    "튜토리얼 2 — 클릭으로 시작/일시정지 & 더블클릭 뒤집기",
                    "복잡한 버튼 없이 모래시계 본체를 클릭하거나 더블클릭하여 조작합니다.");
            }
            else if (t < 16.5)
            {
                // SCENE 4: Switching to E-Ink Display Panel Mode (M Key)
                DrawDesktopWorkspace(g, w, h, true, "단축키 [M] 또는 우클릭으로 'E-Ink 디스플레이 패널 모드' 전환");

                double lt = t - 12.5;
                bool panelMode = lt >= 1.0;
                float hgX = 870f;
                float hgY = 165f;

                double rem = 1044.0 - lt * 15.0; // ~17:24
                hg.RenderCustomFrame(
                    g, hgX, hgY, 1.25f,
                    panelMode, 0, TimerState.Running,
                    1500, rem, false, 1.0f,
                    (lt >= 0.8 && lt <= 1.7) ? "MODE // E-INK PANEL" : null, simMs);

                DrawKeyBadge(g, hgX - 280f, hgY + 65f, "M", lt >= 0.7 && lt <= 1.3,
                    "단축키 [M] 또는 우클릭 메뉴",
                    panelMode ? "현재: E-Ink 디스플레이 패널 카드 모드" : "현재: 투명 라인아트 계기판 모드");

                DrawBottomBanner(g, w, h, 4, 6,
                    "실사용 예시 2 — E-Ink 데스크 디스플레이 패널 모드",
                    "화이트 베젤 프레임, 상단 상태바(RUN), 5×7 도트 시계, 진행률 게이지가 통합된 전자잉크 위젯 모드입니다.");
            }
            else if (t < 20.5)
            {
                // SCENE 5: 5 Ink & Dark Inverted Backlight Themes Showcase
                using (SolidBrush darkBg = new SolidBrush(Color.FromArgb(26, 29, 35)))
                using (SolidBrush lightBg = new SolidBrush(Color.FromArgb(216, 214, 206)))
                {
                    g.FillRectangle(lightBg, 0, 0, w / 2, h);
                    g.FillRectangle(darkBg, w / 2, 0, w - w / 2, h);
                }

                string[] names = new string[]
                {
                    "1. 라이트 잉크 (기본)",
                    "2. 웜 크림 계기판",
                    "3. 다크 반전 (화이트)",
                    "4. 다크 앰버 계기판",
                    "5. 다크 그린 터미널"
                };
                int[] themeOrder = new int[] { 0, 2, 1, 3, 4 };
                bool[] modeSample = new bool[] { true, false, true, true, false };
                double[] progSample = new double[] { 0.30, 0.45, 0.52, 0.68, 0.80 };

                for (int i = 0; i < 5; i++)
                {
                    float cardX = 32f + i * 246f;
                    float cardY = 125f;
                    double tot = 1500;
                    double rem = tot * (1.0 - progSample[i]) - (t - 16.5) * 20.0;

                    hg.RenderCustomFrame(
                        g, cardX, cardY, 1.05f,
                        modeSample[i], themeOrder[i], TimerState.Running,
                        tot, rem, false, 1.0f, null, simMs);

                    DrawThemePill(g, cardX + 110f, cardY + 345f, names[i], i >= 2);
                }

                DrawTopHeaderTitle(g, w, "밝은 바탕화면과 어두운 다크모드 작업 환경 모두에 어울리는 5가지 테마");

                DrawBottomBanner(g, w, h, 5, 6,
                    "실사용 예시 3 — 5가지 잉크 & 다크 반전(백라이트) 테마 지원",
                    "우클릭 메뉴 ➔ [잉크 색상 테마]에서 바탕화면 톤에 맞춰 언제든 색상을 바꿀 수 있습니다.");
            }
            else
            {
                // SCENE 6: Alarm Visual Shake + Chime & Ultra-Lightweight Summary
                DrawDesktopWorkspace(g, w, h, true, "시간 종료 시 시각 진동 + 내장 차임벨 알림 (클릭 시 즉시 리셋)");

                double lt = t - 20.5;
                float hgX = 870f;
                float hgY = 165f;

                bool resetClicked = lt >= 2.3;
                TimerState st = resetClicked ? TimerState.Ready : TimerState.Alarm;
                double rem = resetClicked ? 1500 : 0;

                if (!resetClicked)
                {
                    // Draw radiating sound/alarm waves around the hourglass
                    float wavePhase = (float)((lt * 2.2) % 1.0);
                    using (Pen wavePen = new Pen(Color.FromArgb((int)((1.0 - wavePhase) * 200), 235, 85, 55), 3.0f))
                    {
                        float pad = wavePhase * 42f;
                        g.DrawRectangle(wavePen, hgX - pad, hgY - pad, 210f * 1.25f + pad * 2f, 310f * 1.25f + pad * 2f);
                    }
                }

                hg.RenderCustomFrame(
                    g, hgX, hgY, 1.25f,
                    true, 1, st,
                    1500, rem, false, 1.0f,
                    resetClicked ? "RESET // 25:00" : null, simMs);

                DrawAnimatedCursor(g, hgX + 135f, hgY + 170f, lt >= 2.15 && lt <= 2.45, false);

                DrawCalloutBubble(g, hgX - 340f, hgY + 55f, 305f, 92f,
                    resetClicked ? "✔ 클릭하여 알람 해제 및 리셋 완료!" : "🔔 TIME UP! 흔들림 + 차임벨 알림",
                    "• 모래시계 진동 + 도트 점멸 + 2화음 차임벨\n• 대기/일시정지 시 CPU 점유율 0.0% 초경량 설계\n• 단일 실행 파일 (52KB, 무설치 포터블)");

                DrawBottomBanner(g, w, h, 6, 6,
                    "튜토리얼 3 — 알람 해제 및 초경량 포터블 아키텍처",
                    "알람이 울릴 때 모래시계를 클릭하면 즉시 꺼지며, 백그라운드 리소스를 거의 소모하지 않습니다.");
            }
        }

        private static void DrawDesktopWorkspace(Graphics g, int w, int h, bool darkTheme, string subtitle)
        {
            Color bgTop = darkTheme ? Color.FromArgb(24, 28, 36) : Color.FromArgb(225, 228, 232);
            Color bgBot = darkTheme ? Color.FromArgb(16, 19, 25) : Color.FromArgb(204, 208, 214);
            using (LinearGradientBrush bgBrush = new LinearGradientBrush(new Rectangle(0, 0, w, h), bgTop, bgBot, LinearGradientMode.Vertical))
            {
                g.FillRectangle(bgBrush, 0, 0, w, h);
            }

            // Simulated App Window on the left side (IDE / Document Editor)
            RectangleF winRect = new RectangleF(48f, 78f, 740f, 495f);
            Color winBg = darkTheme ? Color.FromArgb(34, 38, 46) : Color.FromArgb(248, 248, 245);
            Color winBorder = darkTheme ? Color.FromArgb(65, 72, 84) : Color.FromArgb(175, 180, 186);
            Color titleBar = darkTheme ? Color.FromArgb(42, 47, 56) : Color.FromArgb(234, 235, 230);
            Color textPri = darkTheme ? Color.FromArgb(225, 230, 238) : Color.FromArgb(38, 42, 46);
            Color textSec = darkTheme ? Color.FromArgb(130, 140, 155) : Color.FromArgb(115, 120, 126);

            using (GraphicsPath winPath = HourglassForm.CreateRoundedRect(winRect.X, winRect.Y, winRect.Width, winRect.Height, 10f))
            using (SolidBrush wb = new SolidBrush(winBg))
            using (Pen wp = new Pen(winBorder, 1.5f))
            {
                g.FillPath(wb, winPath);
                g.DrawPath(wp, winPath);
            }

            // Window header bar & traffic dots
            using (SolidBrush tb = new SolidBrush(titleBar))
            using (Pen lp = new Pen(winBorder, 1.0f))
            {
                g.FillRectangle(tb, winRect.X + 2f, winRect.Y + 2f, winRect.Width - 4f, 34f);
                g.DrawLine(lp, winRect.X, winRect.Y + 36f, winRect.Right, winRect.Y + 36f);
            }

            using (SolidBrush dotBrush = new SolidBrush(textSec))
            using (Font fTitle = new Font("Malgun Gothic", 9.5f, FontStyle.Bold))
            {
                g.FillEllipse(dotBrush, winRect.X + 16f, winRect.Y + 13f, 10f, 10f);
                g.FillEllipse(dotBrush, winRect.X + 34f, winRect.Y + 13f, 10f, 10f);
                g.FillEllipse(dotBrush, winRect.X + 52f, winRect.Y + 13f, 10f, 10f);
                g.DrawString("Workspace — Project Notes & Tasks", fTitle, dotBrush, winRect.X + 78f, winRect.Y + 9f);
            }

            // Simulated document content inside workspace window
            using (Font fHead = new Font("Malgun Gothic", 14f, FontStyle.Bold))
            using (Font fBody = new Font("Malgun Gothic", 10.5f, FontStyle.Regular))
            using (SolidBrush bPri = new SolidBrush(textPri))
            using (SolidBrush bSec = new SolidBrush(textSec))
            using (SolidBrush barBg = new SolidBrush(darkTheme ? Color.FromArgb(48, 54, 64) : Color.FromArgb(232, 234, 236)))
            {
                g.DrawString("⏳ NAVIGATOR // E-INK 모래시계 실사용 가이드", fHead, bPri, winRect.X + 32f, winRect.Y + 58f);
                g.DrawString(subtitle, fBody, bSec, winRect.X + 32f, winRect.Y + 92f);

                string[] checklist = new string[]
                {
                    "[✓] 모래시계 외곽 완전 투명 배경 (어디든 올려두어도 자연스러움)",
                    "[✓] 마우스 휠로 즉시 시간 조절 (±1분 / Shift ±5분 / Alt ±10초)",
                    "[✓] 클릭 1번으로 시작·일시정지 / 더블클릭으로 180° 뒤집기",
                    "[✓] 단축키 [M]으로 '투명 라인아트' ↔ 'E-Ink 패널 카드' 즉시 전환",
                    "[✓] 대기 중 CPU 점유율 0.0% — 시스템 리소스 걱정 없는 초경량 앱"
                };

                for (int i = 0; i < checklist.Length; i++)
                {
                    float rowY = winRect.Y + 142f + i * 56f;
                    g.FillRectangle(barBg, winRect.X + 32f, rowY, winRect.Width - 64f, 42f);
                    g.DrawString(checklist[i], fBody, bPri, winRect.X + 46f, rowY + 10f);
                }
            }
        }

        private static void DrawTopHeaderTitle(Graphics g, int w, string text)
        {
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(225, 24, 27, 32)))
            using (Font f = new Font("Malgun Gothic", 13f, FontStyle.Bold))
            using (SolidBrush fg = new SolidBrush(Color.FromArgb(242, 242, 238)))
            using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillRectangle(bg, 0, 24, w, 52);
                g.DrawString(text, f, fg, new RectangleF(0, 24, w, 52), sf);
            }
        }

        private static void DrawThemePill(Graphics g, float cx, float cy, string label, bool isDarkSide)
        {
            using (Font f = new Font("Malgun Gothic", 9.5f, FontStyle.Bold))
            {
                SizeF sz = g.MeasureString(label, f);
                float pw = sz.Width + 20f;
                float ph = 28f;
                RectangleF r = new RectangleF(cx - pw * 0.5f, cy, pw, ph);
                using (GraphicsPath p = HourglassForm.CreateRoundedRect(r.X, r.Y, r.Width, r.Height, 14f))
                using (SolidBrush bg = new SolidBrush(isDarkSide ? Color.FromArgb(46, 52, 62) : Color.FromArgb(32, 35, 40)))
                using (SolidBrush fg = new SolidBrush(Color.FromArgb(240, 240, 235)))
                using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.FillPath(bg, p);
                    g.DrawString(label, f, fg, r, sf);
                }
            }
        }

        private static void DrawBottomBanner(Graphics g, int w, int h, int step, int totalSteps, string title, string desc)
        {
            float bannerH = 98f;
            float topY = h - bannerH;

            using (SolidBrush bg = new SolidBrush(Color.FromArgb(242, 22, 25, 30)))
            using (Pen topLine = new Pen(Color.FromArgb(228, 227, 221), 2.0f))
            {
                g.FillRectangle(bg, 0, topY, w, bannerH);
                g.DrawLine(topLine, 0, topY, w, topY);
            }

            // Step badge on the left
            RectangleF stepRect = new RectangleF(42f, topY + 20f, 110f, 34f);
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(228, 227, 221)))
            using (Font sfFont = new Font("Consolas", 11f, FontStyle.Bold))
            using (SolidBrush sfg = new SolidBrush(Color.FromArgb(22, 25, 30)))
            using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillRectangle(sb, stepRect);
                g.DrawString(string.Format("STEP {0}/{1}", step, totalSteps), sfFont, sfg, stepRect, sf);
            }

            using (Font fTitle = new Font("Malgun Gothic", 14.5f, FontStyle.Bold))
            using (Font fDesc = new Font("Malgun Gothic", 10.5f, FontStyle.Regular))
            using (SolidBrush bTitle = new SolidBrush(Color.FromArgb(245, 245, 240)))
            using (SolidBrush bDesc = new SolidBrush(Color.FromArgb(185, 192, 202)))
            {
                g.DrawString(title, fTitle, bTitle, 172f, topY + 18f);
                g.DrawString(desc, fDesc, bDesc, 172f, topY + 52f);
            }
        }

        private static void DrawCalloutBubble(Graphics g, float x, float y, float w, float h, string title, string body)
        {
            using (Font fTitle = new Font("Malgun Gothic", 10.5f, FontStyle.Bold))
            using (Font fBody = new Font("Malgun Gothic", 9.2f, FontStyle.Regular))
            {
                SizeF szTitle = g.MeasureString(title, fTitle);
                SizeF szBody = g.MeasureString(body, fBody);
                float autoW = Math.Max(w, Math.Max(szTitle.Width, szBody.Width) + 32f);
                float autoH = Math.Max(h, 38f + szBody.Height + 14f);
                // Keep right edge anchored so it never overlaps the hourglass on the right
                float adjX = x - (autoW - w);

                using (GraphicsPath path = HourglassForm.CreateRoundedRect(adjX, y, autoW, autoH, 8f))
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(242, 26, 29, 34)))
                using (Pen border = new Pen(Color.FromArgb(228, 227, 221), 2.0f))
                using (SolidBrush bTitle = new SolidBrush(Color.FromArgb(255, 215, 95)))
                using (SolidBrush bBody = new SolidBrush(Color.FromArgb(235, 238, 242)))
                {
                    g.FillPath(bg, path);
                    g.DrawPath(border, path);
                    g.DrawString(title, fTitle, bTitle, adjX + 14f, y + 10f);
                    g.DrawString(body, fBody, bBody, adjX + 14f, y + 36f);
                }
            }
        }

        private static void DrawKeyBadge(Graphics g, float x, float y, string keyLabel, bool pressed, string title, string sub)
        {
            DrawCalloutBubble(g, x, y, 285f, 82f, title, sub);

            float kx = x + 115f;
            float ky = y - 62f + (pressed ? 4f : 0f);
            using (GraphicsPath kp = HourglassForm.CreateRoundedRect(kx, ky, 52f, 52f, 8f))
            using (SolidBrush kbg = new SolidBrush(pressed ? Color.FromArgb(255, 195, 65) : Color.FromArgb(235, 234, 228)))
            using (Pen kpen = new Pen(Color.FromArgb(28, 30, 34), 2.5f))
            using (Font kf = new Font("Consolas", 18f, FontStyle.Bold))
            using (SolidBrush kfg = new SolidBrush(Color.FromArgb(28, 30, 34)))
            using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillPath(kbg, kp);
                g.DrawPath(kpen, kp);
                g.DrawString(keyLabel, kf, kfg, new RectangleF(kx, ky, 52f, 52f), sf);
            }
        }

        private static void DrawWheelIndicator(Graphics g, float x, float y, double lt)
        {
            using (GraphicsPath mp = HourglassForm.CreateRoundedRect(x, y, 46f, 72f, 22f))
            using (SolidBrush mbg = new SolidBrush(Color.FromArgb(235, 28, 31, 36)))
            using (Pen mpen = new Pen(Color.FromArgb(235, 234, 228), 2.2f))
            using (SolidBrush wBrush = new SolidBrush(Color.FromArgb(255, 205, 75)))
            {
                g.FillPath(mbg, mp);
                g.DrawPath(mpen, mp);

                float wheelOffset = (float)(Math.Sin(lt * 10.0) * 5.0);
                g.FillRectangle(wBrush, x + 19f, y + 14f + wheelOffset, 8f, 18f);
            }
        }

        private static void DrawAnimatedCursor(Graphics g, float x, float y, bool clicking, bool doubleClick)
        {
            if (clicking)
            {
                using (Pen ripplePen = new Pen(Color.FromArgb(220, 255, 195, 60), 3.0f))
                {
                    g.DrawEllipse(ripplePen, x - 18f, y - 18f, 36f, 36f);
                    if (doubleClick)
                    {
                        g.DrawEllipse(ripplePen, x - 28f, y - 28f, 56f, 56f);
                    }
                }
            }

            PointF[] cursorPts = new PointF[]
            {
                new PointF(x, y),
                new PointF(x, y + 24f),
                new PointF(x + 6f, y + 19f),
                new PointF(x + 11f, y + 29f),
                new PointF(x + 15f, y + 27f),
                new PointF(x + 10f, y + 17f),
                new PointF(x + 18f, y + 17f)
            };

            using (SolidBrush fill = new SolidBrush(Color.White))
            using (Pen outline = new Pen(Color.FromArgb(25, 25, 25), 2.0f))
            {
                g.FillPolygon(fill, cursorPts);
                g.DrawPolygon(outline, cursorPts);
            }
        }

        private static double Clamp01(double v)
        {
            if (v < 0.0) return 0.0;
            if (v > 1.0) return 1.0;
            return v;
        }

        private static double SmoothStep(double x)
        {
            x = Clamp01(x);
            return x * x * (3.0 - 2.0 * x);
        }
    }
}
