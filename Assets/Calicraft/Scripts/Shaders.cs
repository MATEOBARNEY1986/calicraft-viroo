using System.Collections.Generic;
using UnityEngine;

// Busca shaders sin depender de Shader.Find: al compilar, Unity descarta los shaders (y las variantes:
// transparente, emisión) que ningún material del proyecto usa. Los materiales de
// Resources/ShadersCalicraft (los crea el menú Calicraft → Configurar escena VIROO) los mantienen dentro del build.
public static class Shaders
{
    static Dictionary<string, Shader> cache;

    public static Shader Buscar(string nombre)
    {
        if (cache == null)
        {
            cache = new Dictionary<string, Shader>();
            foreach (var m in Resources.LoadAll<Material>("ShadersCalicraft"))
                if (m != null && m.shader != null) cache[m.shader.name] = m.shader;
        }
        if (cache.TryGetValue(nombre, out var s)) return s;
        s = Shader.Find(nombre);
        if (s == null) Debug.LogWarning("[Calicraft] Shader no encontrado: " + nombre);
        return s;
    }
}
