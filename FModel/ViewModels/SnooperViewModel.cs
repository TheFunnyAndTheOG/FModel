using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Engine;
using Editor;
using FModel.Framework;
using FModel.Services;
using OpenTK.Windowing.Desktop;
using Serilog;
using Snooper.Rendering.Actors;
using Snooper.Rendering.Components;
using Snooper.Rendering.Components.Light;
using Snooper.Rendering.Components.Primitive;
using Snooper.Rendering.Components.Transforms;

namespace FModel.ViewModels;

public class SnooperViewModel : ViewModel, IDisposable
{
    public static SnooperViewModel Instance { get; } = new();

    private readonly Lazy<EditorWindow> _editor;

    private SnooperViewModel()
    {
        var scale = GetDpiScale();
        var htz = GetMaxRefreshFrequency();
        var width = Convert.ToInt32(SystemParameters.MaximizedPrimaryScreenWidth * .75 * scale);
        var height = Convert.ToInt32(SystemParameters.MaximizedPrimaryScreenHeight * .85 * scale);

        _editor = new Lazy<EditorWindow>(() => StartEditor(htz, width, height));
    }

    private static EditorWindow StartEditor(int htz, int width, int height)
    {
        GLFWProvider.CheckForMainThread = false;

        var ready = new ManualResetEventSlim();
        EditorWindow? editor = null;
        Exception? failure = null;

        new Thread(() =>
        {
            try
            {
                editor = new EditorWindow(htz, width, height, ApplicationService.ApplicationView.CUE4Parse.Provider, false, true);
            }
            catch (Exception e)
            {
                failure = e;
                return;
            }
            finally
            {
                ready.Set();
            }

            try
            {
                editor.Run();
            }
            catch (Exception e)
            {
                Log.Error(e, "Snooper crashed");
            }
        }) { IsBackground = true, Name = "Snooper" }.Start();

        ready.Wait();
        return editor ?? throw new InvalidOperationException("Snooper failed to start", failure);
    }

    public void Load(UObject? obj)
    {
        var scene = new Actor("Example Scene");
        scene.Components.Add(new BoxComponent(Vector3.Zero, Vector3.One));

        var camera = new CameraActor("Camera");
        camera.CameraComponent.LocalTransform.Position = new Vector3(1, 2, -0.5f);
        camera.CameraComponent.LocalTransform.Rotation = new Quaternion(0, -1, 0, 1);
        scene.Children.Add(camera);

        var sun = new Actor("Sun Light");
        sun.Components.Add(new DirectionalLightComponent(MathF.PI, new Vector3(1.0f, 0.87f, 0.72f), new Transform(new Quaternion(new Vector3(0.5f, -0.5f, 0.0f), 1.0f)), "Directional Light"));
        scene.Children.Add(sun);

        GridComponent gridComponent = new OpaqueGridComponent();
        switch (obj)
        {
            case UStaticMesh sm:
                scene.Children.Add(new MeshActor(sm));
                break;
            case USkeletalMesh sk:
                scene.Children.Add(new MeshActor(sk));
                break;
            case UWorld w:
                gridComponent = new GridComponent();
                scene.Children.Add(new WorldActor(w));
                break;
        }
        var grid = new Actor("Grid");
        grid.Components.Add(gridComponent);
        scene.Children.Insert(0, grid);

        var editor = _editor.Value;
        editor.Invoke(() =>
        {
            editor.Manager.LoadScene(scene);
            editor.Show();
        });
    }

    public void Run()
    {
        var editor = _editor.Value;
        editor.Invoke(editor.Show);
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVMODE
    {
        private const int CCHDEVICENAME = 0x20;
        private const int CCHFORMNAME = 0x20;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public ScreenOrientation dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;

    }

    private static float GetDpiScale()
    {
        if (Screen.PrimaryScreen is not { } primaryScreen)
            return 1.0f;

        return (float)Math.Max(
            primaryScreen.Bounds.Width / SystemParameters.PrimaryScreenWidth,
            primaryScreen.Bounds.Height / SystemParameters.PrimaryScreenHeight
        );
    }

    private static int GetMaxRefreshFrequency()
    {
        var rf = 60;
        var vDevMode = new DEVMODE();
        var i = 0;
        while (EnumDisplaySettings(null, i, ref vDevMode))
        {
            i++;
            rf = Math.Max(rf, vDevMode.dmDisplayFrequency);
        }

        return rf;
    }

    public void Dispose()
    {
        if (!_editor.IsValueCreated) return;
        _editor.Value.Dispose();
    }
}
