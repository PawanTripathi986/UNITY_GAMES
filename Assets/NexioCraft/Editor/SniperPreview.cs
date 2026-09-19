using System.IO;
using NexioCraft.Sniper;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NexioCraft.EditorTools
{
    /// <summary>
    /// Renders Steel Sniper's 3D world to PNGs without a device, for checking the look quickly.
    /// Needs a graphics device, so run batch mode without -nographics:
    ///   Unity -batchmode -projectPath . -executeMethod NexioCraft.EditorTools.SniperPreview.CommandLine [-mission 12] [-out Screenshots/sniper]
    /// </summary>
    public static class SniperPreview
    {
        const int Width = 1080, Height = 2340;

        public static void CommandLine()
        {
            int code = 0;
            try
            {
                var args = System.Environment.GetCommandLineArgs();
                string output = ArgValue(args, "-out") ?? "Screenshots/sniper";
                string missionArg = ArgValue(args, "-mission");
                Directory.CreateDirectory(output);
                if (missionArg != null) Render(int.Parse(missionArg) - 1, output);
                else
                    foreach (int mission in new[] { 0, 14, 23 }) Render(mission, output);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        [MenuItem("NexioCraft/Sniper/Render Preview")]
        public static void FromMenu() => Render(0, "Screenshots/sniper");

        static void Render(int missionIndex, string output)
        {
            var mission = SniperMissions.Get(missionIndex);
            var session = SniperSession.Create(mission, Rifle.For(new[] { 0, 0, 5, 0, 0 }));
            try
            {
                var camera = session.Camera;
                var eye = SniperWorld.NestEye;
                string prefix = $"{output}/m{mission.Number:00}";

                camera.fieldOfView = SniperSession.BaseFov;
                Save(camera, prefix + "-view.png");
                // The first-person rifle only shows unscoped.
                var viewRifle = camera.transform.Find("View Rifle");
                if (viewRifle != null) viewRifle.gameObject.SetActive(false);

                Robot target = null;
                foreach (var robot in session.Robots)
                    if (robot.Hostile && (target == null || robot.Role == RobotRole.Boss)) target = robot;
                if (target != null)
                {
                    var head = target.HeadPosition;
                    camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(head - eye));
                    camera.fieldOfView = SniperSession.BaseFov / 4f;
                    Save(camera, prefix + "-scope4x.png");
                    camera.fieldOfView = SniperSession.BaseFov / 12f;
                    Save(camera, prefix + "-scope12x.png");

                    // Close-up, as the bullet cam sees it.
                    var front = target.transform.position + target.transform.forward * 3.2f + Vector3.up * 1.6f;
                    camera.transform.SetPositionAndRotation(front, Quaternion.LookRotation(target.ChestPosition - front));
                    camera.fieldOfView = 46f;
                    Save(camera, prefix + "-closeup.png");
                }

                foreach (var robot in session.Robots)
                {
                    if (robot.Role != RobotRole.Civilian) continue;
                    var front = robot.transform.position + robot.transform.forward * 3f + Vector3.up * 1.4f;
                    camera.transform.SetPositionAndRotation(front, Quaternion.LookRotation(robot.ChestPosition - front));
                    camera.fieldOfView = 46f;
                    Save(camera, prefix + "-civilian.png");
                    break;
                }
                Debug.Log($"NXPREVIEW mission {mission.Number} ({SniperMissions.DistrictNames[mission.District]}): {session.Robots.Count} robots rendered to {prefix}-*.png");
            }
            finally
            {
                Object.DestroyImmediate(session.gameObject);
            }
        }

        static void Save(Camera camera, string path)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.aspect = Width / (float)Height;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
        }

        static string ArgValue(string[] args, string name)
        {
            int i = System.Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
