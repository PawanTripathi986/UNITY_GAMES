using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace NexioCraft.EditorTools
{
    /// <summary>
    /// Runs the External Dependency Manager's Android resolver from the command line, so the AdMob
    /// Gradle dependencies are pulled without opening the Editor UI.
    /// </summary>
    public static class AdMobResolve
    {
        public static void List()
        {
            foreach (var type in AppDomain.CurrentDomain.GetAssemblies()
                         .Where(a => a.GetName().Name.StartsWith("Google."))
                         .SelectMany(SafeTypes)
                         .Where(t => t.Name.Contains("Resolver")))
            {
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(m => m.Name.Contains("Resolve"))
                    .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
                Debug.Log($"NXRESOLVER {type.FullName}: {string.Join(" | ", methods)}");
            }
        }

        /// <summary>Logs the External Dependency Manager's iOS post-build steps and their order.</summary>
        public static void ListIosPostProcess()
        {
            foreach (var method in AppDomain.CurrentDomain.GetAssemblies()
                         .Where(a => a.GetName().Name.StartsWith("Google."))
                         .SelectMany(SafeTypes)
                         .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)))
            {
                var attribute = method.GetCustomAttributes(typeof(UnityEditor.Callbacks.PostProcessBuildAttribute), false)
                    .Cast<Attribute>().FirstOrDefault();
                if (attribute != null)
                    Debug.Log($"NXPOST {method.DeclaringType?.Name}.{method.Name} order={OrderOf(attribute)}");
            }
        }

        // callbackOrder is internal on Unity's callback attributes.
        static int OrderOf(Attribute attribute)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (var type = attribute.GetType(); type != null; type = type.BaseType)
            {
                var property = type.GetProperty("callbackOrder", flags);
                if (property != null) return (int)property.GetValue(attribute);
                var field = type.GetField("m_CallbackOrder", flags);
                if (field != null) return (int)field.GetValue(attribute);
            }
            return int.MinValue;
        }

        public static void Run()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeTypes)
                .FirstOrDefault(t => t.FullName == "GooglePlayServices.PlayServicesResolver");
            if (type == null) throw new Exception("PlayServicesResolver not found.");

            // ResolveSync(bool forceResolution) is the batch-friendly entry point.
            var sync = type.GetMethod("ResolveSync",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(bool) }, null);
            if (sync == null) throw new Exception("ResolveSync(bool) not found.");
            bool ok = (bool)sync.Invoke(null, new object[] { true });
            Debug.Log("NexioCraft Android resolve " + (ok ? "SUCCEEDED" : "FAILED"));
            if (!ok) throw new Exception("Android dependency resolution failed.");
        }

        static Type[] SafeTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null).ToArray(); }
        }
    }
}
