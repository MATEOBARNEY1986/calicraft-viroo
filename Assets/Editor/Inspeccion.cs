// Herramienta temporal: vuelca por reflexión la API de VIROO que se necesita para portar Calicraft.
// Uso: Unity -batchmode -executeMethod Inspeccion.Volcar  → escribe Inspeccion.txt en la raíz del proyecto.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;

public static class Inspeccion
{
    static readonly string[] Tipos = {
        "BroadcastObjectAction", "ObjectAction", "NetworkBehaviour", "VirooApi", "PersistType",
        "IVirooNetworkingApi", "IVirooTeleportApi", "IVirooPlayersApi", "IVirooLocomotionApi", "IVirooInputApi",
        "IVirooContextApi", "InputDevices", "VirooDeviceId",
        "XRControllerInput", "ActionPersistenceType", "NetworkVariable`1", "IVirooCamera", "IPlayer",
        "IVirooGravityProvider", "TeleportModes", "HmdInput", "IVirooScenesApi", "ButtonInput", "AxisInput", "InternalTeleportPoint", "SceneConfiguration", "PlayerStart",
    };

    public static void Volcar()
    {
        var sb = new StringBuilder();
        var ensamblados = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name.StartsWith("Viroo") || a.GetName().Name.StartsWith("Virtualware")).ToArray();
        sb.AppendLine("ENSAMBLADOS: " + string.Join(", ", ensamblados.Select(a => a.GetName().Name).OrderBy(n => n)));
        foreach (var a in ensamblados)
        {
            Type[] ts;
            try { ts = a.GetTypes(); } catch (ReflectionTypeLoadException e) { ts = e.Types.Where(t => t != null).ToArray(); }
            foreach (var t in ts)
            {
                if (!Tipos.Contains(t.Name)) continue;
                sb.AppendLine($"\n===== {t.FullName}  [{a.GetName().Name}]  base: {t.BaseType?.FullName}");
                if (t.IsEnum) { sb.AppendLine("  enum: " + string.Join(", ", Enum.GetNames(t))); continue; }
                const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                foreach (var f in t.GetFields(F)) sb.AppendLine($"  campo {(f.IsPublic ? "public" : f.IsFamily ? "protected" : "private")} {f.FieldType.Name} {f.Name}");
                foreach (var p in t.GetProperties(F)) sb.AppendLine($"  prop {p.PropertyType.Name} {p.Name}");
                foreach (var m in t.GetMethods(F).Where(m => !m.IsSpecialName))
                    sb.AppendLine($"  metodo {(m.IsPublic ? "public" : m.IsFamily ? "protected" : m.IsAssembly ? "internal" : "private")}{(m.IsVirtual ? " virtual" : "")}{(m.IsAbstract ? " abstract" : "")} {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
            }
        }
        // extensiones de VirooApi (Networking(), Teleport(), ...)
        sb.AppendLine("\n===== Métodos de extensión sobre VirooApi");
        foreach (var a in ensamblados)
        {
            Type[] ts;
            try { ts = a.GetTypes(); } catch (ReflectionTypeLoadException e) { ts = e.Types.Where(t => t != null).ToArray(); }
            foreach (var t in ts.Where(t => t.IsAbstract && t.IsSealed))
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    if (m.GetParameters().FirstOrDefault()?.ParameterType.Name == "VirooApi")
                        sb.AppendLine($"  {t.FullName}.{m.Name}() -> {m.ReturnType.FullName}");
        }
        File.WriteAllText("Inspeccion.txt", sb.ToString());
    }
}
